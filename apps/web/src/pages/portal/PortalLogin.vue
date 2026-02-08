<script setup lang="ts">
import { computed, nextTick, onMounted, onUnmounted, ref } from 'vue'
import { RouterLink, useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { portalGetMe, portalLogin, portalResolveEvent } from '../../lib/api'
import LoadingState from '../../components/ui/LoadingState.vue'

const route = useRoute()
const router = useRouter()
const { t } = useI18n()

const accessCode = ref('')
const tcNo = ref('')
const accessCodeInput = ref<HTMLInputElement | null>(null)
const tcNoInput = ref<HTMLInputElement | null>(null)
const submitting = ref(false)
const checkingSession = ref(false)
const errorKey = ref<string | null>(null)
const errorMessage = ref<string | null>(null)
const retryAfterSeconds = ref<number | null>(null)
const retryTimer = ref<number | null>(null)
const errorId = 'portal-login-error'

const isAccessCodeError = computed(
  () => errorKey.value === 'portal.login.accessCodeRequired' || errorKey.value === 'portal.login.invalidEventCode'
)
const isTcError = computed(
  () =>
    errorKey.value === 'portal.login.tcRequired' ||
    errorKey.value === 'portal.login.tcNotFound' ||
    errorKey.value === 'portal.login.ambiguousTcNo'
)

const sessionTokenKey = (eventId: string) => `infora.portal.session.${eventId}`
const sessionExpiryKey = (eventId: string) => `infora.portal.session.exp.${eventId}`

const sanitizeAccessCode = (value: string) =>
  value.replace(/[^a-zA-Z0-9]/g, '').toUpperCase().slice(0, 8)

const sanitizeTcNo = (value: string) => value.replace(/\D/g, '').slice(0, 11)

const clearRetryTimer = () => {
  if (retryTimer.value) {
    globalThis.clearInterval(retryTimer.value)
    retryTimer.value = null
  }
  retryAfterSeconds.value = null
}

const handleAccessCodeInput = () => {
  errorKey.value = null
  clearRetryTimer()
  accessCode.value = sanitizeAccessCode(accessCode.value)
}

const handleAccessCodeBlur = () => {
  accessCode.value = sanitizeAccessCode(accessCode.value.trim())
}

const handleTcNoInput = () => {
  errorKey.value = null
  clearRetryTimer()
  tcNo.value = sanitizeTcNo(tcNo.value)
}

const handleTcNoBlur = () => {
  tcNo.value = sanitizeTcNo(tcNo.value.trim())
}

const setSession = (eventId: string, token: string, expiresAt: string) => {
  if (!eventId || !token) {
    return
  }

  globalThis.localStorage?.setItem(sessionTokenKey(eventId), token)
  globalThis.localStorage?.setItem(sessionExpiryKey(eventId), expiresAt)
}

const clearSession = (eventId: string) => {
  globalThis.localStorage?.removeItem(sessionTokenKey(eventId))
  globalThis.localStorage?.removeItem(sessionExpiryKey(eventId))
}

const tryReuseExistingSession = async (eventAccessCode: string) => {
  if (checkingSession.value) {
    return
  }

  checkingSession.value = true
  try {
    const resolved = await portalResolveEvent(eventAccessCode)
    const token = globalThis.localStorage?.getItem(sessionTokenKey(resolved.eventId)) ?? ''
    const expiry = globalThis.localStorage?.getItem(sessionExpiryKey(resolved.eventId)) ?? ''
    if (!token || !expiry) {
      return
    }

    const expiresAt = new Date(expiry)
    if (Number.isNaN(expiresAt.getTime()) || expiresAt <= new Date()) {
      clearSession(resolved.eventId)
      return
    }

    try {
      await portalGetMe(token)
      await router.replace(`/e/${resolved.eventId}`)
    } catch (err) {
      const status = err && typeof err === 'object' && 'status' in err ? (err as { status?: number }).status : null
      if (status === 401 || status === 403) {
        clearSession(resolved.eventId)
      }
    }
  } finally {
    checkingSession.value = false
  }
}

const submitLogin = async () => {
  if (submitting.value) {
    return
  }
  errorKey.value = null
  errorMessage.value = null
  retryAfterSeconds.value = null

  const normalizedCode = sanitizeAccessCode(accessCode.value.trim())
  const normalizedTcNo = sanitizeTcNo(tcNo.value.trim())
  accessCode.value = normalizedCode
  tcNo.value = normalizedTcNo

  if (normalizedCode.length !== 8) {
    errorKey.value = 'portal.login.accessCodeRequired'
    await nextTick()
    accessCodeInput.value?.focus()
    return
  }

  if (normalizedTcNo.length !== 11) {
    errorKey.value = 'portal.login.tcRequired'
    await nextTick()
    tcNoInput.value?.focus()
    return
  }

  submitting.value = true
  try {
    const response = await portalLogin(normalizedCode, normalizedTcNo)
    setSession(response.eventId, response.portalSessionToken, response.expiresAt)
    await router.replace(`/e/${response.eventId}`)
  } catch (err) {
    const status = err && typeof err === 'object' && 'status' in err ? (err as { status?: number }).status : null
    const payload = (err as { payload?: { code?: string; retryAfterSeconds?: number } }).payload
    const code = payload?.code
    if (status === 429 || code === 'rate_limited') {
      const seconds = payload?.retryAfterSeconds ?? 0
      retryAfterSeconds.value = seconds || null
      errorKey.value = seconds ? 'portal.login.rateLimitedWithTime' : 'portal.login.rateLimited'
      startRetryCountdown()
      return
    }

    const mappedKey =
      code === 'invalid_access_code_format'
        ? 'portal.login.accessCodeRequired'
        : code === 'invalid_tcno_format'
          ? 'portal.login.tcRequired'
          : code === 'invalid_event_access_code'
            ? 'portal.login.invalidEventCode'
            : code === 'tcno_not_found'
              ? 'portal.login.tcNotFound'
              : code === 'ambiguous_event_access_code'
                ? 'portal.login.ambiguousEventCode'
                : code === 'ambiguous_tcno'
                  ? 'portal.login.ambiguousTcNo'
                  : null

    if (mappedKey) {
      errorKey.value = mappedKey
      await nextTick()
      if (mappedKey === 'portal.login.accessCodeRequired' || mappedKey === 'portal.login.invalidEventCode') {
        accessCodeInput.value?.focus()
      } else if (
        mappedKey === 'portal.login.tcRequired' ||
        mappedKey === 'portal.login.tcNotFound' ||
        mappedKey === 'portal.login.ambiguousTcNo'
      ) {
        tcNoInput.value?.focus()
      }
    } else {
      errorMessage.value = err instanceof Error ? err.message : null
      if (!errorMessage.value) {
        errorKey.value = 'portal.login.failed'
      }
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

const startRetryCountdown = () => {
  if (retryTimer.value) {
    globalThis.clearInterval(retryTimer.value)
    retryTimer.value = null
  }

  if (!retryAfterSeconds.value || retryAfterSeconds.value <= 0) {
    return
  }

  retryTimer.value = globalThis.setInterval(() => {
    if (!retryAfterSeconds.value) {
      return
    }
    retryAfterSeconds.value = Math.max(0, retryAfterSeconds.value - 1)
    if (retryAfterSeconds.value === 0) {
      if (retryTimer.value) {
        globalThis.clearInterval(retryTimer.value)
        retryTimer.value = null
      }
    }
  }, 1000)
}

onUnmounted(() => {
  if (retryTimer.value) {
    globalThis.clearInterval(retryTimer.value)
  }
})

onMounted(() => {
  const rawCode = route.query.code ?? route.query.eventAccessCode
  if (typeof rawCode === 'string') {
    const normalized = sanitizeAccessCode(rawCode)
    accessCode.value = normalized
    if (normalized.length === 8) {
      void tryReuseExistingSession(normalized)
    }
  }
})
</script>

<template>
  <div class="space-y-6">
    <LoadingState v-if="checkingSession" message-key="portal.login.checkingSession" />
    <section class="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:p-6">
      <div class="space-y-2">
        <h1 class="text-2xl font-semibold">{{ t('portal.login.title') }}</h1>
        <p class="text-sm text-slate-600">{{ t('portal.login.subtitle') }}</p>
      </div>
      <form class="mt-6 grid gap-4" @submit.prevent="submitLogin">
        <label class="grid gap-1 text-sm" for="portal-access-code">
          <span class="text-slate-600">{{ t('portal.login.accessCodeLabel') }}</span>
          <input
            id="portal-access-code"
            v-model.trim="accessCode"
            ref="accessCodeInput"
            class="rounded border border-slate-200 bg-white px-3 py-2 text-sm uppercase tracking-widest focus:border-slate-400 focus:outline-none"
            :placeholder="t('portal.login.accessCodePlaceholder')"
            maxlength="8"
            name="eventAccessCode"
            autocomplete="off"
            autocapitalize="characters"
            spellcheck="false"
            autofocus
            :disabled="submitting || checkingSession"
            :aria-invalid="isAccessCodeError"
            :aria-describedby="isAccessCodeError && (errorKey || errorMessage) ? errorId : undefined"
            @input="handleAccessCodeInput"
            @blur="handleAccessCodeBlur"
          />
        </label>
        <label class="grid gap-1 text-sm" for="portal-tc-no">
          <span class="text-slate-600">{{ t('portal.login.tcLabel') }}</span>
          <input
            id="portal-tc-no"
            v-model.trim="tcNo"
            ref="tcNoInput"
            class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
            :placeholder="t('portal.login.tcPlaceholder')"
            maxlength="11"
            name="tcNo"
            inputmode="numeric"
            pattern="[0-9]*"
            autocomplete="off"
            spellcheck="false"
            :disabled="submitting || checkingSession"
            :aria-invalid="isTcError"
            :aria-describedby="isTcError && (errorKey || errorMessage) ? errorId : undefined"
            @input="handleTcNoInput"
            @blur="handleTcNoBlur"
          />
        </label>
        <button
          class="inline-flex items-center justify-center rounded-full bg-slate-900 px-4 py-2 text-sm font-semibold text-white hover:bg-slate-800 disabled:cursor-not-allowed disabled:bg-slate-400"
          :disabled="submitting || checkingSession || (retryAfterSeconds ?? 0) > 0"
          type="submit"
        >
          <span v-if="submitting" class="h-3 w-3 animate-spin rounded-full border border-white/60 border-t-transparent"></span>
          <span class="ml-2">{{ submitting ? t('portal.login.submitting') : t('portal.login.submit') }}</span>
        </button>
        <p v-if="rateLimitMessage" class="text-sm text-amber-600" :id="errorId" aria-live="polite">
          {{ rateLimitMessage }}
        </p>
        <p v-else-if="errorKey" class="text-sm text-rose-600" :id="errorId" aria-live="polite">
          {{ t(errorKey) }}
        </p>
        <p v-else-if="errorMessage" class="text-sm text-rose-600" :id="errorId" aria-live="polite">
          {{ errorMessage }}
        </p>
      </form>
      <p class="mt-4 border-t border-slate-100 pt-4 text-center text-sm text-slate-500">
        <RouterLink
          to="/login"
          class="font-medium text-slate-700 underline hover:text-slate-900"
        >
          {{ t('portal.login.staffLogin') }}
        </RouterLink>
      </p>
    </section>
  </div>
</template>
