<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import AppModalShell from '../ui/AppModalShell.vue'
import AppSegmentedControl from '../ui/AppSegmentedControl.vue'
import ConfirmDialog from '../ui/ConfirmDialog.vue'
import { apiGet, bulkApplyFlightSegments } from '../../lib/api'
import { useToast } from '../../lib/toast'
import {
  buildBulkFlightTemplateSheetRows,
  buildParticipantsSheetRows,
  FLIGHT_SEGMENTS_SHEET_HEADERS,
  PARTICIPANTS_SHEET_HEADERS,
} from '../../lib/participantsExportWorkbook'
import type {
  BulkApplyFlightSegmentsRequest,
  FlightPanelHelperDirection,
  FlightSegment,
  ParticipantTableItem,
  ParticipantTableResponse,
} from '../../types'

type SelectionScope = 'manual' | 'filtered' | 'allEvent'
type ParticipantStatusFilter = 'all' | 'arrived' | 'not_arrived'
type FlightPresenceFilter = 'all' | 'no_flights' | 'no_arrival' | 'no_return'
type SegmentTextField =
  | 'airline'
  | 'departureAirport'
  | 'arrivalAirport'
  | 'flightCode'
  | 'departureDate'
  | 'departureTime'
  | 'arrivalDate'
  | 'arrivalTime'
  | 'ticketNo'
  | 'pnr'
  | 'cabinBaggage'
type ConfirmAction =
  | { kind: 'allEvent' }
  | { kind: 'apply'; directions: FlightPanelHelperDirection[] }

const props = withDefaults(
  defineProps<{
    eventId: string
    initialQuery?: string
    initialStatus?: 'all' | 'arrived' | 'not_arrived'
    buttonLabel?: string
    buttonClass?: string
  }>(),
  {
    initialQuery: '',
    initialStatus: 'all',
    buttonLabel: '',
    buttonClass:
      'rounded border border-slate-200 bg-white px-3 py-2 text-sm text-slate-700 hover:border-slate-300',
  }
)

const emit = defineEmits<{
  (event: 'applied'): void
}>()

const { t } = useI18n()
const { pushToast } = useToast()

const PAGE_SIZE = 50
const RESOLVE_PAGE_SIZE = 200
const draftStorageKey = computed(() => `tripflow:event:${props.eventId}:flight-panel-helper-draft`)

const modalOpen = ref(false)
const loadingParticipants = ref(false)
const resolvingSelection = ref(false)
const applying = ref(false)
const exporting = ref(false)
const participantsError = ref<string | null>(null)
const applyError = ref<string | null>(null)

const page = ref(1)
const total = ref(0)
const searchInput = ref(props.initialQuery)
const searchQuery = ref(props.initialQuery.trim())
const statusFilter = ref<ParticipantStatusFilter>(props.initialStatus)
const flightFilter = ref<FlightPresenceFilter>('all')
const debounceHandle = ref<number | null>(null)
const rows = ref<ParticipantTableItem[]>([])
const selectedParticipantMap = ref<Record<string, ParticipantTableItem>>({})
const selectedIds = ref<string[]>([])
const selectionScope = ref<SelectionScope>('manual')
const showPasteHelper = ref<Record<FlightPanelHelperDirection, boolean>>({ Arrival: false, Return: false })
const pasteText = ref<Record<FlightPanelHelperDirection, string>>({ Arrival: '', Return: '' })
const activeDirection = ref<FlightPanelHelperDirection>('Arrival')
const arrivalSegments = ref<FlightSegment[]>([])
const returnSegments = ref<FlightSegment[]>([])
const confirmAction = ref<ConfirmAction | null>(null)
const confirmOpen = ref(false)

const totalPages = computed(() => Math.max(1, Math.ceil(total.value / PAGE_SIZE)))
const selectedCount = computed(() => selectedIds.value.length)
const selectedParticipants = computed(() =>
  selectedIds.value
    .map((id) => selectedParticipantMap.value[id])
    .filter((participant): participant is ParticipantTableItem => Boolean(participant))
)
const currentSegments = computed(() =>
  activeDirection.value === 'Arrival' ? arrivalSegments.value : returnSegments.value
)
const arrivalTemplate = computed(() => normalizeTemplateRows(arrivalSegments.value))
const returnTemplate = computed(() => normalizeTemplateRows(returnSegments.value))
const arrivalRowsCreated = computed(() => selectedCount.value * arrivalTemplate.value.length)
const returnRowsCreated = computed(() => selectedCount.value * returnTemplate.value.length)
const totalRowsCreated = computed(() => arrivalRowsCreated.value + returnRowsCreated.value)
const sampleRows = computed(() => {
  const first = selectedParticipants.value[0]
  if (!first) return []
  return buildBulkFlightTemplateSheetRows([first], arrivalTemplate.value, returnTemplate.value).slice(0, 6)
})
const getRowsCreatedForDirections = (directions: FlightPanelHelperDirection[]) =>
  directions.reduce((totalRows, direction) => {
    if (direction === 'Arrival') {
      return totalRows + arrivalRowsCreated.value
    }
    return totalRows + returnRowsCreated.value
  }, 0)
const hasBusyState = computed(
  () => loadingParticipants.value || resolvingSelection.value || applying.value || exporting.value
)
const buttonText = computed(() => props.buttonLabel || t('admin.flightPanelHelper.open'))
const localDirectionError = computed(() => getDirectionTemplateError(activeDirection.value))
const directions: FlightPanelHelperDirection[] = ['Arrival', 'Return']
const activeDirectionBadge = (direction: FlightPanelHelperDirection) =>
  direction === 'Arrival' ? arrivalTemplate.value.length : returnTemplate.value.length
const statusOptions = computed(() => [
  { value: 'all', label: t('admin.participantsTable.statusAll') },
  { value: 'arrived', label: t('admin.participantsTable.statusArrived') },
  { value: 'not_arrived', label: t('admin.participantsTable.statusNotArrived') },
])
const flightFilterOptions = computed(() => [
  { value: 'all', label: t('common.all') },
  { value: 'no_flights', label: t('admin.flightPanelHelper.flightFilters.noFlights') },
  { value: 'no_arrival', label: t('admin.flightPanelHelper.flightFilters.noArrival') },
  { value: 'no_return', label: t('admin.flightPanelHelper.flightFilters.noReturn') },
])

