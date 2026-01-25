<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import * as QRCode from 'qrcode'
import { useI18n } from 'vue-i18n'
import { apiGet, portalConfirmAccess, portalGetMe, portalVerifyAccess } from '../../lib/api'
import PortalTabBar from '../../components/portal/PortalTabBar.vue'
import LoadingState from '../../components/ui/LoadingState.vue'
import ErrorState from '../../components/ui/ErrorState.vue'
import { useToast } from '../../lib/toast'
import type { PortalAccessPolicy, PortalParticipantSummary, Tour, TourPortalInfo } from '../../types'

type TabKey = 'days' | 'docs' | 'qr' | 'info'

const route = useRoute()
const router = useRouter()
const { t } = useI18n()
const { pushToast } = useToast()
const tourId = computed(() => route.params.tourId as string)

const tour = ref<Tour | null>(null)
const portal = ref<TourPortalInfo | null>(null)
const loading = ref(true)
const errorKey = ref<string | null>(null)
const errorMessage = ref<string | null>(null)
const copyStatusKey = ref<string | null>(null)
const codeCopyStatusKey = ref<string | null>(null)
const linkCopyStatusKey = ref<string | null>(null)
const accessState = ref<'idle' | 'checking' | 'verified' | 'missing' | 'invalid'>('idle')
const accessToken = ref('')
const last4 = ref('')
const phoneHint = ref<string | null>(null)
const participantSummary = ref<PortalParticipantSummary | null>(null)
const policy = ref<PortalAccessPolicy | null>(null)
const attemptsRemaining = ref<number | null>(null)
const lockedUntil = ref<Date | null>(null)
const verifyingAccess = ref(false)
const confirmingAccess = ref(false)
const sessionToken = ref('')
const sessionExpiresAt = ref<Date | null>(null)
const nowTick = ref(Date.now())

const activeTab = ref<TabKey>('days')
const selectedDayIndex = ref(0)
const portalRequiresLast4 = computed(
  () => (policy.value?.requireLast4ForPortal ?? false) && !sessionToken.value
)
const qrRequiresLast4 = computed(
  () => (policy.value?.requireLast4ForQr ?? false) && !sessionToken.value
)
const isLocked = computed(() => lockRemainingSeconds.value > 0)
const portalReady = computed(() => {
  if (accessState.value !== 'verified') {
    return false
  }

  if (portalRequiresLast4.value) {
    return false
  }

  return true
})

const tabs = computed<{ id: TabKey; label: string }[]>(() => [
  { id: 'days', label: t('portal.tabs.days') },
  { id: 'docs', label: t('portal.tabs.docs') },
  { id: 'qr', label: t('portal.tabs.qr') },
  { id: 'info', label: t('portal.tabs.info') },
])

const days = computed(() => portal.value?.days ?? [])
const selectedDay = computed(() => days.value[selectedDayIndex.value] ?? null)

const checkInCode = ref('')
const qrDataUrl = ref<string | null>(null)

const resolvePublicBase = () => {
  const envBase = (import.meta.env.VITE_PUBLIC_BASE_URL as string | undefined)?.trim()
  if (envBase) {
    return envBase.replace(/\/$/, '')
  }

  return globalThis.location?.origin ?? ''
}

const accessTokenKey = computed(() => `tripflow.portal.access.${tourId.value}`)
const sessionTokenKey = computed(() => `tripflow.portal.session.${tourId.value}`)
const sessionExpiryKey = computed(() => `tripflow.portal.session.exp.${tourId.value}`)

const saveAccessToken = (token: string) => {
  if (!token) {
    return
  }

  globalThis.localStorage?.setItem(accessTokenKey.value, token)
  accessToken.value = token
}

const clearAccessToken = () => {
  globalThis.localStorage?.removeItem(accessTokenKey.value)
  accessToken.value = ''
}

const saveSession = (token: string, expiresAt: Date) => {
  globalThis.sessionStorage?.setItem(sessionTokenKey.value, token)
  globalThis.sessionStorage?.setItem(sessionExpiryKey.value, expiresAt.toISOString())
  sessionToken.value = token
  sessionExpiresAt.value = expiresAt
}

