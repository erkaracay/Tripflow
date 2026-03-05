<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { RouterLink, useRoute } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { apiGet, bulkApplyParticipantRooms } from '../../lib/api'
import { useToast } from '../../lib/toast'
import LoadingState from '../../components/ui/LoadingState.vue'
import ErrorState from '../../components/ui/ErrorState.vue'
import AppCombobox from '../../components/ui/AppCombobox.vue'
import type {
  AppComboboxOption,
  BulkApplyParticipantRoomsRequest,
  Event as EventDto,
  EventDocTabDto,
  ParticipantRoomPatch,
  ParticipantTableItem,
  ParticipantTableResponse,
} from '../../types'

type RoomDraft = {
  accommodationDocTabId: string | null
  roomNo: string
  roomType: string
  boardType: string
  personNo: string
  hotelCheckInDate: string
  hotelCheckOutDate: string
}

type PasteApplySummary = {
  parsed: number
  matched: number
  notFound: number
  invalid: number
}

const route = useRoute()
const { t } = useI18n()
const { pushToast } = useToast()

const eventId = computed(() => String(route.params.eventId ?? ''))
const loading = ref(false)
const saving = ref(false)
const event = ref<EventDto | null>(null)
const tabs = ref<EventDocTabDto[]>([])
const rows = ref<ParticipantTableItem[]>([])
const total = ref(0)
const errorMessage = ref<string | null>(null)

const searchInput = ref('')
const page = ref(1)
const pageSize = ref(200)
const filters = reactive<{
  query: string
  status: 'all' | 'arrived' | 'not_arrived'
  accommodationFilter: string
}>({
  query: '',
  status: 'all',
  accommodationFilter: 'all',
})

const selectedIds = ref<string[]>([])
const draftById = ref<Record<string, RoomDraft>>({})
const originalById = ref<Record<string, RoomDraft>>({})

const bulkForm = reactive<{
  accommodationDocTabId: string
  roomNo: string
  roomType: string
  boardType: string
  personNo: string
  hotelCheckInDate: string
  hotelCheckOutDate: string
  overwriteMode: 'always' | 'only_empty'
}>({
  accommodationDocTabId: '',
  roomNo: '',
  roomType: '',
  boardType: '',
  personNo: '',
  hotelCheckInDate: '',
  hotelCheckOutDate: '',
  overwriteMode: 'always',
})

const pasteInput = ref('')
const pasteSummary = ref<PasteApplySummary | null>(null)

const normalize = (value: string | null | undefined) => (value ?? '').trim()
const normalizeKey = (value: string) => value
  .toLocaleLowerCase('tr-TR')
  .replace(/ç/g, 'c')
  .replace(/ğ/g, 'g')
  .replace(/ı/g, 'i')
  .replace(/ö/g, 'o')
  .replace(/ş/g, 's')
  .replace(/ü/g, 'u')
  .replace(/[^a-z0-9]+/g, '')

const accommodationTabs = computed(() =>
  [...tabs.value]
    .filter((tab) => (tab.type ?? '').trim().toLowerCase() === 'hotel')
    .sort((a, b) => {
      if (a.sortOrder !== b.sortOrder) return a.sortOrder - b.sortOrder
      return a.title.localeCompare(b.title)
    })
)

const accommodationLabelById = computed(() => {
  const map = new Map<string, string>()
  for (const tab of accommodationTabs.value) {
    map.set(tab.id, tab.title)
  }
  return map
})

const statusOptions = computed<AppComboboxOption[]>(() => [
  { value: 'all', label: t('admin.roomOps.filters.statusAll') },
  { value: 'arrived', label: t('admin.roomOps.filters.statusArrived') },
  { value: 'not_arrived', label: t('admin.roomOps.filters.statusNotArrived') },
])

const accommodationFilterOptions = computed<AppComboboxOption[]>(() => {
  const base: AppComboboxOption[] = [
    { value: 'all', label: t('admin.roomOps.filters.accommodationAll') },
    { value: 'unassigned', label: t('admin.roomOps.filters.accommodationUnassigned') },
  ]

  for (const tab of accommodationTabs.value) {
    base.push({ value: tab.id, label: tab.title })
  }

  return base
})