const participantDisplayName = (participant: Pick<ParticipantTableItem, 'firstName' | 'lastName' | 'fullName'>) => {
  const first = participant.firstName?.trim() ?? ''
  const last = participant.lastName?.trim() ?? ''
  const combined = `${first} ${last}`.trim()
  return combined || participant.fullName
}

const createEmptySegment = (segmentIndex: number): FlightSegment => ({
  segmentIndex,
  airline: '',
  departureAirport: '',
  arrivalAirport: '',
  flightCode: '',
  departureDate: '',
  departureTime: '',
  arrivalDate: '',
  arrivalTime: '',
  pnr: '',
  ticketNo: '',
  baggagePieces: null,
  baggageTotalKg: null,
  cabinBaggage: '',
})

const cloneSegments = (segments: FlightSegment[]) =>
  segments.map((segment, index) => ({
    ...createEmptySegment(index + 1),
    ...segment,
    segmentIndex: index + 1,
  }))

const segmentHasValue = (segment?: FlightSegment | null) => {
  if (!segment) return false
  return Boolean(
    segment.airline?.trim() ||
      segment.departureAirport?.trim() ||
      segment.arrivalAirport?.trim() ||
      segment.flightCode?.trim() ||
      segment.departureDate?.trim() ||
      segment.departureTime?.trim() ||
      segment.arrivalDate?.trim() ||
      segment.arrivalTime?.trim() ||
      segment.pnr?.trim() ||
      segment.ticketNo?.trim() ||
      segment.cabinBaggage?.trim() ||
      (typeof segment.baggagePieces === 'number' && Number.isFinite(segment.baggagePieces)) ||
      (typeof segment.baggageTotalKg === 'number' && Number.isFinite(segment.baggageTotalKg))
  )
}

const normalizeAirport = (value?: string | null) => value?.trim().toUpperCase() ?? ''
const normalizeText = (value?: string | null) => value?.trim() ?? ''
const normalizeTime = (value?: string | null) => {
  const normalized = value?.trim() ?? ''
  if (!normalized) return ''
  if (/^\d{2}:\d{2}$/.test(normalized)) return normalized
  if (/^\d{2}:\d{2}:\d{2}$/.test(normalized)) return normalized.slice(0, 5)
  if (/^\d{4}$/.test(normalized)) return `${normalized.slice(0, 2)}:${normalized.slice(2, 4)}`
  return normalized
}
const normalizeDate = (value?: string | null) => value?.trim() ?? ''

const normalizeTemplateRows = (segments: FlightSegment[]) =>
  cloneSegments(segments)
    .filter((segment) => segmentHasValue(segment))
    .map((segment, index) => ({
      ...segment,
      segmentIndex: index + 1,
      airline: normalizeText(segment.airline),
      departureAirport: normalizeAirport(segment.departureAirport),
      arrivalAirport: normalizeAirport(segment.arrivalAirport),
      flightCode: normalizeText(segment.flightCode),
      departureDate: normalizeDate(segment.departureDate),
      departureTime: normalizeTime(segment.departureTime),
      arrivalDate: normalizeDate(segment.arrivalDate),
      arrivalTime: normalizeTime(segment.arrivalTime),
      pnr: normalizeText(segment.pnr),
      ticketNo: normalizeText(segment.ticketNo),
      cabinBaggage: normalizeText(segment.cabinBaggage),
      baggagePieces:
        typeof segment.baggagePieces === 'number' && Number.isFinite(segment.baggagePieces)
          ? segment.baggagePieces
          : null,
      baggageTotalKg:
        typeof segment.baggageTotalKg === 'number' && Number.isFinite(segment.baggageTotalKg)
          ? segment.baggageTotalKg
          : null,
    }))

const getDirectionRows = (direction: FlightPanelHelperDirection) =>
  direction === 'Arrival' ? arrivalSegments.value : returnSegments.value

const setDirectionRows = (direction: FlightPanelHelperDirection, segments: FlightSegment[]) => {
  const next = cloneSegments(segments)
  if (direction === 'Arrival') {
    arrivalSegments.value = next
  } else {
    returnSegments.value = next
  }
}

const getDirectionTemplateError = (direction: FlightPanelHelperDirection) => {
  const rowsToValidate = normalizeTemplateRows(getDirectionRows(direction))
  if (rowsToValidate.length === 0) {
    return t('admin.flightPanelHelper.noTemplateForDirection', {
      direction: t(`admin.flightPanelHelper.${direction === 'Arrival' ? 'arrival' : 'return'}`),
    })
  }

  for (const [index, row] of rowsToValidate.entries()) {
    if (
      !row.departureAirport ||
      !row.arrivalAirport ||
      !row.flightCode ||
      !row.departureDate ||
      !row.departureTime
    ) {
      return t('admin.flightPanelHelper.segmentRequiredFields', { index: index + 1 })
    }
  }

  return null
}

const resetDraft = () => {
  activeDirection.value = 'Arrival'
  arrivalSegments.value = []
  returnSegments.value = []
  showPasteHelper.value = { Arrival: false, Return: false }
  pasteText.value = { Arrival: '', Return: '' }
}