const clearSession = () => {
  globalThis.sessionStorage?.removeItem(sessionTokenKey.value)
  globalThis.sessionStorage?.removeItem(sessionExpiryKey.value)
  sessionToken.value = ''
  sessionExpiresAt.value = null
}

const buildCheckInLink = (code: string) => {
  const base = resolvePublicBase()
  if (!base) {
    return ''
  }

  return `${base}/guide/tours/${tourId.value}/checkin?code=${encodeURIComponent(code)}`
}

const loadTour = async () => {
  errorKey.value = null
  errorMessage.value = null

  try {
    const tourData = await apiGet<Tour>(`/api/tours/${tourId.value}`)
    tour.value = tourData
  } catch (err) {
    errorMessage.value = err instanceof Error ? err.message : null
    if (!errorMessage.value) {
      errorKey.value = 'errors.portal.load'
    }
  }
}

const parseDate = (value?: string | null) => {
  if (!value) {
    return null
  }

  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(value)
  if (!match) {
    return null
  }

  const year = Number(match[1])
  const month = Number(match[2])
  const day = Number(match[3])

  if (!Number.isFinite(year) || !Number.isFinite(month) || !Number.isFinite(day)) {
    return null
  }

  return new Date(year, month - 1, day)
}

const toDateOnly = (date: Date) => new Date(date.getFullYear(), date.getMonth(), date.getDate())

const diffDays = (start: Date, end: Date) => {
  const ms = toDateOnly(end).getTime() - toDateOnly(start).getTime()
  return Math.floor(ms / (24 * 60 * 60 * 1000))
}

const setDefaultDay = () => {
  const dayCount = days.value.length
  if (dayCount === 0) {
    selectedDayIndex.value = 0
    return
  }

  const startDate = parseDate(tour.value?.startDate)
  const endDate = parseDate(tour.value?.endDate)
  const today = toDateOnly(new Date())

  if (!startDate || !endDate) {
    selectedDayIndex.value = 0
    return
  }

  let index = 0
  if (today >= toDateOnly(startDate) && today <= toDateOnly(endDate)) {
    index = diffDays(startDate, today)
  }

  selectedDayIndex.value = Math.min(Math.max(index, 0), dayCount - 1)
}

const selectDay = (index: number) => {
  selectedDayIndex.value = index
}

const setActiveTab = (value: string) => {
  activeTab.value = value as TabKey
}

const lockRemainingSeconds = computed(() => {
  if (!lockedUntil.value) {
    return 0
  }

  const diff = lockedUntil.value.getTime() - nowTick.value
  return Math.max(0, Math.ceil(diff / 1000))
})

const applyAccessTokenFromQuery = () => {
  const raw = route.query.pt
  const token = typeof raw === 'string' ? raw.trim() : ''
  if (!token) {
    return
  }

  saveAccessToken(token)

  const nextQuery = { ...route.query }
  delete nextQuery.pt
  router.replace({ query: nextQuery })
}

const restoreSession = async () => {
  const storedToken = globalThis.sessionStorage?.getItem(sessionTokenKey.value) ?? ''
  const storedExpiry = globalThis.sessionStorage?.getItem(sessionExpiryKey.value) ?? ''

  if (!storedToken || !storedExpiry) {
    clearSession()
    return false
  }

  const expiresAt = new Date(storedExpiry)
  if (Number.isNaN(expiresAt.getTime()) || expiresAt <= new Date()) {
    clearSession()
    return false
  }

  sessionToken.value = storedToken
  sessionExpiresAt.value = expiresAt

  try {
    const me = await portalGetMe(storedToken)
    if (me.tourId !== tourId.value) {
      clearSession()
      return false
    }
    checkInCode.value = me.checkInCode
    policy.value = me.policy
    accessState.value = 'verified'
    return true
  } catch {
    clearSession()
    return false
  }
}

