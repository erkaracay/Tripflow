<script setup lang="ts">
import { computed, nextTick, onMounted, onUnmounted, ref, watch } from 'vue'
import { useRoute, useRouter, type LocationQueryRaw } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { apiGet, apiPatchWithPayload, apiPost, apiPostWithPayload } from '../../lib/api'
import { normalizeQrCode } from '../../lib/qr'
import { formatUtcToLocal } from '../../lib/formatters'
import { formatPhoneDisplay, normalizeCheckInCode, normalizePhone } from '../../lib/normalize'
import { useToast } from '../../lib/toast'
import { buildWhatsAppUrl } from '../../lib/whatsapp'
import WhatsAppIcon from '../../components/icons/WhatsAppIcon.vue'
import LoadingState from '../../components/ui/LoadingState.vue'
import ErrorState from '../../components/ui/ErrorState.vue'
import QrScannerModal from '../../components/QrScannerModal.vue'
import ConfirmDialog from '../../components/ui/ConfirmDialog.vue'
import type {
  CheckInResponse,
  CheckInSummary,
  CheckInUndoResponse,
  ResetAllCheckInsResponse,
  Participant,
  ParticipantWillNotAttendResponse,
  ParticipantResolve,
  EventPortalInfo,
  Event as EventDto,
} from '../../types'

type CheckInDirection = 'Entry' | 'Exit'
type CheckInMethod = 'Manual' | 'QrScan'

const props = defineProps<{ eventId?: string }>()
const route = useRoute()
const router = useRouter()
const { t } = useI18n()
const eventId = computed(() => (props.eventId ?? route.params.eventId) as string)

const event = ref<EventDto | null>(null)
const participants = ref<Participant[]>([])
const portalInfo = ref<EventPortalInfo | null>(null)
const summary = ref<CheckInSummary>({ arrivedCount: 0, totalCount: 0 })
const loading = ref(true)
const errorKey = ref<string | null>(null)
const errorMessage = ref<string | null>(null)

const searchTerm = ref('')
const debouncedSearchTerm = ref('')
const participantFilter = ref<'all' | 'arrived' | 'not_arrived' | 'will_not_attend'>('all')
const checkInCode = ref('')
const actionMessageKey = ref<string | null>(null)
const actionMessageText = ref<string | null>(null)
const actionErrorKey = ref<string | null>(null)
const actionErrorText = ref<string | null>(null)
const isCheckingIn = ref(false)
const dataLoaded = ref(false)
const lastAutoCode = ref('')
const lastResult = ref<CheckInResponse | null>(null)
const lastErrorResult = ref<string | null>(null)
const lastErrorCode = ref<string | null>(null)
const codeInput = ref<HTMLInputElement | null>(null)
const autoCheckInAfterScan = ref(true)
const autoCheckInStorageKey = 'infora:guide:autoCheckInAfterScan'
const checkInDirection = ref<CheckInDirection>('Entry')
const scannerOpen = ref(false)
const scanErrorKey = ref<string | null>(null)
const scanErrorText = ref<string | null>(null)
const scanFoundCode = ref<string | null>(null)
const scanFoundParticipant = ref<ParticipantResolve | null>(null)
const scanResolving = ref(false)
const highlightParticipantId = ref<string | null>(null)
const undoingParticipantId = ref<string | null>(null)
const updatingWillNotAttendId = ref<string | null>(null)
const resettingAllCheckIns = ref(false)
const confirmOpen = ref(false)
const confirmMessageKey = ref<string | null>(null)
const lastAction = ref<{ participantId: string; participantName: string } | null>(null)
let highlightTimer: ReturnType<typeof setTimeout> | undefined
let lastActionTimer: ReturnType<typeof setTimeout> | undefined
let searchDebounceTimer: ReturnType<typeof setTimeout> | undefined
let lastScannedCode: string | null = null
let lastScannedAt = 0

const { pushToast, removeToast } = useToast()

const filteredParticipants = computed(() => {
  const query = debouncedSearchTerm.value.trim().toLowerCase()
  const filter = participantFilter.value

  let filtered = participants.value
  if (filter === 'will_not_attend') {
    filtered = filtered.filter((participant) => participant.willNotAttend)
  } else {
    filtered = filtered.filter((participant) => !participant.willNotAttend)
    if (filter === 'arrived') {
      filtered = filtered.filter((participant) => participant.arrived)
    } else if (filter === 'not_arrived') {
      filtered = filtered.filter((participant) => !participant.arrived)
    }
  }

  if (!query) {
    return filtered
  }

  return filtered.filter((participant) => {
    const haystack = [
      participant.fullName,
      participant.email,
      participant.phone,
      participant.tcNo,
      participant.checkInCode,
      participant.details?.roomNo,
      participant.details?.agencyName,
    ]
      .filter(Boolean)
      .join(' ')
      .toLowerCase()

    return haystack.includes(query)
  })
})

const hasData = computed(() => Boolean(event.value))
const effectiveTotal = computed(() => Math.max(summary.value.totalCount - willNotAttendCount.value, 0))
const notArrivedCount = computed(() => Math.max(effectiveTotal.value - summary.value.arrivedCount, 0))
const willNotAttendCount = computed(
  () => participants.value.filter((participant) => participant.willNotAttend).length
)

const setParticipantFilter = (value: typeof participantFilter.value) => {
  participantFilter.value = value
}