const loadDraft = () => {
  resetDraft()
  try {
    const raw = window.localStorage.getItem(draftStorageKey.value)
    if (!raw) return
    const parsed = JSON.parse(raw) as {
      activeDirection?: FlightPanelHelperDirection
      arrivalSegments?: FlightSegment[]
      returnSegments?: FlightSegment[]
      pasteText?: Record<FlightPanelHelperDirection, string>
      showPasteHelper?: Record<FlightPanelHelperDirection, boolean>
    }
    if (parsed.activeDirection === 'Arrival' || parsed.activeDirection === 'Return') {
      activeDirection.value = parsed.activeDirection
    }
    if (Array.isArray(parsed.arrivalSegments)) {
      arrivalSegments.value = cloneSegments(parsed.arrivalSegments)
    }
    if (Array.isArray(parsed.returnSegments)) {
      returnSegments.value = cloneSegments(parsed.returnSegments)
    }
    if (parsed.pasteText) {
      pasteText.value = {
        Arrival: parsed.pasteText.Arrival ?? '',
        Return: parsed.pasteText.Return ?? '',
      }
    }
    if (parsed.showPasteHelper) {
      showPasteHelper.value = {
        Arrival: Boolean(parsed.showPasteHelper.Arrival),
        Return: Boolean(parsed.showPasteHelper.Return),
      }
    }
  } catch {
    window.localStorage.removeItem(draftStorageKey.value)
  }
}

const persistDraft = () => {
  if (!modalOpen.value) return
  const payload = {
    activeDirection: activeDirection.value,
    arrivalSegments: arrivalSegments.value,
    returnSegments: returnSegments.value,
    pasteText: pasteText.value,
    showPasteHelper: showPasteHelper.value,
  }
  window.localStorage.setItem(draftStorageKey.value, JSON.stringify(payload))
}

watch([activeDirection, arrivalSegments, returnSegments, pasteText, showPasteHelper], persistDraft, {
  deep: true,
})

const clearSelection = () => {
  selectedIds.value = []
  selectedParticipantMap.value = {}
  selectionScope.value = 'manual'
}

const loadParticipants = async () => {
  if (!modalOpen.value) return
  loadingParticipants.value = true
  participantsError.value = null
  try {
    const response = await apiGet<ParticipantTableResponse>(
      `/api/events/${props.eventId}/participants/table?query=${encodeURIComponent(searchQuery.value)}&status=${statusFilter.value}&flightFilter=${flightFilter.value}&page=${page.value}&pageSize=${PAGE_SIZE}&sort=fullName&dir=asc`
    )
    rows.value = response.items
    total.value = response.total
    page.value = response.page
  } catch (error) {
    participantsError.value = error instanceof Error ? error.message : t('errors.generic')
  } finally {
    loadingParticipants.value = false
  }
}

const handleSearchInput = () => {
  if (debounceHandle.value) {
    window.clearTimeout(debounceHandle.value)
  }
  debounceHandle.value = window.setTimeout(() => {
    searchQuery.value = searchInput.value.trim()
    page.value = 1
    void loadParticipants()
  }, 300)
}

const fetchMatchingParticipants = async (options: { allEvent?: boolean } = {}) => {
  const participants: ParticipantTableItem[] = []
  let resolvedPage = 1
  let resolvedTotal = 0
  do {
    const response = await apiGet<ParticipantTableResponse>(
      `/api/events/${props.eventId}/participants/table?query=${encodeURIComponent(options.allEvent ? '' : searchQuery.value)}&status=${options.allEvent ? 'all' : statusFilter.value}&flightFilter=${options.allEvent ? 'all' : flightFilter.value}&page=${resolvedPage}&pageSize=${RESOLVE_PAGE_SIZE}&sort=fullName&dir=asc`
    )
    participants.push(...response.items)
    resolvedTotal = response.total
    resolvedPage += 1
  } while (participants.length < resolvedTotal)

  return participants
}

const resolveFilteredSelection = async (allEvent = false) => {
  resolvingSelection.value = true
  applyError.value = null
  try {
    const participants = await fetchMatchingParticipants({ allEvent })
    selectedIds.value = participants.map((participant) => participant.id)
    selectedParticipantMap.value = participants.reduce<Record<string, ParticipantTableItem>>((acc, participant) => {
      acc[participant.id] = participant
      return acc
    }, {})
    selectionScope.value = allEvent ? 'allEvent' : 'filtered'
    pushToast({
      key: 'admin.flightPanelHelper.selectionResolved',
      params: { count: participants.length },
      tone: 'info',
    })
  } catch (error) {
    pushToast(error instanceof Error ? error.message : t('errors.generic'), 'error')
  } finally {
    resolvingSelection.value = false
  }
}

const queueAllEventSelection = () => {
  confirmAction.value = { kind: 'allEvent' }
  confirmOpen.value = true
}

const toggleParticipant = (participant: ParticipantTableItem) => {
  const exists = selectedIds.value.includes(participant.id)
  if (exists) {
    selectedIds.value = selectedIds.value.filter((id) => id !== participant.id)
    const nextMap = { ...selectedParticipantMap.value }
    delete nextMap[participant.id]
    selectedParticipantMap.value = nextMap
    selectionScope.value = 'manual'
    return
  }

  selectedIds.value = [...selectedIds.value, participant.id]
  selectedParticipantMap.value = {
    ...selectedParticipantMap.value,
    [participant.id]: participant,
  }
  selectionScope.value = 'manual'
}

const addSegment = (direction: FlightPanelHelperDirection) => {
  const rowsForDirection = getDirectionRows(direction)
  setDirectionRows(direction, [...rowsForDirection, createEmptySegment(rowsForDirection.length + 1)])
}

const removeSegment = (direction: FlightPanelHelperDirection, index: number) => {
  const rowsForDirection = [...getDirectionRows(direction)]
  rowsForDirection.splice(index, 1)
  setDirectionRows(direction, rowsForDirection)
}

const moveSegment = (direction: FlightPanelHelperDirection, index: number, offset: -1 | 1) => {
  const rowsForDirection = [...getDirectionRows(direction)]
  const nextIndex = index + offset
  if (nextIndex < 0 || nextIndex >= rowsForDirection.length) return
  const [moved] = rowsForDirection.splice(index, 1)
  if (!moved) return
  rowsForDirection.splice(nextIndex, 0, moved)
  setDirectionRows(direction, rowsForDirection)
}

