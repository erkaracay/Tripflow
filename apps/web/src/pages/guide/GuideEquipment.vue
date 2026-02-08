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
  EventItem,
  ItemActionResponse,
  ItemParticipantTableResponse,
} from '../../types'

const API = '/api/guide/events'
const route = useRoute()
const { t } = useI18n()
const eventId = computed(() => route.params.eventId as string)
const storageKeyMode = computed(() => `infora:guide:equipment:mode:${eventId.value}`)
const storageKeyAuto = computed(() => `infora:guide:equipment:auto:${eventId.value}`)

const event = ref<EventDto | null>(null)
const items = ref<EventItem[]>([])
const selectedItemId = ref<string | null>(null)
const action = ref<'Give' | 'Return'>('Give')
const code = ref('')
const table = ref<ItemParticipantTableResponse | null>(null)
const tablePage = ref(1)
const tableQuery = ref('')
const tableStatus = ref<'all' | 'given' | 'not_returned'>('all')
const loading = ref(true)
const loadErrorKey = ref<string | null>(null)
const submitting = ref(false)
const scannerOpen = ref(false)
const autoSubmitAfterScan = ref(true)
const codeInput = ref<HTMLInputElement | null>(null)
let lastScannedCode: string | null = null
let lastScannedAt = 0

const { pushToast, removeToast } = useToast()

const loadEventAndItems = async () => {
  loading.value = true
  loadErrorKey.value = null
  try {
    const [eventData, itemsData] = await Promise.all([
      apiGet<EventDto>(`/api/events/${eventId.value}`),
      apiGet<EventItem[]>(`${API}/${eventId.value}/items`),
    ])
    event.value = eventData
    items.value = itemsData
    if (itemsData.length > 0 && !selectedItemId.value) {
      const headset = itemsData.find((i) => i.type === 'Headset' || i.name === 'Headset')
      const first = headset ?? itemsData[0]
      if (first) selectedItemId.value = first.id
    }
  } catch {
    loadErrorKey.value = 'errors.checkIn.load'
  } finally {
    loading.value = false
  }
}

const loadTable = async () => {
  if (!selectedItemId.value) {
    table.value = null
    return
  }
  try {
    const params = new URLSearchParams()
    if (tableQuery.value.trim()) params.set('query', tableQuery.value.trim())
    params.set('status', tableStatus.value === 'not_returned' ? 'not_returned' : tableStatus.value)
    params.set('page', String(tablePage.value))
    params.set('pageSize', '50')
    const res = await apiGet<ItemParticipantTableResponse>(
      `${API}/${eventId.value}/items/${selectedItemId.value}/participants/table?${params}`
    )
    table.value = res
  } catch {
    table.value = null
  }
}

watch([selectedItemId, tablePage, tableQuery, tableStatus], () => {
  void loadTable()
})

const persistMode = () => {
  try {
    globalThis.localStorage?.setItem(storageKeyMode.value, action.value)
  } catch {}
}
const persistAuto = () => {
  try {
    globalThis.localStorage?.setItem(storageKeyAuto.value, autoSubmitAfterScan.value ? '1' : '0')
  } catch {}
}

const submitAction = async (codeValue: string, method: 'Manual' | 'QrScan' = 'Manual') => {
  if (!selectedItemId.value) {
    pushToast({ key: 'equipment.selectItem', tone: 'error' })
    return
  }
  const normalized = normalizeCheckInCode(codeValue).slice(0, 10)
  if (normalized.length < 6) {
    pushToast({ key: 'toast.invalidCodeFormat', tone: 'error' })
    return
  }
  submitting.value = true
  try {
    const res = await apiPostWithPayload<ItemActionResponse>(
      `${API}/${eventId.value}/items/${selectedItemId.value}/actions`,
      { checkInCode: normalized, action: action.value, method }
    )
    pushToast({
      key: action.value === 'Give' ? 'equipment.giveSuccess' : 'equipment.returnSuccess',
      params: { name: res.participantName },
      tone: 'success',
    })
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
  void submitAction(code.value, 'Manual')
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
    pushToast({ key: 'equipment.codeFilled', tone: 'info' })
    return
  }
  const toastId = pushToast({ key: 'common.checkingIn', tone: 'info', timeout: 0 })
  await submitAction(extracted, 'QrScan')
  removeToast(toastId)
}

const formatLastAction = (item: ItemParticipantTableResponse['items'][0]) => {
  const log = item.itemState?.lastLog
  if (!log) return '—'
  const act = log.action === 'Return' ? t('equipment.return') : t('equipment.give')
  const method = log.method === 'QrScan' ? 'QR' : t('common.manual')
  return `${act} · ${method} · ${log.createdAt} · ${log.result}`
}

onMounted(() => {
  const stored = globalThis.localStorage?.getItem(storageKeyMode.value)
  if (stored === 'Return' || stored === 'Give') action.value = stored
  // Tarayıcıya otomatik gönder: her açılışta seçili (varsayılan true)
  void loadEventAndItems()
})

