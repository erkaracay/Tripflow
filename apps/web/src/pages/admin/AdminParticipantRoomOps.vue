<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { RouterLink, useRoute } from 'vue-router'
import { useI18n } from 'vue-i18n'
import {
  apiGet,
  apiPost,
  bulkApplyAccommodationSegmentParticipants,
  createAccommodationSegment,
  deleteAccommodationSegment,
  getAccommodationSegmentParticipants,
  getAccommodationSegments,
  updateAccommodationSegment,
} from '../../lib/api'
import { useToast } from '../../lib/toast'
import LoadingState from '../../components/ui/LoadingState.vue'
import ErrorState from '../../components/ui/ErrorState.vue'
import AppCombobox from '../../components/ui/AppCombobox.vue'
import AppModalShell from '../../components/ui/AppModalShell.vue'
import ConfirmDialog from '../../components/ui/ConfirmDialog.vue'
import { formatDateRange } from '../../lib/formatters'
import type {
  AccommodationSegment,
  AccommodationSegmentParticipantRowUpdate,
  AccommodationSegmentParticipantTableItem,
  AppComboboxOption,
  BulkApplyAccommodationSegmentParticipantsRequest,
  Event as EventDto,
  EventDocTabDto,
  UpsertAccommodationSegmentRequest,
} from '../../types'

type RowDraft = {
  accommodationSelection: string
  roomNo: string
  roomType: string
  boardType: string
  personNo: string
}

type SegmentForm = {
  defaultAccommodationDocTabId: string
  startDate: string
  endDate: string
}

type QuickAccommodationForm = {
  title: string
  address: string
  phone: string
}

type FieldMode = 'keep' | 'set' | 'clear'

const DEFAULT_ACCOMMODATION_VALUE = '__default__'

const route = useRoute()
const { t } = useI18n()
const { pushToast } = useToast()

const eventId = computed(() => String(route.params.eventId ?? ''))

const loading = ref(false)
const tableLoading = ref(false)
const saving = ref(false)
const segmentSaving = ref(false)
const event = ref<EventDto | null>(null)
const tabs = ref<EventDocTabDto[]>([])
const segments = ref<AccommodationSegment[]>([])
const rows = ref<AccommodationSegmentParticipantTableItem[]>([])
const total = ref(0)
const errorMessage = ref<string | null>(null)

const selectedSegmentId = ref('')
const searchInput = ref('')
const page = ref(1)
const pageSize = ref(100)
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
const draftById = ref<Record<string, RowDraft>>({})
const originalById = ref<Record<string, RowDraft>>({})

const bulkForm = reactive<{
  overwriteMode: 'always' | 'only_empty'
  accommodationMode: 'keep' | 'default' | 'override'
  overrideAccommodationDocTabId: string
  roomNoMode: FieldMode
  roomNo: string
  roomTypeMode: FieldMode
  roomType: string
  boardTypeMode: FieldMode
  boardType: string
  personNoMode: FieldMode
  personNo: string
}>({
  overwriteMode: 'always',
  accommodationMode: 'keep',
  overrideAccommodationDocTabId: '',
  roomNoMode: 'keep',
  roomNo: '',
  roomTypeMode: 'keep',
  roomType: '',
  boardTypeMode: 'keep',
  boardType: '',
  personNoMode: 'keep',
  personNo: '',
})

const segmentModalOpen = ref(false)
const segmentModalMode = ref<'create' | 'edit'>('create')
const segmentForm = reactive<SegmentForm>({
  defaultAccommodationDocTabId: '',
  startDate: '',
  endDate: '',
})
const segmentFormError = ref<string | null>(null)
const deletingSegmentId = ref<string | null>(null)
const quickAccommodationModalOpen = ref(false)
const quickAccommodationSaving = ref(false)
const quickAccommodationFormError = ref<string | null>(null)
const quickAccommodationForm = reactive<QuickAccommodationForm>({
  title: '',
  address: '',
  phone: '',
})

const normalize = (value: string | null | undefined) => (value ?? '').trim()

const accommodationTabs = computed(() =>
  [...tabs.value]
    .filter((tab) => (tab.type ?? '').trim().toLowerCase() === 'hotel')
    .sort((a, b) => {
      if (a.sortOrder !== b.sortOrder) return a.sortOrder - b.sortOrder
      return a.title.localeCompare(b.title)
    })
)

const selectedSegment = computed(
  () => segments.value.find((segment) => segment.id === selectedSegmentId.value) ?? null
)

const pageTitle = computed(() => `${t('admin.roomOps.title')} - ${event.value?.name ?? t('common.event')}`)
const hasSegments = computed(() => segments.value.length > 0)
const hasAccommodationTabs = computed(() => accommodationTabs.value.length > 0)
const nextDocTabSortOrder = computed(() => tabs.value.reduce((max, tab) => Math.max(max, tab.sortOrder), 0) + 1)
const totalLabel = computed(() => t('admin.roomOps.total', { count: total.value }))
const hasPreviousPage = computed(() => page.value > 1)
const hasNextPage = computed(() => page.value * pageSize.value < total.value)
const allSelectedOnPage = computed(() => rows.value.length > 0 && rows.value.every((row) => selectedIds.value.includes(row.participantId)))
const canSaveQuickAccommodation = computed(() => normalize(quickAccommodationForm.title).length > 0 && !quickAccommodationSaving.value)
const canCreateSegment = computed(() => hasAccommodationTabs.value && !segmentSaving.value)

const statusOptions = computed<AppComboboxOption[]>(() => [
  { value: 'all', label: t('admin.roomOps.filters.statusAll') },
  { value: 'arrived', label: t('admin.roomOps.filters.statusArrived') },
  { value: 'not_arrived', label: t('admin.roomOps.filters.statusNotArrived') },
])

const accommodationFilterOptions = computed<AppComboboxOption[]>(() => {
  const options: AppComboboxOption[] = [{ value: 'all', label: t('admin.roomOps.filters.accommodationAll') }]
  for (const tab of accommodationTabs.value) {
    options.push({ value: tab.id, label: tab.title })
  }
  return options
})

const overwriteModeOptions = computed<AppComboboxOption[]>(() => [
  { value: 'always', label: t('admin.roomOps.bulk.overwriteAlways') },
  { value: 'only_empty', label: t('admin.roomOps.bulk.overwriteOnlyEmpty') },
])

const fieldModeOptions = computed<AppComboboxOption[]>(() => [
  { value: 'keep', label: t('admin.roomOps.bulk.fieldKeep') },
  { value: 'set', label: t('admin.roomOps.bulk.fieldSet') },
  { value: 'clear', label: t('admin.roomOps.bulk.fieldClear') },
])