const verifyAccessToken = async (token: string, tone: 'info' | 'error' = 'error') => {
  verifyingAccess.value = true
  phoneHint.value = null
  participantSummary.value = null
  attemptsRemaining.value = null
  lockedUntil.value = null

  try {
    const result = await portalVerifyAccess(tourId.value, token)
    if (result.tourId !== tourId.value) {
      clearAccessToken()
      accessState.value = 'invalid'
      pushToast({ key: 'portal.access.invalidLink', tone })
      return
    }
    portal.value = result.portal
    policy.value = result.policy
    participantSummary.value = result.participant
    setDefaultDay()
    attemptsRemaining.value = result.attemptsRemaining

    if (result.isLocked) {
      lockedUntil.value = new Date(Date.now() + result.lockedForSeconds * 1000)
    }

    phoneHint.value = result.phoneHint ?? null
    accessState.value = 'verified'
  } catch {
    clearAccessToken()
    accessState.value = 'invalid'
    pushToast({ key: 'portal.access.invalidLink', tone })
  } finally {
    verifyingAccess.value = false
  }
}

const initAccessFlow = async () => {
  loading.value = true
  accessState.value = 'checking'
  checkInCode.value = ''
  phoneHint.value = null
  attemptsRemaining.value = null
  lockedUntil.value = null
  participantSummary.value = null
  policy.value = null

  try {
    await loadTour()
    const storedToken = globalThis.localStorage?.getItem(accessTokenKey.value) ?? ''
    accessToken.value = storedToken

    if (!storedToken) {
      accessState.value = 'missing'
      return
    }

    await verifyAccessToken(storedToken, 'info')
    const hasSession = await restoreSession()
    const currentPolicy = policy.value as PortalAccessPolicy | null
    if (!hasSession && currentPolicy && !currentPolicy.requireLast4ForQr && !currentPolicy.requireLast4ForPortal) {
      await confirmAccess(true)
    }
  } finally {
    loading.value = false
  }
}

const copyShareLink = async () => {
  copyStatusKey.value = null
  const url = globalThis.location?.href

  if (!url) {
    copyStatusKey.value = 'portal.actions.copyLinkFailed'
    return
  }

  const clipboard = globalThis.navigator?.clipboard
  if (!clipboard?.writeText) {
    copyStatusKey.value = 'errors.copyNotSupported'
    return
  }

  try {
    await clipboard.writeText(url)
    copyStatusKey.value = 'portal.actions.linkCopied'
  } catch {
    copyStatusKey.value = 'errors.copyFailed'
  }
}


const copyCheckInCode = async () => {
  codeCopyStatusKey.value = null
  if (!checkInCode.value) {
    codeCopyStatusKey.value = 'portal.qr.noCode'
    return
  }

  const clipboard = globalThis.navigator?.clipboard
  if (!clipboard?.writeText) {
    codeCopyStatusKey.value = 'errors.copyNotSupported'
    return
  }

  try {
    await clipboard.writeText(checkInCode.value)
    codeCopyStatusKey.value = 'common.copySuccess'
  } catch {
    codeCopyStatusKey.value = 'errors.copyFailed'
  }
}

const confirmAccess = async (skipValidation = false) => {
  if (!accessToken.value) {
    accessState.value = 'missing'
    return
  }

  const needsLast4 = (policy.value?.requireLast4ForPortal ?? false) || (policy.value?.requireLast4ForQr ?? false)
  const value = last4.value.trim()
  if (needsLast4 && !skipValidation && !value) {
    codeCopyStatusKey.value = 'portal.access.last4Required'
    return
  }

  confirmingAccess.value = true
  codeCopyStatusKey.value = null

  try {
    const result = await portalConfirmAccess(tourId.value, accessToken.value, needsLast4 ? value : undefined)
    const expiresAt = new Date(result.expiresAt)
    saveSession(result.sessionToken, expiresAt)
    last4.value = ''
    policy.value = result.policy
    participantSummary.value = result.participant

    const me = await portalGetMe(result.sessionToken)
    if (me.tourId !== tourId.value) {
      clearSession()
      accessState.value = 'invalid'
      return
    }

    checkInCode.value = me.checkInCode
    accessState.value = 'verified'
  } catch (err) {
    // Re-verify to refresh lock/attempts state.
    const message = err instanceof Error ? err.message : ''
    if (!message.includes('Too many attempts')) {
      pushToast({ key: 'portal.access.invalidLast4', tone: 'error' })
    }
    await verifyAccessToken(accessToken.value, 'error')
  } finally {
    confirmingAccess.value = false
  }
}

