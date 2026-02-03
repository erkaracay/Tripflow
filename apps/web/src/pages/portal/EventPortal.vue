<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import * as QRCode from 'qrcode'
import { useI18n } from 'vue-i18n'
import { portalGetMe } from '../../lib/api'
import PortalTabBar from '../../components/portal/PortalTabBar.vue'
import LoadingState from '../../components/ui/LoadingState.vue'
import ErrorState from '../../components/ui/ErrorState.vue'
import { useToast } from '../../lib/toast'
import type { EventPortalInfo, PortalMeResponse } from '../../types'

type TabKey = 'days' | 'docs' | 'qr' | 'info'

type PortalParticipant = PortalMeResponse['participant']

type RetryState = 'idle' | 'retrying'

const route = useRoute()
const router = useRouter()
const { t } = useI18n()
const { pushToast } = useToast()
const eventId = computed(() => route.params.eventId as string)

const event = ref<PortalMeResponse['event'] | null>(null)
const participant = ref<PortalParticipant | null>(null)
const portal = ref<EventPortalInfo | null>(null)
const schedule = ref<PortalMeResponse['schedule'] | null>(null)
const loading = ref(true)
const errorKey = ref<string | null>(null)
const errorMessage = ref<string | null>(null)
const networkErrorKey = ref<string | null>(null)
const retryState = ref<RetryState>('idle')
const sessionExpired = ref(false)

const activeTab = ref<TabKey>('days')
const selectedDayIndex = ref(0)

const checkInCode = ref('')
const qrDataUrl = ref<string | null>(null)
const qrError = ref(false)

const welcomeVisible = ref(false)
const welcomeTimer = ref<number | null>(null)
const hasLoadedOnce = ref(false)

const sessionToken = ref('')
const sessionExpiresAt = ref<Date | null>(null)

const tabs = computed<{ id: TabKey; label: string }[]>(() => [
  { id: 'days', label: t('portal.tabs.days') },
  { id: 'docs', label: t('portal.tabs.docs') },
  { id: 'qr', label: t('portal.tabs.qr') },
  { id: 'info', label: t('portal.tabs.info') },
])

const scheduleDays = computed(() => schedule.value?.days ?? [])
const selectedDay = computed(() => scheduleDays.value[selectedDayIndex.value] ?? null)

const hasSession = computed(() => {
  if (!sessionToken.value || !sessionExpiresAt.value) {
    return false
  }

  return sessionExpiresAt.value > new Date()
})

const requiresLogin = computed(() => !hasSession.value || sessionExpired.value)

const sessionTokenKey = computed(() => `tripflow.portal.session.${eventId.value}`)
const sessionExpiryKey = computed(() => `tripflow.portal.session.exp.${eventId.value}`)

const resolvePublicBase = () => {
  const envBase = (import.meta.env.VITE_PUBLIC_BASE_URL as string | undefined)?.trim()
  if (envBase) {
    return envBase.replace(/\/$/, '')
  }

  return globalThis.location?.origin ?? ''
}

const buildGuideLink = (code: string) => {
  const base = resolvePublicBase()
  if (!base) {
    return ''
  }

  return `${base}/guide/events/${eventId.value}/checkin?code=${encodeURIComponent(code)}`
}

const buildLoginLink = () => {
  const base = resolvePublicBase()
  if (!base) {
    return ''
  }

  return `${base}/e/login`
}

const formatActivityType = (type?: string | null) => {
  if (type === 'Meal') {
    return t('portal.schedule.typeMeal')
  }
  return t('portal.schedule.typeOther')
}

const formatActivityTime = (activity: { startTime?: string | null; endTime?: string | null }) => {
  const start = activity.startTime?.trim()
  const end = activity.endTime?.trim()
  if (start && end) {
    return `${start} – ${end}`
  }
  if (start) {
    return start
  }
  return t('portal.schedule.timeTba')
}