const formatLastLog = (log: Participant['lastLog'] | null | undefined) => {
  if (!log) {
    return '—'
  }
  const directionLabel = log.direction === 'Exit' ? t('common.exit') : t('common.entry')
  const methodLabel = log.method === 'QrScan' ? 'QR' : t('common.manual')
  const time = formatUtcToLocal(String(log.createdAt), { timeOnly: true })
  return `${directionLabel} (${methodLabel}) • ${time}`
}


const loadData = async () => {
  loading.value = true
  errorKey.value = null
  errorMessage.value = null

  try {
    const [eventData, participantsData, summaryData] = await Promise.all([
      apiGet<EventDto>(`/api/events/${eventId.value}`),
      apiGet<Participant[]>(`/api/guide/events/${eventId.value}/participants`),
      apiGet<CheckInSummary>(`/api/guide/events/${eventId.value}/checkins/summary`),
    ])

    event.value = eventData
    participants.value = participantsData
    summary.value = summaryData

    try {
      portalInfo.value = await apiGet<EventPortalInfo>(`/api/events/${eventId.value}/portal`)
    } catch {
      portalInfo.value = null
    }
  } catch (err) {
    errorMessage.value = err instanceof Error ? err.message : null
    if (!errorMessage.value) {
      errorKey.value = 'errors.checkIn.load'
    }
  } finally {
    loading.value = false
    dataLoaded.value = true
  }
}

const updateFromCheckIn = (response: CheckInResponse, method: CheckInMethod) => {
  summary.value = {
    arrivedCount: response.arrivedCount,
    totalCount: response.totalCount,
  }

  const direction = (response.direction ?? 'Entry') as CheckInDirection
  const result = response.result ?? (response.alreadyArrived ? 'AlreadyArrived' : 'Success')
  const createdAt = response.loggedAt ?? null

  participants.value = participants.value.map((participant) => {
    if (participant.id !== response.participantId) {
      return participant
    }

    return {
      ...participant,
      arrived: direction === 'Entry' ? true : participant.arrived,
      lastLog: createdAt
        ? { direction, method, result, createdAt }
        : participant.lastLog ?? null,
    }
  })
}

const setLastAction = (response: CheckInResponse) => {
  if (response.alreadyArrived) {
    return
  }

  lastAction.value = {
    participantId: response.participantId,
    participantName: response.participantName,
  }

  if (lastActionTimer) {
    globalThis.clearTimeout(lastActionTimer)
  }

  lastActionTimer = globalThis.setTimeout(() => {
    lastAction.value = null
    lastActionTimer = undefined
  }, 10000)
}

const updateFromUndo = (response: CheckInUndoResponse) => {
  summary.value = {
    arrivedCount: response.arrivedCount,
    totalCount: response.totalCount,
  }

  participants.value = participants.value.map((participant) =>
    participant.id === response.participantId
      ? { ...participant, arrived: false }
      : participant
  )
}

const undoCheckIn = async (participantId: string) => {
  undoingParticipantId.value = participantId
  try {
    const response = await apiPost<CheckInUndoResponse>(
      `/api/guide/events/${eventId.value}/checkins/undo`,
      { participantId }
    )
    updateFromUndo(response)
    if (lastAction.value?.participantId === participantId) {
      lastAction.value = null
    }
    const tone = response.alreadyUndone ? 'info' : 'success'
    pushToast({
      key: response.alreadyUndone ? 'toast.alreadyUndone' : 'toast.checkInUndone',
      tone,
    })
  } catch (err) {
    pushToast({ key: 'toast.undoFailed', tone: 'error' })
  } finally {
    undoingParticipantId.value = null
  }
}

const requestResetAllCheckIns = () => {
  confirmMessageKey.value = 'guide.checkIn.resetAllConfirm'
  confirmOpen.value = true
}

const resetAllCheckIns = async () => {
  if (resettingAllCheckIns.value) {
    return
  }

  resettingAllCheckIns.value = true
  try {
    const response = await apiPost<ResetAllCheckInsResponse>(
      `/api/guide/events/${eventId.value}/checkins/reset-all`,
      {}
    )
    summary.value = {
      arrivedCount: response.arrivedCount,
      totalCount: response.totalCount,
    }
    participants.value = participants.value.map((participant) => ({ ...participant, arrived: false }))
    lastAction.value = null
    pushToast({ key: 'toast.checkInsResetAll', tone: 'success' })
  } catch {
    pushToast({ key: 'toast.checkInsResetAllFailed', tone: 'error' })
  } finally {
    resettingAllCheckIns.value = false
  }
}

const handleConfirm = async () => {
  await resetAllCheckIns()
}