const handleLast4Input = () => {
  last4.value = last4.value.replace(/\D/g, '').slice(0, 4)
}

const copyCheckInLink = async () => {
  linkCopyStatusKey.value = null
  if (!checkInCode.value) {
    linkCopyStatusKey.value = 'portal.qr.linkNeedsCode'
    return
  }

  const link = buildCheckInLink(checkInCode.value)
  if (!link) {
    linkCopyStatusKey.value = 'portal.actions.copyLinkFailed'
    return
  }

  const clipboard = globalThis.navigator?.clipboard
  if (!clipboard?.writeText) {
    linkCopyStatusKey.value = 'errors.copyNotSupported'
    return
  }

  try {
    await clipboard.writeText(link)
    linkCopyStatusKey.value = 'portal.actions.linkCopied'
  } catch {
    linkCopyStatusKey.value = 'errors.copyFailed'
  }
}

watch([checkInCode, () => tourId.value], async ([value]) => {
  codeCopyStatusKey.value = null
  linkCopyStatusKey.value = null
  if (!value) {
    qrDataUrl.value = null
    return
  }

  try {
    const deepLink = buildCheckInLink(value)
    if (!deepLink) {
      qrDataUrl.value = null
      return
    }

    qrDataUrl.value = await QRCode.toDataURL(deepLink, {
      width: 200,
      margin: 1,
    })
  } catch {
    qrDataUrl.value = null
  }
}, { immediate: true })

watch(
  () => [route.query.pt, tourId.value],
  () => {
    applyAccessTokenFromQuery()
    void initAccessFlow()
  },
  { immediate: true }
)

let tickHandle: number | null = null

onMounted(() => {
  tickHandle = globalThis.setInterval(() => {
    nowTick.value = Date.now()
  }, 1000)
})

onUnmounted(() => {
  if (tickHandle) {
    globalThis.clearInterval(tickHandle)
  }
  tickHandle = null
})
</script>

