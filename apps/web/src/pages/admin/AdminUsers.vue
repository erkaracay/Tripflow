<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { apiGet, apiPost, apiPostWithPayload } from '../../lib/api'
import { getSelectedOrgId } from '../../lib/auth'
import { useToast } from '../../lib/toast'
import LoadingState from '../../components/ui/LoadingState.vue'
import ErrorState from '../../components/ui/ErrorState.vue'
import PasswordModal from '../../components/ui/PasswordModal.vue'
import type { UserListItem } from '../../types'

type CreateRole = 'Admin' | 'Guide'

const { t } = useI18n()
const { pushToast } = useToast()

const selectedOrgId = computed(() => getSelectedOrgId())
const users = ref<UserListItem[]>([])
const loading = ref(true)
const errorKey = ref<string | null>(null)
const errorMessage = ref<string | null>(null)

const createForm = reactive({
  role: 'Admin' as CreateRole,
  email: '',
  password: '',
  fullName: '',
})
const creating = ref(false)
const formErrorKey = ref<string | null>(null)
const formErrorMessage = ref<string | null>(null)
const passwordOpen = ref(false)
const passwordUser = ref<UserListItem | null>(null)
const passwordErrorKey = ref<string | null>(null)
const passwordErrorMessage = ref<string | null>(null)
const passwordSaving = ref(false)

const loadUsers = async () => {
  if (!selectedOrgId.value) {
    users.value = []
    loading.value = false
    return
  }

  loading.value = true
  errorKey.value = null
  errorMessage.value = null
  try {
    users.value = await apiGet<UserListItem[]>(`/api/users?orgId=${selectedOrgId.value}`)
  } catch (err) {
    errorMessage.value = err instanceof Error ? err.message : null
    if (!errorMessage.value) {
      errorKey.value = 'errors.users.load'
    }
  } finally {
    loading.value = false
  }
}

const roleLabel = (role: string) => {
  if (role === 'Guide') {
    return t('admin.users.roles.guide')
  }
  if (role === 'AgencyAdmin') {
    return t('admin.users.roles.admin')
  }
  return role
}