const bulkAccommodationOptions = computed<AppComboboxOption[]>(() => {
  const base: AppComboboxOption[] = [
    { value: '', label: t('admin.roomOps.bulk.noChange') },
    { value: 'unassigned', label: t('admin.roomOps.filters.accommodationUnassigned') },
  ]
  for (const tab of accommodationTabs.value) {
    base.push({ value: tab.id, label: tab.title })
  }
  return base
})

const overwriteModeOptions = computed<AppComboboxOption[]>(() => [
  { value: 'always', label: t('admin.roomOps.bulk.overwriteAlways') },
  { value: 'only_empty', label: t('admin.roomOps.bulk.overwriteOnlyEmpty') },
])

const eventDateMin = computed(() => event.value?.startDate || undefined)
const eventDateMax = computed(() => event.value?.endDate || undefined)

const createDraftFromRow = (row: ParticipantTableItem): RoomDraft => ({
  accommodationDocTabId: row.details?.accommodationDocTabId ?? null,
  roomNo: row.details?.roomNo ?? '',
  roomType: row.details?.roomType ?? '',
  boardType: row.details?.boardType ?? '',
  personNo: row.details?.personNo ?? '',
  hotelCheckInDate: row.details?.hotelCheckInDate ?? '',
  hotelCheckOutDate: row.details?.hotelCheckOutDate ?? '',
})

const cloneDraft = (draft: RoomDraft): RoomDraft => ({
  accommodationDocTabId: draft.accommodationDocTabId,
  roomNo: draft.roomNo,
  roomType: draft.roomType,
  boardType: draft.boardType,
  personNo: draft.personNo,
  hotelCheckInDate: draft.hotelCheckInDate,
  hotelCheckOutDate: draft.hotelCheckOutDate,
})

const hydrateDrafts = () => {
  const nextDraft: Record<string, RoomDraft> = {}
  const nextOriginal: Record<string, RoomDraft> = {}

  for (const row of rows.value) {
    const original = createDraftFromRow(row)
    nextOriginal[row.id] = original
    nextDraft[row.id] = cloneDraft(original)
  }

  draftById.value = nextDraft
  originalById.value = nextOriginal
  selectedIds.value = []
}

const getDraft = (participantId: string): RoomDraft => {
  const existing = draftById.value[participantId]
  if (existing) {
    return existing
  }

  const fallback: RoomDraft = {
    accommodationDocTabId: null,
    roomNo: '',
    roomType: '',
    boardType: '',
    personNo: '',
    hotelCheckInDate: '',
    hotelCheckOutDate: '',
  }
  draftById.value[participantId] = fallback
  return fallback
}

const buildPatch = (participantId: string): ParticipantRoomPatch | null => {
  const draft = draftById.value[participantId]
  const original = originalById.value[participantId]
  if (!draft || !original) {
    return null
  }

  const patch: ParticipantRoomPatch = {}
  let hasAny = false

  if ((draft.accommodationDocTabId ?? null) !== (original.accommodationDocTabId ?? null)) {
    patch.accommodationDocTabId = draft.accommodationDocTabId
    hasAny = true
  }

  const applyTextField = (key: keyof Pick<ParticipantRoomPatch, 'roomNo' | 'roomType' | 'boardType' | 'personNo'>) => {
    const draftValue = normalize(String(draft[key as keyof RoomDraft] ?? ''))
    const originalValue = normalize(String(original[key as keyof RoomDraft] ?? ''))
    if (draftValue !== originalValue) {
      patch[key] = draftValue
      hasAny = true
    }
  }

  applyTextField('roomNo')
  applyTextField('roomType')
  applyTextField('boardType')
  applyTextField('personNo')

  const applyDateField = (key: keyof Pick<ParticipantRoomPatch, 'hotelCheckInDate' | 'hotelCheckOutDate'>) => {
    const draftValue = normalize(String(draft[key as keyof RoomDraft] ?? ''))
    const originalValue = normalize(String(original[key as keyof RoomDraft] ?? ''))
    if (draftValue !== originalValue) {
      patch[key] = draftValue
      hasAny = true
    }
  }

  applyDateField('hotelCheckInDate')
  applyDateField('hotelCheckOutDate')

  return hasAny ? patch : null
}

