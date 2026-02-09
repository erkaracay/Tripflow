<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { portalGetMe } from '../../lib/api'
import PortalInfoTabs from '../../components/portal/PortalInfoTabs.vue'
import LoadingState from '../../components/ui/LoadingState.vue'
import ErrorState from '../../components/ui/ErrorState.vue'
import { clearPortalHeader, setPortalHeader } from '../../lib/portalHeader'
import type { PortalMeResponse } from '../../types'

const route = useRoute()
const router = useRouter()
const { t } = useI18n()

const eventId = computed(() => route.params.eventId as string)

const event = ref<PortalMeResponse['event'] | null>(null)
const portal = ref<PortalMeResponse['portal'] | null>(null)
const docs = ref<PortalMeResponse['docs'] | null>(null)
const participant = ref<PortalMeResponse['participant'] | null>(null)

const loading = ref(true)
const errorKey = ref<string | null>(null)
const errorMessage = ref<string | null>(null)
const sessionExpired = ref(false)
const hasPrinted = ref(false)

const sessionToken = ref('')
const sessionExpiresAt = ref<Date | null>(null)

const sessionTokenKey = computed(() => `infora.portal.session.${eventId.value}`)
const sessionExpiryKey = computed(() => `infora.portal.session.exp.${eventId.value}`)

const hasText = (value?: string | null) => {
  if (!value) return false
  return Boolean(value.trim())
}

const formatPortalDate = (value?: string | null) => {
  if (!value) return ''
  const trimmed = value.trim()
  if (!trimmed) return ''
  const datePart = trimmed.includes('T')
    ? trimmed.split('T')[0] ?? ''
    : trimmed.split(' ')[0] ?? ''
  const [year, month, day] = datePart.split('-')
  if (year && month && day) {
    return `${day}.${month}.${year}`
  }
  return trimmed
}

const portalDateRange = computed(() => {
  if (!event.value) return ''
  const start = formatPortalDate(event.value.startDate)
  const end = formatPortalDate(event.value.endDate)
  if (!start || !end) return ''
  return t('common.dateRange', { start, end })
})

const restoreSession = () => {
  const token = globalThis.localStorage?.getItem(sessionTokenKey.value) ?? ''
  const expiry = globalThis.localStorage?.getItem(sessionExpiryKey.value) ?? ''
  if (!token || !expiry) {
    return false
  }
  const expiresAt = new Date(expiry)
  if (Number.isNaN(expiresAt.getTime()) || expiresAt <= new Date()) {
    return false
  }
  sessionToken.value = token
  sessionExpiresAt.value = expiresAt
  return true
}

const clearSession = () => {
  globalThis.localStorage?.removeItem(sessionTokenKey.value)
  globalThis.localStorage?.removeItem(sessionExpiryKey.value)
  sessionToken.value = ''
  sessionExpiresAt.value = null
}

const requiresLogin = computed(() => {
  if (!sessionToken.value || !sessionExpiresAt.value) {
    return true
  }
  return sessionExpiresAt.value <= new Date()
})

const loadDocs = async () => {
  loading.value = true
  errorKey.value = null
  errorMessage.value = null
  sessionExpired.value = false

  if (!restoreSession()) {
    loading.value = false
    return
  }

  try {
    const response = await portalGetMe(sessionToken.value)
    event.value = response.event
    portal.value = response.portal
    docs.value = response.docs
    participant.value = response.participant
    setPortalHeader(
      response.event.name,
      response.event.logoUrl ?? null,
      response.event.startDate,
      response.event.endDate
    )
    if (!hasPrinted.value) {
      hasPrinted.value = true
      setTimeout(() => {
        window.print()
      }, 250)
    }
  } catch (err) {
    if (err && typeof err === 'object' && 'status' in err) {
      const status = (err as { status?: number }).status
      if (status === 401 || status === 403) {
        clearSession()
        sessionExpired.value = true
        return
      }
    }
    errorMessage.value = err instanceof Error ? err.message : null
    if (!errorMessage.value) {
      errorKey.value = 'errors.portal.load'
    }
  } finally {
    loading.value = false
  }
}

const goToLogin = async () => {
  await router.push({ path: '/e/login', query: { eventId: eventId.value } })
}

onMounted(() => {
  void loadDocs()
})

onUnmounted(() => {
  clearPortalHeader()
})
</script>