const submitCheckIn = async (
  code: string,
  options?: { suppressToast?: boolean; method?: CheckInMethod; direction?: CheckInDirection }
) => {
  actionMessageKey.value = null
  actionMessageText.value = null
  actionErrorKey.value = null
  actionErrorText.value = null
  lastResult.value = null
  lastErrorResult.value = null
  lastErrorCode.value = null

  const normalized = normalizeCheckInCode(code).slice(0, 8)
  if (!normalized) {
    actionErrorKey.value = 'validation.checkInCodeRequired'
    if (!options?.suppressToast) {
      pushToast({ key: 'toast.invalidCodeFormat', tone: 'error' })
    }
    return false
  }

  const direction = options?.direction ?? checkInDirection.value
  isCheckingIn.value = true
  try {
    const response = await apiPostWithPayload<CheckInResponse>(`/api/guide/events/${eventId.value}/checkins`, {
      checkInCode: normalized,
      direction,
      method: options?.method ?? 'Manual',
    })

    updateFromCheckIn(response, options?.method ?? 'Manual')

    const responseDirection = (response.direction ?? direction) as CheckInDirection
    const result = response.result ?? (response.alreadyArrived ? 'AlreadyArrived' : 'Success')
    if (responseDirection === 'Exit') {
      actionMessageText.value = t('common.exitLogged', { name: response.participantName })
    } else {
      actionMessageKey.value = response.alreadyArrived
        ? 'guide.checkIn.alreadyCheckedIn'
        : 'guide.checkIn.success'
    }

    if (!options?.suppressToast) {
      if (responseDirection === 'Exit') {
        pushToast({ key: 'common.exitLogged', params: { name: response.participantName }, tone: 'success' })
      } else if (result === 'AlreadyArrived') {
        pushToast({ key: 'toast.alreadyCheckedIn', tone: 'info' })
      } else {
        pushToast({
          key: 'toast.checkedIn',
          tone: 'success',
          timeout: 10000,
          action: {
            labelKey: 'common.undo',
            onClick: () => {
              void undoCheckIn(response.participantId)
            },
          },
        })
      }
    }
    checkInCode.value = ''
    lastResult.value = response
    if (responseDirection === 'Entry') {
      setLastAction(response)
      highlightParticipantId.value = response.participantId
      if (highlightTimer) {
        globalThis.clearTimeout(highlightTimer)
      }
      highlightTimer = globalThis.setTimeout(() => {
        highlightParticipantId.value = null
      }, 2000)
    }
    await nextTick()
    codeInput.value?.focus()
    return true
  } catch (err) {
    actionErrorText.value = err instanceof Error ? err.message : null
    if (!actionErrorText.value) {
      actionErrorKey.value = 'errors.checkIn.failed'
    }
    if (!options?.suppressToast) {
      const payload = err && typeof err === 'object' ? (err as { payload?: unknown }).payload : undefined
      const code =
        payload && typeof payload === 'object' && payload !== null && 'code' in payload
          ? String((payload as { code?: string }).code ?? '')
          : ''
      const result =
        payload && typeof payload === 'object' && payload !== null && 'result' in payload
          ? String((payload as { result?: string }).result ?? '')
          : ''

      if (code === 'will_not_attend') {
        pushToast({ key: 'toast.willNotAttendBlocked', tone: 'error' })
      } else if (result === 'NotFound') {
        pushToast({ key: 'toast.codeNotFound', tone: 'error' })
      } else if (result === 'InvalidRequest') {
        pushToast({ key: 'toast.invalidCodeFormat', tone: 'error' })
      } else {
        pushToast({ key: direction === 'Entry' ? 'common.checkInFailed' : 'common.exitFailed', tone: 'error' })
      }
    }
    const payload = err && typeof err === 'object' ? (err as { payload?: unknown }).payload : undefined
    if (payload && typeof payload === 'object' && payload !== null && 'code' in payload) {
      lastErrorCode.value = String((payload as { code?: string }).code ?? null)
    }
    if (payload && typeof payload === 'object' && payload !== null && 'result' in payload) {
      lastErrorResult.value = String((payload as { result?: string }).result ?? null)
    }
    return false
  } finally {
    isCheckingIn.value = false
  }
}

const clearSearch = () => {
  searchTerm.value = ''
}

const resetScanState = () => {
  scanErrorKey.value = null
  scanErrorText.value = null
  scanFoundCode.value = null
  scanFoundParticipant.value = null
}

const handleScanCheckIn = async () => {
  if (!scanFoundCode.value) {
    return
  }

  const success = await submitCheckIn(scanFoundCode.value, { method: 'QrScan' })
  if (success) {
    resetScanState()
  }
}

const handleScanResult = async (raw: string) => {
  scannerOpen.value = false
  resetScanState()

  const code = normalizeQrCode(raw)
  if (!code) {
    scanErrorKey.value = 'guide.checkIn.invalidQr'
    return
  }

  const now = Date.now()
  if (lastScannedCode === code && now - lastScannedAt < 2500) {
    return
  }
  lastScannedCode = code
  lastScannedAt = now

  checkInCode.value = code
  await nextTick()
  codeInput.value?.focus()

  if (!autoCheckInAfterScan.value) {
    const action = checkInDirection.value === 'Entry' ? t('common.checkIn') : t('common.exit')
    pushToast(`${t('common.codeCaptured', { code })} ${t('common.pressToContinue', { action })}`, 'info', 4500)
    return
  }

  if (isCheckingIn.value) {
    return
  }

  const progressLabel = checkInDirection.value === 'Entry' ? t('common.checkingIn') : t('common.loggingExit')
  const loadingToastId = pushToast(`${t('common.codeCaptured', { code })} ${progressLabel}`, 'info', { timeout: 0 })
  const success = await submitCheckIn(code, { suppressToast: true, method: 'QrScan' })
  removeToast(loadingToastId)

  if (success) {
    const name = lastResult.value?.participantName ?? code
    if (checkInDirection.value === 'Entry' && lastResult.value?.result === 'AlreadyArrived') {
      pushToast({ key: 'toast.alreadyCheckedIn', tone: 'info' })
    } else {
      const key = checkInDirection.value === 'Entry' ? 'common.entryLogged' : 'common.exitLogged'
      pushToast({ key, params: { name }, tone: 'success' })
    }
    resetScanState()
	  } else {
	    if (lastErrorCode.value === 'will_not_attend') {
	      pushToast({ key: 'toast.willNotAttendBlocked', tone: 'error' })
	    } else if (lastErrorResult.value === 'NotFound') {
	      pushToast({ key: 'toast.codeNotFound', tone: 'error' })
	    } else if (lastErrorResult.value === 'InvalidRequest') {
	      pushToast({ key: 'toast.invalidCodeFormat', tone: 'error' })
	    } else {
	      const key = checkInDirection.value === 'Entry' ? 'common.checkInFailed' : 'common.exitFailed'
	      pushToast({ key, tone: 'error' })
	    }
	  }
}