const updateSegmentInteger = (
  direction: FlightPanelHelperDirection,
  index: number,
  key: 'baggagePieces' | 'baggageTotalKg',
  value: string
) => {
  const rowsForDirection = cloneSegments(getDirectionRows(direction))
  const target = rowsForDirection[index]
  if (!target) return
  target[key] = value === '' ? null : Number.parseInt(value, 10)
  setDirectionRows(direction, rowsForDirection)
}

const updateSegmentField = (
  direction: FlightPanelHelperDirection,
  index: number,
  key: SegmentTextField,
  value: string
) => {
  const rowsForDirection = cloneSegments(getDirectionRows(direction))
  const target = rowsForDirection[index]
  if (!target) return
  target[key] = value
  setDirectionRows(direction, rowsForDirection)
}

const copyArrivalToReturn = () => {
  if (arrivalTemplate.value.length === 0) {
    pushToast({ key: 'admin.flightPanelHelper.noTemplateForDirection', params: { direction: t('admin.flightPanelHelper.arrival') }, tone: 'info' })
    return
  }
  returnSegments.value = cloneSegments(arrivalTemplate.value)
  activeDirection.value = 'Return'
}

const normalizeParsedTime = (value: string) => {
  const normalized = value.replace(':', '')
  if (!/^\d{4}$/.test(normalized)) return ''
  return `${normalized.slice(0, 2)}:${normalized.slice(2, 4)}`
}

const parsePastedSegments = (direction: FlightPanelHelperDirection) => {
  const lines = pasteText.value[direction]
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter(Boolean)

  const parsed: FlightSegment[] = []

  lines.forEach((line) => {
    const upper = line.toUpperCase()
    const flightCode = upper.match(/\b([A-Z]{2,3}\d{2,4})\b/)?.[1] ?? ''
    const airportPair = upper.match(/\b([A-Z]{3})([A-Z]{3})\b/)
    const timeMatches = [...upper.matchAll(/\b(\d{2}:\d{2}|\d{4})\b/g)].map((match) => normalizeParsedTime(match[1] ?? ''))
    const departureAirport = airportPair?.[1] ?? ''
    const arrivalAirport = airportPair?.[2] ?? ''
    const departureTime = timeMatches[0] ?? ''
    const arrivalTime = timeMatches[1] ?? ''

    if (!flightCode && !departureAirport && !arrivalAirport && !departureTime && !arrivalTime) {
      return
    }

    parsed.push({
      ...createEmptySegment(parsed.length + 1),
      flightCode,
      departureAirport,
      arrivalAirport,
      departureTime,
      arrivalTime,
    })
  })

  if (parsed.length === 0) {
    pushToast({ key: 'admin.flightPanelHelper.parseNoMatch', tone: 'info' })
    return
  }

  setDirectionRows(direction, parsed)
  pushToast({ key: 'admin.flightPanelHelper.parseSuccess', params: { count: parsed.length }, tone: 'success' })
}

const queueApply = (directions: FlightPanelHelperDirection[]) => {
  applyError.value = null
  if (selectedCount.value === 0) {
    applyError.value = t('admin.flightPanelHelper.noParticipantsSelected')
    return
  }

  for (const direction of directions) {
    const error = getDirectionTemplateError(direction)
    if (error) {
      applyError.value = error
      activeDirection.value = direction
      return
    }
  }

  confirmAction.value = { kind: 'apply', directions }
  confirmOpen.value = true
}

const buildRequestPayload = (directions: FlightPanelHelperDirection[]): BulkApplyFlightSegmentsRequest => ({
  participantIds: [...selectedIds.value],
  applyDirections: directions,
  segments: {
    Arrival: directions.includes('Arrival') ? arrivalTemplate.value : undefined,
    Return: directions.includes('Return') ? returnTemplate.value : undefined,
  },
  replaceMode: 'ReplaceDirection',
})

const executeConfirmedAction = async () => {
  const action = confirmAction.value
  confirmAction.value = null

  if (!action) return

  if (action.kind === 'allEvent') {
    await resolveFilteredSelection(true)
    return
  }

  applying.value = true
  applyError.value = null
  try {
    const response = await bulkApplyFlightSegments(props.eventId, buildRequestPayload(action.directions))
    pushToast({
      key: 'admin.flightPanelHelper.applySuccess',
      params: { count: response.affectedCount },
      tone: 'success',
    })
    emit('applied')
  } catch (error) {
    const apiError = error as Error & { payload?: unknown }
    const payload = apiError.payload
    if (payload && typeof payload === 'object' && 'message' in payload) {
      applyError.value = String((payload as { message?: string }).message ?? t('errors.generic'))
    } else {
      applyError.value = apiError.message || t('errors.generic')
    }
    pushToast(applyError.value, 'error')
  } finally {
    applying.value = false
  }
}

const exportWorkbook = async () => {
  applyError.value = null
  if (selectedCount.value === 0) {
    applyError.value = t('admin.flightPanelHelper.noParticipantsSelected')
    return
  }
  if (arrivalTemplate.value.length === 0 && returnTemplate.value.length === 0) {
    applyError.value = t('admin.flightPanelHelper.exportRequiresTemplate')
    return
  }

  exporting.value = true
  try {
    const { utils, writeFile } = await import('xlsx')
    const workbook = utils.book_new()
    const participantsSheet = utils.aoa_to_sheet([
      PARTICIPANTS_SHEET_HEADERS,
      ...buildParticipantsSheetRows(selectedParticipants.value),
    ])
    const segmentRows = buildBulkFlightTemplateSheetRows(
      selectedParticipants.value,
      arrivalTemplate.value,
      returnTemplate.value
    )
    const segmentsSheet = utils.aoa_to_sheet([FLIGHT_SEGMENTS_SHEET_HEADERS, ...segmentRows])
    utils.book_append_sheet(workbook, participantsSheet, 'participants')
    utils.book_append_sheet(workbook, segmentsSheet, 'flight_segments')
    const timestamp = new Date()
    const stamp = `${timestamp.getFullYear()}${String(timestamp.getMonth() + 1).padStart(2, '0')}${String(timestamp.getDate()).padStart(2, '0')}_${String(timestamp.getHours()).padStart(2, '0')}${String(timestamp.getMinutes()).padStart(2, '0')}`
    writeFile(workbook, `flight_segments_${props.eventId}_${stamp}.xlsx`)
    pushToast({ key: 'admin.flightPanelHelper.exportSuccess', tone: 'success' })
  } catch (error) {
    pushToast(error instanceof Error ? error.message : t('errors.generic'), 'error')
  } finally {
    exporting.value = false
  }
}

