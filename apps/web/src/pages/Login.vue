<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { apiPost } from '../lib/api'
import { setToken } from '../lib/auth'
import type { LoginResponse } from '../types'

const router = useRouter()

const email = ref('')
const password = ref('')
const loading = ref(false)
const error = ref<string | null>(null)

const submit = async () => {
  error.value = null

  const normalizedEmail = email.value.trim().toLowerCase()
  if (!normalizedEmail || !password.value) {
    error.value = 'Email and password are required.'
    return
  }

  loading.value = true
  try {
    const response = await apiPost<LoginResponse>('/api/auth/login', {
      email: normalizedEmail,
      password: password.value,
    })

    setToken(response.accessToken)
    const target = response.role === 'Guide' ? '/guide/tours' : '/admin/tours'
    await router.replace(target)
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Login failed.'
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <div class="mx-auto w-full max-w-md space-y-6 rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
    <div class="space-y-1">
      <h1 class="text-2xl font-semibold">Tripflow</h1>
      <p class="text-sm text-slate-500">Sign in to continue.</p>
    </div>

    <form class="space-y-4" @submit.prevent="submit">
      <label class="grid gap-1 text-sm">
        <span class="text-slate-600">Email</span>
        <input
          v-model.trim="email"
          class="rounded-xl border border-slate-200 bg-white px-4 py-3 text-sm focus:border-slate-400 focus:outline-none"
          placeholder="you@example.com"
          type="email"
        />
      </label>

      <label class="grid gap-1 text-sm">
        <span class="text-slate-600">Password</span>
        <input
          v-model="password"
          class="rounded-xl border border-slate-200 bg-white px-4 py-3 text-sm focus:border-slate-400 focus:outline-none"
          placeholder="••••••••"
          type="password"
        />
      </label>

      <button
        class="w-full rounded-xl bg-slate-900 px-4 py-3 text-sm font-medium text-white hover:bg-slate-800"
        :disabled="loading"
        type="submit"
      >
        {{ loading ? 'Signing in...' : 'Sign in' }}
      </button>
    </form>

    <p v-if="error" class="text-sm text-rose-600">{{ error }}</p>
  </div>
</template>