<template>
  <div class="portal-docs-print mx-auto max-w-3xl space-y-6 px-4 py-6 sm:px-6">
    <LoadingState v-if="loading" message-key="portal.loading" />

    <ErrorState
      v-else-if="errorKey || errorMessage"
      :message-key="errorKey ?? undefined"
      :message="errorMessage ?? undefined"
      @retry="loadDocs"
    />

    <div
      v-else-if="requiresLogin"
      class="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:p-6"
    >
      <h2 class="text-lg font-semibold">{{ t('portal.login.requiredTitle') }}</h2>
      <p class="mt-2 text-sm text-slate-600">
        {{ sessionExpired ? t('portal.sessionExpired.message') : t('portal.login.requiredSubtitle') }}
      </p>
      <button
        class="mt-4 inline-flex w-full items-center justify-center rounded-full bg-slate-900 px-4 py-2 text-sm font-semibold text-white hover:bg-slate-800 sm:w-auto"
        @click="goToLogin"
      >
        {{ t('portal.sessionExpired.continue') }}
      </button>
    </div>

    <div v-else class="space-y-6">
      <div
        v-if="hasText(participant?.fullName) || hasText(participant?.tcNo) || hasText(participant?.birthDate)"
        class="rounded-2xl border-2 border-slate-300 bg-white p-4 shadow-sm print-card"
      >
        <h1 class="mb-4 text-lg font-bold text-slate-900">{{ t('portal.docs.itineraryTitle') }}</h1>
        <div class="grid grid-cols-1 gap-3 sm:grid-cols-3">
          <div v-if="hasText(participant?.fullName)">
            <div class="text-xs font-semibold uppercase text-slate-500">{{ t('portal.docs.participantName') }}</div>
            <div class="mt-1 text-sm font-medium text-slate-900">{{ participant?.fullName }}</div>
          </div>
          <div v-if="hasText(participant?.tcNo)">
            <div class="text-xs font-semibold uppercase text-slate-500">{{ t('portal.docs.participantTcNo') }}</div>
            <div class="mt-1 text-sm font-medium text-slate-900">{{ participant?.tcNo }}</div>
          </div>
          <div v-if="hasText(participant?.birthDate)">
            <div class="text-xs font-semibold uppercase text-slate-500">{{ t('portal.docs.participantBirthDate') }}</div>
            <div class="mt-1 text-sm font-medium text-slate-900">{{ formatPortalDate(participant?.birthDate) }}</div>
          </div>
        </div>
      </div>

      <div class="flex items-center gap-3">
        <img
          v-if="event?.logoUrl"
          :src="event.logoUrl"
          alt=""
          class="h-12 w-12 rounded-full border border-slate-200 object-cover"
        />
        <div>
          <div class="text-xl font-semibold text-slate-900">{{ event?.name }}</div>
          <div v-if="portalDateRange" class="text-sm text-slate-500">{{ portalDateRange }}</div>
        </div>
      </div>

      <PortalInfoTabs :docs="docs" print-mode />

      <div class="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm print-card">
        <div class="print-title text-sm font-semibold text-slate-900">{{ t('portal.docs.linksTitle') }}</div>
        <div v-if="portal?.links.length === 0" class="mt-2 text-sm text-slate-500">
          {{ t('portal.docs.linksEmpty') }}
        </div>
        <ul v-else class="mt-3 space-y-2 text-sm text-slate-700">
          <li v-for="link in portal?.links" :key="link.url">
            <div class="font-medium">{{ link.label }}</div>
            <div class="text-xs text-slate-500">{{ link.url }}</div>
          </li>
        </ul>
      </div>
    </div>
  </div>
</template>

<style>
@media print {
  header,
  nav,
  .print-hide {
    display: none !important;
  }

  body {
    background: white !important;
  }

  .portal-docs-print {
    padding: 0 !important;
    max-width: none !important;
  }

  .portal-docs-print .print-card {
    break-inside: avoid;
    page-break-inside: avoid;
  }

  .portal-docs-print .print-row {
    break-inside: avoid;
    page-break-inside: avoid;
  }

  .portal-docs-print h1,
  .portal-docs-print h2,
  .portal-docs-print h3 {
    break-after: avoid;
    page-break-after: avoid;
  }

  .portal-docs-print .print-title {
    break-after: avoid;
    page-break-after: avoid;
  }

  .portal-docs-print a[href]::after {
    content: '';
  }
}
</style>