<template>
  <div class="relative">
    <div class="space-y-6 pb-[calc(96px+env(safe-area-inset-bottom))] sm:space-y-8">
      <section class="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:p-6">
        <div class="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
          <div>
            <p class="text-xs uppercase tracking-wide text-slate-500">{{ t('portal.header.kicker') }}</p>
            <h1 class="mt-3 text-2xl font-semibold">
              {{ tour?.name ?? t('common.tour') }}
            </h1>
            <p class="text-sm text-slate-500" v-if="tour">
              {{ t('common.dateRange', { start: tour.startDate, end: tour.endDate }) }}
            </p>
          </div>
          <div class="flex w-full flex-col items-start gap-2 sm:w-auto sm:items-end">
            <button
              class="w-full rounded-xl border border-slate-200 bg-white px-4 py-3 text-sm font-medium text-slate-700 shadow-sm hover:border-slate-300 sm:w-auto"
              type="button" @click="copyShareLink">
              {{ t('portal.actions.copyPageLink') }}
            </button>
            <p v-if="copyStatusKey" class="text-xs text-slate-500">{{ t(copyStatusKey) }}</p>
          </div>
        </div>
      </section>

      <section
        v-if="accessState !== 'verified' || portalRequiresLast4"
        class="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:p-6"
      >
        <h2 class="text-lg font-semibold">{{ t('portal.access.title') }}</h2>
        <p class="mt-1 text-sm text-slate-600">{{ t('portal.access.subtitle') }}</p>
        <div class="mt-4 space-y-3 text-sm text-slate-600">
          <p v-if="accessState === 'checking' || verifyingAccess" class="text-xs text-slate-500">
            {{ t('portal.access.verifying') }}
          </p>
          <p v-else-if="accessState === 'missing'">
            {{ t('portal.access.missing') }}
          </p>
          <p v-else-if="accessState === 'invalid'">
            {{ t('portal.access.invalid') }}
          </p>
          <div v-else-if="portalRequiresLast4" class="space-y-2">
            <div v-if="isLocked" class="space-y-1">
              <p>{{ t('portal.access.locked', { seconds: lockRemainingSeconds }) }}</p>
              <p class="text-xs text-slate-500">{{ t('portal.access.tryLater') }}</p>
            </div>
            <template v-else>
              <p v-if="phoneHint">{{ t('portal.access.phoneHint', { hint: phoneHint }) }}</p>
              <p v-else class="text-sm text-slate-600">{{ t('portal.access.phoneMissing') }}</p>
              <div class="flex flex-col gap-2 sm:flex-row">
                <input
                  v-model.trim="last4"
                  class="w-full rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm uppercase tracking-wide focus:border-slate-400 focus:outline-none"
                  :placeholder="t('portal.access.last4Placeholder')"
                  type="text"
                  inputmode="numeric"
                  maxlength="4"
                  @input="handleLast4Input"
                />
                <button
                  class="w-full rounded-xl bg-slate-900 px-4 py-2 text-sm font-medium text-white hover:bg-slate-800 disabled:cursor-not-allowed disabled:opacity-60 sm:w-auto"
                  type="button"
                  :disabled="confirmingAccess || verifyingAccess || !phoneHint"
                  @click="() => confirmAccess()"
                >
                  {{ confirmingAccess ? t('portal.access.confirming') : t('portal.access.confirm') }}
                </button>
              </div>
              <p v-if="attemptsRemaining !== null" class="text-xs text-slate-500">
                {{ t('portal.access.attemptsRemaining', { count: attemptsRemaining }) }}
              </p>
            </template>
          </div>
          <p v-if="codeCopyStatusKey" class="text-xs text-rose-600">
            {{ t(codeCopyStatusKey) }}
          </p>
        </div>
      </section>

      <div
        v-if="portalReady"
        class="sticky top-16 z-10 hidden items-center gap-6 border-b border-slate-200 bg-slate-50/90 px-1 backdrop-blur md:flex"
      >
        <button
          v-for="tab in tabs"
          :key="tab.id"
          class="border-b-2 pb-3 text-sm font-medium transition"
          :class="
            activeTab === tab.id
              ? 'border-slate-900 text-slate-900'
              : 'border-transparent text-slate-500 hover:text-slate-900'
          "
          type="button"
          @click="setActiveTab(tab.id)"
        >
          {{ tab.label }}
        </button>
      </div>

      <LoadingState v-if="loading" message-key="portal.loading" />
      <ErrorState
        v-else-if="errorKey || errorMessage"
        :message="errorMessage ?? undefined"
        :message-key="errorKey ?? undefined"
        @retry="initAccessFlow"
      />

      <template v-else>
        <div v-if="portal && portalReady" class="space-y-6 sm:space-y-8">
          <section v-if="activeTab === 'days'"
            class="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:p-6">
            <h2 class="text-lg font-semibold">{{ t('portal.days.title') }}</h2>
            <div v-if="days.length === 0"
              class="mt-4 rounded-2xl border border-dashed border-slate-200 bg-slate-50 p-4 text-sm text-slate-500">
              {{ t('portal.days.empty') }}
            </div>
            <template v-else>
              <div class="mt-4 flex gap-2 overflow-x-auto pb-2">
                <button v-for="(day, index) in days" :key="day.day"
                  class="shrink-0 rounded-full border px-4 py-2 text-xs font-medium" :class="selectedDayIndex === index
                      ? 'border-slate-900 bg-slate-900 text-white'
                      : 'border-slate-200 bg-white text-slate-600'
                    " type="button" @click="selectDay(index)">
                  {{ t('portal.days.dayLabel', { day: day.day }) }}
                </button>
              </div>

              <div v-if="selectedDay" class="mt-4 rounded-2xl border border-slate-200 bg-slate-50 p-4 sm:p-5">
                <div class="text-sm font-semibold text-slate-800">
                  {{ t('portal.days.dayTitle', { day: selectedDay.day, title: selectedDay.title }) }}
                </div>
                <ul class="mt-3 space-y-2 text-sm text-slate-600">
                  <li v-for="item in selectedDay.items" :key="item" class="flex items-start gap-2">
                    <span class="mt-1 h-1.5 w-1.5 rounded-full bg-slate-400"></span>
                    <span>{{ item }}</span>
                  </li>
                </ul>
              </div>
            </template>
          </section>

          <section v-if="activeTab === 'docs'"
            class="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:p-6">
            <h2 class="text-lg font-semibold">{{ t('portal.docs.title') }}</h2>
            <div v-if="portal.links.length === 0"
              class="mt-4 rounded-2xl border border-dashed border-slate-200 bg-slate-50 p-4 text-sm text-slate-500">
              {{ t('portal.docs.empty') }}
            </div>
            <ul v-else class="mt-4 space-y-3 text-sm">
              <li v-for="link in portal.links" :key="link.url"
                class="flex flex-col gap-2 rounded-xl border border-slate-200 bg-slate-50 px-4 py-3 sm:flex-row sm:items-center sm:justify-between">
                <span class="font-medium text-slate-700">{{ link.label }}</span>
                <a class="inline-flex w-full items-center justify-center rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm font-medium text-slate-700 shadow-sm hover:border-slate-300 sm:w-auto"
                  :href="link.url" rel="noreferrer" target="_blank">
                  {{ t('common.open') }}
                </a>
              </li>
            </ul>
          </section>

          <section v-if="activeTab === 'qr'" class="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:p-6">
            <h2 class="text-lg font-semibold">{{ t('portal.qr.title') }}</h2>
            <div class="mt-4 space-y-4">
              <div class="rounded-2xl border border-slate-200 bg-slate-50 p-4">
                <p class="text-xs uppercase tracking-wide text-slate-400">{{ t('portal.qr.codeLabel') }}</p>
                <div class="mt-2 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
                  <div class="rounded-xl border border-slate-200 bg-white px-4 py-3 text-lg font-mono text-slate-800">
                    {{ checkInCode || t('portal.qr.codePlaceholder') }}
                  </div>
                  <div class="flex w-full flex-col gap-2 sm:w-auto sm:flex-row">
                    <button
                      class="w-full rounded-xl border border-slate-200 bg-white px-4 py-3 text-sm font-medium text-slate-700 shadow-sm hover:border-slate-300 disabled:cursor-not-allowed disabled:opacity-50 sm:w-auto"
                      type="button"
                      :disabled="!checkInCode"
                      @click="copyCheckInCode"
                    >
                      {{ t('portal.qr.copyCode') }}
                    </button>
                  </div>
                </div>
                <p v-if="codeCopyStatusKey" class="mt-2 text-xs text-slate-500">{{ t(codeCopyStatusKey) }}</p>
                <div v-if="qrRequiresLast4" class="mt-4 space-y-2 text-sm text-slate-600">
                  <div v-if="isLocked" class="space-y-1">
                    <p>{{ t('portal.access.locked', { seconds: lockRemainingSeconds }) }}</p>
                    <p class="text-xs text-slate-500">{{ t('portal.access.tryLater') }}</p>
                  </div>
                  <template v-else>
                    <p v-if="phoneHint">{{ t('portal.qr.last4Prompt', { hint: phoneHint }) }}</p>
                    <p v-else class="text-sm text-slate-600">{{ t('portal.access.phoneMissing') }}</p>
                    <div class="flex flex-col gap-2 sm:flex-row">
                      <input
                        v-model.trim="last4"
                        class="w-full rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm uppercase tracking-wide focus:border-slate-400 focus:outline-none"
                        :placeholder="t('portal.access.last4Placeholder')"
                        type="text"
                        inputmode="numeric"
                        maxlength="4"
                        @input="handleLast4Input"
                      />
                      <button
                        class="w-full rounded-xl bg-slate-900 px-4 py-2 text-sm font-medium text-white hover:bg-slate-800 disabled:cursor-not-allowed disabled:opacity-60 sm:w-auto"
                        type="button"
                        :disabled="confirmingAccess || verifyingAccess || !phoneHint"
                        @click="() => confirmAccess()"
                      >
                        {{ confirmingAccess ? t('portal.access.confirming') : t('portal.access.confirm') }}
                      </button>
                    </div>
                    <p v-if="attemptsRemaining !== null" class="text-xs text-slate-500">
                      {{ t('portal.access.attemptsRemaining', { count: attemptsRemaining }) }}
                    </p>
                  </template>
                </div>
                <p v-else-if="checkInCode" class="mt-3 text-xs text-slate-500">{{ t('portal.qr.helper') }}</p>
              </div>

              <div class="rounded-2xl border border-slate-200 bg-slate-50 p-4">
                <p class="text-xs uppercase tracking-wide text-slate-400">{{ t('portal.qr.title') }}</p>
                <div class="mt-4 flex items-center justify-center">
                  <div v-if="qrDataUrl"
                    class="flex items-center justify-center rounded-2xl border border-slate-200 bg-white p-4">
                    <img :src="qrDataUrl" :alt="t('portal.qr.imageAlt')" class="h-40 w-40" />
                  </div>
                  <div v-else class="text-sm text-slate-500">
                    {{ t('portal.qr.empty') }}
                  </div>
                </div>
                <div class="mt-4 flex flex-col items-start gap-2 sm:flex-row sm:items-center sm:justify-between">
                  <button
                    class="w-full rounded-xl border border-slate-200 bg-white px-4 py-2 text-sm font-medium text-slate-700 shadow-sm hover:border-slate-300 sm:w-auto"
                    type="button"
                    @click="copyCheckInLink"
                  >
                    {{ t('portal.qr.copyGuideLink') }}
                  </button>
                  <p v-if="linkCopyStatusKey" class="text-xs text-slate-500">{{ t(linkCopyStatusKey) }}</p>
                </div>
              </div>
            </div>
          </section>

          <section v-if="activeTab === 'info'" class="space-y-4">
            <div class="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:p-6">
              <h2 class="text-lg font-semibold">{{ t('portal.info.meetingTitle') }}</h2>
              <div class="mt-4 grid gap-3 text-sm text-slate-700 sm:grid-cols-2">
                <div class="rounded-xl border border-slate-200 bg-slate-50 p-3">
                  <p class="text-xs uppercase tracking-wide text-slate-400">{{ t('portal.info.time') }}</p>
                  <p class="mt-1 font-medium">{{ portal.meeting.time }}</p>
                </div>
                <div class="rounded-xl border border-slate-200 bg-slate-50 p-3">
                  <p class="text-xs uppercase tracking-wide text-slate-400">{{ t('portal.info.place') }}</p>
                  <p class="mt-1 font-medium">{{ portal.meeting.place }}</p>
                </div>
                <div class="rounded-xl border border-slate-200 bg-slate-50 p-3 sm:col-span-2">
                  <p class="text-xs uppercase tracking-wide text-slate-400">{{ t('portal.info.maps') }}</p>
                  <a class="mt-2 inline-flex w-full items-center justify-center rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm font-medium text-slate-700 shadow-sm hover:border-slate-300 sm:w-auto"
                    :href="portal.meeting.mapsUrl" rel="noreferrer" target="_blank">
                    {{ t('portal.info.openMaps') }}
                  </a>
                </div>
                <div class="rounded-xl border border-slate-200 bg-slate-50 p-3 sm:col-span-2">
                  <p class="text-xs uppercase tracking-wide text-slate-400">{{ t('portal.info.note') }}</p>
                  <p class="mt-2 text-sm text-slate-600">{{ portal.meeting.note }}</p>
                </div>
              </div>
            </div>

            <div class="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:p-6">
              <h2 class="text-lg font-semibold">{{ t('portal.info.notesTitle') }}</h2>
              <div v-if="portal.notes.length === 0"
                class="mt-4 rounded-2xl border border-dashed border-slate-200 bg-slate-50 p-4 text-sm text-slate-500">
                {{ t('portal.info.notesEmpty') }}
              </div>
              <ul v-else class="mt-4 space-y-2 text-sm text-slate-600">
                <li v-for="note in portal.notes" :key="note"
                  class="rounded-xl border border-slate-200 bg-slate-50 px-4 py-3">
                  {{ note }}
                </li>
              </ul>
            </div>
          </section>
        </div>

        <div v-else-if="portalReady" class="rounded-2xl border border-dashed border-slate-200 bg-white p-4 text-sm text-slate-500">
          {{ t('portal.empty') }}
        </div>
      </template>
    </div>

    <PortalTabBar v-if="portalReady" :tabs="tabs" :active="activeTab" @select="setActiveTab" />
  </div>
</template>
