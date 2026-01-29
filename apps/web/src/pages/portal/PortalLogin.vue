<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { portalLogin } from '../../lib/api'

const route = useRoute()
const router = useRouter()
const { t } = useI18n()

const accessCode = ref('')
const tcNo = ref('')
const submitting = ref(false)
const errorKey = ref<string | null>(null)
const errorMessage = ref<string | null>(null)
const retryAfterSeconds = ref<number | null>(null)

const sessionTokenKey = (eventId: string) => `tripflow.portal.session.${eventId}`
const sessionExpiryKey = (eventId: string) => `tripflow.portal.session.exp.${eventId}`

const sanitizeAccessCode = (value: string) =>
  value.replace(/[^a-zA-Z0-9]/g, '').toUpperCase().slice(0, 8)

const sanitizeTcNo = (value: string) => value.replace(/\D/g, '').slice(0, 11)

const handleAccessCodeInput = () => {
  errorKey.value = null
  retryAfterSeconds.value = null
  accessCode.value = sanitizeAccessCode(accessCode.value)
}

const handleTcNoInput = () => {
  errorKey.value = null
  retryAfterSeconds.value = null
  tcNo.value = sanitizeTcNo(tcNo.value)
}

const setSession = (eventId: string, token: string, expiresAt: string) => {
  if (!eventId || !token) {
    return
  }

  globalThis.localStorage?.setItem(sessionTokenKey(eventId), token)
  globalThis.localStorage?.setItem(sessionExpiryKey(eventId), expiresAt)
}

const submitLogin = async () => {
  errorKey.value = null
  errorMessage.value = null
  retryAfterSeconds.value = null

  const normalizedCode = sanitizeAccessCode(accessCode.value.trim())
  const normalizedTcNo = sanitizeTcNo(tcNo.value.trim())
  accessCode.value = normalizedCode
  tcNo.value = normalizedTcNo

  if (normalizedCode.length !== 8) {
    errorKey.value = 'portal.login.accessCodeRequired'
    return
  }

  if (normalizedTcNo.length !== 11) {
    errorKey.value = 'portal.login.tcRequired'
    return
  }

  submitting.value = true
  try {
    const response = await portalLogin(normalizedCode, normalizedTcNo)
    setSession(response.eventId, response.portalSessionToken, response.expiresAt)
    await router.replace(`/e/${response.eventId}`)
  } catch (err) {
    const status = err && typeof err === 'object' && 'status' in err ? (err as { status?: number }).status : null
    if (status === 429) {
      const payload = (err as { payload?: { retryAfterSeconds?: number } }).payload
      retryAfterSeconds.value = payload?.retryAfterSeconds ?? null
      errorKey.value = 'portal.login.rateLimited'
      return
    }

    errorMessage.value = err instanceof Error ? err.message : null
    if (!errorMessage.value) {
      errorKey.value = 'portal.login.failed'
    }
  } finally {
    submitting.value = false
  }
}

const rateLimitMessage = computed(() => {
  if (!retryAfterSeconds.value) {
    return null
  }
  return t('portal.login.rateLimitedWithTime', { seconds: retryAfterSeconds.value })
})

onMounted(() => {
  const rawCode = route.query.code ?? route.query.eventAccessCode
  if (typeof rawCode === 'string') {
    accessCode.value = sanitizeAccessCode(rawCode)
  }
})
</script>

<template>
  <div class="space-y-6">
    <section class="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:p-6">
      <div class="space-y-2">
        <h1 class="text-2xl font-semibold">{{ t('portal.login.title') }}</h1>
        <p class="text-sm text-slate-600">{{ t('portal.login.subtitle') }}</p>
      </div>
      <div class="mt-6 grid gap-4">
        <label class="grid gap-1 text-sm">
          <span class="text-slate-600">{{ t('portal.login.accessCodeLabel') }}</span>
          <input
            v-model.trim="accessCode"
            class="rounded border border-slate-200 bg-white px-3 py-2 text-sm uppercase tracking-widest focus:border-slate-400 focus:outline-none"
            :placeholder="t('portal.login.accessCodePlaceholder')"
            maxlength="8"
            autocomplete="off"
            @input="handleAccessCodeInput"
          />
        </label>
        <label class="grid gap-1 text-sm">
          <span class="text-slate-600">{{ t('portal.login.tcLabel') }}</span>
          <input
            v-model.trim="tcNo"
            class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
            :placeholder="t('portal.login.tcPlaceholder')"
            maxlength="11"
            inputmode="numeric"
            autocomplete="off"
            @input="handleTcNoInput"
          />
        </label>
        <button
          class="inline-flex items-center justify-center rounded-full bg-slate-900 px-4 py-2 text-sm font-semibold text-white hover:bg-slate-800 disabled:cursor-not-allowed disabled:bg-slate-400"
          :disabled="submitting"
          @click="submitLogin"
        >
          <span v-if="submitting" class="h-3 w-3 animate-spin rounded-full border border-white/60 border-t-transparent"></span>
          <span class="ml-2">{{ submitting ? t('portal.login.submitting') : t('portal.login.submit') }}</span>
        </button>
        <p v-if="rateLimitMessage" class="text-sm text-amber-600">{{ rateLimitMessage }}</p>
        <p v-else-if="errorKey" class="text-sm text-rose-600">{{ t(errorKey) }}</p>
        <p v-else-if="errorMessage" class="text-sm text-rose-600">{{ errorMessage }}</p>
      </div>
    </section>
  </div>
</template>
