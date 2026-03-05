<script setup lang="ts">
import { computed, onUnmounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import * as QRCode from 'qrcode'
import { useI18n } from 'vue-i18n'
import { setLocale, type Locale } from '../../i18n'
import { portalGetMe, portalLogout } from '../../lib/api'
import { resetViewportZoom } from '../../lib/viewport'
import PortalTabBar from '../../components/portal/PortalTabBar.vue'
import PortalInfoTabs from '../../components/portal/PortalInfoTabs.vue'
import PortalMealSelectionCard from '../../components/portal/PortalMealSelectionCard.vue'
import LoadingState from '../../components/ui/LoadingState.vue'
import ErrorState from '../../components/ui/ErrorState.vue'
import AppSegmentedControl from '../../components/ui/AppSegmentedControl.vue'
import RichTextContent from '../../components/editor/RichTextContent.vue'
import { clearPortalHeader, setPortalHeader } from '../../lib/portalHeader'
import { formatPhoneDisplay } from '../../lib/normalize'
import { buildWhatsAppUrl } from '../../lib/whatsapp'
import { useHorizontalSwipeTabs } from '../../composables/useHorizontalSwipeTabs'
import type { EventPortalInfo, PortalMeResponse } from '../../types'

type TabKey = 'days' | 'docs' | 'qr' | 'info'

type PortalParticipant = PortalMeResponse['participant']

type RetryState = 'idle' | 'retrying'

const props = defineProps<{ eventId?: string }>()
const route = useRoute()
const router = useRouter()
const { t, locale } = useI18n()
const eventId = computed(() => (props.eventId ?? route.params.eventId) as string)

const event = ref<PortalMeResponse['event'] | null>(null)
const participant = ref<PortalParticipant | null>(null)
const portal = ref<EventPortalInfo | null>(null)
const schedule = ref<PortalMeResponse['schedule'] | null>(null)
const docs = ref<PortalMeResponse['docs'] | null>(null)
const loading = ref(true)
const errorKey = ref<string | null>(null)
const errorMessage = ref<string | null>(null)
const networkErrorKey = ref<string | null>(null)
const retryState = ref<RetryState>('idle')
const sessionExpired = ref(false)

const activeTab = ref<TabKey>('days')
const previousActiveTab = ref<TabKey>('days')
const selectedDayIndex = ref(0)
const previousSelectedDayIndex = ref(0)

const checkInCode = ref('')
const qrDataUrl = ref<string | null>(null)
const qrError = ref(false)

const welcomeVisible = ref(false)
const welcomeTimer = ref<ReturnType<typeof setTimeout> | null>(null)
const hasLoadedOnce = ref(false)
const showDayScrollHint = ref(false)
let scrollHintTimer: ReturnType<typeof setTimeout> | null = null

const menuExpanded = ref<Record<string, boolean>>({})
const toggleMenu = (activityId: string) => {
  menuExpanded.value[activityId] = !menuExpanded.value[activityId]
  menuExpanded.value = { ...menuExpanded.value }
}

const programExpanded = ref<Record<string, boolean>>({})
const toggleProgram = (activityId: string) => {
  programExpanded.value[activityId] = !programExpanded.value[activityId]
  programExpanded.value = { ...programExpanded.value }
}

const hasValidSession = ref(false)

const tabs = computed<{ id: TabKey; label: string }[]>(() => [
  { id: 'days', label: t('portal.tabs.days') },
  { id: 'docs', label: t('portal.tabs.docs') },
  { id: 'qr', label: t('portal.tabs.qr') },
  { id: 'info', label: t('portal.tabs.info') },
])

const localeOptions = computed(() => [
  { value: 'tr', label: t('portal.info.languageTr') },
  { value: 'en', label: t('portal.info.languageEn') },
])

const tabOrder: TabKey[] = ['days', 'docs', 'qr', 'info']

const getTabIndex = (tab: TabKey) => {
  const index = tabOrder.indexOf(tab)
  return index >= 0 ? index : 0
}

const topTabTransitionName = computed(() =>
  getTabIndex(activeTab.value) >= getTabIndex(previousActiveTab.value)
    ? 'app-tab-slide-forward'
    : 'app-tab-slide-backward'
)

const dayTransitionName = computed(() =>
  selectedDayIndex.value >= previousSelectedDayIndex.value
    ? 'app-tab-slide-forward'
    : 'app-tab-slide-backward'
)

const scheduleDays = computed(() => schedule.value?.days ?? [])
const selectedDay = computed(() => scheduleDays.value[selectedDayIndex.value] ?? null)

const requiresLogin = computed(() => !hasValidSession.value || sessionExpired.value)

const resolvePublicBase = () => {
  const envBase = (import.meta.env.VITE_PUBLIC_BASE_URL as string | undefined)?.trim()
  if (envBase) {
    return envBase.replace(/\/$/, '')
  }

  return globalThis.location?.origin ?? ''
}

const formatPortalDate = (value?: string | null) => {
  if (!value) {
    return '-'
  }

  const trimmed = value.trim()
  if (!trimmed) {
    return '-'
  }

  const datePart = (trimmed.includes('T') ? trimmed.split('T')[0] : trimmed.split(' ')[0]) ?? ''
  const [year, month, day] = datePart.split('-')
  if (year && month && day) {
    return `${day}.${month}.${year}`
  }

  return trimmed
}

const normalizeContactText = (value?: string | null) => {
  const trimmed = value?.trim()
  return trimmed ? trimmed : ''
}

const sanitizePhoneForHref = (value?: string | null) => {
  const raw = normalizeContactText(value)
  if (!raw) {
    return ''
  }

  return raw.replace(/\D/g, '')
}

const toTelHref = (value?: string | null) => {
  const digits = sanitizePhoneForHref(value)
  if (!digits) {
    return ''
  }
  return `tel:+${digits}`
}

const toPhoneDisplay = (value?: string | null) => {
  const raw = normalizeContactText(value)
  if (!raw) {
    return ''
  }
  return formatPhoneDisplay(raw) || raw
}

const toWhatsappHref = (value?: string | null) => {
  const digits = sanitizePhoneForHref(value)
  if (!digits) {
    return ''
  }

  const eventTitle = normalizeContactText(event.value?.name)
  const message = eventTitle
    ? `${eventTitle} - Merhaba, bir konuda yardımcı olabilir misiniz?`
    : 'Merhaba, bir konuda yardımcı olabilir misiniz?'
  return buildWhatsAppUrl(digits, message)
}

type ContactRowRole = 'guide' | 'leader' | 'emergency'

type ContactRow = {
  role: ContactRowRole
  name: string
  phoneDisplay: string
  telHref: string
  whatsappHref: string
}

const contactRows = computed<ContactRow[]>(() => {
  const contacts = portal.value?.eventContacts
  if (!contacts) {
    return []
  }

  const rows: ContactRow[] = []
  const pushRow = (
    role: ContactRowRole,
    nameValue: string | null | undefined,
    phoneValue: string | null | undefined,
    allowWhatsapp: boolean
  ) => {
    const name = normalizeContactText(nameValue)
    const phone = normalizeContactText(phoneValue)
    if (!name && !phone) {
      return
    }

    rows.push({
      role,
      name,
      phoneDisplay: toPhoneDisplay(phone),
      telHref: toTelHref(phone),
      whatsappHref: allowWhatsapp ? toWhatsappHref(phone) : '',
    })
  }

  pushRow('guide', contacts.guideName, contacts.guidePhone, true)
  pushRow('leader', contacts.leaderName, contacts.leaderPhone, true)
  pushRow('emergency', '', contacts.emergencyPhone, false)

  return rows
})

const looksLikeHtml = (value?: string | null) => !!value && /<\/?[a-z][\s\S]*>/i.test(value)

const escapeHtml = (value: string) => value
  .replace(/&/g, '&amp;')
  .replace(/</g, '&lt;')
  .replace(/>/g, '&gt;')
  .replace(/"/g, '&quot;')
  .replace(/'/g, '&#39;')

const portalNotesContent = computed(() => {
  const notes = portal.value?.notes ?? []
  if (notes.length === 0) {
    return ''
  }

  if (notes.length === 1 && looksLikeHtml(notes[0])) {
    return notes[0] ?? ''
  }

  const items = notes
    .map((note) => note.trim())
    .filter(Boolean)
    .map((note) => `<li>${escapeHtml(note)}</li>`)
    .join('')

  return items ? `<ul>${items}</ul>` : ''
})

const whatsappGroupUrl = computed(() => normalizeContactText(portal.value?.eventContacts?.whatsappGroupUrl))
const hasContactsBlock = computed(() => contactRows.value.length > 0 || Boolean(whatsappGroupUrl.value))

const buildGuideLink = (code: string) => {
  const base = resolvePublicBase()
  if (!base) {
    return ''
  }

  return `${base}/guide/events/${eventId.value}/checkin?code=${encodeURIComponent(code)}`
}

const formatActivityType = (type?: string | null) => {
  if (type === 'Meal') return t('portal.schedule.typeMeal')
  if (type === 'Program') return t('portal.schedule.typeProgram')
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
    previousSelectedDayIndex.value = 0
    selectedDayIndex.value = 0
    return
  }

  const today = toDateOnly(new Date())

  const index = scheduleDays.value.findIndex((day) => {
    const date = parseDate(day.date)
    return date && toDateOnly(date).getTime() === today.getTime()
  })

  const nextIndex = index >= 0 ? index : 0
  previousSelectedDayIndex.value = nextIndex
  selectedDayIndex.value = nextIndex
}

const selectDay = (index: number) => {
  if (index === selectedDayIndex.value) {
    return
  }
  previousSelectedDayIndex.value = selectedDayIndex.value
  selectedDayIndex.value = index
}

const setActiveTab = (value: string) => {
  const nextTab = value as TabKey
  if (nextTab === activeTab.value) {
    return
  }
  previousActiveTab.value = activeTab.value
  activeTab.value = nextTab
}

const isTopTabSwipeEnabled = () =>
  typeof window !== 'undefined' && window.matchMedia('(max-width: 767px)').matches && !requiresLogin.value

const { bindSwipeHandlers } = useHorizontalSwipeTabs<TabKey>({
  orderedIds: tabOrder,
  activeId: activeTab,
  setActiveId: (value) => setActiveTab(value),
  enabled: isTopTabSwipeEnabled,
})

const switchLocale = (value: Locale) => {
  if (locale.value === value) {
    return
  }
  setLocale(value)
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

const logoutPortal = async () => {
  hasValidSession.value = false
  try {
    await portalLogout()
  } catch {}
  await router.push({ path: '/e/login' })
}

const loadPortal = async () => {
  loading.value = true
  errorKey.value = null
  errorMessage.value = null
  sessionExpired.value = false
  hasValidSession.value = false

  try {
    const response = await portalGetMe()
    resetViewportZoom()
    hasValidSession.value = true
    event.value = response.event
    setPortalHeader(
      response.event.name,
      (response.event as { logoUrl?: string | null }).logoUrl ?? null,
      response.event.startDate,
      response.event.endDate
    )
    participant.value = response.participant
    portal.value = response.portal
    schedule.value = response.schedule
    docs.value = response.docs
    checkInCode.value = response.participant.checkInCode
    hasLoadedOnce.value = true
    clearNetworkError()
    setDefaultDay()
    showWelcomeBanner(response.participant.id)
  } catch (err) {
    console.error('[Portal] loadPortal: portalGetMe error', err)
    if (err && typeof err === 'object' && 'status' in err) {
      const status = (err as { status?: number }).status
      if (status === 401 || status === 403) {
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

const openDocsPdf = () => {
  const target = `/e/${eventId.value}/docs/print`
  const opened = window.open(target, '_blank', 'noopener,noreferrer')
  if (!opened) {
    void router.push({ path: target })
  }
}

const handleMealSessionExpired = () => {
  hasValidSession.value = false
  sessionExpired.value = true
}

watch(checkInCode, () => {
  void generateQr()
})

watch(
  () => eventId.value,
  (newEventId) => {
    // Only load if eventId is actually available
    if (newEventId && typeof newEventId === 'string' && newEventId.trim() !== '') {
      void loadPortal()
    }
  },
  { immediate: true }
)

watch(
  () => scheduleDays.value.length,
  (count) => {
    if (scrollHintTimer) {
      globalThis.clearTimeout(scrollHintTimer)
      scrollHintTimer = null
    }

    if (count > 2) {
      showDayScrollHint.value = true
      scrollHintTimer = globalThis.setTimeout(() => {
        showDayScrollHint.value = false
        scrollHintTimer = null
      }, 15000)
    } else {
      showDayScrollHint.value = false
    }
  }
)

// Session restoration is handled by watch(() => eventId.value) with immediate: true

onUnmounted(() => {
  if (welcomeTimer.value) {
    globalThis.clearTimeout(welcomeTimer.value)
  }
  if (scrollHintTimer) {
    globalThis.clearTimeout(scrollHintTimer)
  }
  clearPortalHeader()
})
</script>

<template>
  <div :class="requiresLogin ? 'space-y-6' : 'space-y-6 pb-32'">
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
        <nav class="hidden w-full md:block" :aria-label="t('portal.tabs.days')">
          <div
            class="app-segmented w-full shadow-sm"
            :style="{
              '--segment-count': String(Math.max(tabs.length, 1)),
              '--segment-index': String(getTabIndex(activeTab)),
              gridTemplateColumns: `repeat(${Math.max(tabs.length, 1)}, minmax(0, 1fr))`,
            }"
          >
            <span class="app-segmented-indicator" aria-hidden="true" />
            <button
              v-for="tab in tabs"
              :key="tab.id"
              class="app-segmented-button flex min-w-0 items-center justify-center gap-2 px-3 py-2 text-sm font-semibold"
              :class="tab.id === activeTab ? 'app-segmented-button-active' : 'text-slate-600 hover:text-slate-900'"
              type="button"
              role="tab"
              :aria-selected="tab.id === activeTab"
              @click="setActiveTab(tab.id)"
            >
              <span class="flex h-5 w-5 items-center justify-center" aria-hidden="true">
                <svg
                  v-if="tab.id === 'days'"
                  viewBox="0 0 24 24"
                  class="h-4 w-4"
                  fill="none"
                  stroke="currentColor"
                  stroke-width="1.6"
                  stroke-linecap="round"
                  stroke-linejoin="round"
                >
                  <path d="M7 2v3M17 2v3M3 9h18" />
                  <path d="M5 6h14a2 2 0 0 1 2 2v12a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2z" />
                </svg>
                <svg
                  v-else-if="tab.id === 'docs'"
                  viewBox="0 0 24 24"
                  class="h-4 w-4"
                  fill="none"
                  stroke="currentColor"
                  stroke-width="1.6"
                  stroke-linecap="round"
                  stroke-linejoin="round"
                >
                  <path d="M7 2h7l5 5v13a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2z" />
                  <path d="M14 2v6h6" />
                  <path d="M9 13h6M9 17h6" />
                </svg>
                <svg
                  v-else-if="tab.id === 'qr'"
                  viewBox="0 0 24 24"
                  class="h-4 w-4"
                  fill="none"
                  stroke="currentColor"
                  stroke-width="1.6"
                  stroke-linecap="round"
                  stroke-linejoin="round"
                >
                  <rect x="3" y="3" width="6" height="6" rx="1" />
                  <rect x="15" y="3" width="6" height="6" rx="1" />
                  <rect x="3" y="15" width="6" height="6" rx="1" />
                  <rect x="13" y="13" width="2" height="2" rx="0.5" />
                  <rect x="19" y="19" width="2" height="2" rx="0.5" />
                  <rect x="15" y="17" width="2" height="2" rx="0.5" />
                </svg>
                <svg
                  v-else
                  viewBox="0 0 24 24"
                  class="h-4 w-4"
                  fill="none"
                  stroke="currentColor"
                  stroke-width="1.6"
                  stroke-linecap="round"
                  stroke-linejoin="round"
                >
                  <circle cx="12" cy="12" r="9" />
                  <path d="M12 10v6" />
                  <path d="M12 7h.01" />
                </svg>
              </span>
              <span class="truncate">{{ tab.label }}</span>
            </button>
          </div>
        </nav>

        <Transition :name="topTabTransitionName" mode="out-in">
        <div :key="activeTab" class="space-y-6">
          <section v-if="activeTab === 'days'" class="space-y-4">
            <p v-if="participant?.fullName" class="text-base font-medium text-slate-700">
              {{ t('portal.greeting', { name: participant.fullName }) }}
            </p>
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
                    class="min-w-[120px] rounded-2xl border px-3 py-2 text-left text-xs transition-[background-color,color,border-color,box-shadow,transform] duration-200 ease-out"
                    :class="
                      index === selectedDayIndex
                        ? 'border-slate-900 bg-slate-900 text-white shadow-sm'
                        : 'border-slate-200 text-slate-600 hover:border-slate-300 hover:bg-slate-50 hover:text-slate-900'
                    "
                    @click="selectDay(index)"
                  >
                    <div class="mt-1 text-sm font-semibold">
                      {{ day.title || t('portal.schedule.dayFallback', { day: index + 1 }) }}
                    </div>
                    <div class="mt-1 text-[11px] text-slate-400" :class="index === selectedDayIndex ? 'text-white/70' : ''">
                      {{ formatPortalDate(day.date) }}
                    </div>
                  </button>
                </div>
                <div v-if="showDayScrollHint" class="text-center text-xs font-semibold text-rose-600 animate-pulse">
                  {{ t('portal.schedule.scrollHint') }}
                </div>

                <Transition :name="dayTransitionName" mode="out-in">
                <div v-if="selectedDay" :key="selectedDay.id" class="space-y-4">
                  <div>
                    <div class="text-xs text-slate-500">{{ formatPortalDate(selectedDay.date) }}</div>
                    <div v-if="selectedDay.placesToVisit" class="mt-2 flex items-start gap-2">
                      <span class="text-slate-500" aria-hidden="true">📍</span>
                      <div>
                        <div class="text-xs font-medium text-slate-500">{{ t('portal.schedule.placesToVisit') }}</div>
                        <div class="mt-0.5 text-sm font-medium text-slate-900">{{ selectedDay.placesToVisit }}</div>
                      </div>
                    </div>
                    <div v-if="selectedDay.notes" class="mt-2 text-sm text-slate-600">
                      <RichTextContent :content="selectedDay.notes" />
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
                          :class="activity.type === 'Meal' ? 'border-amber-200 bg-amber-50 text-amber-700' : activity.type === 'Program' ? 'border-sky-200 bg-sky-50 text-sky-700' : 'border-slate-200 bg-white text-slate-600'"
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

              <div
                v-if="activity.menuText"
                class="mt-3 rounded-xl border border-amber-100 bg-amber-50 px-3 py-2 text-sm text-amber-800"
              >
                        <button
                          type="button"
                          class="flex w-full list-none cursor-pointer items-center justify-between gap-2 border-0 bg-transparent p-0 text-left"
                          @click="toggleMenu(activity.id)"
                        >
                          <span class="text-xs font-semibold uppercase tracking-[0.2em] text-amber-700">
                            {{ t('portal.schedule.menuLabel') }}
                          </span>
                          <span class="text-xs font-semibold text-amber-700 underline">
                            {{ menuExpanded[activity.id] ? t('portal.schedule.menuHide') : t('portal.schedule.menuView') }}
                          </span>
                        </button>
                        <Transition name="menu-expand">
                          <div
                            v-if="menuExpanded[activity.id]"
                            class="mt-2 overflow-hidden border-t border-amber-200/50 pt-2 text-amber-800"
                          >
                            <RichTextContent :content="activity.menuText" />
                          </div>
                        </Transition>
                      </div>

                      <div
                        v-if="activity.type === 'Program' && activity.programContent"
                        class="mt-3 rounded-xl border border-sky-100 bg-sky-50 px-3 py-2 text-sm text-sky-800"
                      >
                        <button
                          type="button"
                          class="flex w-full list-none cursor-pointer items-center justify-between gap-2 border-0 bg-transparent p-0 text-left"
                          @click="toggleProgram(activity.id)"
                        >
                          <span class="text-xs font-semibold uppercase tracking-[0.2em] text-sky-700">
                            {{ t('portal.schedule.programContent') }}
                          </span>
                          <span class="text-xs font-semibold text-sky-700 underline">
                            {{ programExpanded[activity.id] ? t('portal.schedule.menuHide') : t('portal.schedule.menuView') }}
                          </span>
                        </button>
                        <div
                          v-if="!programExpanded[activity.id]"
                          class="mt-2 overflow-hidden border-t border-sky-200/50 pt-2 text-sky-800 line-clamp-5"
                        >
                          <RichTextContent :content="activity.programContent" />
                        </div>
                        <Transition name="program-expand">
                          <div
                            v-if="programExpanded[activity.id]"
                            class="mt-2 overflow-hidden border-t border-sky-200/50 pt-2 text-sky-800"
                          >
                            <RichTextContent :content="activity.programContent" />
                          </div>
                </Transition>
              </div>

              <PortalMealSelectionCard
                v-if="activity.type === 'Meal'"
                :activity="activity"
                :event-title="event?.name"
                :session-expired="sessionExpired"
                @session-expired="handleMealSessionExpired"
              />

              <div v-if="activity.notes" class="mt-3 text-sm text-slate-600">
                <RichTextContent :content="activity.notes" />
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
                </Transition>
              </div>
            </div>
          </section>

          <section v-else-if="activeTab === 'docs'" class="space-y-4">
            <p v-if="participant?.fullName" class="text-base font-medium text-slate-700">
              {{ t('portal.greeting', { name: participant.fullName }) }}
            </p>
            <div class="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
              <div>
                <h2 class="text-lg font-semibold">{{ t('portal.docs.title') }}</h2>
                <p class="text-sm text-slate-500">{{ t('portal.docs.subtitle') }}</p>
              </div>
              <button
                class="hidden sm:inline-flex items-center justify-center rounded-full border border-slate-200 bg-white px-4 py-2 text-sm font-semibold text-slate-700 hover:border-slate-300"
                type="button"
                @click="openDocsPdf"
              >
                {{ t('portal.docs.pdf') }}
              </button>
            </div>

            <PortalInfoTabs :docs="docs" :participant-name="participant?.fullName" />

            <button
              class="inline-flex sm:hidden w-full items-center justify-center rounded-full border border-slate-200 bg-white px-4 py-2 text-sm font-semibold text-slate-700 hover:border-slate-300"
              type="button"
              @click="openDocsPdf"
            >
              {{ t('portal.docs.pdf') }}
            </button>

            <div class="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm">
              <div class="text-sm font-semibold text-slate-900">{{ t('portal.docs.linksTitle') }}</div>
              <p v-if="portal?.links.length === 0" class="mt-2 text-sm text-slate-500">
                {{ t('portal.docs.linksEmpty') }}
              </p>
              <div v-else class="mt-3 grid gap-3">
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
            <p v-if="participant?.fullName" class="text-base font-medium text-slate-700">
              {{ t('portal.greeting', { name: participant.fullName }) }}
            </p>
            <div class="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:p-6">
              <h2 class="text-lg font-semibold">{{ t('portal.qr.title') }}</h2>

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

                <p class="mt-2 max-w-xs text-center text-xs leading-relaxed text-slate-500">
                  {{ t('portal.qr.screenshotHint') }}
                </p>
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

            <div v-if="hasContactsBlock" class="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:p-6">
              <h2 class="text-lg font-semibold">{{ t('portal.info.contactsTitle') }}</h2>
              <div class="mt-4 space-y-3 text-sm text-slate-600">
                <div
                  v-for="row in contactRows"
                  :key="row.role"
                  class="rounded-xl border border-slate-100 bg-slate-50 px-3 py-3"
                >
                  <div class="text-xs uppercase text-slate-400">
                    {{
                      row.role === 'guide'
                        ? t('portal.info.guide')
                        : row.role === 'leader'
                          ? t('portal.info.leader')
                          : t('portal.info.emergency')
                    }}
                  </div>
                  <div v-if="row.name" class="mt-1 font-medium text-slate-800">{{ row.name }}</div>
                  <div v-if="row.phoneDisplay" class="mt-2 flex flex-wrap items-center gap-x-3 gap-y-1.5">
                    <a
                      v-if="row.telHref"
                      :href="row.telHref"
                      class="font-medium text-slate-800 underline"
                    >
                      {{ row.phoneDisplay }}
                    </a>
                    <span v-else class="font-medium text-slate-800">{{ row.phoneDisplay }}</span>
                    <a
                      v-if="row.whatsappHref"
                      :href="row.whatsappHref"
                      class="text-sm font-medium text-emerald-700 underline"
                      rel="noreferrer"
                      target="_blank"
                    >
                      {{ t('portal.info.messageOnWhatsapp') }}
                    </a>
                  </div>
                </div>

                <a
                  v-if="whatsappGroupUrl"
                  :href="whatsappGroupUrl"
                  class="inline-flex text-sm font-medium text-emerald-700 underline"
                  rel="noreferrer"
                  target="_blank"
                >
                  {{ t('portal.info.joinWhatsappGroup') }}
                </a>
              </div>
            </div>

            <div class="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:p-6">
              <h2 class="text-lg font-semibold">{{ t('portal.info.notesTitle') }}</h2>
              <p v-if="portal?.notes.length === 0" class="mt-2 text-sm text-slate-500">
                {{ t('portal.info.notesEmpty') }}
              </p>
              <div v-else class="mt-3 text-sm text-slate-600">
                <RichTextContent :content="portalNotesContent" />
              </div>
            </div>

            <div class="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:p-6">
              <h2 class="text-lg font-semibold">{{ t('portal.info.languageTitle') }}</h2>
              <AppSegmentedControl
                class-name="mt-3"
                :model-value="locale"
                :options="localeOptions"
                :aria-label="t('portal.info.languageTitle')"
                full-width
                @update:modelValue="switchLocale($event as Locale)"
              />

              <button
                class="mt-5 inline-flex w-full items-center justify-center rounded-full border border-rose-200 px-4 py-2 text-sm font-semibold text-rose-600 hover:border-rose-300"
                type="button"
                @click="logoutPortal"
              >
                {{ t('portal.actions.logout') }}
              </button>
            </div>
          </section>
        </div>
        </Transition>
      </div>
    </section>

  </div>

  <div
    v-if="!requiresLogin"
    class="fixed inset-x-0 bottom-0 z-20 border-t border-slate-200 bg-white/95 backdrop-blur md:hidden"
    style="padding-bottom: env(safe-area-inset-bottom)"
  >
    <PortalTabBar :tabs="tabs" :active="activeTab" :swipe-handlers="bindSwipeHandlers" @select="setActiveTab" />
  </div>
</template>

<style scoped>
.menu-expand-enter-active,
.menu-expand-leave-active {
  transition: opacity 0.3s ease-out, max-height 0.3s ease-out;
}
.menu-expand-enter-from,
.menu-expand-leave-to {
  opacity: 0;
  max-height: 0;
}
.menu-expand-enter-to,
.menu-expand-leave-from {
  opacity: 1;
  max-height: 500px;
}

.program-expand-enter-active,
.program-expand-leave-active {
  transition: opacity 0.3s ease-out, max-height 0.3s ease-out;
}
.program-expand-enter-from,
.program-expand-leave-to {
  opacity: 0;
  max-height: 0;
}
.program-expand-enter-to,
.program-expand-leave-from {
  opacity: 1;
  max-height: 2000px;
}
</style>