const changedRowCount = computed(() => {
  let count = 0
  for (const row of rows.value) {
    if (buildPatch(row.id)) {
      count += 1
    }
  }
  return count
})

const canApply = computed(() => changedRowCount.value > 0 && !saving.value)

const participantByTcNo = computed(() => {
  const map = new Map<string, ParticipantTableItem>()
  for (const row of rows.value) {
    map.set(normalize(row.tcNo), row)
  }
  return map
})

const resolveAccommodationInput = (value: string): string | null | undefined => {
  const raw = normalize(value)
  if (!raw) {
    return undefined
  }

  const key = normalizeKey(raw)
  if (['atanmamis', 'unassigned', 'none', 'bos', 'yok'].includes(key)) {
    return null
  }

  const directMatch = accommodationTabs.value.find((tab) => tab.id === raw)
  if (directMatch) {
    return directMatch.id
  }

  const nameMatch = accommodationTabs.value.find((tab) => normalizeKey(tab.title) === key)
  if (nameMatch) {
    return nameMatch.id
  }

  return undefined
}

const loadEventContext = async () => {
  const [eventData, tabData] = await Promise.all([
    apiGet<EventDto>(`/api/events/${eventId.value}`),
    apiGet<EventDocTabDto[]>(`/api/events/${eventId.value}/docs/tabs`),
  ])
  event.value = eventData
  tabs.value = tabData
}

const fetchTable = async () => {
  loading.value = true
  errorMessage.value = null

  try {
    const params = new URLSearchParams()
    params.set('query', filters.query)
    params.set('status', filters.status)
    params.set('flightFilter', 'all')
    params.set('page', String(page.value))
    params.set('pageSize', String(pageSize.value))
    params.set('sort', 'fullName')
    params.set('dir', 'asc')
    if (filters.accommodationFilter !== 'all') {
      params.set('accommodationFilter', filters.accommodationFilter)
    }

    const response = await apiGet<ParticipantTableResponse>(
      `/api/events/${eventId.value}/participants/table?${params.toString()}`
    )
    rows.value = response.items
    total.value = response.total
    page.value = response.page
    pageSize.value = response.pageSize
    hydrateDrafts()
  } catch (err) {
    errorMessage.value = err instanceof Error ? err.message : t('errors.generic')
  } finally {
    loading.value = false
  }
}

const reload = async () => {
  await Promise.all([loadEventContext(), fetchTable()])
}

const runSearch = async () => {
  filters.query = searchInput.value.trim()
  page.value = 1
  await fetchTable()
}

const clearSearch = async () => {
  searchInput.value = ''
  filters.query = ''
  page.value = 1
  await fetchTable()
}

const onStatusChange = async () => {
  page.value = 1
  await fetchTable()
}

const onAccommodationFilterChange = async () => {
  page.value = 1
  await fetchTable()
}

const toggleRowSelection = (participantId: string, checked: boolean) => {
  if (checked) {
    if (!selectedIds.value.includes(participantId)) {
      selectedIds.value = [...selectedIds.value, participantId]
    }
    return
  }
  selectedIds.value = selectedIds.value.filter((id) => id !== participantId)
}

