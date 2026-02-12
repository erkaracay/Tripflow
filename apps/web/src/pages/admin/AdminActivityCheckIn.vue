<script setup lang="ts">
import { computed, nextTick, onMounted, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { apiGet, apiPost, apiPostWithPayload, apiPatchWithPayload } from '../../lib/api'
import { normalizeQrCode } from '../../lib/qr'
import { normalizeCheckInCode, normalizePhone } from '../../lib/normalize'
import { useToast } from '../../lib/toast'
import { buildWhatsAppUrl } from '../../lib/whatsapp'
import WhatsAppIcon from '../../components/icons/WhatsAppIcon.vue'
import QrScannerModal from '../../components/QrScannerModal.vue'
import LoadingState from '../../components/ui/LoadingState.vue'
import ErrorState from '../../components/ui/ErrorState.vue'
import ConfirmDialog from '../../components/ui/ConfirmDialog.vue'
import { formatTime, formatUtcToLocal } from '../../lib/formatters'
import type {
  Event as EventDto,
  EventDay,
  EventActivity,
  ActivityCheckInResponse,
  ActivityParticipantTableResponse,
  ActivityParticipantTableItem,
  ActivityParticipantWillNotAttendResponse,
  ResetAllActivityCheckInsResponse,
} from '../../types'

const route = useRoute()
const { t } = useI18n()
const eventId = computed(() => route.params.eventId as string)
const storageKeyMode = computed(() => `infora:admin:activityCheckIn:mode:${eventId.value}`)
const storageKeyAuto = computed(() => `infora:admin:activityCheckIn:auto:${eventId.value}`)

const event = ref<EventDto | null>(null)
const days = ref<EventDay[]>([])
const activities = ref<EventActivity[]>([])
const selectedActivityId = ref<string | null>(null)
const direction = ref<'Entry' | 'Exit'>('Entry')
const code = ref('')
const table = ref<ActivityParticipantTableResponse | null>(null)
const summaryTable = ref<ActivityParticipantTableResponse | null>(null)
const willNotAttendCount = ref(0)
const tablePage = ref(1)
const tableQuery = ref('')
const tableStatus = ref<'all' | 'checked_in' | 'not_checked_in' | 'will_not_attend'>('all')
const tableSort = ref('fullName')
const tableDir = ref<'asc' | 'desc'>('asc')
const loading = ref(true)
const loadErrorKey = ref<string | null>(null)
const submitting = ref(false)
const scannerOpen = ref(false)
const autoSubmitAfterScan = ref(true)
const codeInput = ref<HTMLInputElement | null>(null)
const updatingWillNotAttendId = ref<string | null>(null)
const resettingAllCheckIns = ref(false)
const confirmOpen = ref(false)
const confirmMessageKey = ref<string | null>(null)
let lastScannedCode: string | null = null
let lastScannedAt = 0

const { pushToast, removeToast } = useToast()

const loadEventAndActivities = async () => {
  loading.value = true
  loadErrorKey.value = null
  try {
    const [eventData, daysData, activitiesData] = await Promise.all([
      apiGet<EventDto>(`/api/events/${eventId.value}`),
      apiGet<EventDay[]>(`/api/events/${eventId.value}/days`),
      apiGet<EventActivity[]>(`/api/events/${eventId.value}/activities/for-checkin`),
    ])
    event.value = eventData
    days.value = daysData ?? []
    activities.value = activitiesData
    if (activitiesData.length > 0 && !selectedActivityId.value) {
      const first = activitiesData[0]
      if (first) selectedActivityId.value = first.id
    }
  } catch {
    loadErrorKey.value = 'errors.checkIn.load'
  } finally {
    loading.value = false
  }
}

const loadTable = async () => {
  if (!selectedActivityId.value) {
    table.value = null
    summaryTable.value = null
    willNotAttendCount.value = 0
    return
  }
  try {
    const params = new URLSearchParams()
    if (tableQuery.value.trim()) params.set('query', tableQuery.value.trim())
    params.set('status', tableStatus.value)
    params.set('page', String(tablePage.value))
    params.set('pageSize', '50')
    params.set('sort', tableSort.value)
    params.set('dir', tableDir.value)
    const res = await apiGet<ActivityParticipantTableResponse>(
      `/api/events/${eventId.value}/activities/${selectedActivityId.value}/participants/table?${params}`
    )
    table.value = res

    // Load summary data (all participants, first page only, no search query)
    if (tableStatus.value === 'all' && !tableQuery.value.trim()) {
      // If we're already loading "all", use it for summary
      summaryTable.value = res
    } else {
      // Otherwise, load summary separately
      const summaryParams = new URLSearchParams()
      summaryParams.set('status', 'all')
      summaryParams.set('page', '1')
      summaryParams.set('pageSize', '50')
      summaryParams.set('sort', 'fullName')
      summaryParams.set('dir', 'asc')
      try {
        const summaryRes = await apiGet<ActivityParticipantTableResponse>(
          `/api/events/${eventId.value}/activities/${selectedActivityId.value}/participants/table?${summaryParams}`
        )
        summaryTable.value = summaryRes
      } catch {
        // If summary load fails, use current table if it's "all"
        if (tableStatus.value === 'all') {
          summaryTable.value = res
        }
      }
    }

    // Load will-not-attend count separately (backend excludes them from "all" status)
    try {
      const willNotAttendParams = new URLSearchParams()
      willNotAttendParams.set('status', 'will_not_attend')
      willNotAttendParams.set('page', '1')
      willNotAttendParams.set('pageSize', '1') // We only need the total count
      const willNotAttendRes = await apiGet<ActivityParticipantTableResponse>(
        `/api/events/${eventId.value}/activities/${selectedActivityId.value}/participants/table?${willNotAttendParams}`
      )
      willNotAttendCount.value = willNotAttendRes.total
    } catch {
      willNotAttendCount.value = 0
    }
  } catch {
    table.value = null
    summaryTable.value = null
    willNotAttendCount.value = 0
  }
}

watch([selectedActivityId, tablePage, tableQuery, tableStatus, tableSort, tableDir], () => {
  void loadTable()
})

const setTableSort = (col: string) => {
  if (tableSort.value === col) {
    tableDir.value = tableDir.value === 'asc' ? 'desc' : 'asc'
  } else {
    tableSort.value = col
    tableDir.value = 'asc'
  }
  tablePage.value = 1
  void loadTable()
}

const persistMode = () => {
  try {
    globalThis.localStorage?.setItem(storageKeyMode.value, direction.value)
  } catch {}
}
const persistAuto = () => {
  try {
    globalThis.localStorage?.setItem(storageKeyAuto.value, autoSubmitAfterScan.value ? '1' : '0')
  } catch {}
}

const activitiesByDay = computed(() => {
  const byDay = new Map<string, EventActivity[]>()
  for (const a of activities.value) {
    const list = byDay.get(a.eventDayId) ?? []
    list.push(a)
    byDay.set(a.eventDayId, list)
  }
  const dayOrder = days.value.slice().sort((x, y) => x.sortOrder - y.sortOrder)
  return dayOrder
    .filter((d) => byDay.has(d.id))
    .map((d) => ({
      day: d,
      activities: byDay.get(d.id) ?? [],
    }))
})

const dayLabel = (day: EventDay) =>
  (day.title && day.title.trim()) ? day.title.trim() : t('activityCheckIn.dayLabel', { n: day.sortOrder })

const activityOptionLabel = (a: EventActivity) =>
  t('activityCheckIn.optionTimeActivity', { time: formatTime(a.startTime), title: a.title })

const selectedActivity = computed(() => {
  if (!selectedActivityId.value) return null
  return activities.value.find((a) => a.id === selectedActivityId.value) ?? null
})

const submitCheckIn = async (
  codeValue: string,
  options?: { method?: 'Manual' | 'QrScan'; direction?: 'Entry' | 'Exit' }
) => {
  if (!selectedActivityId.value) {
    pushToast({ key: 'activityCheckIn.selectActivity', tone: 'error' })
    return
  }
  const normalized = normalizeCheckInCode(codeValue).slice(0, 10)
  if (normalized.length < 6) {
    pushToast({ key: 'toast.invalidCodeFormat', tone: 'error' })
    return
  }
  submitting.value = true
  try {
    const checkInDirection = options?.direction ?? direction.value
    const checkInMethod = options?.method ?? 'Manual'
    const res = await apiPostWithPayload<ActivityCheckInResponse>(
      `/api/events/${eventId.value}/activities/${selectedActivityId.value}/checkins`,
      { checkInCode: normalized, direction: checkInDirection, method: checkInMethod }
    )
    if (res.result === 'AlreadyCheckedIn') {
      pushToast({ key: 'toast.alreadyCheckedIn', tone: 'info' })
    } else {
      const key = res.direction === 'Exit' ? 'activityCheckIn.exitSuccess' : 'activityCheckIn.entrySuccess'
      pushToast({ key, params: { name: res.participantName }, tone: 'success' })
    }
    code.value = ''
    await loadTable()
  } catch (err: unknown) {
    const payload = err && typeof err === 'object' ? (err as { payload?: unknown }).payload : undefined
    const result = payload && typeof payload === 'object' && payload !== null && 'result' in payload
      ? String((payload as { result?: string }).result ?? '')
      : ''
    if (result === 'NotFound') pushToast({ key: 'toast.codeNotFound', tone: 'error' })
    else if (result === 'InvalidRequest') pushToast({ key: 'toast.invalidCodeFormat', tone: 'error' })
    else pushToast({ key: 'common.checkInFailed', tone: 'error' })
  } finally {
    submitting.value = false
  }
}

const onCodeSubmit = () => {
  void submitCheckIn(code.value, { method: 'Manual' })
}

const openScanner = () => {
  scannerOpen.value = true
}

const onScanResult = async (raw: string) => {
  scannerOpen.value = false
  const extracted = normalizeQrCode(raw) || raw.trim().toUpperCase().replace(/[^A-Z0-9]/g, '').slice(0, 10)
  if (!extracted || extracted.length < 6) {
    pushToast({ key: 'toast.invalidCode', tone: 'error' })
    return
  }
  const now = Date.now()
  if (lastScannedCode === extracted && now - lastScannedAt < 2500) return
  lastScannedCode = extracted
  lastScannedAt = now
  code.value = extracted
  await nextTick()
  codeInput.value?.focus()
  if (submitting.value) {
    pushToast({ key: 'common.checkingIn', tone: 'info' })
    return
  }
  if (!autoSubmitAfterScan.value) {
    pushToast({ key: 'activityCheckIn.codeFilled', tone: 'info' })
    return
  }
  const toastId = pushToast({ key: 'common.checkingIn', tone: 'info', timeout: 0 })
  await submitCheckIn(extracted, { method: 'QrScan' })
  removeToast(toastId)
}

const formatLastAction = (item: ActivityParticipantTableResponse['items'][0]) => {
  const log = item.activityState?.lastLog
  if (!log) return '—'
  const dir = log.direction === 'Exit' ? t('common.exit') : t('common.entry')
  const method = log.method === 'QrScan' ? 'QR' : t('common.manual')
  return `${dir} · ${method} · ${formatUtcToLocal(log.createdAt, { timeOnly: true })} · ${log.result}`
}

const filteredTableItems = computed(() => {
  if (!table.value) {
    return []
  }
  let filtered = table.value.items

  // Exclude willNotAttend participants from all filters except "will_not_attend" (matching event check-in behavior)
  // This is a safety check in case backend filtering isn't working correctly
  if (tableStatus.value !== 'will_not_attend') {
    filtered = filtered.filter((item) => !item.activityState?.willNotAttend)
  }

  const query = tableQuery.value.trim().toLowerCase()
  if (query) {
    filtered = filtered.filter((item) =>
      item.fullName.toLowerCase().includes(query) ||
      item.checkInCode.toLowerCase().includes(query) ||
      item.phone?.toLowerCase().includes(query) ||
      item.email?.toLowerCase().includes(query) ||
      item.tcNo.toLowerCase().includes(query) ||
      item.roomNo?.toLowerCase().includes(query) ||
      item.agencyName?.toLowerCase().includes(query)
    )
  }

  return filtered
})

const activitySummary = computed(() => {
  // Use summaryTable for accurate counts (all participants, not filtered)
  const dataTable = summaryTable.value ?? table.value
  if (!dataTable) {
    return { checkedInCount: 0, totalCount: 0, notCheckedInCount: 0, willNotAttendCount: 0, effectiveTotal: 0 }
  }
  // Calculate from all items in summary table (first page of "all" status)
  const allItems = dataTable.items
  const checkedInCount = allItems.filter((item) => item.activityState?.isCheckedIn).length
  const totalCount = dataTable.total
  // Use separate willNotAttendCount ref (fetched from backend with status="will_not_attend")
  const effectiveTotal = Math.max(totalCount - willNotAttendCount.value, 0)
  const notCheckedInCount = Math.max(effectiveTotal - checkedInCount, 0)
  return { checkedInCount, totalCount, notCheckedInCount, willNotAttendCount: willNotAttendCount.value, effectiveTotal }
})

const setActivityFilter = (value: typeof tableStatus.value) => {
  tableStatus.value = value
  tablePage.value = 1
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

const openCall = (row: ActivityParticipantTableItem) => {
  const phone = row.phone?.trim()
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

const openWhatsApp = (row: ActivityParticipantTableItem) => {
  const phone = row.phone?.trim()
  if (!phone) {
    pushToast({ key: 'warnings.phoneRequired', tone: 'error' })
    return
  }

  const selectedActivity = activities.value.find((a) => a.id === selectedActivityId.value)
  const activityName = selectedActivity?.title?.trim() || t('activityCheckIn.activity')
  const message = t('activityCheckIn.whatsappMessage', {
    name: row.fullName,
    activity: activityName,
  })

  const normalizedPhone = normalizePhone(phone).normalized || phone
  const url = buildWhatsAppUrl(normalizedPhone, message)
  if (!url) {
    pushToast({ key: 'warnings.phoneRequired', tone: 'error' })
    return
  }

  globalThis.open(url, '_blank', 'noopener,noreferrer')
}

const markActivityArrived = async (row: ActivityParticipantTableItem) => {
  if (row.activityState?.isCheckedIn) {
    return
  }
  await submitCheckIn(row.checkInCode, { method: 'Manual', direction: 'Entry' })
}

const markActivityDeparted = async (row: ActivityParticipantTableItem) => {
  if (!row.activityState?.isCheckedIn) {
    return
  }
  await submitCheckIn(row.checkInCode, { method: 'Manual', direction: 'Exit' })
}

const setActivityWillNotAttend = async (row: ActivityParticipantTableItem, willNotAttend: boolean) => {
  if (!selectedActivityId.value) {
    return
  }
  if (updatingWillNotAttendId.value === row.id) {
    return
  }

  updatingWillNotAttendId.value = row.id
  try {
    const response = await apiPatchWithPayload<ActivityParticipantWillNotAttendResponse>(
      `/api/events/${eventId.value}/activities/${selectedActivityId.value}/participants/${row.id}/will-not-attend`,
      { willNotAttend }
    )

    if (table.value) {
      table.value.items = table.value.items.map((item) =>
        item.id === row.id
          ? {
              ...item,
              activityState: response.activityState,
            }
          : item
      )
    }

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

const requestResetAllActivityCheckIns = () => {
  confirmMessageKey.value = 'admin.participants.resetAllActivityConfirm'
  confirmOpen.value = true
}

const resetAllActivityCheckIns = async () => {
  if (!selectedActivityId.value || resettingAllCheckIns.value) {
    return
  }

  resettingAllCheckIns.value = true
  try {
    await apiPost<ResetAllActivityCheckInsResponse>(
      `/api/events/${eventId.value}/activities/${selectedActivityId.value}/checkins/reset-all`,
      {}
    )
    tableStatus.value = 'all'
    await loadTable()
    pushToast({ key: 'toast.checkInsResetAll', tone: 'success' })
  } catch {
    pushToast({ key: 'toast.checkInsResetAllFailed', tone: 'error' })
  } finally {
    resettingAllCheckIns.value = false
  }
}

const handleConfirm = async () => {
  confirmOpen.value = false
  await resetAllActivityCheckIns()
}

onMounted(() => {
  const storedMode = globalThis.localStorage?.getItem(storageKeyMode.value)
  if (storedMode === 'Exit' || storedMode === 'Entry') direction.value = storedMode
  // Tarayıcıya otomatik gönder: her açılışta seçili (varsayılan true)
  void loadEventAndActivities()
})

watch(direction, persistMode)
watch(autoSubmitAfterScan, persistAuto)
watch(selectedActivityId, () => {
  tablePage.value = 1
  void loadTable()
})
</script>

<template>
  <div class="space-y-6">
    <div class="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:p-6">
      <div class="flex flex-wrap items-center gap-2">
        <RouterLink
          class="rounded border border-slate-200 bg-white px-3 py-2 text-sm text-slate-700 shadow-sm hover:border-slate-300"
          :to="`/admin/events/${eventId}`"
        >
          {{ t('nav.backToEvent') }}
        </RouterLink>
        <RouterLink
          class="rounded border border-slate-200 bg-white px-3 py-2 text-sm text-slate-700 shadow-sm hover:border-slate-300"
          :to="`/admin/events/${eventId}/checkin`"
        >
          {{ t('common.checkIn') }}
        </RouterLink>
      </div>
      <h1 class="mt-2 text-2xl font-semibold">
        {{ selectedActivity?.title ?? event?.name ?? t('activityCheckIn.title') }}
      </h1>
      <p v-if="event" class="text-sm text-slate-500">
        {{ t('common.dateRange', { start: event.startDate, end: event.endDate }) }}
      </p>
    </div>

    <LoadingState v-if="loading" message-key="admin.eventDetail.loading" />
    <ErrorState v-else-if="loadErrorKey" :message-key="loadErrorKey" @retry="loadEventAndActivities" />

    <template v-else>
      <div class="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:p-6">
        <div class="flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
          <div class="flex flex-1 flex-col gap-3 sm:flex-row sm:items-end">
            <label class="min-w-0 flex-1">
              <span class="block text-sm text-slate-600">{{ t('activityCheckIn.activity') }}</span>
              <select
                v-model="selectedActivityId"
                class="mt-1 w-full rounded border border-slate-200 bg-white px-3 py-2 text-sm"
              >
                <template v-for="group in activitiesByDay" :key="group.day.id">
                  <optgroup :label="dayLabel(group.day)">
                    <option v-for="a in group.activities" :key="a.id" :value="a.id">
                      {{ activityOptionLabel(a) }}
                    </option>
                  </optgroup>
                </template>
                <option v-if="activities.length === 0" value="" disabled>{{ t('activityCheckIn.noActivities') }}</option>
              </select>
            </label>
            <div class="flex items-center gap-2">
              <button
                type="button"
                :class="direction === 'Entry' ? 'bg-slate-800 text-white' : 'bg-slate-100 text-slate-700'"
                class="rounded px-3 py-2 text-sm font-medium"
                @click="direction = 'Entry'"
              >
                {{ t('common.entry') }}
              </button>
              <button
                type="button"
                :class="direction === 'Exit' ? 'bg-slate-800 text-white' : 'bg-slate-100 text-slate-700'"
                class="rounded px-3 py-2 text-sm font-medium"
                @click="direction = 'Exit'"
              >
                {{ t('common.exit') }}
              </button>
            </div>
          </div>
          <div class="flex items-center gap-2">
            <button
              type="button"
              class="inline-flex items-center justify-center gap-2 rounded border border-slate-200 bg-white px-3 py-2 text-sm shadow-sm transition-opacity duration-200 hover:border-slate-300 disabled:pointer-events-none disabled:opacity-70"
              :disabled="submitting"
              @click="openScanner"
            >
              <span
                v-if="submitting"
                class="h-3 w-3 shrink-0 animate-spin rounded-full border-2 border-slate-300 border-t-slate-600"
                aria-hidden="true"
              />
              <span>{{ submitting ? t('common.checkingIn') : t('activityCheckIn.scanQr') }}</span>
            </button>
            <label class="flex items-center gap-2 text-sm">
              <input v-model="autoSubmitAfterScan" type="checkbox" />
              {{ t('activityCheckIn.autoSubmit') }}
            </label>
          </div>
        </div>

        <form class="mt-4 flex flex-wrap items-end gap-2" @submit.prevent="onCodeSubmit">
          <label class="min-w-0 flex-1 sm:max-w-xs">
            <span class="sr-only">{{ t('activityCheckIn.code') }}</span>
            <input
              ref="codeInput"
              v-model="code"
              type="text"
              inputmode="text"
              autocomplete="off"
              class="w-full rounded border border-slate-200 px-3 py-2 text-sm uppercase"
              :placeholder="t('activityCheckIn.codePlaceholder')"
              :disabled="!selectedActivityId || submitting"
              @input="(e) => { code = normalizeCheckInCode((e.target as HTMLInputElement).value).slice(0, 10) }"
            />
          </label>
          <button
            type="submit"
            class="rounded bg-slate-800 px-4 py-2 text-sm font-medium text-white hover:bg-slate-700 disabled:opacity-50"
            :disabled="!selectedActivityId || submitting || !code.trim()"
          >
            {{ submitting ? t('common.saving') : (direction === 'Entry' ? t('common.entry') : t('common.exit')) }}
          </button>
        </form>
      </div>

      <div v-if="selectedActivityId && table" class="mt-6 rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:p-6">
        <div class="flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
          <div class="flex flex-1 flex-col gap-3 sm:flex-row sm:items-end">
            <div>
              <h2 class="text-lg font-semibold text-slate-900">{{ selectedActivity?.title ?? t('activityCheckIn.title') }}</h2>
            </div>
          </div>
          <div class="rounded-xl border border-slate-200 bg-slate-50 px-4 py-3 text-sm">
            <div class="text-xs uppercase tracking-wide text-slate-400">{{ t('common.arrivedLabel') }}</div>
            <div class="mt-1 text-xl font-semibold text-slate-800">
              {{ activitySummary.checkedInCount }} / {{ activitySummary.effectiveTotal }}
            </div>
            <div class="mt-2 flex flex-col gap-1">
              <button
                class="block text-left text-xs font-semibold text-slate-600 underline-offset-2 hover:text-slate-900 hover:underline"
                type="button"
                @click="setActivityFilter(tableStatus === 'not_checked_in' ? 'all' : 'not_checked_in')"
              >
                {{ t('activityCheckIn.filterNotCheckedIn') }}: {{ activitySummary.notCheckedInCount }}
              </button>
              <button
                class="block text-left text-xs font-semibold text-slate-500 underline-offset-2 hover:text-slate-900 hover:underline"
                type="button"
                @click="setActivityFilter(tableStatus === 'will_not_attend' ? 'all' : 'will_not_attend')"
              >
                {{ t('common.willNotAttend') }}: {{ activitySummary.willNotAttendCount }}
              </button>
            </div>
          </div>
        </div>
      </div>

      <section v-if="selectedActivityId" class="mt-6 rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:p-6">
        <div class="flex flex-wrap items-center justify-between gap-2">
          <h2 class="text-lg font-semibold">{{ t('guide.checkIn.participantsTitle') }}</h2>
          <div class="flex flex-wrap items-center gap-2">
            <button
              class="max-w-[150px] whitespace-normal rounded border border-slate-200 bg-white px-3 py-1.5 text-center text-xs font-medium leading-snug text-slate-700 hover:border-slate-300 disabled:cursor-not-allowed disabled:opacity-50 sm:max-w-none sm:whitespace-nowrap"
              type="button"
              :disabled="!table || table.items.length === 0 || resettingAllCheckIns"
              @click="requestResetAllActivityCheckIns"
            >
              {{ resettingAllCheckIns ? t('common.saving') : t('admin.participants.resetAll') }}
            </button>
            <span class="text-xs text-slate-500">
              {{ t('common.shown', { count: filteredTableItems.length }) }}
            </span>
          </div>
        </div>

        <div class="mt-4 flex flex-wrap items-center gap-2">
          <button
            class="rounded-full border px-3 py-1 text-xs font-semibold"
            :class="
              tableStatus === 'all'
                ? 'border-slate-900 bg-slate-900 text-white'
                : 'border-slate-200 bg-white text-slate-700 hover:border-slate-300'
            "
            type="button"
            @click="setActivityFilter('all')"
          >
            {{ t('common.all') }} ({{ activitySummary.effectiveTotal }})
          </button>
          <button
            class="rounded-full border px-3 py-1 text-xs font-semibold"
            :class="
              tableStatus === 'checked_in'
                ? 'border-slate-900 bg-slate-900 text-white'
                : 'border-slate-200 bg-white text-slate-700 hover:border-slate-300'
            "
            type="button"
            @click="setActivityFilter('checked_in')"
          >
            {{ t('activityCheckIn.filterCheckedIn') }} ({{ activitySummary.checkedInCount }})
          </button>
          <button
            class="rounded-full border px-3 py-1 text-xs font-semibold"
            :class="
              tableStatus === 'not_checked_in'
                ? 'border-slate-900 bg-slate-900 text-white'
                : 'border-slate-200 bg-white text-slate-700 hover:border-slate-300'
            "
            type="button"
            @click="setActivityFilter('not_checked_in')"
          >
            {{ t('activityCheckIn.filterNotCheckedIn') }} ({{ activitySummary.notCheckedInCount }})
          </button>
          <button
            class="rounded-full border px-3 py-1 text-xs font-semibold"
            :class="
              tableStatus === 'will_not_attend'
                ? 'border-slate-900 bg-slate-900 text-white'
                : 'border-slate-200 bg-white text-slate-700 hover:border-slate-300'
            "
            type="button"
            @click="setActivityFilter('will_not_attend')"
          >
            {{ t('common.willNotAttendPlural') }} ({{ activitySummary.willNotAttendCount }})
          </button>
        </div>

        <div class="mt-4">
          <input
            v-model.trim="tableQuery"
            type="search"
            class="w-full rounded border border-slate-200 px-3 py-2 text-sm sm:w-48"
            :placeholder="t('common.search')"
          />
        </div>

        <div class="mt-4 overflow-x-auto">
          <table class="w-full text-sm">
            <thead>
              <tr class="border-b border-slate-200 text-left text-slate-600">
                <th class="p-2">
                  <button
                    type="button"
                    class="inline-flex cursor-pointer select-none items-center gap-0.5 rounded hover:bg-slate-100"
                    @click="setTableSort('fullName')"
                  >
                    {{ t('common.name') }}{{ tableSort === 'fullName' ? (tableDir === 'asc' ? ' ↑' : ' ↓') : '' }}
                  </button>
                </th>
                <th class="p-2">{{ t('activityCheckIn.code') }}</th>
                <th class="p-2">{{ t('activityCheckIn.status') }}</th>
                <th class="p-2">{{ t('activityCheckIn.lastAction') }}</th>
              </tr>
            </thead>
            <tbody>
              <template v-for="row in filteredTableItems" :key="row.id">
                <tr class="border-b border-slate-100">
                  <td class="p-2 font-medium">{{ row.fullName }}</td>
                  <td class="p-2">{{ row.checkInCode }}</td>
                  <td class="p-2">
                    <span v-if="row.activityState?.isCheckedIn" class="whitespace-nowrap rounded bg-emerald-100 px-2 py-1 text-xs font-medium text-emerald-800">
                      {{ t('activityCheckIn.checkedIn') }}
                    </span>
                    <span v-else-if="row.activityState?.lastLog?.direction === 'Exit'" class="whitespace-nowrap rounded bg-slate-100 px-2 py-1 text-xs font-medium text-slate-700">
                      {{ t('activityCheckIn.exited') }}
                    </span>
                    <span v-else class="text-slate-500">—</span>
                  </td>
                  <td class="p-2 text-slate-600">{{ formatLastAction(row) }}</td>
                </tr>
                <tr class="border-b border-slate-200 bg-slate-50">
                  <td colspan="4" class="p-2">
                    <div class="flex flex-row flex-wrap items-center gap-2">
                      <button
                        v-if="!row.activityState?.isCheckedIn"
                        class="rounded-full border border-slate-200 bg-white px-3 py-1 text-xs font-medium text-slate-700 hover:border-slate-300 disabled:cursor-not-allowed disabled:opacity-50"
                        type="button"
                        :disabled="submitting"
                        @click="markActivityArrived(row)"
                      >
                        {{ t('guide.checkIn.markArrived') }}
                      </button>
                      <button
                        v-if="row.activityState?.isCheckedIn"
                        class="rounded-full border border-slate-200 bg-white px-3 py-1 text-xs font-medium text-slate-700 hover:border-slate-300 disabled:cursor-not-allowed disabled:opacity-50"
                        type="button"
                        :disabled="submitting"
                        @click="markActivityDeparted(row)"
                      >
                        {{ t('guide.checkIn.markDeparted') }}
                      </button>
                      <button
                        class="rounded-full border border-slate-200 bg-white px-3 py-1 text-xs font-medium text-slate-700 hover:border-slate-300 disabled:cursor-not-allowed disabled:opacity-50"
                        type="button"
                        :disabled="!row.phone"
                        @click="openCall(row)"
                      >
                        {{ t('actions.call') }}
                      </button>
                      <button
                        class="inline-flex items-center gap-1 rounded-full border border-emerald-200 bg-emerald-50 px-3 py-1 text-xs font-semibold text-emerald-800 hover:border-emerald-300 disabled:cursor-not-allowed disabled:opacity-50"
                        type="button"
                        :aria-label="t('actions.whatsappAria')"
                        :disabled="!row.phone"
                        @click="openWhatsApp(row)"
                      >
                        <WhatsAppIcon class="text-emerald-700" :size="14" />
                        {{ t('actions.whatsapp') }}
                      </button>
                      <button
                        class="rounded-full border border-slate-200 bg-white px-3 py-1 text-xs font-medium text-slate-700 hover:border-slate-300 disabled:cursor-not-allowed disabled:opacity-50"
                        :disabled="updatingWillNotAttendId === row.id"
                        type="button"
                        @click="setActivityWillNotAttend(row, !row.activityState?.willNotAttend)"
                      >
                        {{
                          updatingWillNotAttendId === row.id
                            ? t('common.saving')
                            : row.activityState?.willNotAttend
                              ? t('common.willAttend')
                              : t('common.willNotAttend')
                        }}
                      </button>
                    </div>
                  </td>
                </tr>
              </template>
              <tr v-if="filteredTableItems.length === 0">
                <td colspan="4" class="p-4 text-center text-slate-500">{{ t('activityCheckIn.noParticipants') }}</td>
              </tr>
            </tbody>
          </table>
        </div>
        <div v-if="table && table.total > table.pageSize" class="mt-2 flex justify-between text-sm text-slate-500">
          <button
            type="button"
            class="rounded px-2 py-1 hover:bg-slate-100 disabled:opacity-50"
            :disabled="tablePage <= 1"
            @click="tablePage--"
          >
            {{ t('common.previous') }}
          </button>
          <span>{{ t('common.pageOf', { page: table.page, total: Math.ceil(table.total / table.pageSize) }) }}</span>
          <button
            type="button"
            class="rounded px-2 py-1 hover:bg-slate-100 disabled:opacity-50"
            :disabled="tablePage >= Math.ceil(table.total / table.pageSize)"
            @click="tablePage++"
          >
            {{ t('common.next') }}
          </button>
        </div>
      </section>
    </template>

    <QrScannerModal :open="scannerOpen" @close="scannerOpen = false" @result="onScanResult" />
    <ConfirmDialog
      v-model:open="confirmOpen"
      :title="t('common.confirm')"
      :message="confirmMessageKey ? t(confirmMessageKey) : ''"
      :confirm-label="t('common.confirm')"
      :cancel-label="t('common.cancel')"
      @confirm="handleConfirm"
    />
  </div>
</template>