const accommodationActionOptions = computed<AppComboboxOption[]>(() => [
  { value: 'keep', label: t('admin.roomOps.bulk.accommodationModeKeep') },
  { value: 'default', label: t('admin.roomOps.bulk.accommodationModeDefault') },
  { value: 'override', label: t('admin.roomOps.bulk.accommodationModeOverride') },
])

const segmentAccommodationOptions = computed<AppComboboxOption[]>(() =>
  accommodationTabs.value.map((tab) => ({ value: tab.id, label: tab.title }))
)

const rowAccommodationOptions = computed<AppComboboxOption[]>(() => {
  const defaultLabel = t('admin.roomOps.segments.defaultAccommodationOption', {
    title: selectedSegment.value?.defaultAccommodationTitle ?? t('admin.roomOps.filters.accommodationUnknown'),
  })

  return [
    { value: DEFAULT_ACCOMMODATION_VALUE, label: defaultLabel },
    ...accommodationTabs.value.map((tab) => ({ value: tab.id, label: tab.title })),
  ]
})

const changedRowCount = computed(() =>
  rows.value.reduce((count, row) => count + (buildRowUpdate(row.participantId) ? 1 : 0), 0)
)

const canApplyDraftChanges = computed(() => changedRowCount.value > 0 && !saving.value)

const segmentSummaryLabel = computed(() => {
  if (!selectedSegment.value) {
    return ''
  }

  return formatDateRange(selectedSegment.value.startDate, selectedSegment.value.endDate)
})

const createDraftFromRow = (row: AccommodationSegmentParticipantTableItem): RowDraft => ({
  accommodationSelection: row.usesOverride ? row.effectiveAccommodationDocTabId : DEFAULT_ACCOMMODATION_VALUE,
  roomNo: row.roomNo ?? '',
  roomType: row.roomType ?? '',
  boardType: row.boardType ?? '',
  personNo: row.personNo ?? '',
})

const cloneDraft = (draft: RowDraft): RowDraft => ({ ...draft })

const hydrateDrafts = () => {
  const nextDraft: Record<string, RowDraft> = {}
  const nextOriginal: Record<string, RowDraft> = {}

  for (const row of rows.value) {
    const original = createDraftFromRow(row)
    nextOriginal[row.participantId] = original
    nextDraft[row.participantId] = cloneDraft(original)
  }

  draftById.value = nextDraft
  originalById.value = nextOriginal
  selectedIds.value = []
}

const buildRowUpdate = (participantId: string): AccommodationSegmentParticipantRowUpdate | null => {
  const draft = draftById.value[participantId]
  const original = originalById.value[participantId]
  if (!draft || !original) {
    return null
  }

  const currentAccommodation = normalize(draft.accommodationSelection) || DEFAULT_ACCOMMODATION_VALUE
  const originalAccommodation = normalize(original.accommodationSelection) || DEFAULT_ACCOMMODATION_VALUE
  const roomNo = normalize(draft.roomNo)
  const roomType = normalize(draft.roomType)
  const boardType = normalize(draft.boardType)
  const personNo = normalize(draft.personNo)
  const originalRoomNo = normalize(original.roomNo)
  const originalRoomType = normalize(original.roomType)
  const originalBoardType = normalize(original.boardType)
  const originalPersonNo = normalize(original.personNo)

  const changed =
    currentAccommodation !== originalAccommodation
    || roomNo !== originalRoomNo
    || roomType !== originalRoomType
    || boardType !== originalBoardType
    || personNo !== originalPersonNo

  if (!changed) {
    return null
  }

  return {
    participantId,
    accommodationMode: currentAccommodation === DEFAULT_ACCOMMODATION_VALUE ? 'default' : 'override',
    overrideAccommodationDocTabId: currentAccommodation === DEFAULT_ACCOMMODATION_VALUE ? null : currentAccommodation,
    roomNo: roomNo || null,
    roomType: roomType || null,
    boardType: boardType || null,
    personNo: personNo || null,
  }
}

const rowWarningMessage = (code: string, roomNo?: string | null, assignedCount?: number, declaredCount?: number) => {
  if (code === 'room_capacity_mismatch') {
    return t('admin.roomOps.warnings.roomCapacityMismatch', {
      roomNo: normalize(roomNo) || '—',
      assignedCount: assignedCount ?? 0,
      declaredCount: declaredCount ?? 0,
    })
  }

  return t('warnings.generic')
}

const handleApiError = (err: unknown, fallbackKey = 'errors.generic') => {
  if (err instanceof Error && err.message) {
    return err.message
  }
  return t(fallbackKey)
}

const loadEventContext = async () => {
  const [eventData, tabData, segmentData] = await Promise.all([
    apiGet<EventDto>(`/api/events/${eventId.value}`),
    apiGet<EventDocTabDto[]>(`/api/events/${eventId.value}/docs/tabs`),
    getAccommodationSegments(eventId.value),
  ])

  event.value = eventData
  tabs.value = tabData
  segments.value = segmentData

  if (!segments.value.some((segment) => segment.id === selectedSegmentId.value)) {
    selectedSegmentId.value = segments.value[0]?.id ?? ''
  }
}

const fetchTable = async () => {
  if (!selectedSegmentId.value) {
    rows.value = []
    total.value = 0
    hydrateDrafts()
    return
  }

  tableLoading.value = true
  errorMessage.value = null

  try {
    const params = new URLSearchParams()
    params.set('query', filters.query)
    params.set('status', filters.status)
    params.set('page', String(page.value))
    params.set('pageSize', String(pageSize.value))
    params.set('sort', 'fullName')
    params.set('dir', 'asc')
    if (filters.accommodationFilter !== 'all') {
      params.set('accommodationFilter', filters.accommodationFilter)
    }

    const response = await getAccommodationSegmentParticipants(eventId.value, selectedSegmentId.value, params)
    rows.value = response.items
    total.value = response.total
    page.value = response.page
    pageSize.value = response.pageSize
    hydrateDrafts()
  } catch (err) {
    errorMessage.value = handleApiError(err)
  } finally {
    tableLoading.value = false
  }
}