const createUser = async () => {
  formErrorKey.value = null
  formErrorMessage.value = null

  if (!selectedOrgId.value) {
    formErrorKey.value = 'errors.users.orgRequired'
    return
  }

  if (!createForm.email.trim() || !createForm.password.trim()) {
    formErrorKey.value = 'validation.userEmailPasswordRequired'
    return
  }

  if (createForm.password.trim().length < 8) {
    formErrorKey.value = 'validation.passwordMin'
    return
  }

  creating.value = true
  try {
    const payload = {
      email: createForm.email.trim(),
      password: createForm.password.trim(),
      role: createForm.role,
      organizationId: selectedOrgId.value,
      fullName: createForm.fullName.trim() || undefined,
    }
    const created = await apiPost<UserListItem>('/api/users', payload)
    users.value = [created, ...users.value]
    createForm.email = ''
    createForm.password = ''
    createForm.fullName = ''
    createForm.role = 'Admin'
    pushToast({ key: 'toast.userCreated', tone: 'success' })
  } catch (err) {
    const message = err instanceof Error ? err.message : ''
    if (message.toLowerCase().includes('email')) {
      formErrorKey.value = 'errors.users.emailExists'
    } else {
      formErrorMessage.value = message || t('errors.users.create')
    }
    pushToast({ key: 'toast.userCreateFailed', tone: 'error' })
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

onMounted(loadUsers)
</script>

<template>
  <div class="space-y-6">
    <section class="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
      <h1 class="text-2xl font-semibold">{{ t('admin.users.title') }}</h1>
      <p class="mt-2 text-sm text-slate-600">{{ t('admin.users.subtitle') }}</p>
      <p v-if="!selectedOrgId" class="mt-3 text-sm text-amber-600">
        {{ t('admin.users.orgRequired') }}
      </p>
    </section>

    <section class="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
      <h2 class="text-lg font-semibold">{{ t('admin.users.createTitle') }}</h2>
      <form class="mt-4 grid grid-cols-1 gap-6 md:grid-cols-2" @submit.prevent="createUser">
        <label class="grid min-w-0 gap-1 text-sm">
          <span class="text-slate-600">{{ t('admin.users.form.roleLabel') }}</span>
          <select
            v-model="createForm.role"
            class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
          >
            <option value="Admin">{{ t('admin.users.roles.admin') }}</option>
            <option value="Guide">{{ t('admin.users.roles.guide') }}</option>
          </select>
        </label>
        <label class="grid min-w-0 gap-1 text-sm">
          <span class="text-slate-600">{{ t('admin.users.form.emailLabel') }}</span>
          <input
            v-model.trim="createForm.email"
            class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
            type="email"
            :placeholder="t('admin.users.form.emailPlaceholder')"
          />
        </label>
        <label class="grid min-w-0 gap-1 text-sm">
          <span class="text-slate-600">{{ t('admin.users.form.passwordLabel') }}</span>
          <input
            v-model.trim="createForm.password"
            class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
            type="password"
            :placeholder="t('admin.users.form.passwordPlaceholder')"
          />
        </label>
        <label class="grid min-w-0 gap-1 text-sm">
          <span class="text-slate-600">{{ t('admin.users.form.fullNameLabel') }}</span>
          <input
            v-model.trim="createForm.fullName"
            class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
            type="text"
            :placeholder="t('admin.users.form.fullNamePlaceholder')"
          />
        </label>
        <div class="md:col-span-2 flex flex-wrap items-center gap-3">
          <button
            class="inline-flex items-center gap-2 rounded bg-slate-900 px-4 py-2 text-sm font-medium text-white hover:bg-slate-800 disabled:cursor-not-allowed disabled:opacity-60"
            type="submit"
            :disabled="creating || !selectedOrgId"
          >
            <span v-if="creating" class="h-3 w-3 animate-spin rounded-full border border-white/60 border-t-transparent"></span>
            {{ creating ? t('admin.users.creating') : t('admin.users.create') }}
          </button>
          <span v-if="formErrorKey" class="text-xs text-rose-600">{{ t(formErrorKey) }}</span>
          <span v-else-if="formErrorMessage" class="text-xs text-rose-600">{{ formErrorMessage }}</span>
        </div>
      </form>
    </section>

    <section class="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
      <div class="flex items-center justify-between">
        <h2 class="text-lg font-semibold">{{ t('admin.users.listTitle') }}</h2>
        <span class="text-xs text-slate-500">{{ users.length }} {{ t('common.total') }}</span>
      </div>

      <LoadingState v-if="loading" message-key="admin.users.loading" />
      <ErrorState v-else-if="errorKey || errorMessage" :message-key="errorKey ?? undefined" :message="errorMessage ?? undefined" @retry="loadUsers" />

      <div
        v-else-if="users.length === 0"
        class="mt-4 rounded-2xl border border-dashed border-slate-200 bg-slate-50 p-4 text-sm text-slate-500"
      >
        {{ t('admin.users.empty') }}
      </div>

      <div v-else class="mt-4 space-y-3">
        <div
          v-for="user in users"
          :key="user.id"
          class="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-slate-200 bg-white px-4 py-3 text-sm"
        >
          <div>
            <div class="font-medium text-slate-900">{{ user.fullName || user.email }}</div>
            <div class="text-xs text-slate-500">{{ user.email }}</div>
          </div>
          <div class="flex flex-wrap items-center gap-2">
            <button
              class="rounded border border-slate-200 px-3 py-1 text-xs font-semibold text-slate-700 hover:bg-slate-50"
              type="button"
              @click="openPasswordModal(user)"
            >
              {{ t('admin.users.actions.changePassword') }}
            </button>
            <span class="rounded-full bg-slate-100 px-3 py-1 text-xs font-semibold text-slate-700">
              {{ roleLabel(user.role) }}
            </span>
          </div>
        </div>
      </div>
    </section>

    <PasswordModal
      :open="passwordOpen"
      :title="t('admin.users.password.title')"
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
