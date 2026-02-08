<script setup lang="ts">
import { computed, nextTick, onMounted, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { apiGet, apiPostWithPayload } from '../../lib/api'
import { normalizeQrCode } from '../../lib/qr'
import { normalizeCheckInCode } from '../../lib/normalize'
import { useToast } from '../../lib/toast'
import QrScannerModal from '../../components/QrScannerModal.vue'
import LoadingState from '../../components/ui/LoadingState.vue'
import ErrorState from '../../components/ui/ErrorState.vue'
import type {
  Event as EventDto,
  EventActivity,
  ActivityCheckInResponse,
  ActivityParticipantTableResponse,
} from '../../types'

const route = useRoute()
const { t } = useI18n()
const eventId = computed(() => route.params.eventId as string)
const storageKeyMode = computed(() => `infora:admin:activityCheckIn:mode:${eventId.value}`)
const storageKeyAuto = computed(() => `infora:admin:activityCheckIn:auto:${eventId.value}`)

const event = ref<EventDto | null>(null)
const activities = ref<EventActivity[]>([])
const selectedActivityId = ref<string | null>(null)
const direction = ref<'Entry' | 'Exit'>('Entry')
const code = ref('')
const table = ref<ActivityParticipantTableResponse | null>(null)
const tablePage = ref(1)
const tableQuery = ref('')
const tableStatus = ref<'all' | 'checked_in' | 'not_checked_in'>('all')
const loading = ref(true)
const loadErrorKey = ref<string | null>(null)
const submitting = ref(false)
const scannerOpen = ref(false)
const autoSubmitAfterScan = ref(false)
const codeInput = ref<HTMLInputElement | null>(null)
let lastScannedCode: string | null = null
let lastScannedAt = 0

const { pushToast, removeToast } = useToast()

const loadEventAndActivities = async () => {
  loading.value = true
  loadErrorKey.value = null
  try {
    const [eventData, activitiesData] = await Promise.all([
      apiGet<EventDto>(`/api/events/${eventId.value}`),
      apiGet<EventActivity[]>(`/api/events/${eventId.value}/activities/for-checkin`),
    ])
    event.value = eventData
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
    return
  }
  try {
    const params = new URLSearchParams()
    if (tableQuery.value.trim()) params.set('query', tableQuery.value.trim())
    params.set('status', tableStatus.value)
    params.set('page', String(tablePage.value))
    params.set('pageSize', '50')
    const res = await apiGet<ActivityParticipantTableResponse>(
      `/api/events/${eventId.value}/activities/${selectedActivityId.value}/participants/table?${params}`
    )
    table.value = res
  } catch {
    table.value = null
  }
}

watch([selectedActivityId, tablePage, tableQuery, tableStatus], () => {
  void loadTable()
})

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

const submitCheckIn = async (codeValue: string, method: 'Manual' | 'QrScan' = 'Manual') => {
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
    const res = await apiPostWithPayload<ActivityCheckInResponse>(
      `/api/events/${eventId.value}/activities/${selectedActivityId.value}/checkins`,
      { checkInCode: normalized, direction: direction.value, method }
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
  void submitCheckIn(code.value, 'Manual')
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
  if (!autoSubmitAfterScan.value) {
    pushToast({ key: 'activityCheckIn.codeFilled', tone: 'info' })
    return
  }
  const toastId = pushToast({ key: 'common.checkingIn', tone: 'info', timeout: 0 })
  await submitCheckIn(extracted, 'QrScan')
  removeToast(toastId)
}

const formatLastAction = (item: ActivityParticipantTableResponse['items'][0]) => {
  const log = item.activityState?.lastLog
  if (!log) return '—'
  const dir = log.direction === 'Exit' ? t('common.exit') : t('common.entry')
  const method = log.method === 'QrScan' ? 'QR' : t('common.manual')
  return `${dir} · ${method} · ${log.createdAt} · ${log.result}`
}

onMounted(() => {
  const storedMode = globalThis.localStorage?.getItem(storageKeyMode.value)
  if (storedMode === 'Exit' || storedMode === 'Entry') direction.value = storedMode
  autoSubmitAfterScan.value = globalThis.localStorage?.getItem(storageKeyAuto.value) === '1'
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
      <h1 class="mt-2 text-2xl font-semibold">{{ event?.name ?? t('activityCheckIn.title') }}</h1>
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
                <option v-for="a in activities" :key="a.id" :value="a.id">{{ a.title }}</option>
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
              class="rounded border border-slate-200 bg-white px-3 py-2 text-sm shadow-sm hover:border-slate-300"
              @click="openScanner"
            >
              {{ t('activityCheckIn.scanQr') }}
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

      <div v-if="selectedActivityId" class="mt-6 rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:p-6">
        <div class="flex flex-wrap items-center gap-2">
          <input
            v-model.trim="tableQuery"
            type="search"
            class="rounded border border-slate-200 px-3 py-2 text-sm sm:w-48"
            :placeholder="t('common.search')"
          />
          <button
            type="button"
            :class="tableStatus === 'all' ? 'bg-slate-800 text-white' : 'bg-slate-100 text-slate-700'"
            class="rounded px-3 py-1.5 text-sm"
            @click="tableStatus = 'all'"
          >
            {{ t('activityCheckIn.filterAll') }}
          </button>
          <button
            type="button"
            :class="tableStatus === 'checked_in' ? 'bg-slate-800 text-white' : 'bg-slate-100 text-slate-700'"
            class="rounded px-3 py-1.5 text-sm"
            @click="tableStatus = 'checked_in'"
          >
            {{ t('activityCheckIn.filterCheckedIn') }}
          </button>
          <button
            type="button"
            :class="tableStatus === 'not_checked_in' ? 'bg-slate-800 text-white' : 'bg-slate-100 text-slate-700'"
            class="rounded px-3 py-1.5 text-sm"
            @click="tableStatus = 'not_checked_in'"
          >
            {{ t('activityCheckIn.filterNotCheckedIn') }}
          </button>
        </div>

        <div class="mt-4 overflow-x-auto">
          <table class="w-full text-sm">
            <thead>
              <tr class="border-b border-slate-200 text-left text-slate-600">
                <th class="p-2">{{ t('common.name') }}</th>
                <th class="p-2">{{ t('activityCheckIn.code') }}</th>
                <th class="p-2">{{ t('activityCheckIn.status') }}</th>
                <th class="p-2">{{ t('activityCheckIn.lastAction') }}</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="row in table?.items" :key="row.id" class="border-b border-slate-100">
                <td class="p-2 font-medium">{{ row.fullName }}</td>
                <td class="p-2">{{ row.checkInCode }}</td>
                <td class="p-2">
                  <span v-if="row.activityState?.isCheckedIn" class="rounded bg-emerald-100 px-2 py-0.5 text-emerald-800">
                    {{ t('activityCheckIn.checkedIn') }}
                  </span>
                  <span v-else-if="row.activityState?.lastLog?.direction === 'Exit'" class="rounded bg-slate-100 px-2 py-0.5 text-slate-700">
                    {{ t('activityCheckIn.exited') }}
                  </span>
                  <span v-else class="text-slate-500">—</span>
                </td>
                <td class="p-2 text-slate-600">{{ formatLastAction(row) }}</td>
              </tr>
              <tr v-if="table && table.items.length === 0">
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
      </div>
    </template>

    <QrScannerModal :open="scannerOpen" @close="scannerOpen = false" @result="onScanResult" />
  </div>
</template>