const buildMapsLink = (activity: { locationName?: string | null; address?: string | null }) => {
  const query = activity.address?.trim() || activity.locationName?.trim()
  if (!query) {
    return ''
  }
  return `https://maps.google.com/?q=${encodeURIComponent(query)}`
}

const copyToClipboard = async (value: string, successKey: string, errorKeyValue: string) => {
  const clipboard = globalThis.navigator?.clipboard
  if (!clipboard?.writeText) {
    pushToast({ key: 'errors.copyNotSupported', tone: 'error' })
    return
  }

  try {
    await clipboard.writeText(value)
    pushToast({ key: successKey, tone: 'success' })
  } catch {
    pushToast({ key: errorKeyValue, tone: 'error' })
  }
}

const copyLoginLink = async () => {
  const url = buildLoginLink()
  if (!url) {
    pushToast({ key: 'portal.actions.copyLinkFailed', tone: 'error' })
    return
  }

  await copyToClipboard(url, 'portal.actions.linkCopied', 'portal.actions.copyLinkFailed')
}

const copyCheckInCode = async () => {
  if (!checkInCode.value) {
    pushToast({ key: 'portal.qr.enterCode', tone: 'error' })
    return
  }

  await copyToClipboard(checkInCode.value, 'portal.qr.codeCopied', 'portal.actions.copyLinkFailed')
}

