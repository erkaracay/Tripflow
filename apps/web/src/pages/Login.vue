<script setup lang="ts">
import { nextTick, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { apiPost } from '../lib/api'
import { setAuthState } from '../lib/auth'
import type { LoginResponse } from '../types'

const router = useRouter()
const { t } = useI18n()

const email = ref('')
const password = ref('')
const emailInput = ref<HTMLInputElement | null>(null)
const passwordInput = ref<HTMLInputElement | null>(null)
const loading = ref(false)
const errorKey = ref<string | null>(null)
const errorMessage = ref<string | null>(null)
const errorId = 'auth-login-error'

const submit = async () => {
  if (loading.value) {
    return
  }
  errorKey.value = null
  errorMessage.value = null

  const normalizedEmail = email.value.trim().toLowerCase()
  if (!normalizedEmail || !password.value) {
    errorKey.value = 'auth.login.errors.required'
    await nextTick()
    if (!normalizedEmail) {
      emailInput.value?.focus()
    } else {
      passwordInput.value?.focus()
    }
    return
  }

  loading.value = true
  try {
    const response = await apiPost<LoginResponse>('/api/auth/login', {
      email: normalizedEmail,
      password: password.value,
    })

    setAuthState({ role: response.role, userId: response.userId, fullName: response.fullName ?? null })
    let target = '/admin/events'
    if (response.role === 'Guide') {
      target = '/guide/events'
    } else if (response.role === 'SuperAdmin') {
      target = '/admin/orgs'
    }

    await router.replace(target)
  } catch (err) {
    errorMessage.value = err instanceof Error ? err.message : null
    if (!errorMessage.value) {
      errorKey.value = 'auth.login.errors.failed'
    }
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <div class="mx-auto w-full max-w-md space-y-6 rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
    <div class="space-y-1">
      <h1 class="text-2xl font-semibold">{{ t('common.appName') }}</h1>
      <p class="text-sm text-slate-500">{{ t('auth.login.subtitle') }}</p>
    </div>

    <form class="space-y-4" @submit.prevent="submit">
      <label class="grid gap-1 text-sm">
        <span class="text-slate-600">{{ t('auth.login.emailLabel') }}</span>
        <input
          v-model.trim="email"
          ref="emailInput"
          class="rounded-xl border border-slate-200 bg-white px-4 py-3 text-sm focus:border-slate-400 focus:outline-none"
          :placeholder="t('auth.login.emailPlaceholder')"
          type="email"
          name="email"
          autocomplete="email"
          autofocus
          :disabled="loading"
          :aria-invalid="errorKey === 'auth.login.errors.required' && !email.trim()"
          :aria-describedby="errorKey ? errorId : undefined"
        />
      </label>

      <label class="grid gap-1 text-sm">
        <span class="text-slate-600">{{ t('auth.login.passwordLabel') }}</span>
        <input
          v-model="password"
          ref="passwordInput"
          class="rounded-xl border border-slate-200 bg-white px-4 py-3 text-sm focus:border-slate-400 focus:outline-none"
          :placeholder="t('auth.login.passwordPlaceholder')"
          type="password"
          name="password"
          autocomplete="current-password"
          :disabled="loading"
          :aria-invalid="errorKey === 'auth.login.errors.required' && !password"
          :aria-describedby="errorKey ? errorId : undefined"
        />
      </label>

      <button
        class="w-full rounded-xl bg-slate-900 px-4 py-3 text-sm font-medium text-white hover:bg-slate-800"
        :disabled="loading"
        type="submit"
      >
        {{ loading ? t('auth.login.submitting') : t('auth.login.submit') }}
      </button>
    </form>

    <p v-if="errorKey || errorMessage" class="text-sm text-rose-600" :id="errorId" aria-live="polite">
      {{ errorKey ? t(errorKey) : errorMessage }}
    </p>
  </div>
</template>
