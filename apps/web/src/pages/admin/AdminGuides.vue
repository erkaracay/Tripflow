<script setup lang="ts">
import { nextTick, onMounted, reactive, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { apiGet, apiPost, apiPostWithPayload } from '../../lib/api'
import { useToast } from '../../lib/toast'
import LoadingState from '../../components/ui/LoadingState.vue'
import ErrorState from '../../components/ui/ErrorState.vue'
import PasswordModal from '../../components/ui/PasswordModal.vue'
import type { UserListItem } from '../../types'

const { t } = useI18n()
const { pushToast } = useToast()

const guides = ref<UserListItem[]>([])
const loading = ref(true)
const errorKey = ref<string | null>(null)
const errorMessage = ref<string | null>(null)

const createForm = reactive({
  email: '',
  password: '',
  fullName: '',
})
const emailInput = ref<HTMLInputElement | null>(null)
const passwordInput = ref<HTMLInputElement | null>(null)
const creating = ref(false)
const formErrorKey = ref<string | null>(null)
const formErrorMessage = ref<string | null>(null)
const passwordOpen = ref(false)
const passwordUser = ref<UserListItem | null>(null)
const passwordErrorKey = ref<string | null>(null)
const passwordErrorMessage = ref<string | null>(null)
const passwordSaving = ref(false)

const loadGuides = async () => {
  loading.value = true
  errorKey.value = null
  errorMessage.value = null
  try {
    guides.value = await apiGet<UserListItem[]>('/api/users?role=Guide')
  } catch (err) {
    errorMessage.value = err instanceof Error ? err.message : null
    if (!errorMessage.value) {
      errorKey.value = 'errors.guides.load'
    }
  } finally {
    loading.value = false
  }
}

const createGuide = async () => {
  if (creating.value) {
    return
  }
  formErrorKey.value = null
  formErrorMessage.value = null

  if (!createForm.email.trim() || !createForm.password.trim()) {
    formErrorKey.value = 'validation.userEmailPasswordRequired'
    await nextTick()
    if (!createForm.email.trim()) {
      emailInput.value?.focus()
    } else {
      passwordInput.value?.focus()
    }
    return
  }

  if (createForm.password.trim().length < 8) {
    formErrorKey.value = 'validation.passwordMin'
    await nextTick()
    passwordInput.value?.focus()
    return
  }

  creating.value = true
  try {
    const payload = {
      email: createForm.email.trim(),
      password: createForm.password.trim(),
      fullName: createForm.fullName.trim() || undefined,
    }
    const created = await apiPost<UserListItem>('/api/users/guides', payload)
    guides.value = [created, ...guides.value]
    createForm.email = ''
    createForm.password = ''
    createForm.fullName = ''
    pushToast({ key: 'toast.guideCreated', tone: 'success' })
  } catch (err) {
    const message = err instanceof Error ? err.message : ''
    if (message.toLowerCase().includes('email')) {
      formErrorKey.value = 'errors.users.emailExists'
    } else {
      formErrorMessage.value = message || t('errors.guides.create')
    }
    pushToast({ key: 'toast.guideCreateFailed', tone: 'error' })
  } finally {
    creating.value = false
  }
}

const openPasswordModal = (user: UserListItem) => {
  passwordUser.value = user
  passwordErrorKey.value = null
  passwordErrorMessage.value = null
  passwordOpen.value = true
}

const closePasswordModal = () => {
  passwordOpen.value = false
  passwordUser.value = null
}

const submitPassword = async (payload: { password: string; confirm: string }) => {
  passwordErrorKey.value = null
  passwordErrorMessage.value = null

  if (!payload.password || !payload.confirm) {
    passwordErrorKey.value = 'errors.users.password.required'
    return
  }

  if (payload.password.length < 8) {
    passwordErrorKey.value = 'errors.users.password.tooShort'
    return
  }

  if (payload.password !== payload.confirm) {
    passwordErrorKey.value = 'errors.users.password.mismatch'
    return
  }

  if (!passwordUser.value) {
    return
  }

  passwordSaving.value = true
  try {
    await apiPostWithPayload<void>(`/api/users/${passwordUser.value.id}/password`, {
      newPassword: payload.password,
    })
    pushToast({ key: 'toast.passwordUpdated', tone: 'success' })
    closePasswordModal()
  } catch (err) {
    const payloadError = err as { payload?: unknown }
    const code =
      payloadError?.payload && typeof payloadError.payload === 'object' && 'code' in payloadError.payload
        ? String((payloadError.payload as { code?: string }).code ?? '')
        : ''

    if (code === 'password_too_short') {
      passwordErrorKey.value = 'errors.users.password.tooShort'
    } else if (code === 'invalid_role_target') {
      passwordErrorKey.value = 'errors.users.password.invalidRole'
    } else if (code === 'user_not_found') {
      passwordErrorKey.value = 'errors.users.password.notFound'
    } else if (code === 'forbidden') {
      passwordErrorKey.value = 'errors.users.password.forbidden'
    } else {
      passwordErrorMessage.value = err instanceof Error ? err.message : t('errors.users.password.failed')
    }
    pushToast({ key: 'toast.passwordUpdateFailed', tone: 'error' })
  } finally {
    passwordSaving.value = false
  }
}

onMounted(loadGuides)
</script>

<template>
  <div class="space-y-6">
    <section class="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
      <h1 class="text-2xl font-semibold">{{ t('admin.guides.title') }}</h1>
      <p class="mt-2 text-sm text-slate-600">{{ t('admin.guides.subtitle') }}</p>
    </section>

    <section class="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
      <h2 class="text-lg font-semibold">{{ t('admin.guides.createTitle') }}</h2>
      <form class="mt-4 grid gap-4 gap-y-4 md:grid-cols-3 md:gap-x-4" @submit.prevent="createGuide">
        <label class="grid min-w-0 gap-1 text-sm">
          <span class="text-slate-600">{{ t('admin.guides.form.emailLabel') }}</span>
          <input
            v-model.trim="createForm.email"
            ref="emailInput"
            class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
            type="email"
            :placeholder="t('admin.guides.form.emailPlaceholder')"
            autocomplete="email"
            :disabled="creating"
            name="email"
          />
        </label>
        <label class="grid min-w-0 gap-1 text-sm">
          <span class="text-slate-600">{{ t('admin.guides.form.passwordLabel') }}</span>
          <input
            v-model.trim="createForm.password"
            ref="passwordInput"
            class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
            type="password"
            :placeholder="t('admin.guides.form.passwordPlaceholder')"
            autocomplete="new-password"
            :disabled="creating"
            name="password"
          />
        </label>
        <label class="grid min-w-0 gap-1 text-sm">
          <span class="text-slate-600">{{ t('admin.guides.form.fullNameLabel') }}</span>
          <input
            v-model.trim="createForm.fullName"
            class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
            type="text"
            :placeholder="t('admin.guides.form.fullNamePlaceholder')"
            :disabled="creating"
            name="fullName"
          />
        </label>
        <div class="md:col-span-3 flex flex-wrap items-center gap-3">
          <button
            class="inline-flex items-center gap-2 rounded bg-slate-900 px-4 py-2 text-sm font-medium text-white hover:bg-slate-800 disabled:cursor-not-allowed disabled:opacity-60"
            type="submit"
            :disabled="creating"
          >
            <span v-if="creating" class="h-3 w-3 animate-spin rounded-full border border-white/60 border-t-transparent"></span>
            {{ creating ? t('admin.guides.creating') : t('admin.guides.create') }}
          </button>
          <span v-if="formErrorKey" class="text-xs text-rose-600">{{ t(formErrorKey) }}</span>
          <span v-else-if="formErrorMessage" class="text-xs text-rose-600">{{ formErrorMessage }}</span>
        </div>
      </form>
    </section>

    <section class="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
      <div class="flex items-center justify-between">
        <h2 class="text-lg font-semibold">{{ t('admin.guides.listTitle') }}</h2>
        <span class="text-xs text-slate-500">{{ guides.length }} {{ t('common.total') }}</span>
      </div>

      <LoadingState v-if="loading" message-key="admin.guides.loading" />
      <ErrorState v-else-if="errorKey || errorMessage" :message-key="errorKey ?? undefined" :message="errorMessage ?? undefined" @retry="loadGuides" />

      <div
        v-else-if="guides.length === 0"
        class="mt-4 rounded-2xl border border-dashed border-slate-200 bg-slate-50 p-4 text-sm text-slate-500"
      >
        {{ t('admin.guides.empty') }}
      </div>

      <div v-else class="mt-4 space-y-3">
        <div
          v-for="guide in guides"
          :key="guide.id"
          class="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-slate-200 bg-white px-4 py-3 text-sm"
        >
          <div>
            <div class="font-medium text-slate-900">{{ guide.fullName || guide.email }}</div>
            <div class="text-xs text-slate-500">{{ guide.email }}</div>
          </div>
          <div class="flex flex-wrap items-center gap-2">
            <button
              class="rounded border border-slate-200 px-3 py-1 text-xs font-semibold text-slate-700 hover:bg-slate-50"
              type="button"
              @click="openPasswordModal(guide)"
            >
              {{ t('admin.guides.actions.changePassword') }}
            </button>
            <span class="rounded-full bg-slate-100 px-3 py-1 text-xs font-semibold text-slate-700">
              {{ t('admin.users.roles.guide') }}
            </span>
          </div>
        </div>
      </div>
    </section>

    <PasswordModal
      :open="passwordOpen"
      :title="t('admin.guides.password.title')"
      :name="passwordUser?.fullName ?? null"
      :email="passwordUser?.email ?? null"
      :loading="passwordSaving"
      :error-key="passwordErrorKey"
      :error-message="passwordErrorMessage"
      @close="closePasswordModal"
      @submit="submitPassword"
    />
  </div>
</template>