const copyGuideLink = async () => {
  if (!checkInCode.value) {
    pushToast({ key: 'portal.qr.linkNeedsCode', tone: 'error' })
    return
  }

  const link = buildGuideLink(checkInCode.value)
  if (!link) {
    pushToast({ key: 'portal.actions.copyLinkFailed', tone: 'error' })
    return
  }

  await copyToClipboard(link, 'portal.actions.linkCopied', 'portal.actions.copyLinkFailed')
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

const setDefaultDay = () => {
  const dayCount = scheduleDays.value.length
  if (dayCount === 0) {
    selectedDayIndex.value = 0
    return
  }

  const today = toDateOnly(new Date())

  const index = scheduleDays.value.findIndex((day) => {
    const date = parseDate(day.date)
    return date && toDateOnly(date).getTime() === today.getTime()
  })

  selectedDayIndex.value = index >= 0 ? index : 0
}

const selectDay = (index: number) => {
  selectedDayIndex.value = index
}

const setActiveTab = (value: string) => {
  activeTab.value = value as TabKey
}

const welcomeStorageKey = (participantId: string) =>
  `tf_welcome_shown:${eventId.value}:${participantId}`

const showWelcomeBanner = (participantId: string) => {
  if (!participantId) {
    return
  }

  const key = welcomeStorageKey(participantId)
  const alreadyShown = globalThis.localStorage?.getItem(key)
  if (alreadyShown) {
    return
  }

  globalThis.localStorage?.setItem(key, '1')
  welcomeVisible.value = true

  if (welcomeTimer.value) {
    globalThis.clearTimeout(welcomeTimer.value)
  }

  welcomeTimer.value = globalThis.setTimeout(() => {
    welcomeVisible.value = false
    welcomeTimer.value = null
  }, 4000)
}

const dismissWelcome = () => {
  welcomeVisible.value = false
  if (welcomeTimer.value) {
    globalThis.clearTimeout(welcomeTimer.value)
    welcomeTimer.value = null
  }
}

const setNetworkError = () => {
  networkErrorKey.value = 'portal.networkError'
}

const clearNetworkError = () => {
  networkErrorKey.value = null
}

const clearSession = () => {
  globalThis.localStorage?.removeItem(sessionTokenKey.value)
  globalThis.localStorage?.removeItem(sessionExpiryKey.value)
  sessionToken.value = ''
  sessionExpiresAt.value = null
}

const logoutPortal = async () => {
  clearSession()
  await router.push({ path: '/e/login' })
}

const restoreSession = () => {
  const token = globalThis.localStorage?.getItem(sessionTokenKey.value) ?? ''
  const expiry = globalThis.localStorage?.getItem(sessionExpiryKey.value) ?? ''

  if (!token || !expiry) {
    clearSession()
    return false
  }

  const expiresAt = new Date(expiry)
  if (Number.isNaN(expiresAt.getTime()) || expiresAt <= new Date()) {
    clearSession()
    return false
  }

  sessionToken.value = token
  sessionExpiresAt.value = expiresAt
  return true
}

const loadPortal = async () => {
  loading.value = true
  errorKey.value = null
  errorMessage.value = null
  sessionExpired.value = false

  if (!restoreSession()) {
    loading.value = false
    sessionExpired.value = false
    return
  }

  try {
    const response = await portalGetMe(sessionToken.value)
    event.value = response.event
    participant.value = response.participant
    portal.value = response.portal
    schedule.value = response.schedule
    checkInCode.value = response.participant.checkInCode
    hasLoadedOnce.value = true
    clearNetworkError()
    setDefaultDay()
    showWelcomeBanner(response.participant.id)
  } catch (err) {
    if (err && typeof err === 'object' && 'status' in err) {
      const status = (err as { status?: number }).status
      if (status === 401 || status === 403) {
        clearSession()
        sessionExpired.value = true
        return
      }
    }

    if (err instanceof TypeError || (err instanceof Error && /Failed to fetch|NetworkError/i.test(err.message))) {
      setNetworkError()
      if (!hasLoadedOnce.value) {
        errorKey.value = 'errors.portal.load'
      }
      return
    }

    errorMessage.value = err instanceof Error ? err.message : null
    if (!errorMessage.value) {
      errorKey.value = 'errors.portal.load'
    }
  } finally {
    loading.value = false
  }
}

const retryLoad = async () => {
  retryState.value = 'retrying'
  await loadPortal()
  retryState.value = 'idle'
}

const goToLogin = async () => {
  await router.push({ path: '/e/login', query: { eventId: eventId.value } })
}

const generateQr = async () => {
  qrError.value = false
  qrDataUrl.value = null

  if (!checkInCode.value) {
    return
  }

  const link = buildGuideLink(checkInCode.value)
  if (!link) {
    return
  }

  try {
    qrDataUrl.value = await QRCode.toDataURL(link, { margin: 1, width: 220 })
  } catch {
    qrError.value = true
  }
}

watch(checkInCode, () => {
  void generateQr()
})

watch(
  () => eventId.value,
  () => {
    void loadPortal()
  }
)

onMounted(() => {
  void loadPortal()
})

onUnmounted(() => {
  if (welcomeTimer.value) {
    globalThis.clearTimeout(welcomeTimer.value)
  }
})
</script>

<template>
  <div class="space-y-6">
    <div
      v-if="welcomeVisible && event"
      class="flex items-start justify-between rounded-2xl border border-emerald-200 bg-emerald-50 px-4 py-3 text-sm text-emerald-900"
    >
      <span>{{ t('portal.welcome', { event: event.name }) }}</span>
      <button class="text-emerald-700" type="button" @click="dismissWelcome">×</button>
    </div>

    <div
      v-if="networkErrorKey"
      class="flex flex-col gap-3 rounded-2xl border border-amber-200 bg-amber-50 p-4 text-sm text-amber-900"
    >
      <div>{{ t(networkErrorKey) }}</div>
      <button
        class="inline-flex w-full items-center justify-center rounded-full border border-amber-300 bg-white px-3 py-2 text-sm font-semibold text-amber-900 sm:w-auto"
        :disabled="retryState === 'retrying'"
        @click="retryLoad"
      >
        <span v-if="retryState === 'retrying'" class="mr-2 h-3 w-3 animate-spin rounded-full border border-amber-400 border-t-transparent"></span>
        {{ t('common.retry') }}
      </button>
    </div>

    <LoadingState v-if="loading && !hasLoadedOnce" message-key="portal.loading" />

    <ErrorState
      v-else-if="errorKey && !hasLoadedOnce"
      :message-key="errorKey"
      :message="errorMessage ?? undefined"
      @retry="retryLoad"
    />

    <section v-else class="space-y-6">
      <div
        v-if="requiresLogin"
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
        <div class="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:p-6">
          <div class="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
            <div>
              <div class="text-xs uppercase tracking-[0.2em] text-slate-400">{{ t('portal.header.kicker') }}</div>
              <h1 class="mt-3 text-2xl font-semibold">{{ event?.name ?? t('common.event') }}</h1>
              <p class="mt-1 text-sm text-slate-500" v-if="event">
                {{ t('common.dateRange', { start: event.startDate, end: event.endDate }) }}
              </p>
            </div>
            <div class="flex flex-col gap-2 sm:flex-row sm:items-center">
              <button
                class="inline-flex w-full items-center justify-center rounded-full border border-slate-200 px-3 py-2 text-sm font-semibold text-slate-700 hover:border-slate-300 sm:w-auto"
                @click="copyLoginLink"
              >
                {{ t('portal.actions.copyLoginLink') }}
              </button>
              <button
                class="inline-flex w-full items-center justify-center rounded-full border border-slate-200 px-3 py-2 text-sm font-semibold text-slate-700 hover:border-slate-300 sm:w-auto"
                @click="logoutPortal"
              >
                {{ t('portal.actions.logout') }}
              </button>
            </div>
          </div>
        </div>

        <div class="hidden md:flex gap-4 border-b border-slate-200 text-sm">
          <button
            v-for="tab in tabs"
            :key="tab.id"
            class="-mb-px border-b-2 px-1 pb-2 transition"
            :class="
              activeTab === tab.id
                ? 'border-slate-900 text-slate-900'
                : 'border-transparent text-slate-500 hover:text-slate-900'
            "
            @click="setActiveTab(tab.id)"
          >
            {{ tab.label }}
          </button>
        </div>

        <div class="space-y-6">
          <section v-if="activeTab === 'days'" class="space-y-4">
            <div class="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:p-6">
              <h2 class="text-lg font-semibold">{{ t('portal.days.title') }}</h2>
              <p class="mt-1 text-sm text-slate-500" v-if="scheduleDays.length === 0">
                {{ t('portal.schedule.empty') }}
              </p>
              <div v-else class="mt-4 space-y-4">
                <div class="flex gap-2 overflow-x-auto">
                  <button
                    v-for="(day, index) in scheduleDays"
                    :key="day.id"
                    class="min-w-[120px] rounded-2xl border px-3 py-2 text-left text-xs"
                    :class="
                      index === selectedDayIndex
                        ? 'border-slate-900 bg-slate-900 text-white'
                        : 'border-slate-200 text-slate-600'
                    "
                    @click="selectDay(index)"
                  >
                    <div class="text-[10px] uppercase tracking-[0.15em] text-slate-400" :class="index === selectedDayIndex ? 'text-white/70' : ''">
                      {{ t('portal.schedule.dayLabel', { day: index + 1 }) }}
                    </div>
                    <div class="mt-1 text-sm font-semibold">
                      {{ day.title || t('portal.schedule.dayFallback', { day: index + 1 }) }}
                    </div>
                    <div class="mt-1 text-[11px] text-slate-400" :class="index === selectedDayIndex ? 'text-white/70' : ''">
                      {{ day.date }}
                    </div>
                  </button>
                </div>

                <div v-if="selectedDay" class="space-y-4">
                  <div>
                    <div class="text-sm font-semibold text-slate-900">
                      {{ selectedDay.title || t('portal.schedule.dayFallback', { day: selectedDayIndex + 1 }) }}
                    </div>
                    <div class="text-xs text-slate-500">{{ selectedDay.date }}</div>
                    <div v-if="selectedDay.notes" class="mt-2 text-sm text-slate-600">
                      {{ selectedDay.notes }}
                    </div>
                  </div>

                  <div v-if="selectedDay.activities.length === 0" class="text-sm text-slate-500">
                    {{ t('portal.schedule.noActivities') }}
                  </div>

                  <div v-else class="space-y-3">
                    <div
                      v-for="activity in selectedDay.activities"
                      :key="activity.id"
                      class="rounded-2xl border border-slate-200 bg-slate-50 p-4"
                    >
                      <div class="flex items-start justify-between gap-3">
                        <div>
                          <div class="text-xs font-semibold uppercase tracking-[0.2em] text-slate-500">
                            {{ formatActivityTime(activity) }}
                          </div>
                          <div class="mt-1 text-base font-semibold text-slate-900">{{ activity.title }}</div>
                        </div>
                        <span
                          class="rounded-full border px-3 py-1 text-xs font-semibold"
                          :class="activity.type === 'Meal' ? 'border-amber-200 bg-amber-50 text-amber-700' : 'border-slate-200 bg-white text-slate-600'"
                        >
                          {{ formatActivityType(activity.type) }}
                        </span>
                      </div>

                      <div v-if="activity.locationName || activity.address" class="mt-3 text-sm text-slate-600">
                        <div class="font-medium text-slate-700" v-if="activity.locationName">{{ activity.locationName }}</div>
                        <div v-if="activity.address">{{ activity.address }}</div>
                        <a
                          v-if="buildMapsLink(activity)"
                          :href="buildMapsLink(activity)"
                          target="_blank"
                          rel="noreferrer"
                          class="mt-2 inline-flex items-center gap-2 text-sm font-semibold text-slate-700 underline"
                        >
                          {{ t('portal.schedule.openMap') }}
                        </a>
                      </div>

                      <div v-if="activity.directions" class="mt-2 text-sm text-slate-500">
                        {{ activity.directions }}
                      </div>

                      <div v-if="activity.menuText" class="mt-3 rounded-xl border border-amber-100 bg-amber-50 px-3 py-2 text-sm text-amber-800">
                        <div class="text-xs font-semibold uppercase tracking-[0.2em] text-amber-700">
                          {{ t('portal.schedule.menuLabel') }}
                        </div>
                        <div class="mt-1 whitespace-pre-line">{{ activity.menuText }}</div>
                      </div>

                      <div v-if="activity.notes" class="mt-3 text-sm text-slate-600">
                        {{ activity.notes }}
                      </div>

                      <a
                        v-if="activity.surveyUrl"
                        :href="activity.surveyUrl"
                        target="_blank"
                        rel="noreferrer"
                        class="mt-3 inline-flex items-center gap-2 text-sm font-semibold text-slate-900 underline"
                      >
                        {{ t('portal.schedule.openSurvey') }}
                      </a>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </section>

          <section v-else-if="activeTab === 'docs'" class="space-y-4">
            <div class="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:p-6">
              <h2 class="text-lg font-semibold">{{ t('portal.docs.title') }}</h2>
              <p class="mt-1 text-sm text-slate-500" v-if="portal?.links.length === 0">
                {{ t('portal.docs.empty') }}
              </p>
              <div v-else class="mt-4 grid gap-3">
                <a
                  v-for="link in portal?.links"
                  :key="link.url"
                  :href="link.url"
                  class="flex items-center justify-between rounded-xl border border-slate-200 px-3 py-2 text-sm text-slate-700 hover:border-slate-300"
                  target="_blank"
                  rel="noreferrer"
                >
                  <span>{{ link.label }}</span>
                  <span class="text-xs text-slate-400">↗</span>
                </a>
              </div>
            </div>
          </section>

          <section v-else-if="activeTab === 'qr'" class="space-y-4">
            <div class="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:p-6">
              <h2 class="text-lg font-semibold">{{ t('portal.qr.title') }}</h2>
              <p class="mt-1 text-sm text-slate-500">{{ t('portal.qr.helper') }}</p>

              <div class="mt-5 flex flex-col items-center gap-4">
                <div class="text-center">
                  <div class="text-xs uppercase tracking-[0.2em] text-slate-400">{{ t('portal.qr.codeLabel') }}</div>
                  <div class="mt-1 text-2xl font-semibold tracking-[0.2em]">
                    {{ checkInCode || t('portal.qr.noCode') }}
                  </div>
                </div>

                <div v-if="qrError" class="text-sm text-rose-600">
                  {{ t('portal.qr.failed') }}
                </div>
                <img
                  v-else-if="qrDataUrl"
                  :src="qrDataUrl"
                  :alt="t('portal.qr.imageAlt')"
                  class="h-56 w-56 rounded-xl border border-slate-200 bg-white p-2"
                />
                <div v-else class="text-sm text-slate-500">{{ t('portal.qr.empty') }}</div>

                <div class="flex w-full flex-col gap-2 sm:flex-row sm:items-center sm:justify-center">
                  <button
                    class="inline-flex w-full items-center justify-center rounded-full border border-slate-200 px-3 py-2 text-sm font-semibold text-slate-700 hover:border-slate-300 sm:w-auto"
                    @click="copyCheckInCode"
                  >
                    {{ t('portal.qr.copyCode') }}
                  </button>
                  <button
                    class="inline-flex w-full items-center justify-center rounded-full border border-slate-200 px-3 py-2 text-sm font-semibold text-slate-700 hover:border-slate-300 sm:w-auto"
                    @click="copyGuideLink"
                  >
                    {{ t('portal.qr.copyGuideLink') }}
                  </button>
                </div>
              </div>
            </div>
          </section>

          <section v-else class="space-y-4">
            <div class="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:p-6">
              <h2 class="text-lg font-semibold">{{ t('portal.info.meetingTitle') }}</h2>
              <div class="mt-4 space-y-3 text-sm text-slate-600">
                <div>
                  <div class="text-xs uppercase text-slate-400">{{ t('portal.info.time') }}</div>
                  <div class="mt-1 font-medium text-slate-800">{{ portal?.meeting.time || '-' }}</div>
                </div>
                <div>
                  <div class="text-xs uppercase text-slate-400">{{ t('portal.info.place') }}</div>
                  <div class="mt-1 font-medium text-slate-800">{{ portal?.meeting.place || '-' }}</div>
                </div>
                <div>
                  <div class="text-xs uppercase text-slate-400">{{ t('portal.info.maps') }}</div>
                  <a
                    v-if="portal?.meeting.mapsUrl"
                    :href="portal.meeting.mapsUrl"
                    class="mt-1 inline-flex text-sm text-slate-700 underline"
                    target="_blank"
                    rel="noreferrer"
                  >
                    {{ t('portal.info.openMaps') }}
                  </a>
                  <div v-else class="mt-1 text-sm text-slate-500">-</div>
                </div>
                <div>
                  <div class="text-xs uppercase text-slate-400">{{ t('portal.info.note') }}</div>
                  <div class="mt-1 text-sm text-slate-600">{{ portal?.meeting.note || '-' }}</div>
                </div>
              </div>
            </div>

            <div class="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:p-6">
              <h2 class="text-lg font-semibold">{{ t('portal.info.notesTitle') }}</h2>
              <p v-if="portal?.notes.length === 0" class="mt-2 text-sm text-slate-500">
                {{ t('portal.info.notesEmpty') }}
              </p>
              <ul v-else class="mt-3 list-disc space-y-2 pl-5 text-sm text-slate-600">
                <li v-for="note in portal?.notes" :key="note">{{ note }}</li>
              </ul>
            </div>
          </section>
        </div>
      </div>
    </section>

    <PortalTabBar class="md:hidden" :tabs="tabs" :active="activeTab" @select="setActiveTab" />
  </div>
</template>