const resetModalState = () => {
  page.value = 1
  searchInput.value = props.initialQuery
  searchQuery.value = props.initialQuery.trim()
  statusFilter.value = props.initialStatus
  flightFilter.value = 'all'
  rows.value = []
  total.value = 0
  participantsError.value = null
  applyError.value = null
  clearSelection()
  loadDraft()
}

const openModal = () => {
  resetModalState()
  modalOpen.value = true
  void loadParticipants()
}

const closeModal = () => {
  if (hasBusyState.value) return
  modalOpen.value = false
  confirmOpen.value = false
  confirmAction.value = null
}

watch(page, () => {
  if (modalOpen.value) {
    void loadParticipants()
  }
})

watch([statusFilter, flightFilter], () => {
  if (!modalOpen.value) return
  page.value = 1
  void loadParticipants()
})

watch(modalOpen, (isOpen) => {
  if (!isOpen && debounceHandle.value) {
    window.clearTimeout(debounceHandle.value)
    debounceHandle.value = null
  }
})

onMounted(loadDraft)

onBeforeUnmount(() => {
  if (debounceHandle.value) {
    window.clearTimeout(debounceHandle.value)
  }
})
</script>

<template>
  <button :class="buttonClass" type="button" @click="openModal">
    {{ buttonText }}
  </button>

  <AppModalShell
    :open="modalOpen"
    :close-on-overlay="!hasBusyState"
    content-class="px-3 sm:px-6"
    @close="closeModal"
  >
    <template #default="{ panelClass }">
      <div :class="[panelClass, 'flex h-[90vh] w-full max-w-6xl flex-col overflow-hidden rounded-3xl bg-white shadow-xl']">
        <div class="border-b border-slate-200 px-4 py-4 sm:px-6">
          <div class="flex flex-wrap items-start justify-between gap-3">
            <div>
              <h2 class="text-xl font-semibold text-slate-900">{{ t('admin.flightPanelHelper.title') }}</h2>
              <p class="mt-1 text-sm text-slate-500">{{ t('admin.flightPanelHelper.subtitle') }}</p>
            </div>
            <button
              class="rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm text-slate-700 hover:border-slate-300 disabled:cursor-not-allowed disabled:opacity-50"
              type="button"
              :disabled="hasBusyState"
              @click="closeModal"
            >
              {{ t('common.close') }}
            </button>
          </div>
        </div>

        <div class="flex-1 overflow-y-auto px-4 py-4 sm:px-6">
          <section class="rounded-2xl border border-slate-200 bg-slate-50 p-4">
              <div class="flex flex-wrap items-end gap-3">
                <div class="min-w-[220px] flex-1">
                  <label class="text-xs font-semibold uppercase tracking-wide text-slate-500">{{ t('admin.participantsTable.searchLabel') }}</label>
                  <input
                    v-model="searchInput"
                    class="mt-1 w-full rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm text-slate-700 outline-none transition focus:border-slate-300"
                    :placeholder="t('admin.flightPanelHelper.searchPlaceholder')"
                    type="text"
                    @input="handleSearchInput"
                  />
                </div>
                <button
                  class="rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm text-slate-700 hover:border-slate-300 disabled:cursor-not-allowed disabled:opacity-50"
                  type="button"
                  :disabled="resolvingSelection"
                  @click="resolveFilteredSelection(false)"
                >
                  {{ t('admin.flightPanelHelper.selectFiltered') }}
                </button>
                <button
                  class="rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm text-slate-700 hover:border-slate-300"
                  type="button"
                  @click="clearSelection"
                >
                  {{ t('admin.flightPanelHelper.clearSelection') }}
                </button>
                <button
                  class="rounded-xl border border-amber-200 bg-amber-50 px-3 py-2 text-sm font-medium text-amber-900 hover:border-amber-300 disabled:cursor-not-allowed disabled:opacity-50"
                  type="button"
                  :disabled="resolvingSelection"
                  @click="queueAllEventSelection"
                >
                  {{ t('admin.flightPanelHelper.applyAllEvent') }}
                </button>
              </div>

              <div class="mt-3 flex flex-wrap items-center gap-3 text-sm text-slate-600">
                <span class="rounded-full bg-slate-900 px-2.5 py-1 text-xs font-semibold text-white">
                  {{ t('admin.flightPanelHelper.selectedCount', { count: selectedCount }) }}
                </span>
                <span class="rounded-full bg-slate-100 px-2.5 py-1 text-xs font-medium text-slate-700">
                  {{ t(`admin.flightPanelHelper.scope.${selectionScope}`) }}
                </span>
                <span v-if="resolvingSelection" class="text-xs text-slate-500">
                  {{ t('admin.flightPanelHelper.resolveSelectionLoading') }}
                </span>
              </div>

              <div class="mt-4 grid gap-3 xl:grid-cols-[minmax(0,1fr)_minmax(0,1.35fr)]">
                <div>
                  <div class="mb-1 text-xs font-semibold uppercase tracking-wide text-slate-500">
                    {{ t('admin.flightPanelHelper.filters.status') }}
                  </div>
                  <AppSegmentedControl
                    v-model="statusFilter"
                    :options="statusOptions"
                    size="sm"
                    full-width
                    :aria-label="t('admin.flightPanelHelper.filters.status')"
                  />
                </div>
                <div>
                  <div class="mb-1 text-xs font-semibold uppercase tracking-wide text-slate-500">
                    {{ t('admin.flightPanelHelper.filters.flightState') }}
                  </div>
                  <AppSegmentedControl
                    v-model="flightFilter"
                    :options="flightFilterOptions"
                    size="sm"
                    full-width
                    :aria-label="t('admin.flightPanelHelper.filters.flightState')"
                  />
                </div>
              </div>

              <div class="mt-4 rounded-2xl border border-slate-200 bg-white">
                <div v-if="loadingParticipants" class="px-4 py-8 text-sm text-slate-500">{{ t('common.loading') }}</div>
                <div v-else-if="participantsError" class="px-4 py-8 text-sm text-rose-600">{{ participantsError }}</div>
                <div v-else-if="rows.length === 0" class="px-4 py-8 text-sm text-slate-500">{{ t('admin.participantsTable.empty') }}</div>
                <ul v-else class="divide-y divide-slate-100">
                  <li v-for="participant in rows" :key="participant.id" class="flex items-start gap-3 px-4 py-3">
                    <input
                      :checked="selectedIds.includes(participant.id)"
                      class="mt-1 h-4 w-4 rounded border-slate-300"
                      type="checkbox"
                      @change="toggleParticipant(participant)"
                    />
                    <div class="min-w-0 flex-1">
                      <div class="truncate text-sm font-medium text-slate-900">{{ participantDisplayName(participant) }}</div>
                      <div class="mt-1 flex flex-wrap items-center gap-2 text-xs text-slate-500">
                        <span>{{ participant.tcNo }}</span>
                        <span v-if="participant.details?.roomNo">· {{ participant.details.roomNo }}</span>
                        <span v-if="participant.details?.agencyName">· {{ participant.details.agencyName }}</span>
                        <span v-if="participant.phone">· {{ participant.phone }}</span>
                      </div>
                      <div class="mt-2 flex flex-wrap items-center gap-1.5">
                        <span
                          class="rounded-full px-2 py-0.5 text-[11px] font-medium"
                          :class="participant.hasArrivalSegments ? 'bg-emerald-100 text-emerald-800' : 'bg-slate-100 text-slate-500'"
                        >
                          {{ t('admin.flightPanelHelper.arrival') }}
                        </span>
                        <span
                          class="rounded-full px-2 py-0.5 text-[11px] font-medium"
                          :class="participant.hasReturnSegments ? 'bg-sky-100 text-sky-800' : 'bg-slate-100 text-slate-500'"
                        >
                          {{ t('admin.flightPanelHelper.return') }}
                        </span>
                      </div>
                    </div>
                  </li>
                </ul>
                <div class="flex items-center justify-between border-t border-slate-100 px-4 py-3 text-xs text-slate-500">
                  <span>{{ t('admin.participantsTable.pagination', { page, totalPages, total }) }}</span>
                  <div class="flex items-center gap-2">
                    <button
                      class="rounded border border-slate-200 bg-white px-2 py-1 text-xs text-slate-700 hover:border-slate-300 disabled:cursor-not-allowed disabled:opacity-50"
                      :disabled="page <= 1 || loadingParticipants"
                      type="button"
                      @click="page -= 1"
                    >
                      {{ t('common.prev') }}
                    </button>
                    <button
                      class="rounded border border-slate-200 bg-white px-2 py-1 text-xs text-slate-700 hover:border-slate-300 disabled:cursor-not-allowed disabled:opacity-50"
                      :disabled="page >= totalPages || loadingParticipants"
                      type="button"
                      @click="page += 1"
                    >
                      {{ t('common.next') }}
                    </button>
                  </div>
                </div>
              </div>
            </section>

            <section class="mt-6 rounded-2xl border border-slate-200 bg-white p-4">
              <div class="flex flex-wrap items-center gap-2 border-b border-slate-200 pb-4">
                <button
                  v-for="direction in directions"
                  :key="direction"
                  class="rounded-full px-3 py-1.5 text-sm font-medium transition"
                  :class="activeDirection === direction ? 'bg-slate-900 text-white' : 'bg-slate-100 text-slate-700 hover:bg-slate-200'"
                  type="button"
                  @click="activeDirection = direction"
                >
                  {{ t(`admin.flightPanelHelper.${direction === 'Arrival' ? 'arrival' : 'return'}`) }}
                  <span class="ml-1 rounded-full bg-white/20 px-1.5 py-0.5 text-[11px]">{{ activeDirectionBadge(direction) }}</span>
                </button>
                <button
                  class="ml-auto rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm text-slate-700 hover:border-slate-300 disabled:cursor-not-allowed disabled:opacity-50"
                  type="button"
                  :disabled="arrivalTemplate.length === 0"
                  @click="copyArrivalToReturn"
                >
                  {{ t('admin.flightPanelHelper.copyArrivalToReturn') }}
                </button>
              </div>

              <div v-if="localDirectionError" class="mt-4 rounded-xl border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-800">
                {{ localDirectionError }}
              </div>

              <div class="mt-4 space-y-4">
                <div v-if="currentSegments.length === 0" class="rounded-2xl border border-dashed border-slate-300 bg-slate-50 px-4 py-6 text-sm text-slate-500">
                  <p>{{ t('admin.flightPanelHelper.emptyDirection') }}</p>
                  <button
                    class="mt-3 rounded-xl bg-slate-900 px-3 py-2 text-sm font-medium text-white hover:bg-slate-800"
                    type="button"
                    @click="addSegment(activeDirection)"
                  >
                    {{ t('admin.flightPanelHelper.addSegment') }}
                  </button>
                </div>

                <div
                  v-for="(segment, index) in currentSegments"
                  :key="`${activeDirection}-${index}`"
                  class="rounded-2xl border border-slate-200 bg-slate-50 p-4"
                >
                  <div class="flex flex-wrap items-center justify-between gap-3">
                    <div class="text-sm font-semibold text-slate-900">
                      {{ t('admin.flightPanelHelper.segmentLabel', { index: index + 1 }) }}
                    </div>
                    <div class="flex flex-wrap items-center gap-2">
                      <button
                        class="rounded border border-slate-200 bg-white px-2 py-1 text-xs text-slate-700 hover:border-slate-300 disabled:cursor-not-allowed disabled:opacity-50"
                        type="button"
                        :disabled="index === 0"
                        @click="moveSegment(activeDirection, index, -1)"
                      >
                        {{ t('admin.flightPanelHelper.moveUp') }}
                      </button>
                      <button
                        class="rounded border border-slate-200 bg-white px-2 py-1 text-xs text-slate-700 hover:border-slate-300 disabled:cursor-not-allowed disabled:opacity-50"
                        type="button"
                        :disabled="index === currentSegments.length - 1"
                        @click="moveSegment(activeDirection, index, 1)"
                      >
                        {{ t('admin.flightPanelHelper.moveDown') }}
                      </button>
                      <button
                        class="rounded border border-rose-200 bg-white px-2 py-1 text-xs text-rose-700 hover:border-rose-300"
                        type="button"
                        @click="removeSegment(activeDirection, index)"
                      >
                        {{ t('admin.flightPanelHelper.removeSegment') }}
                      </button>
                    </div>
                  </div>

                  <div class="mt-4 grid gap-3 md:grid-cols-2 xl:grid-cols-4">
                    <label class="text-xs font-semibold text-slate-500">
                      {{ t('admin.flightPanelHelper.departureAirport') }}
                      <input
                        :value="segment.departureAirport ?? ''"
                        class="mt-1 w-full rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm text-slate-700"
                        type="text"
                        @input="updateSegmentField(activeDirection, index, 'departureAirport', ($event.target as HTMLInputElement).value)"
                      />
                    </label>
                    <label class="text-xs font-semibold text-slate-500">
                      {{ t('admin.flightPanelHelper.arrivalAirport') }}
                      <input
                        :value="segment.arrivalAirport ?? ''"
                        class="mt-1 w-full rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm text-slate-700"
                        type="text"
                        @input="updateSegmentField(activeDirection, index, 'arrivalAirport', ($event.target as HTMLInputElement).value)"
                      />
                    </label>
                    <label class="text-xs font-semibold text-slate-500">
                      {{ t('admin.flightPanelHelper.flightCode') }}
                      <input
                        :value="segment.flightCode ?? ''"
                        class="mt-1 w-full rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm text-slate-700"
                        type="text"
                        @input="updateSegmentField(activeDirection, index, 'flightCode', ($event.target as HTMLInputElement).value)"
                      />
                    </label>
                    <label class="text-xs font-semibold text-slate-500">
                      {{ t('admin.flightPanelHelper.airline') }}
                      <input
                        :value="segment.airline ?? ''"
                        class="mt-1 w-full rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm text-slate-700"
                        type="text"
                        @input="updateSegmentField(activeDirection, index, 'airline', ($event.target as HTMLInputElement).value)"
                      />
                    </label>
                    <label class="text-xs font-semibold text-slate-500">
                      {{ t('admin.flightPanelHelper.departureDate') }}
                      <input
                        :value="segment.departureDate ?? ''"
                        class="mt-1 w-full rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm text-slate-700"
                        type="date"
                        @input="updateSegmentField(activeDirection, index, 'departureDate', ($event.target as HTMLInputElement).value)"
                      />
                    </label>
                    <label class="text-xs font-semibold text-slate-500">
                      {{ t('admin.flightPanelHelper.departureTime') }}
                      <input
                        :value="segment.departureTime ?? ''"
                        class="mt-1 w-full rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm text-slate-700"
                        type="time"
                        @input="updateSegmentField(activeDirection, index, 'departureTime', ($event.target as HTMLInputElement).value)"
                      />
                    </label>
                    <label class="text-xs font-semibold text-slate-500">
                      {{ t('admin.flightPanelHelper.arrivalDate') }}
                      <input
                        :value="segment.arrivalDate ?? ''"
                        class="mt-1 w-full rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm text-slate-700"
                        type="date"
                        @input="updateSegmentField(activeDirection, index, 'arrivalDate', ($event.target as HTMLInputElement).value)"
                      />
                    </label>
                    <label class="text-xs font-semibold text-slate-500">
                      {{ t('admin.flightPanelHelper.arrivalTime') }}
                      <input
                        :value="segment.arrivalTime ?? ''"
                        class="mt-1 w-full rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm text-slate-700"
                        type="time"
                        @input="updateSegmentField(activeDirection, index, 'arrivalTime', ($event.target as HTMLInputElement).value)"
                      />
                    </label>
                    <label class="text-xs font-semibold text-slate-500">
                      {{ t('admin.flightPanelHelper.baggagePieces') }}
                      <input
                        :value="segment.baggagePieces ?? ''"
                        class="mt-1 w-full rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm text-slate-700"
                        type="number"
                        min="0"
                        @input="updateSegmentInteger(activeDirection, index, 'baggagePieces', ($event.target as HTMLInputElement).value)"
                      />
                    </label>
                    <label class="text-xs font-semibold text-slate-500">
                      {{ t('admin.flightPanelHelper.baggageKg') }}
                      <input
                        :value="segment.baggageTotalKg ?? ''"
                        class="mt-1 w-full rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm text-slate-700"
                        type="number"
                        min="0"
                        @input="updateSegmentInteger(activeDirection, index, 'baggageTotalKg', ($event.target as HTMLInputElement).value)"
                      />
                    </label>
                    <label class="text-xs font-semibold text-slate-500">
                      {{ t('admin.flightPanelHelper.ticketNo') }}
                      <input
                        :value="segment.ticketNo ?? ''"
                        class="mt-1 w-full rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm text-slate-700"
                        type="text"
                        @input="updateSegmentField(activeDirection, index, 'ticketNo', ($event.target as HTMLInputElement).value)"
                      />
                    </label>
                    <label class="text-xs font-semibold text-slate-500">
                      {{ t('admin.flightPanelHelper.pnr') }}
                      <input
                        :value="segment.pnr ?? ''"
                        class="mt-1 w-full rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm text-slate-700"
                        type="text"
                        @input="updateSegmentField(activeDirection, index, 'pnr', ($event.target as HTMLInputElement).value)"
                      />
                    </label>
                    <label class="text-xs font-semibold text-slate-500 md:col-span-2 xl:col-span-2">
                      {{ t('admin.flightPanelHelper.cabinBaggage') }}
                      <input
                        :value="segment.cabinBaggage ?? ''"
                        class="mt-1 w-full rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm text-slate-700"
                        type="text"
                        @input="updateSegmentField(activeDirection, index, 'cabinBaggage', ($event.target as HTMLInputElement).value)"
                      />
                    </label>
                  </div>
                </div>
              </div>

              <div class="mt-4 flex flex-wrap items-center gap-3">
                <button
                  class="rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm text-slate-700 hover:border-slate-300"
                  type="button"
                  @click="addSegment(activeDirection)"
                >
                  {{ t('admin.flightPanelHelper.addSegment') }}
                </button>
                <button
                  class="rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm text-slate-700 hover:border-slate-300"
                  type="button"
                  @click="showPasteHelper[activeDirection] = !showPasteHelper[activeDirection]"
                >
                  {{ t('admin.flightPanelHelper.pasteHelper') }}
                </button>
              </div>

              <div v-if="showPasteHelper[activeDirection]" class="mt-4 rounded-2xl border border-slate-200 bg-slate-50 p-4">
                <label class="text-xs font-semibold uppercase tracking-wide text-slate-500">{{ t('admin.flightPanelHelper.pasteHelper') }}</label>
                <textarea
                  v-model="pasteText[activeDirection]"
                  class="mt-2 min-h-[120px] w-full rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm text-slate-700"
                />
                <div class="mt-3 flex justify-end">
                  <button
                    class="rounded-xl bg-slate-900 px-3 py-2 text-sm font-medium text-white hover:bg-slate-800"
                    type="button"
                    @click="parsePastedSegments(activeDirection)"
                  >
                    {{ t('admin.flightPanelHelper.parsePaste') }}
                  </button>
                </div>
              </div>
          </section>
        </div>

        <div class="border-t border-slate-200 bg-white px-4 py-4 sm:px-6">
          <div class="rounded-2xl border border-slate-200 bg-slate-50 p-4">
              <div class="flex flex-wrap items-start justify-between gap-4">
                <div class="space-y-2 text-sm text-slate-700">
                  <div class="text-sm font-semibold text-slate-900">{{ t('admin.flightPanelHelper.previewTitle') }}</div>
                  <div>{{ t('admin.flightPanelHelper.rowsWillBeCreated', { selected: selectedCount, arrival: arrivalRowsCreated, return: returnRowsCreated, total: totalRowsCreated }) }}</div>
                  <div v-if="sampleRows.length > 0" class="space-y-1 text-xs text-slate-500">
                    <div class="font-medium text-slate-700">{{ t('admin.flightPanelHelper.samplePreview') }}</div>
                    <div v-for="row in sampleRows" :key="`${row[0]}-${row[1]}-${row[2]}`" class="font-mono">
                      {{ row[0] }} · {{ row[1] }} #{{ row[2] }} · {{ row[3] }} → {{ row[4] }} · {{ row[5] }} {{ row[6] }} {{ row[7] }}
                    </div>
                  </div>
                  <div v-if="applyError" class="rounded-xl border border-rose-200 bg-rose-50 px-3 py-2 text-sm text-rose-700">
                    {{ applyError }}
                  </div>
                </div>

                <div class="flex flex-wrap items-center justify-end gap-2">
                  <button
                    class="rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm text-slate-700 hover:border-slate-300 disabled:cursor-not-allowed disabled:opacity-50"
                    type="button"
                    :disabled="hasBusyState"
                    @click="exportWorkbook"
                  >
                    {{ exporting ? t('common.loading') : t('admin.flightPanelHelper.exportXlsx') }}
                  </button>
                  <button
                    class="rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm text-slate-700 hover:border-slate-300 disabled:cursor-not-allowed disabled:opacity-50"
                    type="button"
                    :disabled="hasBusyState"
                    @click="queueApply(['Arrival'])"
                  >
                    {{ t('admin.flightPanelHelper.applyArrival') }}
                  </button>
                  <button
                    class="rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm text-slate-700 hover:border-slate-300 disabled:cursor-not-allowed disabled:opacity-50"
                    type="button"
                    :disabled="hasBusyState"
                    @click="queueApply(['Return'])"
                  >
                    {{ t('admin.flightPanelHelper.applyReturn') }}
                  </button>
                  <button
                    class="rounded-xl bg-slate-900 px-4 py-2 text-sm font-medium text-white hover:bg-slate-800 disabled:cursor-not-allowed disabled:opacity-50"
                    type="button"
                    :disabled="hasBusyState"
                    @click="queueApply(['Arrival', 'Return'])"
                  >
                    {{ applying ? t('common.loading') : t('admin.flightPanelHelper.applyBoth') }}
                  </button>
                </div>
              </div>
          </div>
        </div>
      </div>
    </template>
  </AppModalShell>

  <ConfirmDialog
    v-model:open="confirmOpen"
    :title="confirmAction?.kind === 'allEvent' ? t('admin.flightPanelHelper.confirmApplyAllEvent') : t('admin.flightPanelHelper.confirmApply')"
    :message="confirmAction?.kind === 'allEvent'
      ? t('admin.flightPanelHelper.confirmAllEventMessage')
      : t('admin.flightPanelHelper.confirmApplyMessage', {
          count: selectedCount,
          directions: confirmAction?.kind === 'apply' ? confirmAction.directions.map((direction) => t(`admin.flightPanelHelper.${direction === 'Arrival' ? 'arrival' : 'return'}`)).join(', ') : '',
          rows: confirmAction?.kind === 'apply' ? getRowsCreatedForDirections(confirmAction.directions) : totalRowsCreated,
        })"
    :confirm-label="t('common.confirm')"
    :cancel-label="t('common.cancel')"
    :confirm-disabled="hasBusyState"
    tone="danger"
    @confirm="executeConfirmedAction"
  />
</template>
