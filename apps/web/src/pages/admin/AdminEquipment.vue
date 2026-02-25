<script setup lang="ts">
import { computed, nextTick, onMounted, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { apiDelete, apiGet, apiPostWithPayload, apiPut } from '../../lib/api'
import { normalizeQrCode } from '../../lib/qr'
import { normalizeCheckInCode } from '../../lib/normalize'
import { useToast } from '../../lib/toast'
import QrScannerModal from '../../components/QrScannerModal.vue'
import LoadingState from '../../components/ui/LoadingState.vue'
import ErrorState from '../../components/ui/ErrorState.vue'
import ConfirmDialog from '../../components/ui/ConfirmDialog.vue'
import CopyIcon from '../../components/icons/CopyIcon.vue'
import { formatUtcToLocal } from '../../lib/formatters'
import type {
  Event as EventDto,
  EventItem,
  ItemActionResponse,
  ItemParticipantTableResponse,
} from '../../types'

const route = useRoute()
const { t } = useI18n()
const eventId = computed(() => route.params.eventId as string)
const storageKeyMode = computed(() => `infora:admin:equipment:mode:${eventId.value}`)
const storageKeyAuto = computed(() => `infora:admin:equipment:auto:${eventId.value}`)

const event = ref<EventDto | null>(null)
const items = ref<EventItem[]>([])
const activeItems = computed(() => items.value.filter((i) => i.isActive))
const selectedItemId = ref<string | null>(null)
const selectedItem = computed(() => {
  if (!selectedItemId.value) return null
  return items.value.find((i) => i.id === selectedItemId.value) ?? null
})
const action = ref<'Give' | 'Return'>('Give')
const code = ref('')
const table = ref<ItemParticipantTableResponse | null>(null)
const tablePage = ref(1)
const tableQuery = ref('')
const tableStatus = ref<'all' | 'given' | 'not_returned' | 'returned' | 'never_given'>('all')
const tableSort = ref('fullName')
const tableDir = ref<'asc' | 'desc'>('asc')
const loading = ref(true)
const loadErrorKey = ref<string | null>(null)
const submitting = ref(false)
const processingParticipantId = ref<string | null>(null)
const scannerOpen = ref(false)
const autoSubmitAfterScan = ref(true)
const codeInput = ref<HTMLInputElement | null>(null)
let lastScannedCode: string | null = null
let lastScannedAt = 0

const EQUIPMENT_TYPES = ['Headset', 'Badge', 'Kit', 'Other'] as const

const itemsSortKey = ref<'name' | 'type'>('name')
const itemsSortDir = ref<'asc' | 'desc'>('asc')

const sortedItems = computed(() => {
  const list = [...items.value]
  const dir = itemsSortDir.value === 'asc' ? 1 : -1
  list.sort((a, b) => {
    const cmp = itemsSortKey.value === 'name'
      ? (a.name ?? '').localeCompare(b.name ?? '')
      : (a.type ?? '').localeCompare(b.type ?? '')
    return cmp * dir
  })
  return list
})

const setItemsSort = (key: 'name' | 'type') => {
  if (itemsSortKey.value === key) {
    itemsSortDir.value = itemsSortDir.value === 'asc' ? 'desc' : 'asc'
  } else {
    itemsSortKey.value = key
    itemsSortDir.value = 'asc'
  }
}

const typeLabel = (type: string) => {
  return EQUIPMENT_TYPES.includes(type as (typeof EQUIPMENT_TYPES)[number])
    ? t('equipment.types.' + type)
    : type
}

const showAddForm = ref(false)
const addForm = ref({ type: 'Headset' as string, name: '' })
const addSaving = ref(false)
const editingId = ref<string | null>(null)
const editForm = ref({ type: 'Headset', name: '', isActive: true })
const editSaving = ref(false)
const deleteConfirmOpen = ref(false)
const itemToDelete = ref<EventItem | null>(null)
const deleteSaving = ref(false)

const { pushToast, removeToast } = useToast()

const loadEventAndItems = async () => {
  loading.value = true
  loadErrorKey.value = null
  try {
    const [eventData, itemsData] = await Promise.all([
      apiGet<EventDto>(`/api/events/${eventId.value}`),
      apiGet<EventItem[]>(`/api/events/${eventId.value}/items?includeInactive=true`),
    ])
    event.value = eventData
    items.value = itemsData
    const active = itemsData.filter((i) => i.isActive)
    if (active.length > 0 && !selectedItemId.value) {
      const headset = active.find((i) => i.type === 'Headset' || i.name === 'Headset')
      const first = headset ?? active[0]
      if (first) selectedItemId.value = first.id
    }
  } catch {
    loadErrorKey.value = 'errors.checkIn.load'
  } finally {
    loading.value = false
  }
}

const openAdd = () => {
  showAddForm.value = true
  addForm.value = { type: 'Headset', name: '' }
}
const cancelAdd = () => {
  showAddForm.value = false
}
const submitAdd = async () => {
  const name = addForm.value.name?.trim()
  if (!name) {
    pushToast({ key: 'equipment.validation.nameRequired', tone: 'error' })
    return
  }
  addSaving.value = true
  try {
    await apiPostWithPayload<EventItem>(`/api/events/${eventId.value}/items/create`, {
      type: addForm.value.type || 'Headset',
      title: addForm.value.type || 'Headset',
      name,
    })
    pushToast({ key: 'equipment.createSuccess', tone: 'success' })
    showAddForm.value = false
    await loadEventAndItems()
  } catch {
    pushToast({ key: 'common.saveFailed', tone: 'error' })
  } finally {
    addSaving.value = false
  }
}

const openEdit = (item: EventItem) => {
  editingId.value = item.id
  editForm.value = {
    type: EQUIPMENT_TYPES.includes(item.type as (typeof EQUIPMENT_TYPES)[number]) ? item.type : 'Other',
    name: item.name,
    isActive: item.isActive,
  }
}
const cancelEdit = () => {
  editingId.value = null
}
const submitEdit = async () => {
  const id = editingId.value
  if (!id) return
  const name = editForm.value.name?.trim()
  if (!name) {
    pushToast({ key: 'equipment.validation.nameRequired', tone: 'error' })
    return
  }
  editSaving.value = true
  try {
    await apiPut<EventItem>(`/api/events/${eventId.value}/items/${id}`, {
      type: editForm.value.type,
      title: editForm.value.type,
      name,
      isActive: editForm.value.isActive,
    })
    pushToast({ key: 'equipment.updateSuccess', tone: 'success' })
    editingId.value = null
    await loadEventAndItems()
  } catch {
    pushToast({ key: 'common.saveFailed', tone: 'error' })
  } finally {
    editSaving.value = false
  }
}

const openDeleteConfirm = (item: EventItem) => {
  itemToDelete.value = item
  deleteConfirmOpen.value = true
}
const closeDeleteConfirm = () => {
  deleteConfirmOpen.value = false
  itemToDelete.value = null
}
const confirmDelete = async () => {
  const item = itemToDelete.value
  if (!item) return
  deleteSaving.value = true
  try {
    await apiDelete(`/api/events/${eventId.value}/items/${item.id}`)
    pushToast({ key: 'equipment.deleteSuccess', tone: 'success' })
    closeDeleteConfirm()
    await loadEventAndItems()
    if (selectedItemId.value === item.id) selectedItemId.value = activeItems.value[0]?.id ?? null
  } catch {
    pushToast({ key: 'common.saveFailed', tone: 'error' })
  } finally {
    deleteSaving.value = false
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
    if (tableStatus.value !== 'all') {
      params.set('status', tableStatus.value)
    }
    params.set('page', String(tablePage.value))
    params.set('pageSize', '50')
    params.set('sort', tableSort.value)
    params.set('dir', tableDir.value)
    const res = await apiGet<ItemParticipantTableResponse>(
      `/api/events/${eventId.value}/items/${selectedItemId.value}/participants/table?${params}`
    )
    table.value = res
  } catch {
    table.value = null
  }
}

watch([selectedItemId, tablePage, tableQuery, tableStatus, tableSort, tableDir], () => {
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
    globalThis.localStorage?.setItem(storageKeyMode.value, action.value)
  } catch {}
}
const persistAuto = () => {
  try {
    globalThis.localStorage?.setItem(storageKeyAuto.value, autoSubmitAfterScan.value ? '1' : '0')
  } catch {}
}

const submitAction = async (codeValue: string, method: 'Manual' | 'QrScan' = 'Manual', actionOverride?: 'Give' | 'Return') => {
  if (!selectedItemId.value) {
    pushToast({ key: 'equipment.selectItem', tone: 'error' })
    return
  }
  const normalized = normalizeCheckInCode(codeValue).slice(0, 10)
  if (normalized.length < 6) {
    pushToast({ key: 'toast.invalidCodeFormat', tone: 'error' })
    return
  }
  const actionToUse = actionOverride ?? action.value
  submitting.value = true
  try {
    const res = await apiPostWithPayload<ItemActionResponse>(
      `/api/events/${eventId.value}/items/${selectedItemId.value}/actions`,
      { checkInCode: normalized, action: actionToUse, method }
    )
    pushToast({
      key: actionToUse === 'Give' ? 'equipment.giveSuccess' : 'equipment.returnSuccess',
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

const markGiven = async (row: ItemParticipantTableResponse['items'][0]) => {
  if (row.itemState?.given || processingParticipantId.value === row.id) {
    return
  }
  processingParticipantId.value = row.id
  try {
    await submitAction(row.checkInCode, 'Manual', 'Give')
  } finally {
    processingParticipantId.value = null
  }
}

const markReturned = async (row: ItemParticipantTableResponse['items'][0]) => {
  if (!row.itemState?.given || processingParticipantId.value === row.id) {
    return
  }
  processingParticipantId.value = row.id
  try {
    await submitAction(row.checkInCode, 'Manual', 'Return')
  } finally {
    processingParticipantId.value = null
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
  if (submitting.value) {
    pushToast({ key: 'common.checkingIn', tone: 'info' })
    return
  }
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
  const time = formatUtcToLocal(log.createdAt, { timeOnly: true })
  return `${act} · ${time}`
}

const equipmentSummary = computed(() => {
  if (!table.value) {
    return { givenCount: 0, totalCount: 0, notReturnedCount: 0 }
  }
  const givenCount = table.value.items.filter((item) => item.itemState?.given).length
  const totalCount = table.value.total
  const notReturnedCount = givenCount // given=true means not returned yet
  return { givenCount, totalCount, notReturnedCount }
})

const setEquipmentFilter = (value: typeof tableStatus.value) => {
  tableStatus.value = value
  tablePage.value = 1
}

const copyCode = async (value: string) => {
  if (!value) return
  try {
    if (globalThis.navigator?.clipboard?.writeText) {
      await globalThis.navigator.clipboard.writeText(value)
      pushToast({ key: 'common.copySuccess', tone: 'success' })
    } else {
      pushToast({ key: 'errors.copyNotSupported', tone: 'error' })
    }
  } catch {
    pushToast({ key: 'errors.copyFailed', tone: 'error' })
  }
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
          :to="`/admin/events/${eventId}`"
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
        <h2 class="mb-3 text-lg font-medium text-slate-800">{{ t('equipment.listTitle') }}</h2>
        <div class="mb-4 overflow-x-auto">
          <table class="w-full text-sm">
            <thead>
              <tr class="border-b border-slate-200 text-left text-slate-600">
                <th class="p-2">
                  <button
                    type="button"
                    class="inline-flex cursor-pointer select-none items-center gap-0.5 rounded hover:bg-slate-100"
                    @click="setItemsSort('name')"
                  >
                    {{ t('equipment.name') }}{{ itemsSortKey === 'name' ? (itemsSortDir === 'asc' ? ' ↑' : ' ↓') : '' }}
                  </button>
                </th>
                <th class="p-2">
                  <button
                    type="button"
                    class="inline-flex cursor-pointer select-none items-center gap-0.5 rounded hover:bg-slate-100"
                    @click="setItemsSort('type')"
                  >
                    {{ t('equipment.type') }}{{ itemsSortKey === 'type' ? (itemsSortDir === 'asc' ? ' ↑' : ' ↓') : '' }}
                  </button>
                </th>
                <th class="p-2">{{ t('equipment.active') }}</th>
                <th class="p-2 w-0"></th>
              </tr>
            </thead>
            <tbody>
              <template v-for="i in sortedItems" :key="i.id">
                <tr v-if="editingId !== i.id" class="border-b border-slate-100">
                  <td class="p-2 font-medium">{{ i.name }}</td>
                  <td class="p-2 text-slate-600">{{ typeLabel(i.type) }}</td>
                  <td class="p-2">
                    <span v-if="i.isActive" class="text-emerald-600">{{ t('common.yes') }}</span>
                    <span v-else class="text-slate-400">{{ t('common.no') }}</span>
                  </td>
                  <td class="p-2">
                    <button
                      type="button"
                      class="rounded px-2 py-1 text-slate-600 hover:bg-slate-100"
                      @click="openEdit(i)"
                    >
                      {{ t('equipment.edit') }}
                    </button>
                    <button
                      type="button"
                      class="rounded px-2 py-1 text-red-600 hover:bg-red-50"
                      @click="openDeleteConfirm(i)"
                    >
                      {{ t('equipment.delete') }}
                    </button>
                  </td>
                </tr>
                <tr v-else class="border-b border-slate-100 bg-slate-50">
                  <td class="p-2" colspan="4">
                    <form class="flex w-full" @submit.prevent="submitEdit">
                      <table class="w-full text-sm" style="table-layout: fixed">
                        <tbody>
                          <tr>
                            <td class="w-[40%] p-2 align-middle">
                            <label class="block">
                              <span class="sr-only">{{ t('equipment.name') }}</span>
                              <input
                                v-model.trim="editForm.name"
                                type="text"
                                class="w-full min-w-0 rounded border border-slate-200 px-2 py-1.5 text-sm"
                                :placeholder="t('equipment.namePlaceholder')"
                              />
                            </label>
                          </td>
                          <td class="w-[25%] p-2 align-middle">
                            <label class="block">
                              <span class="sr-only">{{ t('equipment.type') }}</span>
                              <select
                                v-model="editForm.type"
                                class="w-full min-w-0 rounded border border-slate-200 px-2 py-1.5 text-sm"
                              >
                                <option v-for="opt in EQUIPMENT_TYPES" :key="opt" :value="opt">
                                  {{ t('equipment.types.' + opt) }}
                                </option>
                              </select>
                            </label>
                          </td>
                          <td class="w-[20%] p-2 align-middle">
                            <label class="inline-flex cursor-pointer items-center gap-1.5 text-slate-600">
                              <input v-model="editForm.isActive" type="checkbox" class="h-4 w-4 rounded border-slate-300" />
                              <span>{{ t('equipment.active') }}</span>
                            </label>
                          </td>
                          <td class="w-[15%] p-2 align-middle text-right">
                            <button
                              type="submit"
                              class="rounded bg-slate-800 px-3 py-1.5 text-sm text-white hover:bg-slate-700 disabled:opacity-50"
                              :disabled="editSaving"
                            >
                              {{ editSaving ? t('common.saving') : t('equipment.save') }}
                            </button>
                            <button
                              type="button"
                              class="ml-1 rounded border border-slate-200 px-3 py-1.5 text-sm hover:bg-slate-100"
                              @click="cancelEdit"
                            >
                              {{ t('equipment.cancel') }}
                            </button>
                          </td>
                          </tr>
                        </tbody>
                      </table>
                    </form>
                  </td>
                </tr>
              </template>
              <tr v-if="items.length === 0 && !showAddForm">
                <td colspan="4" class="p-4 text-center text-slate-500">{{ t('equipment.noItemsYet') }}</td>
              </tr>
            </tbody>
          </table>
        </div>
        <div v-if="!showAddForm" class="mb-6">
          <button
            type="button"
            class="rounded border border-slate-200 bg-white px-3 py-2 text-sm shadow-sm hover:border-slate-300"
            @click="openAdd"
          >
            {{ t('equipment.addItem') }}
          </button>
        </div>
        <form
          v-else
          class="mb-6 flex flex-wrap items-end gap-2 rounded border border-slate-200 bg-slate-50 p-3"
          @submit.prevent="submitAdd"
        >
          <label class="min-w-0">
            <span class="block text-xs text-slate-500">{{ t('equipment.name') }}</span>
            <input
              v-model.trim="addForm.name"
              type="text"
              class="mt-0.5 w-40 rounded border border-slate-200 px-2 py-1.5 text-sm"
              :placeholder="t('equipment.namePlaceholder')"
            />
          </label>
          <label class="min-w-0">
            <span class="block text-xs text-slate-500">{{ t('equipment.type') }}</span>
            <select
              v-model="addForm.type"
              class="mt-0.5 w-28 rounded border border-slate-200 px-2 py-1.5 text-sm"
            >
              <option v-for="opt in EQUIPMENT_TYPES" :key="opt" :value="opt">
                {{ t('equipment.types.' + opt) }}
              </option>
            </select>
          </label>
          <button
            type="submit"
            class="rounded bg-slate-800 px-3 py-2 text-sm text-white hover:bg-slate-700 disabled:opacity-50"
            :disabled="addSaving"
          >
            {{ addSaving ? t('common.saving') : t('equipment.addItem') }}
          </button>
          <button
            type="button"
            class="rounded border border-slate-200 bg-white px-3 py-2 text-sm hover:bg-slate-100"
            @click="cancelAdd"
          >
            {{ t('equipment.cancel') }}
          </button>
        </form>
      </div>

      <div class="mt-6 rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:p-6">
        <h2 class="mb-3 text-lg font-medium text-slate-800">{{ t('equipment.giveReturnTitle') }}</h2>
        <div class="flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
          <div class="flex flex-1 flex-col gap-3 sm:flex-row sm:items-end">
            <label class="min-w-0 flex-1">
              <span class="block text-sm text-slate-600">{{ t('equipment.item') }}</span>
              <select
                v-model="selectedItemId"
                class="mt-1 w-full rounded border border-slate-200 bg-white px-3 py-2 text-sm"
              >
                <option v-for="i in activeItems" :key="i.id" :value="i.id">{{ i.name }}</option>
                <option v-if="activeItems.length === 0" value="" disabled>{{ t('equipment.noItems') }}</option>
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
              class="inline-flex items-center justify-center gap-2 rounded border border-slate-200 bg-white px-3 py-2 text-sm shadow-sm transition-opacity duration-200 hover:border-slate-300 disabled:pointer-events-none disabled:opacity-70"
              :disabled="submitting"
              @click="openScanner"
            >
              <span
                v-if="submitting"
                class="h-3 w-3 shrink-0 animate-spin rounded-full border-2 border-slate-300 border-t-slate-600"
                aria-hidden="true"
              />
              <span>{{ submitting ? t('common.checkingIn') : t('equipment.scanQr') }}</span>
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

      <div v-if="selectedItemId && table" class="mt-6 flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
        <div class="min-w-0 flex-1">
          <h2 class="text-2xl font-semibold text-slate-900">
            {{ selectedItem?.name ?? t('equipment.title') }}
          </h2>
        </div>
        <div class="min-w-[240px] rounded-xl border border-slate-200 bg-slate-50 px-4 py-3 text-sm">
          <div class="text-xs uppercase tracking-wide text-slate-400">{{ t('equipment.givenLabel') }}</div>
          <div class="mt-1 text-xl font-semibold text-slate-800">
            {{ equipmentSummary.givenCount }} / {{ equipmentSummary.totalCount }}
          </div>
          <div class="mt-2 flex flex-col gap-1">
            <button
              class="block text-left text-xs font-semibold text-slate-600 underline-offset-2 hover:text-slate-900 hover:underline"
              type="button"
              @click="setEquipmentFilter(tableStatus === 'not_returned' ? 'all' : 'not_returned')"
            >
              {{ t('equipment.notReturnedLabel') }}: {{ equipmentSummary.notReturnedCount }}
            </button>
          </div>
        </div>
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
            class="rounded-full border px-3 py-1 text-xs font-semibold"
            :class="
              tableStatus === 'all'
                ? 'border-slate-900 bg-slate-900 text-white'
                : 'border-slate-200 bg-white text-slate-700 hover:border-slate-300'
            "
            @click="setEquipmentFilter('all')"
          >
            {{ t('equipment.filterAll') }}
          </button>
          <button
            type="button"
            class="rounded-full border px-3 py-1 text-xs font-semibold"
            :class="
              tableStatus === 'given'
                ? 'border-slate-900 bg-slate-900 text-white'
                : 'border-slate-200 bg-white text-slate-700 hover:border-slate-300'
            "
            @click="setEquipmentFilter('given')"
          >
            {{ t('equipment.filterGiven') }}
          </button>
          <button
            type="button"
            class="rounded-full border px-3 py-1 text-xs font-semibold"
            :class="
              tableStatus === 'not_returned'
                ? 'border-slate-900 bg-slate-900 text-white'
                : 'border-slate-200 bg-white text-slate-700 hover:border-slate-300'
            "
            @click="setEquipmentFilter('not_returned')"
          >
            {{ t('equipment.filterNotReturned') }}
          </button>
          <button
            type="button"
            class="rounded-full border px-3 py-1 text-xs font-semibold"
            :class="
              tableStatus === 'returned'
                ? 'border-slate-900 bg-slate-900 text-white'
                : 'border-slate-200 bg-white text-slate-700 hover:border-slate-300'
            "
            @click="setEquipmentFilter('returned')"
          >
            {{ t('equipment.filterReturned') }}
          </button>
          <button
            type="button"
            class="rounded-full border px-3 py-1 text-xs font-semibold"
            :class="
              tableStatus === 'never_given'
                ? 'border-slate-900 bg-slate-900 text-white'
                : 'border-slate-200 bg-white text-slate-700 hover:border-slate-300'
            "
            @click="setEquipmentFilter('never_given')"
          >
            {{ t('equipment.filterNeverGiven') }}
          </button>
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
                <th class="p-2">{{ t('equipment.code') }}</th>
                <th class="min-w-[120px] p-2">{{ t('equipment.status') }}</th>
                <th class="min-w-[220px] p-2">{{ t('equipment.lastAction') }}</th>
              </tr>
            </thead>
            <tbody>
              <template v-for="row in table?.items" :key="row.id">
                <tr class="border-b border-slate-100">
                  <td class="p-2 font-medium">{{ row.fullName }}</td>
                  <td class="p-2">
                    <button
                      type="button"
                      class="inline-flex items-center gap-1.5 rounded font-mono text-slate-700 hover:bg-slate-100 hover:text-slate-900"
                      :title="t('common.copy')"
                      @click="copyCode(row.checkInCode)"
                    >
                      <span>{{ row.checkInCode }}</span>
                      <CopyIcon :size="14" icon-class="shrink-0 text-slate-500" />
                    </button>
                  </td>
                  <td class="min-w-[120px] whitespace-nowrap p-2">
                    <span v-if="row.itemState?.given" class="inline-block rounded bg-amber-100 px-2 py-0.5 text-amber-800">
                      {{ t('equipment.given') }}
                    </span>
                    <span v-else-if="row.itemState?.lastLog?.action === 'Return'" class="inline-block rounded bg-slate-100 px-2 py-0.5 text-slate-700">
                      {{ t('equipment.returned') }}
                    </span>
                    <span v-else class="text-slate-500">—</span>
                  </td>
                  <td class="min-w-[220px] whitespace-nowrap p-2 text-slate-600">{{ formatLastAction(row) }}</td>
                </tr>
                <tr class="border-b border-slate-200 bg-slate-50">
                  <td colspan="4" class="p-2">
                    <div class="flex flex-row flex-wrap items-center gap-2">
                      <button
                        v-if="!row.itemState?.given"
                        class="rounded-full border border-slate-200 bg-white px-3 py-1 text-xs font-medium text-slate-700 hover:border-slate-300 disabled:cursor-not-allowed disabled:opacity-50"
                        type="button"
                        :disabled="processingParticipantId === row.id"
                        @click="markGiven(row)"
                      >
                        {{ t('equipment.give') }}
                      </button>
                      <button
                        v-if="row.itemState?.given"
                        class="rounded-full border border-slate-200 bg-white px-3 py-1 text-xs font-medium text-slate-700 hover:border-slate-300 disabled:cursor-not-allowed disabled:opacity-50"
                        type="button"
                        :disabled="processingParticipantId === row.id"
                        @click="markReturned(row)"
                      >
                        {{ t('equipment.return') }}
                      </button>
                    </div>
                  </td>
                </tr>
              </template>
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
    <ConfirmDialog
      :open="deleteConfirmOpen"
      :title="t('equipment.deleteConfirmTitle')"
      :message="itemToDelete ? t('equipment.deleteConfirmMessage', { name: itemToDelete.name }) : ''"
      :confirm-label="t('equipment.delete')"
      tone="danger"
      @update:open="deleteConfirmOpen = $event"
      @confirm="confirmDelete"
      @cancel="closeDeleteConfirm"
    />
  </div>
</template>