const openScanner = () => {
  resetScanState()
  scannerOpen.value = true
}

const scanAgain = () => {
  resetScanState()
  scannerOpen.value = true
}

const focusManualCode = async () => {
  await nextTick()
  codeInput.value?.focus()
}

const handleCheckInSubmit = async () => {
  await submitCheckIn(checkInCode.value, { method: 'Manual' })
}

const handleCodeInput = (event: Event) => {
  const target = event.target as HTMLInputElement
  checkInCode.value = normalizeCheckInCode(target.value).slice(0, 8)
}

const markArrived = async (participant: Participant) => {
  if (participant.arrived) {
    return
  }

  await submitCheckIn(participant.checkInCode, { direction: 'Entry', method: 'Manual' })
}

const setWillNotAttend = async (participant: Participant, willNotAttend: boolean) => {
  if (updatingWillNotAttendId.value === participant.id) {
    return
  }

  updatingWillNotAttendId.value = participant.id
  try {
    const response = await apiPatchWithPayload<ParticipantWillNotAttendResponse>(
      `/api/guide/events/${eventId.value}/participants/${participant.id}/will-not-attend`,
      { willNotAttend }
    )

    participants.value = participants.value.map((item) =>
      item.id === participant.id
        ? {
            ...item,
            willNotAttend: response.willNotAttend,
            arrived: response.arrived,
            lastLog: response.lastLog ?? item.lastLog ?? null,
          }
        : item
    )

    pushToast({
      key: willNotAttend ? 'toast.willNotAttendEnabled' : 'toast.willNotAttendDisabled',
      tone: 'success',
    })
  } catch {
    pushToast({ key: 'toast.willNotAttendFailed', tone: 'error' })
  } finally {
    updatingWillNotAttendId.value = null
  }
}

const openWhatsApp = (participant: Participant) => {
  const phone = participant.phone?.trim()
  if (!phone) {
    pushToast({ key: 'warnings.phoneRequired', tone: 'error' })
    return
  }

  const eventName = event.value?.name?.trim() || t('common.event')
  const meetingPoint = portalInfo.value?.meeting?.place?.trim() || ''
  const locationPart = meetingPoint ? t('guide.checkIn.whatsappLocation', { location: meetingPoint }) : ''
  const message = t('guide.checkIn.whatsappMessage', {
    name: participant.fullName,
    event: eventName,
    locationPart,
  })

  const normalizedPhone = normalizePhone(phone).normalized || phone
  const url = buildWhatsAppUrl(normalizedPhone, message)
  if (!url) {
    pushToast({ key: 'warnings.phoneRequired', tone: 'error' })
    return
  }

  globalThis.open(url, '_blank', 'noopener,noreferrer')
}

const buildTelLink = (phone: string) => {
  const normalized = normalizePhone(phone).normalized || phone
  const digits = normalized.replace(/\D/g, '')
  if (!digits) {
    return ''
  }
  if (normalized.startsWith('+')) {
    return `tel:${normalized}`
  }
  return `tel:+${digits}`
}

const openCall = (participant: Participant) => {
  const phone = participant.phone?.trim()
  if (!phone) {
    pushToast({ key: 'warnings.phoneRequired', tone: 'error' })
    return
  }

  const telLink = buildTelLink(phone)
  if (!telLink) {
    pushToast({ key: 'warnings.phoneRequired', tone: 'error' })
    return
  }

  globalThis.location.href = telLink
}

const clearCheckInQuery = async () => {
  const nextQuery: LocationQueryRaw = { ...route.query }
  delete nextQuery.code
  delete nextQuery.checkInCode
  await router.replace({ query: nextQuery })
}

const resolveQueryCode = async () => {
  if (!dataLoaded.value || isCheckingIn.value) {
    return
  }

  const raw = route.query.code ?? route.query.checkInCode
  const queryCode = typeof raw === 'string' ? normalizeCheckInCode(raw).slice(0, 8) : ''
  if (!queryCode) {
    return
  }

  checkInCode.value = queryCode

  if (lastAutoCode.value === queryCode) {
    return
  }

  lastAutoCode.value = queryCode
  if (autoCheckInAfterScan.value) {
    const success = await submitCheckIn(queryCode, { method: 'QrScan' })
    if (success) {
      await clearCheckInQuery()
    }
  }
}

const initialize = async () => {
  await loadData()
  await resolveQueryCode()
  await nextTick()
  codeInput.value?.focus()
}