watch(action, persistMode)
watch(autoSubmitAfterScan, persistAuto)
watch(selectedItemId, () => {
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
          :to="`/guide/events/${eventId}/checkin`"
        >
          {{ t('nav.backToEvent') }}
        </RouterLink>
      </div>
      <h1 class="mt-2 text-2xl font-semibold">{{ event?.name ?? t('equipment.title') }}</h1>
      <p v-if="event" class="text-sm text-slate-500">
        {{ t('common.dateRange', { start: event.startDate, end: event.endDate }) }}
      </p>
    </div>

    <LoadingState v-if="loading" message-key="admin.eventDetail.loading" />
    <ErrorState v-else-if="loadErrorKey" :message-key="loadErrorKey" @retry="loadEventAndItems" />

    <template v-else>
      <div class="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:p-6">
        <div class="flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
          <div class="flex flex-1 flex-col gap-3 sm:flex-row sm:items-end">
            <label class="min-w-0 flex-1">
              <span class="block text-sm text-slate-600">{{ t('equipment.item') }}</span>
              <select
                v-model="selectedItemId"
                class="mt-1 w-full rounded border border-slate-200 bg-white px-3 py-2 text-sm"
              >
                <option v-for="i in items" :key="i.id" :value="i.id">{{ i.name }}</option>
                <option v-if="items.length === 0" value="" disabled>{{ t('equipment.noItems') }}</option>
              </select>
            </label>
            <div class="flex items-center gap-2">
              <button
                type="button"
                :class="action === 'Give' ? 'bg-slate-800 text-white' : 'bg-slate-100 text-slate-700'"
                class="rounded px-3 py-2 text-sm font-medium"
                @click="action = 'Give'"
              >
                {{ t('equipment.give') }}
              </button>
              <button
                type="button"
                :class="action === 'Return' ? 'bg-slate-800 text-white' : 'bg-slate-100 text-slate-700'"
                class="rounded px-3 py-2 text-sm font-medium"
                @click="action = 'Return'"
              >
                {{ t('equipment.return') }}
              </button>
            </div>
          </div>
          <div class="flex items-center gap-2">
            <button
              type="button"
              class="rounded border border-slate-200 bg-white px-3 py-2 text-sm shadow-sm hover:border-slate-300"
              @click="openScanner"
            >
              {{ t('equipment.scanQr') }}
            </button>
            <label class="flex items-center gap-2 text-sm">
              <input v-model="autoSubmitAfterScan" type="checkbox" />
              {{ t('equipment.autoSubmit') }}
            </label>
          </div>
        </div>

        <form class="mt-4 flex flex-wrap items-end gap-2" @submit.prevent="onCodeSubmit">
          <label class="min-w-0 flex-1 sm:max-w-xs">
            <span class="sr-only">{{ t('equipment.code') }}</span>
            <input
              ref="codeInput"
              v-model="code"
              type="text"
              inputmode="text"
              autocomplete="off"
              class="w-full rounded border border-slate-200 px-3 py-2 text-sm uppercase"
              :placeholder="t('equipment.codePlaceholder')"
              :disabled="!selectedItemId || submitting"
              @input="(e) => { code = normalizeCheckInCode((e.target as HTMLInputElement).value).slice(0, 10) }"
            />
          </label>
          <button
            type="submit"
            class="rounded bg-slate-800 px-4 py-2 text-sm font-medium text-white hover:bg-slate-700 disabled:opacity-50"
            :disabled="!selectedItemId || submitting || !code.trim()"
          >
            {{ submitting ? t('common.saving') : (action === 'Give' ? t('equipment.give') : t('equipment.return')) }}
          </button>
        </form>
      </div>

      <div v-if="selectedItemId" class="mt-6 rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:p-6">
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
            {{ t('equipment.filterAll') }}
          </button>
          <button
            type="button"
            :class="tableStatus === 'not_returned' ? 'bg-slate-800 text-white' : 'bg-slate-100 text-slate-700'"
            class="rounded px-3 py-1.5 text-sm"
            @click="tableStatus = 'not_returned'"
          >
            {{ t('equipment.filterNotReturned') }}
          </button>
        </div>

        <div class="mt-4 overflow-x-auto">
          <table class="w-full text-sm">
            <thead>
              <tr class="border-b border-slate-200 text-left text-slate-600">
                <th class="p-2">{{ t('common.name') }}</th>
                <th class="p-2">{{ t('equipment.code') }}</th>
                <th class="p-2">{{ t('equipment.status') }}</th>
                <th class="p-2">{{ t('equipment.lastAction') }}</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="row in table?.items" :key="row.id" class="border-b border-slate-100">
                <td class="p-2 font-medium">{{ row.fullName }}</td>
                <td class="p-2">{{ row.checkInCode }}</td>
                <td class="p-2">
                  <span v-if="row.itemState?.given" class="rounded bg-amber-100 px-2 py-0.5 text-amber-800">
                    {{ t('equipment.given') }}
                  </span>
                  <span v-else-if="row.itemState?.lastLog?.action === 'Return'" class="rounded bg-slate-100 px-2 py-0.5 text-slate-700">
                    {{ t('equipment.returned') }}
                  </span>
                  <span v-else class="text-slate-500">—</span>
                </td>
                <td class="p-2 text-slate-600">{{ formatLastAction(row) }}</td>
              </tr>
              <tr v-if="table && table.items.length === 0">
                <td colspan="4" class="p-4 text-center text-slate-500">{{ t('equipment.noParticipants') }}</td>
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