const reload = async () => {
  loading.value = true
  errorMessage.value = null
  try {
    await loadEventContext()
    await fetchTable()
  } catch (err) {
    errorMessage.value = handleApiError(err)
  } finally {
    loading.value = false
  }
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

const selectSegment = async (segmentId: string) => {
  if (selectedSegmentId.value === segmentId) {
    return
  }

  selectedSegmentId.value = segmentId
  page.value = 1
  filters.accommodationFilter = 'all'
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

const toggleSelectAll = (checked: boolean) => {
  if (checked) {
    selectedIds.value = rows.value.map((row) => row.participantId)
    return
  }
  selectedIds.value = []
}

const resetBulkForm = () => {
  bulkForm.overwriteMode = 'always'
  bulkForm.accommodationMode = 'keep'
  bulkForm.overrideAccommodationDocTabId = ''
  bulkForm.roomNoMode = 'keep'
  bulkForm.roomNo = ''
  bulkForm.roomTypeMode = 'keep'
  bulkForm.roomType = ''
  bulkForm.boardTypeMode = 'keep'
  bulkForm.boardType = ''
  bulkForm.personNoMode = 'keep'
  bulkForm.personNo = ''
}

const validateRowUpdates = (rowUpdates: AccommodationSegmentParticipantRowUpdate[]) => {
  for (const row of rowUpdates) {
    if (row.accommodationMode === 'override' && !normalize(row.overrideAccommodationDocTabId ?? '')) {
      pushToast({ key: 'admin.roomOps.validation.overrideRequired', tone: 'error' })
      return false
    }
  }
  return true
}

const applyChanges = async () => {
  if (!selectedSegmentId.value) {
    return
  }

  const rowUpdates = rows.value
    .map((row) => buildRowUpdate(row.participantId))
    .filter((item): item is AccommodationSegmentParticipantRowUpdate => item !== null)

  if (rowUpdates.length === 0) {
    pushToast({ key: 'admin.roomOps.noDraftChanges', tone: 'info' })
    return
  }

  if (!validateRowUpdates(rowUpdates)) {
    return
  }

  saving.value = true
  try {
    const response = await bulkApplyAccommodationSegmentParticipants(eventId.value, selectedSegmentId.value, {
      rowUpdates,
    })
    pushToast({
      key: 'admin.roomOps.applySuccess',
      params: { count: response.createdCount + response.updatedCount + response.deletedCount },
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
    pushToast(handleApiError(err), 'error')
  } finally {
    saving.value = false
  }
}

const applyBulkChanges = async () => {
  if (!selectedSegmentId.value) {
    return
  }

  if (selectedIds.value.length === 0) {
    pushToast({ key: 'admin.roomOps.bulk.selectRowsFirst', tone: 'info' })
    return
  }

  const hasAnyMutation =
    bulkForm.accommodationMode !== 'keep'
    || bulkForm.roomNoMode !== 'keep'
    || bulkForm.roomTypeMode !== 'keep'
    || bulkForm.boardTypeMode !== 'keep'
    || bulkForm.personNoMode !== 'keep'

  if (!hasAnyMutation) {
    pushToast({ key: 'admin.roomOps.bulk.noValues', tone: 'info' })
    return
  }

  if (bulkForm.accommodationMode === 'override' && !normalize(bulkForm.overrideAccommodationDocTabId)) {
    pushToast({ key: 'admin.roomOps.validation.overrideRequired', tone: 'error' })
    return
  }

  const payload: BulkApplyAccommodationSegmentParticipantsRequest = {
    participantIds: selectedIds.value,
    overwriteMode: bulkForm.overwriteMode,
    accommodationMode: bulkForm.accommodationMode,
    overrideAccommodationDocTabId:
      bulkForm.accommodationMode === 'override' ? bulkForm.overrideAccommodationDocTabId : null,
    roomNoMode: bulkForm.roomNoMode,
    roomNo: bulkForm.roomNoMode === 'set' ? normalize(bulkForm.roomNo) : null,
    roomTypeMode: bulkForm.roomTypeMode,
    roomType: bulkForm.roomTypeMode === 'set' ? normalize(bulkForm.roomType) : null,
    boardTypeMode: bulkForm.boardTypeMode,
    boardType: bulkForm.boardTypeMode === 'set' ? normalize(bulkForm.boardType) : null,
    personNoMode: bulkForm.personNoMode,
    personNo: bulkForm.personNoMode === 'set' ? normalize(bulkForm.personNo) : null,
  }

  saving.value = true
  try {
    const response = await bulkApplyAccommodationSegmentParticipants(eventId.value, selectedSegmentId.value, payload)
    pushToast({
      key: 'admin.roomOps.bulk.applySuccess',
      params: {
        created: response.createdCount,
        updated: response.updatedCount,
        deleted: response.deletedCount,
      },
      tone: 'success',
    })
    if (response.errors.length > 0) {
      pushToast({
        key: 'admin.roomOps.applyPartial',
        params: { count: response.errors.length },
        tone: 'info',
      })
    }
    resetBulkForm()
    await fetchTable()
  } catch (err) {
    pushToast(handleApiError(err), 'error')
  } finally {
    saving.value = false
  }
}

const resetQuickAccommodationForm = () => {
  quickAccommodationForm.title = ''
  quickAccommodationForm.address = ''
  quickAccommodationForm.phone = ''
  quickAccommodationFormError.value = null
}

const openQuickAccommodationModal = () => {
  resetQuickAccommodationForm()
  quickAccommodationModalOpen.value = true
}

const closeQuickAccommodationModal = () => {
  if (quickAccommodationSaving.value) {
    return
  }
  quickAccommodationModalOpen.value = false
  quickAccommodationFormError.value = null
}

const createQuickAccommodation = async () => {
  const title = normalize(quickAccommodationForm.title)
  if (!title) {
    quickAccommodationFormError.value = t('admin.roomOps.segments.quickAddNameRequired')
    return
  }

  const hadAccommodationTabs = hasAccommodationTabs.value
  quickAccommodationSaving.value = true
  quickAccommodationFormError.value = null

  try {
    const created = await apiPost<EventDocTabDto>(`/api/events/${eventId.value}/docs/tabs`, {
      title,
      type: 'Hotel',
      sortOrder: nextDocTabSortOrder.value,
      isActive: true,
      content: {
        hotelName: title,
        address: normalize(quickAccommodationForm.address),
        phone: normalize(quickAccommodationForm.phone),
        checkInDate: '',
        checkOutDate: '',
        checkInNote: '',
        checkOutNote: '',
      },
    })

    quickAccommodationModalOpen.value = false
    pushToast({ key: 'admin.roomOps.segments.quickAddSuccess', tone: 'success' })

    await loadEventContext()

    if (segmentModalOpen.value) {
      segmentForm.defaultAccommodationDocTabId = created.id
    } else if (!hadAccommodationTabs) {
      openCreateSegmentModal(created.id)
    }
  } catch (err) {
    quickAccommodationFormError.value = handleApiError(err)
  } finally {
    quickAccommodationSaving.value = false
  }
}

const openCreateSegmentModal = (defaultAccommodationDocTabId?: string) => {
  segmentModalMode.value = 'create'
  segmentForm.defaultAccommodationDocTabId =
    defaultAccommodationDocTabId
    ?? selectedSegment.value?.defaultAccommodationDocTabId
    ?? accommodationTabs.value[0]?.id
    ?? ''
  segmentForm.startDate = ''
  segmentForm.endDate = ''
  segmentFormError.value = null
  segmentModalOpen.value = true
}

const openEditSegmentModal = (segment: AccommodationSegment) => {
  segmentModalMode.value = 'edit'
  segmentForm.defaultAccommodationDocTabId = segment.defaultAccommodationDocTabId
  segmentForm.startDate = segment.startDate
  segmentForm.endDate = segment.endDate
  segmentFormError.value = null
  selectedSegmentId.value = segment.id
  segmentModalOpen.value = true
}

const saveSegment = async () => {
  if (!normalize(segmentForm.defaultAccommodationDocTabId)) {
    segmentFormError.value = t('admin.roomOps.validation.segmentAccommodationRequired')
    return
  }

  if (!normalize(segmentForm.startDate) || !normalize(segmentForm.endDate)) {
    segmentFormError.value = t('admin.roomOps.validation.segmentDatesRequired')
    return
  }

  if (segmentForm.endDate < segmentForm.startDate) {
    segmentFormError.value = t('admin.roomOps.validation.segmentDateRange')
    return
  }

  segmentSaving.value = true
  segmentFormError.value = null

  try {
    const payload: UpsertAccommodationSegmentRequest = {
      defaultAccommodationDocTabId: segmentForm.defaultAccommodationDocTabId,
      startDate: segmentForm.startDate,
      endDate: segmentForm.endDate,
    }

    const saved =
      segmentModalMode.value === 'create'
        ? await createAccommodationSegment(eventId.value, payload)
        : await updateAccommodationSegment(eventId.value, selectedSegmentId.value, payload)

    await loadEventContext()
    selectedSegmentId.value = saved.id
    segmentModalOpen.value = false
    pushToast({
      key: segmentModalMode.value === 'create' ? 'admin.roomOps.segments.createSuccess' : 'admin.roomOps.segments.updateSuccess',
      tone: 'success',
    })
    await fetchTable()
  } catch (err) {
    segmentFormError.value = handleApiError(err)
  } finally {
    segmentSaving.value = false
  }
}

const confirmDeleteSegment = (segmentId: string) => {
  deletingSegmentId.value = segmentId
}

const deleteSegmentAndReload = async () => {
  if (!deletingSegmentId.value) {
    return
  }

  segmentSaving.value = true
  try {
    await deleteAccommodationSegment(eventId.value, deletingSegmentId.value)
    const deletedSegmentId = deletingSegmentId.value
    deletingSegmentId.value = null
    await loadEventContext()
    if (selectedSegmentId.value === deletedSegmentId) {
      selectedSegmentId.value = segments.value[0]?.id ?? ''
    }
    pushToast({ key: 'admin.roomOps.segments.deleteSuccess', tone: 'success' })
    await fetchTable()
  } catch (err) {
    deletingSegmentId.value = null
    pushToast(handleApiError(err), 'error')
  } finally {
    segmentSaving.value = false
  }
}

const goToPreviousPage = async () => {
  if (!hasPreviousPage.value) {
    return
  }
  page.value -= 1
  await fetchTable()
}

const goToNextPage = async () => {
  if (!hasNextPage.value) {
    return
  }
  page.value += 1
  await fetchTable()
}

watch(selectedSegmentId, async (next, previous) => {
  if (next && next !== previous) {
    page.value = 1
    filters.accommodationFilter = 'all'
    await fetchTable()
  }
})

onMounted(async () => {
  await reload()
})
</script>

<template>
  <div class="mx-auto max-w-7xl space-y-6">
    <div class="flex flex-wrap items-center justify-between gap-3">
      <div>
        <RouterLink
          class="text-sm text-slate-600 underline-offset-2 hover:text-slate-900 hover:underline"
          :to="`/admin/events/${eventId}`"
        >
          {{ t('admin.import.backToEvent') }}
        </RouterLink>
        <h1 class="mt-1 text-2xl font-semibold text-slate-900">{{ pageTitle }}</h1>
        <p class="mt-1 text-sm text-slate-500">{{ t('admin.roomOps.subtitle') }}</p>
      </div>
    </div>

    <LoadingState v-if="loading" message-key="common.loading" />
    <ErrorState v-else-if="errorMessage" :message="errorMessage" @retry="reload" />

    <template v-else>
      <section class="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:p-6">
        <div class="flex flex-wrap items-center justify-between gap-3">
          <div>
            <h2 class="text-lg font-semibold text-slate-900">{{ t('admin.roomOps.segments.title') }}</h2>
            <p class="mt-1 text-sm text-slate-500">{{ t('admin.roomOps.segments.subtitle') }}</p>
          </div>
          <div class="flex flex-wrap items-center gap-2">
            <button
              class="rounded-lg border border-slate-200 bg-white px-4 py-2 text-sm font-medium text-slate-700 hover:border-slate-300"
              type="button"
              @click="openQuickAccommodationModal"
            >
              {{ t('admin.roomOps.segments.quickAddAccommodation') }}
            </button>
            <button
              class="rounded-lg bg-slate-900 px-4 py-2 text-sm font-medium text-white hover:bg-slate-800 disabled:cursor-not-allowed disabled:opacity-60"
              :disabled="!canCreateSegment"
              type="button"
              @click="openCreateSegmentModal()"
            >
              {{ t('admin.roomOps.segments.create') }}
            </button>
            <RouterLink
              class="px-2 py-1 text-sm font-medium text-slate-600 underline-offset-2 hover:text-slate-900 hover:underline"
              :to="`/admin/events/${eventId}/docs/tabs`"
            >
              {{ t('admin.roomOps.segments.manageAccommodations') }}
            </RouterLink>
          </div>
        </div>

        <div class="mt-4 rounded-2xl border border-slate-200 bg-slate-50 px-4 py-3 text-sm text-slate-600">
          <p>{{ t('admin.roomOps.segments.hotelSourceHint') }}</p>
        </div>

        <div v-if="!hasSegments" class="mt-6 rounded-2xl border border-dashed border-slate-300 bg-slate-50 px-5 py-8 text-center">
          <h3 class="text-base font-semibold text-slate-900">
            {{ hasAccommodationTabs ? t('admin.roomOps.segments.emptyTitle') : t('admin.roomOps.segments.emptyNoAccommodationTitle') }}
          </h3>
          <p class="mt-2 text-sm text-slate-500">
            {{ hasAccommodationTabs ? t('admin.roomOps.segments.emptyMessage') : t('admin.roomOps.segments.emptyNoAccommodationMessage') }}
          </p>
          <div class="mt-4 flex flex-wrap items-center justify-center gap-2">
            <button
              class="rounded-lg bg-slate-900 px-4 py-2 text-sm font-medium text-white hover:bg-slate-800"
              type="button"
              @click="hasAccommodationTabs ? openCreateSegmentModal() : openQuickAccommodationModal()"
            >
              {{ hasAccommodationTabs ? t('admin.roomOps.segments.createFirst') : t('admin.roomOps.segments.addAccommodationFirst') }}
            </button>
            <RouterLink
              class="px-2 py-1 text-sm font-medium text-slate-600 underline-offset-2 hover:text-slate-900 hover:underline"
              :to="`/admin/events/${eventId}/docs/tabs`"
            >
              {{ t('admin.roomOps.segments.manageAccommodations') }}
            </RouterLink>
          </div>
        </div>

        <div v-else class="mt-5 space-y-5">
          <div class="grid gap-4 2xl:grid-cols-[minmax(0,1.35fr)_minmax(320px,0.85fr)]">
            <div class="grid gap-3 [grid-template-columns:repeat(auto-fit,minmax(240px,1fr))]">
              <div
                v-for="segment in segments"
                :key="segment.id"
                class="rounded-2xl border p-4 transition"
                :class="
                  segment.id === selectedSegmentId
                    ? 'border-slate-900 bg-slate-900 text-white shadow-md'
                    : 'border-slate-200 bg-white text-slate-900 hover:border-slate-300'
                "
              >
                <button class="w-full text-left" type="button" @click="selectSegment(segment.id)">
                  <div class="min-w-0">
                    <div class="text-sm font-semibold leading-5">{{ segment.defaultAccommodationTitle }}</div>
                    <div class="mt-2 text-xs leading-5" :class="segment.id === selectedSegmentId ? 'text-slate-200' : 'text-slate-500'">
                      {{ formatDateRange(segment.startDate, segment.endDate) }}
                    </div>
                  </div>
                </button>

                <div class="mt-4 flex flex-wrap gap-2">
                  <button
                    class="rounded-md px-2.5 py-1.5 text-[11px] font-medium"
                    :class="segment.id === selectedSegmentId ? 'bg-white/10 text-white hover:bg-white/20' : 'bg-slate-100 text-slate-700 hover:bg-slate-200'"
                    type="button"
                    @click.stop="openEditSegmentModal(segment)"
                  >
                    {{ t('common.edit') }}
                  </button>
                  <button
                    class="rounded-md px-2.5 py-1.5 text-[11px] font-medium"
                    :class="segment.id === selectedSegmentId ? 'bg-rose-500/20 text-white hover:bg-rose-500/30' : 'bg-rose-50 text-rose-700 hover:bg-rose-100'"
                    type="button"
                    @click.stop="confirmDeleteSegment(segment.id)"
                  >
                    {{ t('common.delete') }}
                  </button>
                </div>
              </div>
            </div>

            <div v-if="selectedSegment" class="rounded-2xl border border-slate-200 bg-slate-50 p-4">
              <div class="text-xs font-medium uppercase tracking-wide text-slate-500">
                {{ t('admin.roomOps.segments.selectedSummary') }}
              </div>
              <p class="mt-2 text-xs leading-5 text-slate-500">
                {{ t('admin.roomOps.segments.selectedHint') }}
              </p>
              <div class="mt-3 grid gap-3 sm:grid-cols-2">
                <div>
                  <div class="text-xs text-slate-500">{{ t('admin.roomOps.segments.defaultLabel') }}</div>
                  <div class="mt-1 text-sm font-semibold text-slate-900">{{ selectedSegment.defaultAccommodationTitle }}</div>
                </div>
                <div>
                  <div class="text-xs text-slate-500">{{ t('admin.roomOps.segments.dateLabel') }}</div>
                  <div class="mt-1 text-sm font-semibold text-slate-900">{{ segmentSummaryLabel }}</div>
                </div>
              </div>
            </div>
          </div>
        </div>
      </section>

      <template v-if="selectedSegment">
        <section class="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:p-6">
          <div class="grid gap-4 2xl:grid-cols-[minmax(0,1.1fr)_minmax(360px,0.9fr)]">
            <div class="space-y-4 rounded-2xl border border-slate-200 bg-slate-50 p-4 sm:p-5">
              <div class="flex flex-wrap items-start justify-between gap-3">
                <div>
                  <h3 class="text-sm font-semibold text-slate-900">{{ t('admin.roomOps.filters.title') }}</h3>
                  <p class="mt-1 text-xs text-slate-500">{{ t('admin.roomOps.filters.subtitle') }}</p>
                </div>
                <span class="rounded-full bg-white px-3 py-1 text-xs font-medium text-slate-600">{{ totalLabel }}</span>
              </div>

              <div class="grid gap-3">
                <label class="grid gap-1 text-sm">
                  <span class="text-slate-600">{{ t('admin.roomOps.filters.query') }}</span>
                  <input
                    v-model.trim="searchInput"
                    class="rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
                    :placeholder="t('admin.roomOps.filters.queryPlaceholder')"
                    type="text"
                    @keydown.enter.prevent="runSearch"
                  />
                </label>

                <div class="grid gap-3 md:grid-cols-2">
                  <label class="grid gap-1 text-sm">
                    <span class="text-slate-600">{{ t('admin.roomOps.filters.status') }}</span>
                    <AppCombobox
                      v-model="filters.status"
                      :options="statusOptions"
                      :placeholder="t('admin.roomOps.filters.status')"
                      :aria-label="t('admin.roomOps.filters.status')"
                      :searchable="false"
                      @update:model-value="onStatusChange"
                    />
                  </label>

                  <label class="grid gap-1 text-sm">
                    <span class="text-slate-600">{{ t('admin.roomOps.filters.accommodation') }}</span>
                    <AppCombobox
                      v-model="filters.accommodationFilter"
                      :options="accommodationFilterOptions"
                      :placeholder="t('admin.roomOps.filters.accommodation')"
                      :aria-label="t('admin.roomOps.filters.accommodation')"
                      :searchable="false"
                      @update:model-value="onAccommodationFilterChange"
                    />
                  </label>
                </div>
              </div>

              <div class="flex flex-wrap items-center gap-2">
                <button
                  class="rounded-lg bg-slate-900 px-4 py-2 text-sm font-medium text-white hover:bg-slate-800"
                  type="button"
                  @click="runSearch"
                >
                  {{ t('common.search') }}
                </button>
                <button
                  class="rounded-lg border border-slate-200 bg-white px-4 py-2 text-sm font-medium text-slate-700 hover:border-slate-300"
                  type="button"
                  @click="clearSearch"
                >
                  {{ t('common.clear') }}
                </button>
              </div>
            </div>

            <div class="rounded-2xl border border-slate-200 bg-slate-50 p-4 sm:p-5">
              <div class="flex flex-wrap items-start justify-between gap-3">
                <div>
                  <h3 class="text-sm font-semibold text-slate-900">{{ t('admin.roomOps.bulk.title') }}</h3>
                  <p class="mt-1 text-xs text-slate-500">{{ t('admin.roomOps.bulk.subtitle') }}</p>
                </div>
                <span class="text-xs font-medium text-slate-500">
                  {{ t('admin.roomOps.bulk.selectedCount', { count: selectedIds.length }) }}
                </span>
              </div>

              <div class="mt-4 grid gap-3 xl:grid-cols-2">
                <label class="grid gap-1 text-sm">
                  <span class="text-slate-600">{{ t('admin.roomOps.bulk.overwriteMode') }}</span>
                  <AppCombobox
                    v-model="bulkForm.overwriteMode"
                    :options="overwriteModeOptions"
                    :placeholder="t('admin.roomOps.bulk.overwriteMode')"
                    :aria-label="t('admin.roomOps.bulk.overwriteMode')"
                    :searchable="false"
                  />
                </label>

                <label class="grid gap-1 text-sm">
                  <span class="text-slate-600">{{ t('admin.roomOps.bulk.accommodationModeLabel') }}</span>
                  <AppCombobox
                    v-model="bulkForm.accommodationMode"
                    :options="accommodationActionOptions"
                    :placeholder="t('admin.roomOps.bulk.accommodationModeLabel')"
                    :aria-label="t('admin.roomOps.bulk.accommodationModeLabel')"
                    :searchable="false"
                  />
                </label>

                <label v-if="bulkForm.accommodationMode === 'override'" class="grid gap-1 text-sm sm:col-span-2">
                  <span class="text-slate-600">{{ t('admin.roomOps.bulk.accommodation') }}</span>
                  <AppCombobox
                    v-model="bulkForm.overrideAccommodationDocTabId"
                    :options="segmentAccommodationOptions"
                    :placeholder="t('admin.roomOps.bulk.accommodation')"
                    :aria-label="t('admin.roomOps.bulk.accommodation')"
                  />
                </label>

                <div class="grid gap-3 sm:col-span-2 lg:grid-cols-2">
                  <div class="grid gap-2">
                    <label class="grid gap-1 text-sm">
                      <span class="text-slate-600">{{ t('admin.roomOps.columns.roomNo') }}</span>
                      <AppCombobox
                        v-model="bulkForm.roomNoMode"
                        :options="fieldModeOptions"
                        :placeholder="t('admin.roomOps.columns.roomNo')"
                        :aria-label="t('admin.roomOps.columns.roomNo')"
                        :searchable="false"
                      />
                    </label>
                    <input
                      v-model="bulkForm.roomNo"
                      class="rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
                      :disabled="bulkForm.roomNoMode !== 'set'"
                      type="text"
                    />
                  </div>

                  <div class="grid gap-2">
                    <label class="grid gap-1 text-sm">
                      <span class="text-slate-600">{{ t('admin.roomOps.columns.roomType') }}</span>
                      <AppCombobox
                        v-model="bulkForm.roomTypeMode"
                        :options="fieldModeOptions"
                        :placeholder="t('admin.roomOps.columns.roomType')"
                        :aria-label="t('admin.roomOps.columns.roomType')"
                        :searchable="false"
                      />
                    </label>
                    <input
                      v-model="bulkForm.roomType"
                      class="rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
                      :disabled="bulkForm.roomTypeMode !== 'set'"
                      type="text"
                    />
                  </div>

                  <div class="grid gap-2">
                    <label class="grid gap-1 text-sm">
                      <span class="text-slate-600">{{ t('admin.roomOps.columns.boardType') }}</span>
                      <AppCombobox
                        v-model="bulkForm.boardTypeMode"
                        :options="fieldModeOptions"
                        :placeholder="t('admin.roomOps.columns.boardType')"
                        :aria-label="t('admin.roomOps.columns.boardType')"
                        :searchable="false"
                      />
                    </label>
                    <input
                      v-model="bulkForm.boardType"
                      class="rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
                      :disabled="bulkForm.boardTypeMode !== 'set'"
                      type="text"
                    />
                  </div>

                  <div class="grid gap-2">
                    <label class="grid gap-1 text-sm">
                      <span class="text-slate-600">{{ t('admin.roomOps.columns.personNo') }}</span>
                      <AppCombobox
                        v-model="bulkForm.personNoMode"
                        :options="fieldModeOptions"
                        :placeholder="t('admin.roomOps.columns.personNo')"
                        :aria-label="t('admin.roomOps.columns.personNo')"
                        :searchable="false"
                      />
                    </label>
                    <input
                      v-model="bulkForm.personNo"
                      class="rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
                      :disabled="bulkForm.personNoMode !== 'set'"
                      type="text"
                    />
                  </div>
                </div>
              </div>

              <div class="mt-4 flex flex-wrap items-center gap-2">
                <button
                  class="rounded-lg bg-slate-900 px-4 py-2 text-sm font-medium text-white hover:bg-slate-800 disabled:cursor-not-allowed disabled:opacity-60"
                  :disabled="saving"
                  type="button"
                  @click="applyBulkChanges"
                >
                  {{ t('admin.roomOps.bulk.applyToSelected', { count: selectedIds.length }) }}
                </button>
                <button
                  class="rounded-lg border border-slate-200 bg-white px-4 py-2 text-sm font-medium text-slate-700 hover:border-slate-300"
                  type="button"
                  @click="resetBulkForm"
                >
                  {{ t('common.clear') }}
                </button>
              </div>
            </div>
          </div>
        </section>

        <section class="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:p-6">
          <div class="flex flex-wrap items-center justify-between gap-3">
            <div>
              <h2 class="text-lg font-semibold text-slate-900">{{ t('admin.roomOps.tableTitle') }}</h2>
              <p class="mt-1 text-sm text-slate-500">{{ t('admin.roomOps.changes', { changed: changedRowCount }) }}</p>
            </div>
            <button
              class="rounded-lg bg-emerald-600 px-4 py-2 text-sm font-medium text-white hover:bg-emerald-500 disabled:cursor-not-allowed disabled:opacity-60"
              :disabled="!canApplyDraftChanges"
              type="button"
              @click="applyChanges"
            >
              {{ saving ? t('common.saving') : t('admin.roomOps.applyButton') }}
            </button>
          </div>

          <div v-if="tableLoading" class="mt-4">
            <LoadingState message-key="common.loading" />
          </div>

          <div v-else class="mt-4 overflow-hidden rounded-2xl border border-slate-200">
            <div class="overflow-auto">
              <table class="min-w-full text-sm">
                <thead class="bg-slate-100 text-left text-xs uppercase tracking-wide text-slate-600">
                  <tr>
                    <th class="px-3 py-3">
                      <input
                        type="checkbox"
                        class="h-4 w-4 rounded border-slate-300 text-slate-900 focus:ring-slate-400"
                        :checked="allSelectedOnPage"
                        @change="toggleSelectAll(($event.target as HTMLInputElement).checked)"
                      />
                    </th>
                    <th class="px-3 py-3">{{ t('admin.roomOps.columns.fullName') }}</th>
                    <th class="px-3 py-3">{{ t('admin.roomOps.columns.tcNo') }}</th>
                    <th class="min-w-[240px] px-3 py-3">{{ t('admin.roomOps.columns.accommodation') }}</th>
                    <th class="px-3 py-3">{{ t('admin.roomOps.columns.roomNo') }}</th>
                    <th class="px-3 py-3">{{ t('admin.roomOps.columns.roomType') }}</th>
                    <th class="px-3 py-3">{{ t('admin.roomOps.columns.boardType') }}</th>
                    <th class="px-3 py-3">{{ t('admin.roomOps.columns.personNo') }}</th>
                  </tr>
                </thead>
                <tbody>
                  <tr
                    v-for="row in rows"
                    :key="row.participantId"
                    class="border-t border-slate-200 align-top odd:bg-white even:bg-slate-50"
                  >
                    <td class="px-3 py-3">
                      <input
                        type="checkbox"
                        class="h-4 w-4 rounded border-slate-300 text-slate-900 focus:ring-slate-400"
                        :checked="selectedIds.includes(row.participantId)"
                        @change="toggleRowSelection(row.participantId, ($event.target as HTMLInputElement).checked)"
                      />
                    </td>
                    <td class="px-3 py-3">
                      <div class="font-medium text-slate-900">{{ row.fullName }}</div>
                      <div v-if="row.warnings?.length" class="mt-1 space-y-1">
                        <p
                          v-for="warning in row.warnings"
                          :key="`${row.participantId}-${warning.code}-${warning.roomNo ?? 'none'}`"
                          class="text-xs font-medium text-amber-700"
                        >
                          {{ rowWarningMessage(warning.code, warning.roomNo, warning.assignedCount, warning.declaredCount) }}
                        </p>
                      </div>
                    </td>
                    <td class="px-3 py-3 font-mono text-xs text-slate-700">{{ row.tcNo }}</td>
                    <td class="px-3 py-3">
                      <AppCombobox
                        v-model="draftById[row.participantId]!.accommodationSelection"
                        :options="rowAccommodationOptions"
                        :placeholder="t('admin.roomOps.columns.accommodation')"
                        :aria-label="`${row.fullName} ${t('admin.roomOps.columns.accommodation')}`"
                      />
                    </td>
                    <td class="px-3 py-3">
                      <input
                        v-model="draftById[row.participantId]!.roomNo"
                        class="w-full rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
                        type="text"
                      />
                    </td>
                    <td class="px-3 py-3">
                      <input
                        v-model="draftById[row.participantId]!.roomType"
                        class="w-full rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
                        type="text"
                      />
                    </td>
                    <td class="px-3 py-3">
                      <input
                        v-model="draftById[row.participantId]!.boardType"
                        class="w-full rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
                        type="text"
                      />
                    </td>
                    <td class="px-3 py-3">
                      <input
                        v-model="draftById[row.participantId]!.personNo"
                        class="w-full rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
                        type="text"
                      />
                    </td>
                  </tr>
                  <tr v-if="rows.length === 0">
                    <td colspan="8" class="px-3 py-6 text-center text-sm text-slate-500">
                      {{ t('admin.roomOps.empty') }}
                    </td>
                  </tr>
                </tbody>
              </table>
            </div>

            <div class="flex flex-wrap items-center justify-between gap-3 border-t border-slate-200 bg-slate-50 px-4 py-3 text-sm text-slate-600">
              <span>{{ totalLabel }}</span>
              <div class="flex items-center gap-2">
                <button
                  class="rounded-lg border border-slate-200 bg-white px-3 py-1.5 hover:border-slate-300 disabled:cursor-not-allowed disabled:opacity-50"
                  :disabled="!hasPreviousPage"
                  type="button"
                  @click="goToPreviousPage"
                >
                  {{ t('common.previous') }}
                </button>
                <span>{{ page }}</span>
                <button
                  class="rounded-lg border border-slate-200 bg-white px-3 py-1.5 hover:border-slate-300 disabled:cursor-not-allowed disabled:opacity-50"
                  :disabled="!hasNextPage"
                  type="button"
                  @click="goToNextPage"
                >
                  {{ t('common.next') }}
                </button>
              </div>
            </div>
          </div>
        </section>
      </template>
    </template>

    <AppModalShell :open="segmentModalOpen" @close="segmentModalOpen = false">
      <template #default="{ panelClass }">
        <section :class="[panelClass, 'relative z-10 w-full max-w-lg rounded-3xl bg-white p-6 shadow-2xl']">
          <div class="flex items-start justify-between gap-4">
            <div>
              <h2 class="text-lg font-semibold text-slate-900">
                {{ segmentModalMode === 'create' ? t('admin.roomOps.segments.createTitle') : t('admin.roomOps.segments.editTitle') }}
              </h2>
              <p class="mt-1 text-sm text-slate-500">{{ t('admin.roomOps.segments.modalSubtitle') }}</p>
            </div>
            <button
              class="rounded-lg border border-slate-200 bg-white px-3 py-1.5 text-sm text-slate-700 hover:border-slate-300"
              type="button"
              @click="segmentModalOpen = false"
            >
              {{ t('common.close') }}
            </button>
          </div>

          <div class="mt-5 grid gap-4">
            <label class="grid gap-1 text-sm">
              <span class="text-slate-600">{{ t('admin.roomOps.segments.defaultHotelField') }}</span>
              <AppCombobox
                v-model="segmentForm.defaultAccommodationDocTabId"
                :options="segmentAccommodationOptions"
                :placeholder="t('admin.roomOps.segments.defaultHotelField')"
                :aria-label="t('admin.roomOps.segments.defaultHotelField')"
              />
            </label>

            <div class="rounded-2xl border border-slate-200 bg-slate-50 px-4 py-3 text-sm text-slate-600">
              <p>{{ t('admin.roomOps.segments.modalHint') }}</p>
              <div class="mt-3 flex flex-wrap items-center gap-2">
                <button
                  class="rounded-lg border border-slate-200 bg-white px-3 py-1.5 text-sm font-medium text-slate-700 hover:border-slate-300"
                  type="button"
                  @click="openQuickAccommodationModal"
                >
                  {{ hasAccommodationTabs ? t('admin.roomOps.segments.quickAddAccommodation') : t('admin.roomOps.segments.addAccommodationFirst') }}
                </button>
                <RouterLink
                  class="px-2 py-1 text-sm font-medium text-slate-600 underline-offset-2 hover:text-slate-900 hover:underline"
                  :to="`/admin/events/${eventId}/docs/tabs`"
                >
                  {{ t('admin.roomOps.segments.manageAccommodations') }}
                </RouterLink>
              </div>
            </div>

            <div class="grid gap-4 sm:grid-cols-2">
              <label class="grid gap-1 text-sm">
                <span class="text-slate-600">{{ t('admin.roomOps.segments.startDate') }}</span>
                <input
                  v-model="segmentForm.startDate"
                  class="rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
                  type="date"
                  :min="event?.startDate"
                  :max="event?.endDate"
                />
              </label>

              <label class="grid gap-1 text-sm">
                <span class="text-slate-600">{{ t('admin.roomOps.segments.endDate') }}</span>
                <input
                  v-model="segmentForm.endDate"
                  class="rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
                  type="date"
                  :min="event?.startDate"
                  :max="event?.endDate"
                />
              </label>
            </div>

            <p v-if="segmentFormError" class="text-sm text-rose-600">{{ segmentFormError }}</p>
          </div>

          <div class="mt-6 flex flex-wrap justify-end gap-2">
            <button
              class="rounded-lg border border-slate-200 bg-white px-4 py-2 text-sm font-medium text-slate-700 hover:border-slate-300"
              type="button"
              @click="segmentModalOpen = false"
            >
              {{ t('common.cancel') }}
            </button>
            <button
              class="rounded-lg bg-slate-900 px-4 py-2 text-sm font-medium text-white hover:bg-slate-800 disabled:cursor-not-allowed disabled:opacity-60"
              :disabled="!canCreateSegment"
              type="button"
              @click="saveSegment"
            >
              {{ segmentSaving ? t('common.saving') : t('common.save') }}
            </button>
          </div>
        </section>
      </template>
    </AppModalShell>

    <AppModalShell :open="quickAccommodationModalOpen" @close="closeQuickAccommodationModal">
      <template #default="{ panelClass }">
        <section :class="[panelClass, 'relative z-20 w-full max-w-lg rounded-3xl bg-white p-6 shadow-2xl']">
          <div class="flex items-start justify-between gap-4">
            <div>
              <h2 class="text-lg font-semibold text-slate-900">{{ t('admin.roomOps.segments.quickAddTitle') }}</h2>
              <p class="mt-1 text-sm text-slate-500">{{ t('admin.roomOps.segments.quickAddSubtitle') }}</p>
            </div>
            <button
              class="rounded-lg border border-slate-200 bg-white px-3 py-1.5 text-sm text-slate-700 hover:border-slate-300"
              type="button"
              @click="closeQuickAccommodationModal"
            >
              {{ t('common.close') }}
            </button>
          </div>

          <div class="mt-5 grid gap-4">
            <label class="grid gap-1 text-sm">
              <span class="text-slate-600">{{ t('admin.roomOps.segments.quickAddNameLabel') }}</span>
              <input
                v-model="quickAccommodationForm.title"
                class="rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
                :placeholder="t('admin.roomOps.segments.quickAddNameLabel')"
                type="text"
              />
            </label>

            <label class="grid gap-1 text-sm">
              <span class="text-slate-600">{{ t('admin.roomOps.segments.quickAddAddressLabel') }}</span>
              <input
                v-model="quickAccommodationForm.address"
                class="rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
                :placeholder="t('admin.roomOps.segments.quickAddAddressLabel')"
                type="text"
              />
            </label>

            <label class="grid gap-1 text-sm">
              <span class="text-slate-600">{{ t('admin.roomOps.segments.quickAddPhoneLabel') }}</span>
              <input
                v-model="quickAccommodationForm.phone"
                class="rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
                :placeholder="t('admin.roomOps.segments.quickAddPhoneLabel')"
                type="text"
              />
            </label>

            <p v-if="quickAccommodationFormError" class="text-sm text-rose-600">{{ quickAccommodationFormError }}</p>
          </div>

          <div class="mt-6 flex flex-wrap justify-end gap-2">
            <RouterLink
              class="rounded-lg border border-slate-200 bg-white px-4 py-2 text-sm font-medium text-slate-700 hover:border-slate-300"
              :to="`/admin/events/${eventId}/docs/tabs`"
            >
              {{ t('admin.roomOps.segments.manageAccommodations') }}
            </RouterLink>
            <button
              class="rounded-lg border border-slate-200 bg-white px-4 py-2 text-sm font-medium text-slate-700 hover:border-slate-300"
              type="button"
              @click="closeQuickAccommodationModal"
            >
              {{ t('common.cancel') }}
            </button>
            <button
              class="rounded-lg bg-slate-900 px-4 py-2 text-sm font-medium text-white hover:bg-slate-800 disabled:cursor-not-allowed disabled:opacity-60"
              :disabled="!canSaveQuickAccommodation"
              type="button"
              @click="createQuickAccommodation"
            >
              {{ quickAccommodationSaving ? t('common.saving') : t('common.save') }}
            </button>
          </div>
        </section>
      </template>
    </AppModalShell>

    <ConfirmDialog
      :open="Boolean(deletingSegmentId)"
      :title="t('admin.roomOps.segments.deleteConfirmTitle')"
      :message="t('admin.roomOps.segments.deleteConfirmMessage')"
      :confirm-label="t('common.delete')"
      :cancel-label="t('common.cancel')"
      tone="danger"
      @update:open="(value) => { if (!value) deletingSegmentId = null }"
      @cancel="deletingSegmentId = null"
      @confirm="deleteSegmentAndReload"
    />
  </div>
</template>