watch(searchTerm, (value) => {
  if (searchDebounceTimer) {
    globalThis.clearTimeout(searchDebounceTimer)
  }
  searchDebounceTimer = globalThis.setTimeout(() => {
    debouncedSearchTerm.value = value
  }, 200)
}, { immediate: true })

watch(
  () => [route.query.code, route.query.checkInCode, dataLoaded.value],
  () => {
    void resolveQueryCode()
  }
)

watch(autoCheckInAfterScan, (value) => {
  globalThis.localStorage?.setItem(autoCheckInStorageKey, value ? '1' : '0')
})

onMounted(() => {
  // Tarayıcıya otomatik gönder: her açılışta seçili (varsayılan true)
  void initialize()
})

onUnmounted(() => {
  if (highlightTimer) {
    globalThis.clearTimeout(highlightTimer)
  }
  if (lastActionTimer) {
    globalThis.clearTimeout(lastActionTimer)
  }
  if (searchDebounceTimer) {
    globalThis.clearTimeout(searchDebounceTimer)
  }
})
</script>

<template>
  <div class="space-y-6 sm:space-y-8">
    <div class="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:p-6">
      <div class="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
        <div class="min-w-0 flex-1">
          <RouterLink
            class="inline-block text-sm text-slate-600 underline-offset-2 hover:text-slate-900"
            to="/guide/events"
          >
            {{ t('nav.backToGuideEvents') }}
          </RouterLink>
          <nav class="mt-3 flex flex-wrap items-center gap-2" aria-label="Event sections">
            <RouterLink
              :to="`/guide/events/${eventId}/checkin`"
              active-class="bg-slate-100 border-slate-300 font-medium text-slate-900"
              class="whitespace-nowrap rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm text-slate-700 transition-colors hover:border-slate-300 hover:bg-slate-50"
            >
              {{ t('common.checkIn') }}
            </RouterLink>
            <RouterLink
              :to="`/guide/events/${eventId}/activities/checkin`"
              active-class="bg-slate-100 border-slate-300 font-medium text-slate-900"
              class="whitespace-nowrap rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm text-slate-700 transition-colors hover:border-slate-300 hover:bg-slate-50"
            >
              {{ t('admin.eventDetail.activityCheckIn') }}
            </RouterLink>
            <RouterLink
              :to="`/guide/events/${eventId}/equipment`"
              active-class="bg-slate-100 border-slate-300 font-medium text-slate-900"
              class="whitespace-nowrap rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm text-slate-700 transition-colors hover:border-slate-300 hover:bg-slate-50"
            >
              {{ t('admin.eventDetail.equipment') }}
            </RouterLink>
            <RouterLink
              :to="`/guide/events/${eventId}/program`"
              active-class="bg-slate-100 border-slate-300 font-medium text-slate-900"
              class="whitespace-nowrap rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm text-slate-700 transition-colors hover:border-slate-300 hover:bg-slate-50"
            >
              {{ t('admin.eventDetail.openProgram') }}
            </RouterLink>
          </nav>
          <h1 class="mt-4 text-2xl font-semibold text-slate-900">{{ event?.name ?? t('guide.checkIn.title') }}</h1>
          <p class="mt-1 text-sm text-slate-500" v-if="event">
            {{ t('common.dateRange', { start: event.startDate, end: event.endDate }) }}
          </p>
        </div>
        <div class="rounded-xl border border-slate-200 bg-slate-50 px-4 py-3 text-sm">
          <div class="text-xs uppercase tracking-wide text-slate-400">{{ t('common.arrivedLabel') }}</div>
          <div class="mt-1 text-xl font-semibold text-slate-800">
            {{ summary.arrivedCount }} / {{ effectiveTotal }}
          </div>
          <div class="mt-2 flex flex-col gap-1">
            <button
              class="block text-left text-xs font-semibold text-slate-600 underline-offset-2 hover:text-slate-900 hover:underline"
              type="button"
              @click="setParticipantFilter(participantFilter === 'not_arrived' ? 'all' : 'not_arrived')"
            >
              {{ t('common.notArrived') }}: {{ notArrivedCount }}
            </button>
            <button
              class="block text-left text-xs font-semibold text-slate-500 underline-offset-2 hover:text-slate-900 hover:underline"
              type="button"
              @click="setParticipantFilter(participantFilter === 'will_not_attend' ? 'all' : 'will_not_attend')"
            >
              {{ t('common.willNotAttend') }}: {{ willNotAttendCount }}
            </button>
          </div>
        </div>
      </div>
    </div>

    <LoadingState v-if="loading && !dataLoaded" message-key="guide.checkIn.loading" />
    <ErrorState
      v-else-if="(errorKey || errorMessage) && !dataLoaded"
      :message="errorMessage ?? undefined"
      :message-key="errorKey ?? undefined"
      @retry="initialize"
    />

    <template v-else>
      <ErrorState
        v-if="errorKey || errorMessage"
        :message="errorMessage ?? undefined"
        :message-key="errorKey ?? undefined"
        @retry="initialize"
      />
      <div v-if="!hasData" class="text-sm text-slate-500">{{ t('common.noData') }}</div>
      <template v-else>
      <section class="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:p-6">
        <h2 class="text-lg font-semibold">{{ t('common.checkIn') }}</h2>
        <div class="mt-3 flex flex-wrap items-center gap-3 text-xs">
          <span class="font-medium text-slate-500">{{ t('common.mode') }}</span>
          <div class="inline-flex rounded-xl border border-slate-200 bg-slate-50 p-1">
            <button
              type="button"
              class="rounded-lg px-3 py-1.5 font-semibold"
              :class="
                checkInDirection === 'Entry'
                  ? 'bg-white text-slate-900 shadow-sm'
                  : 'text-slate-600 hover:text-slate-900'
              "
              :aria-pressed="checkInDirection === 'Entry'"
              @click="checkInDirection = 'Entry'"
            >
              {{ t('common.entry') }}
            </button>
            <button
              type="button"
              class="rounded-lg px-3 py-1.5 font-semibold"
              :class="
                checkInDirection === 'Exit'
                  ? 'bg-white text-slate-900 shadow-sm'
                  : 'text-slate-600 hover:text-slate-900'
              "
              :aria-pressed="checkInDirection === 'Exit'"
              @click="checkInDirection = 'Exit'"
            >
              {{ t('common.exit') }}
            </button>
          </div>
        </div>
        <div class="mt-4">
          <button
            class="w-full rounded-xl bg-slate-900 px-4 py-3 text-sm font-semibold text-white hover:bg-slate-800"
            type="button"
            @click="openScanner"
          >
            {{ t('common.scanQr') }}
          </button>
        </div>
        <form class="mt-3 grid gap-3 sm:grid-cols-[1fr_auto]" @submit.prevent="handleCheckInSubmit">
          <input
            v-model.trim="checkInCode"
            ref="codeInput"
            autofocus
            class="rounded-xl border border-slate-200 bg-white px-4 py-3 text-sm uppercase tracking-wide focus:border-slate-400 focus:outline-none"
            :placeholder="t('guide.checkIn.codePlaceholder')"
            type="text"
            maxlength="8"
            @input="handleCodeInput"
          />
          <button
            class="w-full rounded-xl border border-slate-200 bg-white px-4 py-3 text-sm font-medium text-slate-700 hover:border-slate-300 sm:w-auto"
            :disabled="isCheckingIn"
            type="submit"
          >
            {{
              isCheckingIn
                ? t('guide.checkIn.submitting')
                : checkInDirection === 'Entry'
                  ? t('common.checkIn')
                  : t('common.exit')
            }}
          </button>
        </form>
        <div class="mt-3 flex flex-wrap items-center gap-3 text-xs">
          <span v-if="actionMessageKey || actionMessageText" class="text-emerald-600">
            {{ actionMessageKey ? t(actionMessageKey) : actionMessageText }}
          </span>
          <span v-if="actionErrorKey || actionErrorText" class="text-rose-600">
            {{ actionErrorKey ? t(actionErrorKey) : actionErrorText }}
          </span>
        </div>
        <div
          v-if="scanErrorKey || scanErrorText"
          class="mt-4 rounded-2xl border border-rose-100 bg-rose-50 p-4 text-sm text-rose-700"
        >
          <div class="font-semibold">
            {{ scanErrorKey ? t(scanErrorKey) : scanErrorText }}
          </div>
          <div class="mt-3 flex flex-wrap gap-2">
            <button
              class="rounded-full border border-rose-200 bg-white px-3 py-1.5 text-xs font-semibold text-rose-700 hover:border-rose-300"
              type="button"
              @click="scanAgain"
            >
              {{ t('guide.checkIn.scanAgain') }}
            </button>
            <button
              class="rounded-full border border-slate-200 bg-white px-3 py-1.5 text-xs font-semibold text-slate-700 hover:border-slate-300"
              type="button"
              @click="focusManualCode"
            >
              {{ t('guide.checkIn.useManualCode') }}
            </button>
          </div>
        </div>
        <div
          v-if="scanFoundCode"
          class="mt-4 rounded-2xl border border-slate-200 bg-slate-50 p-4 text-sm"
        >
          <div class="text-xs uppercase tracking-wide text-slate-400">
            {{ t('guide.checkIn.foundParticipant') }}
          </div>
          <div class="mt-2 flex flex-wrap items-center gap-2">
            <span class="font-semibold text-slate-800">
              {{ scanFoundParticipant?.fullName ?? scanFoundCode }}
            </span>
            <span v-if="scanResolving" class="text-xs text-slate-500">
              {{ t('common.loading') }}
            </span>
            <span
              v-if="scanFoundParticipant"
              class="rounded-full px-3 py-1 text-xs font-semibold"
              :class="
                scanFoundParticipant.arrived
                  ? 'bg-emerald-100 text-emerald-700'
                  : 'bg-amber-100 text-amber-700'
              "
            >
              {{ scanFoundParticipant.arrived ? t('common.arrivedLabel') : t('common.pendingLabel') }}
            </span>
          </div>
          <div class="mt-1 text-xs font-mono text-slate-500">{{ scanFoundCode }}</div>
          <div class="mt-3 flex flex-wrap gap-2">
            <button
              class="rounded-full bg-slate-900 px-3 py-1.5 text-xs font-semibold text-white hover:bg-slate-800 disabled:cursor-not-allowed disabled:opacity-60"
              type="button"
              :disabled="isCheckingIn"
              @click="handleScanCheckIn"
            >
              {{
                isCheckingIn
                  ? t('guide.checkIn.submitting')
                  : checkInDirection === 'Entry'
                    ? t('guide.checkIn.checkInNow')
                    : t('common.exit')
              }}
            </button>
            <button
              class="rounded-full border border-slate-200 bg-white px-3 py-1.5 text-xs font-semibold text-slate-700 hover:border-slate-300"
              type="button"
              @click="scanAgain"
            >
              {{ t('guide.checkIn.scanAgain') }}
            </button>
            <button
              class="rounded-full border border-slate-200 bg-white px-3 py-1.5 text-xs font-semibold text-slate-700 hover:border-slate-300"
              type="button"
              @click="resetScanState"
            >
              {{ t('common.dismiss') }}
            </button>
          </div>
        </div>
        <label class="mt-4 inline-flex items-center gap-2 text-xs text-slate-600">
          <input v-model="autoCheckInAfterScan" type="checkbox" class="h-4 w-4 rounded border-slate-300" />
          {{ t('common.autoCheckInAfterScan') }}
        </label>
        <div v-if="lastResult" class="mt-4 rounded-2xl border border-emerald-100 bg-emerald-50 p-4 text-sm">
          <div class="font-semibold text-emerald-800">
            {{
              (lastResult.direction ?? 'Entry') === 'Exit'
                ? t('common.exitLogged', { name: lastResult.participantName })
                : lastResult.alreadyArrived
                  ? t('common.arrivedAlready')
                  : t('common.checkedIn')
            }}
          </div>
          <div v-if="(lastResult.direction ?? 'Entry') !== 'Exit'" class="mt-1 text-emerald-700">
            {{ lastResult.participantName }}
          </div>
        </div>
      </section>

      <section class="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:p-6">
        <div class="flex flex-wrap items-center justify-between gap-2">
          <h2 class="text-lg font-semibold">{{ t('guide.checkIn.participantsTitle') }}</h2>
          <div class="flex flex-wrap items-center gap-2">
            <button
              class="max-w-[150px] whitespace-normal rounded border border-slate-200 bg-white px-3 py-1.5 text-center text-xs font-medium leading-snug text-slate-700 hover:border-slate-300 disabled:cursor-not-allowed disabled:opacity-50 sm:max-w-none sm:whitespace-nowrap"
              type="button"
              :disabled="participants.length === 0 || resettingAllCheckIns"
              @click="requestResetAllCheckIns"
            >
              {{ resettingAllCheckIns ? t('common.saving') : t('guide.checkIn.resetAll') }}
            </button>
            <span class="text-xs text-slate-500">
              {{ t('common.shown', { count: filteredParticipants.length }) }}
            </span>
          </div>
        </div>

        <div class="mt-4 flex flex-wrap items-center gap-2">
          <button
            class="rounded-full border px-3 py-1 text-xs font-semibold"
            :class="
              participantFilter === 'all'
                ? 'border-slate-900 bg-slate-900 text-white'
                : 'border-slate-200 bg-white text-slate-700 hover:border-slate-300'
            "
            type="button"
            @click="setParticipantFilter('all')"
          >
            {{ t('common.all') }} ({{ effectiveTotal }})
          </button>
          <button
            class="rounded-full border px-3 py-1 text-xs font-semibold"
            :class="
              participantFilter === 'arrived'
                ? 'border-slate-900 bg-slate-900 text-white'
                : 'border-slate-200 bg-white text-slate-700 hover:border-slate-300'
            "
            type="button"
            @click="setParticipantFilter('arrived')"
          >
            {{ t('common.arrivedLabel') }} ({{ summary.arrivedCount }})
          </button>
          <button
            class="rounded-full border px-3 py-1 text-xs font-semibold"
            :class="
              participantFilter === 'not_arrived'
                ? 'border-slate-900 bg-slate-900 text-white'
                : 'border-slate-200 bg-white text-slate-700 hover:border-slate-300'
            "
            type="button"
            @click="setParticipantFilter('not_arrived')"
          >
            {{ t('common.notArrived') }} ({{ notArrivedCount }})
          </button>
          <button
            class="rounded-full border px-3 py-1 text-xs font-semibold"
            :class="
              participantFilter === 'will_not_attend'
                ? 'border-slate-900 bg-slate-900 text-white'
                : 'border-slate-200 bg-white text-slate-700 hover:border-slate-300'
            "
            type="button"
            @click="setParticipantFilter('will_not_attend')"
          >
            {{ t('common.willNotAttendPlural') }} ({{ willNotAttendCount }})
          </button>
        </div>

        <div
          v-if="lastAction"
          class="mt-4 flex flex-col gap-3 rounded-2xl border border-emerald-100 bg-emerald-50 p-4 text-sm text-emerald-900 sm:flex-row sm:items-center sm:justify-between"
        >
          <span>{{ t('guide.checkIn.lastAction', { name: lastAction.participantName }) }}</span>
          <button
            class="rounded-full border border-emerald-200 bg-white px-3 py-1.5 text-xs font-semibold text-emerald-700 hover:border-emerald-300 disabled:cursor-not-allowed disabled:opacity-60"
            type="button"
            :disabled="undoingParticipantId === lastAction.participantId"
            @click="undoCheckIn(lastAction.participantId)"
          >
            {{ undoingParticipantId === lastAction.participantId ? t('common.undoing') : t('common.undo') }}
          </button>
        </div>

        <div class="sticky top-16 z-10 -mx-4 mt-4 bg-white/95 px-4 pb-3 pt-3 backdrop-blur sm:static sm:mx-0 sm:bg-transparent sm:px-0 sm:pb-0 sm:pt-0">
          <div class="relative">
            <input
              v-model.trim="searchTerm"
              class="w-full rounded-xl border border-slate-200 bg-white px-4 py-3 pr-16 text-sm focus:border-slate-400 focus:outline-none"
              :placeholder="t('common.searchPlaceholder')"
              type="text"
            />
            <button
              v-if="searchTerm"
              class="absolute right-3 top-1/2 -translate-y-1/2 rounded-full border border-slate-200 bg-white px-2 py-1 text-xs font-semibold text-slate-600 hover:border-slate-300"
              type="button"
              @click="clearSearch"
            >
              {{ t('common.clearSearch') }}
            </button>
          </div>
        </div>

        <div
          v-if="filteredParticipants.length === 0"
          class="mt-4 rounded-2xl border border-dashed border-slate-200 bg-slate-50 p-4 text-sm text-slate-500"
        >
          {{ t('guide.checkIn.empty') }}
        </div>

        <ul v-else class="mt-4 space-y-3">
          <li
            v-for="participant in filteredParticipants"
            :key="participant.id"
            class="relative rounded-2xl border border-slate-200 bg-slate-50 p-4 transition"
            :class="highlightParticipantId === participant.id ? 'ring-2 ring-emerald-300' : ''"
          >
            <div class="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
              <div>
                <div class="text-base font-medium text-slate-800">{{ participant.fullName }}</div>
                <div class="mt-1 text-xs text-slate-500" v-if="participant.email || participant.phone">
                  <span v-if="participant.email">{{ participant.email }}</span>
                  <span v-if="participant.email && participant.phone"> | </span>
                  <span v-if="participant.phone">{{ formatPhoneDisplay(participant.phone) }}</span>
                </div>
                <div class="mt-1 text-xs text-slate-500">
                  {{ t('common.lastActionLabel') }}:
                  <span class="font-medium text-slate-700">{{ formatLastLog(participant.lastLog) }}</span>
                </div>
                <div class="mt-3 flex flex-wrap items-center gap-2">
                  <span
                    class="rounded-full px-3 py-1 text-xs font-semibold"
                    :class="
                      participant.arrived
                        ? 'bg-emerald-100 text-emerald-700'
                        : 'bg-amber-100 text-amber-700'
                    "
                  >
                    {{ participant.arrived ? t('common.arrivedLabel') : t('common.pendingLabel') }}
                  </span>
                  <button
                    v-if="!participant.arrived"
                    class="rounded-full border border-slate-200 bg-white px-3 py-1 text-xs font-medium text-slate-700 hover:border-slate-300 disabled:cursor-not-allowed disabled:opacity-50"
                    :disabled="participant.willNotAttend"
                    type="button"
                    @click="markArrived(participant)"
                  >
                    {{ t('guide.checkIn.markArrived') }}
                  </button>
                  <button
                    v-else
                    class="rounded-full border border-slate-200 bg-white px-3 py-1 text-xs font-medium text-slate-700 hover:border-slate-300 disabled:cursor-not-allowed disabled:opacity-50"
                    :disabled="undoingParticipantId === participant.id"
                    type="button"
                    @click="undoCheckIn(participant.id)"
                  >
                    {{ undoingParticipantId === participant.id ? t('common.undoing') : t('common.undo') }}
                  </button>
                  <div class="flex flex-wrap items-center gap-2">
                    <button
                      class="rounded-full border border-slate-200 bg-white px-3 py-1 text-xs font-medium text-slate-700 hover:border-slate-300 disabled:cursor-not-allowed disabled:opacity-50"
                      :disabled="updatingWillNotAttendId === participant.id"
                      type="button"
                      @click="setWillNotAttend(participant, !participant.willNotAttend)"
                    >
                      {{
                        updatingWillNotAttendId === participant.id
                          ? t('common.saving')
                          : participant.willNotAttend
                            ? t('common.willAttend')
                            : t('common.willNotAttend')
                      }}
                    </button>
                    <button
                      class="rounded-full border border-slate-200 bg-white px-3 py-1 text-xs font-medium text-slate-700 hover:border-slate-300"
                      type="button"
                      :disabled="!participant.phone"
                      @click="openCall(participant)"
                    >
                      {{ t('actions.call') }}
                    </button>
                    <button
                      class="inline-flex items-center gap-1 rounded-full border border-emerald-200 bg-emerald-50 px-3 py-1 text-xs font-semibold text-emerald-800 hover:border-emerald-300 disabled:cursor-not-allowed disabled:opacity-50"
                      type="button"
                      :aria-label="t('actions.whatsappAria')"
                      :disabled="!participant.phone"
                      @click="openWhatsApp(participant)"
                    >
                      <WhatsAppIcon class="text-emerald-700" :size="14" />
                      {{ t('actions.whatsapp') }}
                    </button>
                  </div>
                </div>
              </div>
            </div>
          </li>
        </ul>
      </section>
      </template>
    </template>
  </div>
  <QrScannerModal :open="scannerOpen" @close="scannerOpen = false" @result="handleScanResult" />
  <ConfirmDialog
    v-model:open="confirmOpen"
    :title="t('common.confirm')"
    :message="confirmMessageKey ? t(confirmMessageKey) : ''"
    :confirm-label="t('common.confirm')"
    :cancel-label="t('common.cancel')"
    @confirm="handleConfirm"
  />
</template>