const applyBulkDraft = () => {
  if (selectedIds.value.length === 0) {
    pushToast({ key: 'admin.roomOps.bulk.selectRowsFirst', tone: 'info' })
    return
  }

  const allowOverwrite = bulkForm.overwriteMode === 'always'
  const hasAnyBulkInput =
    normalize(bulkForm.accommodationDocTabId) ||
    normalize(bulkForm.roomNo) ||
    normalize(bulkForm.roomType) ||
    normalize(bulkForm.boardType) ||
    normalize(bulkForm.personNo) ||
    normalize(bulkForm.hotelCheckInDate) ||
    normalize(bulkForm.hotelCheckOutDate)

  if (!hasAnyBulkInput) {
    pushToast({ key: 'admin.roomOps.bulk.noValues', tone: 'info' })
    return
  }

  for (const participantId of selectedIds.value) {
    const draft = getDraft(participantId)
    const canSetAccommodation = allowOverwrite || !normalize(draft.accommodationDocTabId)
    if (bulkForm.accommodationDocTabId && canSetAccommodation) {
      draft.accommodationDocTabId =
        bulkForm.accommodationDocTabId === 'unassigned' ? null : bulkForm.accommodationDocTabId
    }

    const applyText = (key: keyof Pick<RoomDraft, 'roomNo' | 'roomType' | 'boardType' | 'personNo'>, value: string) => {
      const normalizedValue = normalize(value)
      if (!normalizedValue) {
        return
      }
      if (!allowOverwrite && normalize(draft[key])) {
        return
      }
      draft[key] = normalizedValue
    }

    applyText('roomNo', bulkForm.roomNo)
    applyText('roomType', bulkForm.roomType)
    applyText('boardType', bulkForm.boardType)
    applyText('personNo', bulkForm.personNo)

    const applyDate = (key: keyof Pick<RoomDraft, 'hotelCheckInDate' | 'hotelCheckOutDate'>, value: string) => {
      const normalizedValue = normalize(value)
      if (!normalizedValue) {
        return
      }
      if (!allowOverwrite && normalize(draft[key])) {
        return
      }
      draft[key] = normalizedValue
    }

    applyDate('hotelCheckInDate', bulkForm.hotelCheckInDate)
    applyDate('hotelCheckOutDate', bulkForm.hotelCheckOutDate)
  }
}

const applyPaste = () => {
  const raw = pasteInput.value.trim()
  if (!raw) {
    pasteSummary.value = null
    return
  }

  const lines = raw.split(/\r?\n/).map((line) => line.trim()).filter(Boolean)
  if (lines.length === 0) {
    pasteSummary.value = null
    return
  }

  const splitRow = (line: string) => line.split('\t').map((cell) => cell.trim())
  const firstLine = lines[0]
  if (!firstLine) {
    pasteSummary.value = null
    return
  }
  const firstCells = splitRow(firstLine)
  const normalizedHeaders = firstCells.map((header) => normalizeKey(header))
  const hasHeader = normalizedHeaders.includes('tcno') || normalizedHeaders.includes('tckimlikno')

  const headerMap = new Map<string, number>()
  if (hasHeader) {
    normalizedHeaders.forEach((key, index) => {
      headerMap.set(key, index)
    })
  }

  const readCell = (cells: string[], index: number | undefined) => (index === undefined ? undefined : cells[index] ?? '')

  const resolveIndex = (aliases: string[], fallbackIndex: number) => {
    if (!hasHeader) return fallbackIndex
    for (const alias of aliases) {
      const idx = headerMap.get(alias)
      if (idx !== undefined) return idx
    }
    return undefined
  }

  const tcNoIndex = resolveIndex(['tcno', 'tckimlikno'], 0)
  const accommodationIndex = resolveIndex(['accommodation', 'konaklama', 'hotel', 'otel'], 1)
  const roomNoIndex = resolveIndex(['roomno', 'oda', 'odano'], 2)
  const roomTypeIndex = resolveIndex(['roomtype', 'odatipi'], 3)
  const boardIndex = resolveIndex(['boardtype', 'board', 'pansiyon'], 4)
  const personNoIndex = resolveIndex(['personno', 'kisino'], 5)
  const checkInIndex = resolveIndex(['hotelcheckindate', 'checkindate', 'giris'], 6)
  const checkOutIndex = resolveIndex(['hotelcheckoutdate', 'checkoutdate', 'cikis'], 7)

  const dataLines = hasHeader ? lines.slice(1) : lines
  const summary: PasteApplySummary = {
    parsed: 0,
    matched: 0,
    notFound: 0,
    invalid: 0,
  }

  for (const line of dataLines) {
    const cells = splitRow(line)
    const tcNo = normalize(readCell(cells, tcNoIndex))
    if (!tcNo) {
      summary.invalid += 1
      continue
    }

    summary.parsed += 1

    const participant = participantByTcNo.value.get(tcNo)
    if (!participant) {
      summary.notFound += 1
      continue
    }

    const draft = getDraft(participant.id)
    summary.matched += 1

    const parsedAccommodation = readCell(cells, accommodationIndex)
    if (parsedAccommodation !== undefined) {
      const resolved = resolveAccommodationInput(parsedAccommodation)
      if (resolved !== undefined) {
        draft.accommodationDocTabId = resolved
      }
    }

    const assign = (key: keyof Pick<RoomDraft, 'roomNo' | 'roomType' | 'boardType' | 'personNo' | 'hotelCheckInDate' | 'hotelCheckOutDate'>, value: string | undefined) => {
      if (value === undefined) return
      draft[key] = normalize(value)
    }

    assign('roomNo', readCell(cells, roomNoIndex))
    assign('roomType', readCell(cells, roomTypeIndex))
    assign('boardType', readCell(cells, boardIndex))
    assign('personNo', readCell(cells, personNoIndex))
    assign('hotelCheckInDate', readCell(cells, checkInIndex))
    assign('hotelCheckOutDate', readCell(cells, checkOutIndex))
  }

  pasteSummary.value = summary
}

