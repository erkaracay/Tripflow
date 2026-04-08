<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { RouterLink, useRoute } from 'vue-router'
import { useI18n } from 'vue-i18n'
import {
  apiGet,
  bulkApplyParticipantRooms,
  getParticipantStays,
  createParticipantStay,
  updateParticipantStay,
  deleteParticipantStay,
} from '../../lib/api'
import { useToast } from '../../lib/toast'
import LoadingState from '../../components/ui/LoadingState.vue'
import ErrorState from '../../components/ui/ErrorState.vue'
import AppCombobox from '../../components/ui/AppCombobox.vue'
import AppDrawerShell from '../../components/ui/AppDrawerShell.vue'
import ConfirmDialog from '../../components/ui/ConfirmDialog.vue'
import type {
  AppComboboxOption,
  BulkApplyParticipantRoomsRequest,
  Event as EventDto,
  EventDocTabDto,
  ParticipantAccommodationStay,
  UpsertParticipantAccommodationStayRequest,
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
    void loadStaysBadges(response.items.map((r) => r.id))
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


// --- Stays drawer ---

const stayCountById = ref<Record<string, number | undefined>>({})
const drawerOpen = ref(false)
const drawerParticipantId = ref<string | null>(null)
const drawerStays = ref<ParticipantAccommodationStay[]>([])
const drawerLoading = ref(false)
const drawerSaving = ref(false)
const drawerDirtyId = ref<string | null>(null) // stay being edited

type StayForm = {
  eventAccommodationId: string
  roomNo: string
  roomType: string
  boardType: string
  personNo: string
  checkIn: string
  checkOut: string
}

const emptyStayForm = (): StayForm => ({
  eventAccommodationId: '',
  roomNo: '',
  roomType: '',
  boardType: '',
  personNo: '',
  checkIn: '',
  checkOut: '',
})

const addForm = ref<StayForm>(emptyStayForm())
const editFormById = ref<Record<string, StayForm>>({})
const stayFormErrors = ref<Record<string, string>>({})

const stayAccommodationOptions = computed<AppComboboxOption[]>(() =>
  accommodationTabs.value.map((tab) => ({ value: tab.id, label: tab.title }))
)

// Prevent stays drawer from closing on overlay tap-through when a
// combobox inside the drawer is open or has just closed (mobile sheet tap-through guard).
const drawerComboboxOpen = ref(false)
let drawerComboboxCloseTimer: ReturnType<typeof setTimeout> | null = null

const onDrawerComboboxOpen = () => {
  if (drawerComboboxCloseTimer !== null) {
    clearTimeout(drawerComboboxCloseTimer)
    drawerComboboxCloseTimer = null
  }
  drawerComboboxOpen.value = true
}

const onDrawerComboboxClose = () => {
  drawerComboboxCloseTimer = setTimeout(() => {
    drawerComboboxOpen.value = false
    drawerComboboxCloseTimer = null
  }, 320)
}

const syncDraftFromStays = (pid: string) => {
  const first = drawerStays.value[0]
  if (!draftById.value[pid]) return
  if (first) {
    draftById.value[pid] = {
      ...draftById.value[pid],
      accommodationDocTabId: first.eventAccommodationId,
      roomNo: first.roomNo ?? '',
      roomType: first.roomType ?? '',
      boardType: first.boardType ?? '',
      personNo: first.personNo ?? '',
      hotelCheckInDate: first.checkIn ?? '',
      hotelCheckOutDate: first.checkOut ?? '',
    }
  } else {
    draftById.value[pid] = {
      ...draftById.value[pid],
      accommodationDocTabId: null,
      roomNo: '',
      roomType: '',
      boardType: '',
      personNo: '',
      hotelCheckInDate: '',
      hotelCheckOutDate: '',
    }
  }
  originalById.value[pid] = { ...draftById.value[pid] }
}

const validateStayForm = (form: StayForm, formKey: string): boolean => {
  stayFormErrors.value = { ...stayFormErrors.value }
  if (!form.eventAccommodationId) {
    stayFormErrors.value[`${formKey}.acc`] = t('admin.stays.accRequired')
    return false
  }
  if (form.checkIn && form.checkOut && form.checkOut < form.checkIn) {
    stayFormErrors.value[`${formKey}.date`] = t('admin.stays.checkOutBeforeCheckIn')
    return false
  }
  const min = event.value?.startDate
  const max = event.value?.endDate
  if (min && max) {
    if (form.checkIn && (form.checkIn < min || form.checkIn > max)) {
      stayFormErrors.value[`${formKey}.date`] = t('admin.stays.checkInOutOfRange')
      return false
    }
    if (form.checkOut && (form.checkOut < min || form.checkOut > max)) {
      stayFormErrors.value[`${formKey}.date`] = t('admin.stays.checkOutOutOfRange')
      return false
    }
  }
  delete stayFormErrors.value[`${formKey}.acc`]
  delete stayFormErrors.value[`${formKey}.date`]
  return true
}

const loadStaysBadges = async (participantIds: string[]) => {
  if (!eventId.value) return
  await Promise.allSettled(
    participantIds.map(async (pid) => {
      try {
        const stays = await getParticipantStays(eventId.value, pid)
        stayCountById.value = { ...stayCountById.value, [pid]: stays.length }
      } catch {
        // ignore
      }
    })
  )
}

const openStaysDrawer = async (participantId: string) => {
  drawerParticipantId.value = participantId
  drawerOpen.value = true
  drawerLoading.value = true
  addForm.value = emptyStayForm()
  editFormById.value = {}
  stayFormErrors.value = {}
  try {
    drawerStays.value = await getParticipantStays(eventId.value, participantId)
  } catch {
    pushToast({ key: 'errors.generic', tone: 'error' })
  } finally {
    drawerLoading.value = false
  }
}

const closeStaysDrawer = async () => {
  drawerOpen.value = false
  if (drawerParticipantId.value) {
    stayCountById.value = { ...stayCountById.value, [drawerParticipantId.value]: drawerStays.value.length }
  }
  drawerParticipantId.value = null
  drawerStays.value = []
  addForm.value = emptyStayForm()
  editFormById.value = {}
  drawerDirtyId.value = null
}

const copyPreviousStay = () => {
  const last = drawerStays.value[drawerStays.value.length - 1]
  if (!last) return
  addForm.value = {
    eventAccommodationId: last.eventAccommodationId,
    roomNo: last.roomNo ?? '',
    roomType: last.roomType ?? '',
    boardType: last.boardType ?? '',
    personNo: last.personNo ?? '',
    checkIn: last.checkIn ?? '',
    checkOut: last.checkOut ?? '',
  }
}

const submitAddStay = async () => {
  if (!drawerParticipantId.value) return
  if (!validateStayForm(addForm.value, 'add')) return
  drawerSaving.value = true
  try {
    const req: UpsertParticipantAccommodationStayRequest = {
      eventAccommodationId: addForm.value.eventAccommodationId || null,
      roomNo: addForm.value.roomNo || null,
      roomType: addForm.value.roomType || null,
      boardType: addForm.value.boardType || null,
      personNo: addForm.value.personNo || null,
      checkIn: addForm.value.checkIn || null,
      checkOut: addForm.value.checkOut || null,
    }
    await createParticipantStay(eventId.value, drawerParticipantId.value, req)
    drawerStays.value = await getParticipantStays(eventId.value, drawerParticipantId.value)
    addForm.value = emptyStayForm()
    const pid = drawerParticipantId.value
    if (pid) syncDraftFromStays(pid)
  } catch (err: unknown) {
    const msg = err instanceof Error ? err.message : ''
    if (msg.includes('overlap')) pushToast({ key: 'admin.stays.overlapError', tone: 'error' })
    else if (msg.includes('checkout_before_checkin')) pushToast({ key: 'admin.stays.checkOutBeforeCheckIn', tone: 'error' })
    else pushToast({ key: 'errors.generic', tone: 'error' })
  } finally {
    drawerSaving.value = false
  }
}

const startEditStay = (stay: ParticipantAccommodationStay) => {
  drawerDirtyId.value = stay.id
  editFormById.value = {
    ...editFormById.value,
    [stay.id]: {
      eventAccommodationId: stay.eventAccommodationId,
      roomNo: stay.roomNo ?? '',
      roomType: stay.roomType ?? '',
      boardType: stay.boardType ?? '',
      personNo: stay.personNo ?? '',
      checkIn: stay.checkIn ?? '',
      checkOut: stay.checkOut ?? '',
    },
  }
}

const cancelEditStay = (stayId: string) => {
  drawerDirtyId.value = null
  const forms = { ...editFormById.value }
  delete forms[stayId]
  editFormById.value = forms
}

const submitEditStay = async (stayId: string) => {
  if (!drawerParticipantId.value) return
  const form = editFormById.value[stayId]
  if (!form) return
  if (!validateStayForm(form, `edit-${stayId}`)) return
  drawerSaving.value = true
  try {
    const req: UpsertParticipantAccommodationStayRequest = {
      eventAccommodationId: form.eventAccommodationId || null,
      roomNo: form.roomNo || null,
      roomType: form.roomType || null,
      boardType: form.boardType || null,
      personNo: form.personNo || null,
      checkIn: form.checkIn || null,
      checkOut: form.checkOut || null,
    }
    await updateParticipantStay(eventId.value, drawerParticipantId.value, stayId, req)
    drawerStays.value = await getParticipantStays(eventId.value, drawerParticipantId.value)
    cancelEditStay(stayId)
    const pid = drawerParticipantId.value
    if (pid) syncDraftFromStays(pid)
  } catch (err: unknown) {
    const msg = err instanceof Error ? err.message : ''
    if (msg.includes('overlap')) pushToast({ key: 'admin.stays.overlapError', tone: 'error' })
    else if (msg.includes('checkout_before_checkin')) pushToast({ key: 'admin.stays.checkOutBeforeCheckIn', tone: 'error' })
    else pushToast({ key: 'errors.generic', tone: 'error' })
  } finally {
    drawerSaving.value = false
  }
}

const deleteConfirmOpen = ref(false)
const deleteConfirmStayId = ref<string | null>(null)

const openDeleteConfirm = (stayId: string) => {
  deleteConfirmStayId.value = stayId
  deleteConfirmOpen.value = true
}

const confirmDeleteStay = async () => {
  const stayId = deleteConfirmStayId.value
  if (!stayId || !drawerParticipantId.value) return
  deleteConfirmOpen.value = false
  drawerSaving.value = true
  try {
    await deleteParticipantStay(eventId.value, drawerParticipantId.value, stayId)
    drawerStays.value = await getParticipantStays(eventId.value, drawerParticipantId.value)
    const pid = drawerParticipantId.value
    if (pid) syncDraftFromStays(pid)
  } catch {
    pushToast({ key: 'errors.generic', tone: 'error' })
  } finally {
    drawerSaving.value = false
    deleteConfirmStayId.value = null
  }
}

const formatDate = (d: string | null | undefined) => {
  if (!d) return ''
  const parts = d.split('-')
  if (parts.length !== 3) return d
  return `${parts[2]}.${parts[1]}.${parts[0]}`
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
      <div class="grid gap-3 sm:grid-cols-2 lg:grid-cols-[2fr,1fr,1fr,auto]">
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
      <div class="grid gap-3 sm:grid-cols-2 xl:grid-cols-[1.4fr,1fr,1fr,1fr,1fr,1fr,1fr]">
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
          <thead class="sticky top-0 bg-slate-100 text-left text-xs uppercase tracking-wide text-slate-600 whitespace-nowrap">
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
                <div class="mt-1 flex items-center gap-2 text-[11px] text-slate-500">
                  <button
                    type="button"
                    class="rounded bg-slate-100 px-1.5 py-0.5 text-[10px] font-medium text-slate-600 hover:bg-slate-200"
                    @click.stop="openStaysDrawer(row.id)"
                  >
                    {{ t('admin.stays.badge', { count: stayCountById[row.id] ?? '…' }) }}
                  </button>
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

  <!-- Stays Drawer -->
  <AppDrawerShell
    :open="drawerOpen"
    :close-on-overlay="!drawerComboboxOpen"
    desktop-width="lg"
    desktop-breakpoint="md"
    swipe-to-close
    labelled-by="stays-drawer-title"
    @close="closeStaysDrawer"
  >
    <template #default="{ panelClass }">
      <section :class="[panelClass, 'flex flex-col overflow-hidden bg-white']" role="dialog" aria-modal="true">
        <!-- Swipe handle (mobile) -->
        <div class="flex justify-center pt-3 pb-1 md:hidden" data-drawer-swipe-handle>
          <div class="h-1 w-10 rounded-full bg-slate-300" />
        </div>

        <!-- Header -->
        <div class="flex items-center justify-between gap-3 border-b border-slate-200 px-5 py-3">
          <h2 id="stays-drawer-title" class="text-base font-semibold text-slate-900">{{ t('admin.stays.title') }}</h2>
          <button
            type="button"
            class="-mr-2 flex h-9 w-9 items-center justify-center rounded-lg text-slate-400 transition-colors hover:bg-slate-100 hover:text-slate-600"
            :aria-label="t('common.close')"
            @click="closeStaysDrawer"
          >
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" class="h-5 w-5"><path d="M6.28 5.22a.75.75 0 0 0-1.06 1.06L8.94 10l-3.72 3.72a.75.75 0 1 0 1.06 1.06L10 11.06l3.72 3.72a.75.75 0 1 0 1.06-1.06L11.06 10l3.72-3.72a.75.75 0 0 0-1.06-1.06L10 8.94 6.28 5.22Z" /></svg>
          </button>
        </div>

        <!-- Content -->
        <div class="flex-1 overflow-y-auto overscroll-contain px-5 py-5">
          <!-- Loading -->
          <div v-if="drawerLoading" class="flex items-center justify-center py-16">
            <div class="h-6 w-6 animate-spin rounded-full border-2 border-slate-200 border-t-slate-600" />
          </div>

          <template v-else>
            <!-- Empty state -->
            <div v-if="drawerStays.length === 0" class="py-12 text-center">
              <div class="mx-auto mb-3 flex h-12 w-12 items-center justify-center rounded-full bg-slate-100">
                <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" class="h-5 w-5 text-slate-400"><path d="M10.75 6.5a.75.75 0 0 0-1.5 0v2.75H6.5a.75.75 0 0 0 0 1.5h2.75v2.75a.75.75 0 0 0 1.5 0v-2.75h2.75a.75.75 0 0 0 0-1.5h-2.75V6.5Z" /><path fill-rule="evenodd" d="M10 18a8 8 0 1 0 0-16 8 8 0 0 0 0 16Zm0-1.5a6.5 6.5 0 1 0 0-13 6.5 6.5 0 0 0 0 13Z" clip-rule="evenodd" /></svg>
              </div>
              <p class="text-sm text-slate-500">{{ t('admin.stays.empty') }}</p>
            </div>

            <!-- Stay cards -->
            <div v-else class="space-y-3">
              <div
                v-for="stay in drawerStays"
                :key="stay.id"
                :class="[
                  'rounded-xl border p-4 transition-colors',
                  stay.isCurrent ? 'border-blue-200 bg-blue-50/60' : 'border-slate-200 bg-white',
                ]"
              >
                <!-- View mode -->
                <template v-if="drawerDirtyId !== stay.id">
                  <div class="flex items-start justify-between gap-3">
                    <div class="min-w-0 flex-1 space-y-1.5">
                      <div class="flex flex-wrap items-center gap-2">
                        <span class="text-sm font-semibold text-slate-900">{{ stay.accommodationTitle }}</span>
                        <span
                          v-if="stay.isCurrent"
                          class="inline-flex items-center rounded-full bg-blue-100 px-2 py-0.5 text-[11px] font-medium text-blue-700"
                        >{{ t('portal.docs.currentStay') }}</span>
                      </div>
                      <div v-if="stay.checkIn || stay.checkOut" class="text-sm text-slate-600">
                        {{ formatDate(stay.checkIn) }} – {{ formatDate(stay.checkOut) }}
                        <span v-if="stay.nightCount" class="ml-1 text-slate-400">({{ t('admin.stays.nightsShort', { count: stay.nightCount }) }})</span>
                      </div>
                      <div class="flex flex-wrap gap-x-4 gap-y-1 text-sm text-slate-600">
                        <span v-if="stay.roomNo">{{ t('admin.roomOps.columns.roomNo') }}: {{ stay.roomNo }}</span>
                        <span v-if="stay.roomType">{{ t('admin.roomOps.columns.roomType') }}: {{ stay.roomType }}</span>
                        <span v-if="stay.boardType">{{ t('admin.roomOps.columns.boardType') }}: {{ stay.boardType }}</span>
                        <span v-if="stay.personNo">{{ t('admin.roomOps.columns.personNo') }}: {{ stay.personNo }}</span>
                      </div>
                      <div v-if="stay.roommates && stay.roommates.length > 0" class="text-sm text-slate-500">
                        {{ t('portal.docs.roommates') }}: {{ stay.roommates.join(', ') }}
                      </div>
                    </div>
                    <div class="flex shrink-0 gap-1">
                      <button
                        type="button"
                        class="flex h-8 w-8 items-center justify-center rounded-lg text-slate-400 transition-colors hover:bg-slate-100 hover:text-slate-700"
                        :aria-label="t('common.edit')"
                        @click="startEditStay(stay)"
                      >
                        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" class="h-4 w-4"><path d="m5.433 13.917 1.262-3.155A4 4 0 0 1 7.58 9.42l6.92-6.918a2.121 2.121 0 0 1 3 3l-6.92 6.918c-.383.383-.84.685-1.343.886l-3.154 1.262a.5.5 0 0 1-.65-.65Z" /><path d="M3.5 5.75c0-.69.56-1.25 1.25-1.25H10A.75.75 0 0 0 10 3H4.75A2.75 2.75 0 0 0 2 5.75v9.5A2.75 2.75 0 0 0 4.75 18h9.5A2.75 2.75 0 0 0 17 15.25V10a.75.75 0 0 0-1.5 0v5.25c0 .69-.56 1.25-1.25 1.25h-9.5c-.69 0-1.25-.56-1.25-1.25v-9.5Z" /></svg>
                      </button>
                      <button
                        type="button"
                        class="flex h-8 w-8 items-center justify-center rounded-lg text-slate-400 transition-colors hover:bg-red-50 hover:text-red-500"
                        :disabled="drawerSaving"
                        :aria-label="t('common.delete')"
                        @click="openDeleteConfirm(stay.id)"
                      >
                        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" class="h-4 w-4"><path fill-rule="evenodd" d="M8.75 1A2.75 2.75 0 0 0 6 3.75v.443c-.795.077-1.584.176-2.365.298a.75.75 0 1 0 .23 1.482l.149-.022.841 10.518A2.75 2.75 0 0 0 7.596 19h4.807a2.75 2.75 0 0 0 2.742-2.53l.841-10.52.149.023a.75.75 0 0 0 .23-1.482A41.03 41.03 0 0 0 14 4.193V3.75A2.75 2.75 0 0 0 11.25 1h-2.5ZM10 4c.84 0 1.673.025 2.5.075V3.75c0-.69-.56-1.25-1.25-1.25h-2.5c-.69 0-1.25.56-1.25 1.25v.325C8.327 4.025 9.16 4 10 4ZM8.58 7.72a.75.75 0 0 0-1.5.06l.3 7.5a.75.75 0 1 0 1.5-.06l-.3-7.5Zm4.34.06a.75.75 0 1 0-1.5-.06l-.3 7.5a.75.75 0 1 0 1.5.06l.3-7.5Z" clip-rule="evenodd" /></svg>
                      </button>
                    </div>
                  </div>
                </template>

                <!-- Edit mode -->
                <template v-else-if="editFormById[stay.id]">
                  <div class="space-y-3">
                    <div>
                      <label class="mb-1 block text-xs font-medium text-slate-600">{{ t('admin.roomOps.columns.accommodation') }}</label>
                      <AppCombobox
                        v-model="editFormById[stay.id]!.eventAccommodationId"
                        :options="stayAccommodationOptions"
                        :invalid="!!stayFormErrors[`edit-${stay.id}.acc`]"
                        @open="onDrawerComboboxOpen"
                        @close="onDrawerComboboxClose"
                      />
                      <p v-if="stayFormErrors[`edit-${stay.id}.acc`]" class="mt-1 text-xs text-red-500">{{ stayFormErrors[`edit-${stay.id}.acc`] }}</p>
                    </div>
                    <div class="grid grid-cols-1 gap-3 sm:grid-cols-2">
                      <label class="block">
                        <span class="mb-1 block text-xs font-medium text-slate-600">{{ t('admin.roomOps.columns.roomNo') }}</span>
                        <input v-model.trim="editFormById[stay.id]!.roomNo" type="text" class="w-full rounded-lg border border-slate-200 px-3 py-2 text-sm focus:border-slate-400 focus:outline-none" />
                      </label>
                      <label class="block">
                        <span class="mb-1 block text-xs font-medium text-slate-600">{{ t('admin.roomOps.columns.roomType') }}</span>
                        <input v-model.trim="editFormById[stay.id]!.roomType" type="text" class="w-full rounded-lg border border-slate-200 px-3 py-2 text-sm focus:border-slate-400 focus:outline-none" />
                      </label>
                      <label class="block">
                        <span class="mb-1 block text-xs font-medium text-slate-600">{{ t('admin.roomOps.columns.boardType') }}</span>
                        <input v-model.trim="editFormById[stay.id]!.boardType" type="text" class="w-full rounded-lg border border-slate-200 px-3 py-2 text-sm focus:border-slate-400 focus:outline-none" />
                      </label>
                      <label class="block">
                        <span class="mb-1 block text-xs font-medium text-slate-600">{{ t('admin.roomOps.columns.personNo') }}</span>
                        <input v-model.trim="editFormById[stay.id]!.personNo" type="text" class="w-full rounded-lg border border-slate-200 px-3 py-2 text-sm focus:border-slate-400 focus:outline-none" />
                      </label>
                      <label class="block">
                        <span class="mb-1 block text-xs font-medium text-slate-600">{{ t('admin.roomOps.columns.checkInDate') }}</span>
                        <input v-model="editFormById[stay.id]!.checkIn" type="date" class="w-full rounded-lg border border-slate-200 px-3 py-2 text-sm focus:border-slate-400 focus:outline-none" :min="eventDateMin" :max="eventDateMax" />
                      </label>
                      <label class="block">
                        <span class="mb-1 block text-xs font-medium text-slate-600">{{ t('admin.roomOps.columns.checkOutDate') }}</span>
                        <input v-model="editFormById[stay.id]!.checkOut" type="date" class="w-full rounded-lg border border-slate-200 px-3 py-2 text-sm focus:border-slate-400 focus:outline-none" :min="eventDateMin" :max="eventDateMax" />
                      </label>
                    </div>
                    <p v-if="stayFormErrors[`edit-${stay.id}.date`]" class="text-xs text-red-500">{{ stayFormErrors[`edit-${stay.id}.date`] }}</p>
                    <div class="flex gap-2 pt-1">
                      <button
                        type="button"
                        class="rounded-lg bg-slate-900 px-4 py-2 text-sm font-semibold text-white hover:bg-slate-800 disabled:opacity-60"
                        :disabled="drawerSaving"
                        @click="submitEditStay(stay.id)"
                      >
                        {{ t('common.save') }}
                      </button>
                      <button
                        type="button"
                        class="rounded-lg border border-slate-200 px-4 py-2 text-sm text-slate-600 hover:bg-slate-50"
                        @click="cancelEditStay(stay.id)"
                      >
                        {{ t('common.cancel') }}
                      </button>
                    </div>
                  </div>
                </template>
              </div>
            </div>

            <!-- Add stay form -->
            <div class="mt-5 rounded-xl border border-dashed border-slate-300 bg-slate-50/80 p-4">
              <div class="mb-3 flex items-center justify-between gap-3">
                <span class="text-sm font-semibold text-slate-800">{{ t('admin.stays.add') }}</span>
                <button
                  v-if="drawerStays.length > 0"
                  type="button"
                  class="rounded-md px-2 py-1 text-xs font-medium text-blue-600 transition-colors hover:bg-blue-50"
                  @click="copyPreviousStay"
                >
                  {{ t('admin.stays.copyPrevious') }}
                </button>
              </div>
              <div class="space-y-3">
                <div>
                  <label class="mb-1 block text-xs font-medium text-slate-600">{{ t('admin.roomOps.columns.accommodation') }}</label>
                  <AppCombobox
                    v-model="addForm.eventAccommodationId"
                    :options="stayAccommodationOptions"
                    :invalid="!!stayFormErrors['add.acc']"
                    @open="onDrawerComboboxOpen"
                    @close="onDrawerComboboxClose"
                  />
                  <p v-if="stayFormErrors['add.acc']" class="mt-1 text-xs text-red-500">{{ stayFormErrors['add.acc'] }}</p>
                </div>
                <div class="grid grid-cols-1 gap-3 sm:grid-cols-2">
                  <label class="block">
                    <span class="mb-1 block text-xs font-medium text-slate-600">{{ t('admin.roomOps.columns.roomNo') }}</span>
                    <input v-model.trim="addForm.roomNo" type="text" class="w-full rounded-lg border border-slate-200 px-3 py-2 text-sm focus:border-slate-400 focus:outline-none" />
                  </label>
                  <label class="block">
                    <span class="mb-1 block text-xs font-medium text-slate-600">{{ t('admin.roomOps.columns.roomType') }}</span>
                    <input v-model.trim="addForm.roomType" type="text" class="w-full rounded-lg border border-slate-200 px-3 py-2 text-sm focus:border-slate-400 focus:outline-none" />
                  </label>
                  <label class="block">
                    <span class="mb-1 block text-xs font-medium text-slate-600">{{ t('admin.roomOps.columns.boardType') }}</span>
                    <input v-model.trim="addForm.boardType" type="text" class="w-full rounded-lg border border-slate-200 px-3 py-2 text-sm focus:border-slate-400 focus:outline-none" />
                  </label>
                  <label class="block">
                    <span class="mb-1 block text-xs font-medium text-slate-600">{{ t('admin.roomOps.columns.personNo') }}</span>
                    <input v-model.trim="addForm.personNo" type="text" class="w-full rounded-lg border border-slate-200 px-3 py-2 text-sm focus:border-slate-400 focus:outline-none" />
                  </label>
                  <label class="block">
                    <span class="mb-1 block text-xs font-medium text-slate-600">{{ t('admin.roomOps.columns.checkInDate') }}</span>
                    <input v-model="addForm.checkIn" type="date" class="w-full rounded-lg border border-slate-200 px-3 py-2 text-sm focus:border-slate-400 focus:outline-none" :min="eventDateMin" :max="eventDateMax" />
                  </label>
                  <label class="block">
                    <span class="mb-1 block text-xs font-medium text-slate-600">{{ t('admin.roomOps.columns.checkOutDate') }}</span>
                    <input v-model="addForm.checkOut" type="date" class="w-full rounded-lg border border-slate-200 px-3 py-2 text-sm focus:border-slate-400 focus:outline-none" :min="eventDateMin" :max="eventDateMax" />
                  </label>
                </div>
                <p v-if="stayFormErrors['add.date']" class="text-xs text-red-500">{{ stayFormErrors['add.date'] }}</p>
                <button
                  type="button"
                  class="w-full rounded-lg bg-slate-900 px-4 py-2.5 text-sm font-semibold text-white hover:bg-slate-800 disabled:opacity-60 sm:w-auto"
                  :disabled="drawerSaving"
                  @click="submitAddStay"
                >
                  {{ t('admin.stays.add') }}
                </button>
              </div>
            </div>
          </template>
        </div>
      </section>
    </template>
  </AppDrawerShell>

  <!-- Delete stay confirmation -->
  <ConfirmDialog
    :open="deleteConfirmOpen"
    :title="t('admin.stays.deleteConfirmTitle')"
    :message="t('admin.stays.deleteConfirmMessage')"
    tone="danger"
    :confirm-disabled="drawerSaving"
    @update:open="deleteConfirmOpen = $event"
    @confirm="confirmDeleteStay"
  />
</template>