const applyChanges = async () => {
  const rowUpdates = rows.value
    .map((row) => {
      const patch = buildPatch(row.id)
      if (!patch) {
        return null
      }

      return {
        participantId: row.id,
        tcNo: row.tcNo,
        patch,
      }
    })
    .filter((item): item is NonNullable<typeof item> => item !== null)

  if (rowUpdates.length === 0) {
    pushToast({ key: 'admin.roomOps.noDraftChanges', tone: 'info' })
    return
  }

  saving.value = true
  try {
    const payload: BulkApplyParticipantRoomsRequest = {
      overwriteMode: 'always',
      rowUpdates,
    }
    const response = await bulkApplyParticipantRooms(eventId.value, payload)
    pushToast({
      key: 'admin.roomOps.applySuccess',
      params: { count: response.updatedCount },
      tone: 'success',
    })

    if (response.errors.length > 0) {
      pushToast({
        key: 'admin.roomOps.applyPartial',
        params: { count: response.errors.length },
        tone: 'info',
      })
    }

    await fetchTable()
  } catch (err) {
    pushToast({ key: 'errors.generic', tone: 'error' })
  } finally {
    saving.value = false
  }
}

const accommodationLabel = (docTabId: string | null | undefined) => {
  if (!docTabId) {
    return t('admin.roomOps.filters.accommodationUnassigned')
  }

  return accommodationLabelById.value.get(docTabId) ?? t('admin.roomOps.filters.accommodationUnknown')
}

watch(
  () => eventId.value,
  () => {
    void reload()
  }
)

onMounted(() => {
  void reload()
})
</script>

<template>
  <div class="mx-auto w-full max-w-7xl space-y-6 px-4 py-6 sm:px-6">
    <div class="flex flex-wrap items-center justify-between gap-3">
      <div>
        <RouterLink class="text-sm text-slate-600 hover:text-slate-900" :to="`/admin/events/${eventId}`">
          {{ t('admin.logs.backToEvent') }}
        </RouterLink>
        <h1 class="mt-1 text-2xl font-semibold text-slate-900">{{ t('admin.roomOps.title') }}</h1>
        <p class="mt-1 text-sm text-slate-500">
          {{ event?.name ?? t('admin.roomOps.subtitle') }}
        </p>
      </div>
      <div class="text-sm text-slate-500">
        {{ t('admin.roomOps.total', { count: total }) }}
      </div>
    </div>

    <section class="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:p-5">
      <div class="grid gap-3 lg:grid-cols-[2fr,1fr,1fr,auto]">
        <div>
          <label class="text-xs font-semibold text-slate-500">{{ t('admin.roomOps.filters.query') }}</label>
          <div class="mt-1 flex items-center gap-2 rounded border border-slate-200 bg-white px-2 py-1.5">
            <input
              v-model="searchInput"
              class="w-full text-sm text-slate-700 focus:outline-none"
              :placeholder="t('admin.roomOps.filters.queryPlaceholder')"
              type="text"
              @keydown.enter.prevent="runSearch"
            />
            <button v-if="searchInput" class="text-xs text-slate-500 hover:text-slate-700" type="button" @click="clearSearch">
              {{ t('common.clearSearch') }}
            </button>
          </div>
        </div>
        <div>
          <label class="text-xs font-semibold text-slate-500">{{ t('admin.roomOps.filters.status') }}</label>
          <AppCombobox
            v-model="filters.status"
            class="mt-1"
            :options="statusOptions"
            :placeholder="t('admin.roomOps.filters.status')"
            :aria-label="t('admin.roomOps.filters.status')"
            :searchable="false"
            compact
            @update:modelValue="onStatusChange"
          />
        </div>
        <div>
          <label class="text-xs font-semibold text-slate-500">{{ t('admin.roomOps.filters.accommodation') }}</label>
          <AppCombobox
            v-model="filters.accommodationFilter"
            class="mt-1"
            :options="accommodationFilterOptions"
            :placeholder="t('admin.roomOps.filters.accommodation')"
            :aria-label="t('admin.roomOps.filters.accommodation')"
            :searchable="false"
            compact
            @update:modelValue="onAccommodationFilterChange"
          />
        </div>
        <div class="flex items-end">
          <button
            class="w-full rounded border border-slate-300 bg-white px-3 py-2 text-sm font-semibold text-slate-700 hover:border-slate-400"
            type="button"
            @click="runSearch"
          >
            {{ t('common.search') }}
          </button>
        </div>
      </div>
    </section>

    <section class="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:p-5">
      <div class="grid gap-3 md:grid-cols-2 xl:grid-cols-[1.4fr,1fr,1fr,1fr,1fr,1fr,1fr]">
        <div>
          <label class="text-xs font-semibold text-slate-500">{{ t('admin.roomOps.bulk.accommodation') }}</label>
          <AppCombobox
            v-model="bulkForm.accommodationDocTabId"
            class="mt-1"
            :options="bulkAccommodationOptions"
            :placeholder="t('admin.roomOps.bulk.noChange')"
            :aria-label="t('admin.roomOps.bulk.accommodation')"
            :searchable="false"
            compact
          />
        </div>
        <label class="grid gap-1 text-sm">
          <span class="text-xs font-semibold text-slate-500">{{ t('admin.roomOps.columns.roomNo') }}</span>
          <input v-model.trim="bulkForm.roomNo" type="text" class="rounded border border-slate-200 px-3 py-2 text-sm" />
        </label>
        <label class="grid gap-1 text-sm">
          <span class="text-xs font-semibold text-slate-500">{{ t('admin.roomOps.columns.roomType') }}</span>
          <input v-model.trim="bulkForm.roomType" type="text" class="rounded border border-slate-200 px-3 py-2 text-sm" />
        </label>
        <label class="grid gap-1 text-sm">
          <span class="text-xs font-semibold text-slate-500">{{ t('admin.roomOps.columns.boardType') }}</span>
          <input v-model.trim="bulkForm.boardType" type="text" class="rounded border border-slate-200 px-3 py-2 text-sm" />
        </label>
        <label class="grid gap-1 text-sm">
          <span class="text-xs font-semibold text-slate-500">{{ t('admin.roomOps.columns.personNo') }}</span>
          <input v-model.trim="bulkForm.personNo" type="text" class="rounded border border-slate-200 px-3 py-2 text-sm" />
        </label>
        <label class="grid gap-1 text-sm">
          <span class="text-xs font-semibold text-slate-500">{{ t('admin.roomOps.columns.checkInDate') }}</span>
          <input
            v-model="bulkForm.hotelCheckInDate"
            type="date"
            class="rounded border border-slate-200 px-3 py-2 text-sm"
            :min="eventDateMin"
            :max="bulkForm.hotelCheckOutDate || eventDateMax"
          />
        </label>
        <label class="grid gap-1 text-sm">
          <span class="text-xs font-semibold text-slate-500">{{ t('admin.roomOps.columns.checkOutDate') }}</span>
          <input
            v-model="bulkForm.hotelCheckOutDate"
            type="date"
            class="rounded border border-slate-200 px-3 py-2 text-sm"
            :min="bulkForm.hotelCheckInDate || eventDateMin"
            :max="eventDateMax"
          />
        </label>
      </div>

      <div class="mt-3 flex flex-wrap items-end gap-3">
        <div class="min-w-[220px]">
          <label class="text-xs font-semibold text-slate-500">{{ t('admin.roomOps.bulk.overwriteMode') }}</label>
          <AppCombobox
            v-model="bulkForm.overwriteMode"
            class="mt-1"
            :options="overwriteModeOptions"
            :placeholder="t('admin.roomOps.bulk.overwriteMode')"
            :aria-label="t('admin.roomOps.bulk.overwriteMode')"
            :searchable="false"
            compact
          />
        </div>
        <button
          class="rounded border border-slate-300 bg-white px-3 py-2 text-sm font-semibold text-slate-700 hover:border-slate-400"
          type="button"
          @click="applyBulkDraft"
        >
          {{ t('admin.roomOps.bulk.applyToSelected', { count: selectedIds.length }) }}
        </button>
      </div>
    </section>

    <section class="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:p-5">
      <label class="grid gap-1 text-sm">
        <span class="text-xs font-semibold text-slate-500">{{ t('admin.roomOps.paste.label') }}</span>
        <textarea
          v-model.trim="pasteInput"
          rows="4"
          class="rounded border border-slate-200 px-3 py-2 font-mono text-xs"
          :placeholder="t('admin.roomOps.paste.placeholder')"
        ></textarea>
      </label>
      <div class="mt-3 flex flex-wrap items-center gap-3">
        <button
          class="rounded border border-slate-300 bg-white px-3 py-2 text-sm font-semibold text-slate-700 hover:border-slate-400"
          type="button"
          @click="applyPaste"
        >
          {{ t('admin.roomOps.paste.apply') }}
        </button>
        <p v-if="pasteSummary" class="text-xs text-slate-600">
          {{ t('admin.roomOps.paste.summary', pasteSummary) }}
        </p>
      </div>
    </section>

    <LoadingState v-if="loading" message-key="common.loading" />
    <ErrorState v-else-if="errorMessage" :message="errorMessage" @retry="reload" />

    <section v-else class="rounded-2xl border border-slate-200 bg-white shadow-sm">
      <div class="max-h-[65vh] overflow-auto">
        <table class="min-w-full text-sm">
          <thead class="sticky top-0 bg-slate-100 text-left text-xs uppercase tracking-wide text-slate-600">
            <tr>
              <th class="px-3 py-2">
                <input
                  type="checkbox"
                  class="h-4 w-4 rounded border-slate-300"
                  :checked="rows.length > 0 && selectedIds.length === rows.length"
                  @change="selectedIds = ($event.target as HTMLInputElement).checked ? rows.map((row) => row.id) : []"
                />
              </th>
              <th class="px-3 py-2">{{ t('admin.roomOps.columns.fullName') }}</th>
              <th class="px-3 py-2">{{ t('admin.roomOps.columns.tcNo') }}</th>
              <th class="px-3 py-2">{{ t('admin.roomOps.columns.accommodation') }}</th>
              <th class="px-3 py-2">{{ t('admin.roomOps.columns.roomNo') }}</th>
              <th class="px-3 py-2">{{ t('admin.roomOps.columns.roomType') }}</th>
              <th class="px-3 py-2">{{ t('admin.roomOps.columns.boardType') }}</th>
              <th class="px-3 py-2">{{ t('admin.roomOps.columns.personNo') }}</th>
              <th class="px-3 py-2">{{ t('admin.roomOps.columns.checkInDate') }}</th>
              <th class="px-3 py-2">{{ t('admin.roomOps.columns.checkOutDate') }}</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="row in rows" :key="row.id" class="odd:bg-white even:bg-slate-50">
              <td class="px-3 py-2 align-top">
                <input
                  type="checkbox"
                  class="h-4 w-4 rounded border-slate-300"
                  :checked="selectedIds.includes(row.id)"
                  @change="toggleRowSelection(row.id, ($event.target as HTMLInputElement).checked)"
                />
              </td>
              <td class="px-3 py-2 align-top text-xs text-slate-700">{{ row.fullName }}</td>
              <td class="px-3 py-2 align-top text-xs text-slate-700">{{ row.tcNo }}</td>
              <td class="px-3 py-2 align-top">
                <select
                  v-model="getDraft(row.id).accommodationDocTabId"
                  class="w-48 rounded border border-slate-200 bg-white px-2 py-1.5 text-xs text-slate-700"
                >
                  <option :value="null">{{ t('admin.roomOps.filters.accommodationUnassigned') }}</option>
                  <option v-for="tab in accommodationTabs" :key="tab.id" :value="tab.id">
                    {{ tab.title }}
                  </option>
                </select>
                <div class="mt-1 text-[11px] text-slate-500">
                  {{ accommodationLabel(getDraft(row.id).accommodationDocTabId) }}
                </div>
              </td>
              <td class="px-3 py-2 align-top">
                <input v-model.trim="getDraft(row.id).roomNo" type="text" class="w-28 rounded border border-slate-200 bg-white px-2 py-1.5 text-xs text-slate-700" />
              </td>
              <td class="px-3 py-2 align-top">
                <input v-model.trim="getDraft(row.id).roomType" type="text" class="w-28 rounded border border-slate-200 bg-white px-2 py-1.5 text-xs text-slate-700" />
              </td>
              <td class="px-3 py-2 align-top">
                <input v-model.trim="getDraft(row.id).boardType" type="text" class="w-28 rounded border border-slate-200 bg-white px-2 py-1.5 text-xs text-slate-700" />
              </td>
              <td class="px-3 py-2 align-top">
                <input v-model.trim="getDraft(row.id).personNo" type="text" class="w-24 rounded border border-slate-200 bg-white px-2 py-1.5 text-xs text-slate-700" />
              </td>
              <td class="px-3 py-2 align-top">
                <input
                  v-model="getDraft(row.id).hotelCheckInDate"
                  type="date"
                  class="w-36 rounded border border-slate-200 bg-white px-2 py-1.5 text-xs text-slate-700"
                  :min="eventDateMin"
                  :max="getDraft(row.id).hotelCheckOutDate || eventDateMax"
                />
              </td>
              <td class="px-3 py-2 align-top">
                <input
                  v-model="getDraft(row.id).hotelCheckOutDate"
                  type="date"
                  class="w-36 rounded border border-slate-200 bg-white px-2 py-1.5 text-xs text-slate-700"
                  :min="getDraft(row.id).hotelCheckInDate || eventDateMin"
                  :max="eventDateMax"
                />
              </td>
            </tr>
            <tr v-if="rows.length === 0">
              <td class="px-3 py-6 text-center text-sm text-slate-500" colspan="10">
                {{ t('admin.roomOps.empty') }}
              </td>
            </tr>
          </tbody>
        </table>
      </div>
      <div class="flex flex-wrap items-center justify-between gap-3 border-t border-slate-200 px-4 py-3">
        <p class="text-xs text-slate-600">
          {{ t('admin.roomOps.changes', { changed: changedRowCount }) }}
        </p>
        <button
          class="rounded bg-slate-900 px-4 py-2 text-sm font-semibold text-white hover:bg-slate-800 disabled:cursor-not-allowed disabled:opacity-60"
          :disabled="!canApply"
          type="button"
          @click="applyChanges"
        >
          {{ saving ? t('common.saving') : t('admin.roomOps.applyButton') }}
        </button>
      </div>
    </section>
  </div>
</template>
