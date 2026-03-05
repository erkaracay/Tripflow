<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { useRoute, RouterLink } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { apiDelete, apiGet, apiPost, apiPut } from '../../lib/api'
import { useToast } from '../../lib/toast'
import AppModalShell from '../../components/ui/AppModalShell.vue'
import AppSegmentedControl from '../../components/ui/AppSegmentedControl.vue'
import EventDocPreviewDrawer from '../../components/docs/EventDocPreviewDrawer.vue'
import LoadingState from '../../components/ui/LoadingState.vue'
import ErrorState from '../../components/ui/ErrorState.vue'
import type { Event as TripEvent, EventDocTabDto } from '../../types'

const route = useRoute()
const { t } = useI18n()
const { pushToast } = useToast()

const eventId = computed(() => String(route.params.eventId ?? ''))

const event = ref<TripEvent | null>(null)
const eventError = ref<string | null>(null)

const loading = ref(false)
const errorMessage = ref<string | null>(null)
const tabs = ref<EventDocTabDto[]>([])

const modalOpen = ref(false)
const saving = ref(false)
const editingTab = ref<EventDocTabDto | null>(null)
const updatingId = ref<string | null>(null)
const previewOpen = ref(false)
const previewTab = ref<EventDocTabDto | null>(null)

const form = reactive({
  title: '',
  type: 'Hotel' as 'Hotel' | 'Insurance' | 'Transfer' | 'Custom',
  customType: '',
  sortOrder: 1,
  isActive: true,
  hotelName: '',
  hotelAddress: '',
  hotelPhone: '',
  hotelCheckInDate: '',
  hotelCheckOutDate: '',
  hotelCheckInNote: '',
  hotelCheckOutNote: '',
  transferArrivalPickupTime: '',
  transferArrivalPickupPlace: '',
  transferArrivalDropoffPlace: '',
  transferArrivalVehicle: '',
  transferArrivalPlate: '',
  transferArrivalDriverInfo: '',
  transferArrivalNote: '',
  transferReturnPickupTime: '',
  transferReturnPickupPlace: '',
  transferReturnDropoffPlace: '',
  transferReturnVehicle: '',
  transferReturnPlate: '',
  transferReturnDriverInfo: '',
  transferReturnNote: '',
  customText: '',
  customFields: [] as { id: string; label: string; value: string }[],
})

const formError = ref<string | null>(null)
const showAdvanced = ref(false)
const rawJson = ref('')
const rawJsonError = ref<string | null>(null)
let fieldCounter = 0
const customEditorModeOptions = computed(() => [
  { value: 'form', label: t('admin.docs.customText') },
  { value: 'advanced', label: t('admin.docs.advancedJson') },
])

const resolvedType = computed(() => {
  if (form.type === 'Custom') {
    return form.customType.trim()
  }
  return form.type
})

const nextSortOrder = computed(() =>
  tabs.value.reduce((max, tab) => Math.max(max, tab.sortOrder), 0) + 1
)

const sortedByOrderTabs = computed(() =>
  [...tabs.value].sort((a, b) => {
    if (a.sortOrder !== b.sortOrder) {
      return a.sortOrder - b.sortOrder
    }

    return a.title.localeCompare(b.title)
  })
)

const normalizeDocType = (type: string | null | undefined) => (type ?? '').trim().toLowerCase()
const accommodationAliasSet = new Set(['hotel', 'otel', 'accommodation', 'konaklama'])

const isAccommodationDocType = (type: string | null | undefined) => normalizeDocType(type) === 'hotel'

const isLockedSystemDocType = (type: string | null | undefined) => {
  const normalized = normalizeDocType(type)
  return normalized === 'insurance' || normalized === 'transfer'
}

const isLockedFormType = computed(() => isLockedSystemDocType(form.type))

const accommodationTabs = computed(() =>
  sortedByOrderTabs.value.filter((tab) => isAccommodationDocType(tab.type))
)

const systemTabs = computed(() =>
  sortedByOrderTabs.value.filter((tab) => isLockedSystemDocType(tab.type))
)

const customTabs = computed(() =>
  sortedByOrderTabs.value.filter((tab) => !isAccommodationDocType(tab.type) && !isLockedSystemDocType(tab.type))
)

const systemTabWarnings = computed(() => {
  const warnings: string[] = []
  const insuranceCount = systemTabs.value.filter((tab) => normalizeDocType(tab.type) === 'insurance').length
  const transferCount = systemTabs.value.filter((tab) => normalizeDocType(tab.type) === 'transfer').length

  if (insuranceCount > 1) {
    warnings.push(t('admin.docs.systemTabs.duplicateWarning', { type: t('admin.docs.types.insurance'), count: insuranceCount }))
  }

  if (transferCount > 1) {
    warnings.push(t('admin.docs.systemTabs.duplicateWarning', { type: t('admin.docs.types.transfer'), count: transferCount }))
  }

  return warnings
})

const formatType = (type: string) => {
  const normalized = normalizeDocType(type)
  if (normalized === 'hotel') return t('admin.docs.types.hotel')
  if (normalized === 'insurance') return t('admin.docs.types.insurance')
  if (normalized === 'transfer') return t('admin.docs.types.transfer')
  return type
}

const displayTitle = (tab: EventDocTabDto) => {
  if (!isAccommodationDocType(tab.type)) {
    return tab.title
  }

  const normalizedTitle = normalizeDocType(tab.title)
  return accommodationAliasSet.has(normalizedTitle) ? t('admin.docs.types.hotel') : tab.title
}

const toObject = (content: unknown): Record<string, unknown> => {
  if (!content || typeof content !== 'object' || Array.isArray(content)) {
    return {}
  }
  return content as Record<string, unknown>
}

const readContent = (content: Record<string, unknown>, key: string) => {
  const value = content[key]
  if (typeof value !== 'string') return ''
  return value
}

const tryParseIsoDate = (value: string) => {
  const trimmed = value.trim()
  if (!trimmed) return null
  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(trimmed)
  if (!match) return null
  const year = Number(match[1] ?? 0)
  const month = Number(match[2] ?? 0)
  const day = Number(match[3] ?? 0)
  const parsed = new Date(Date.UTC(year, month - 1, day))
  if (
    Number.isNaN(parsed.getTime())
    || parsed.getUTCFullYear() !== year
    || parsed.getUTCMonth() !== month - 1
    || parsed.getUTCDate() !== day
  ) {
    return null
  }
  return parsed
}

const eventStartDate = computed(() => {
  const value = event.value?.startDate?.trim()
  if (!value) return null
  return tryParseIsoDate(value)
})

const eventEndDate = computed(() => {
  const value = event.value?.endDate?.trim()
  if (!value) return null
  return tryParseIsoDate(value)
})

const isOutsideEventDateRange = (date: Date | null) => {
  if (!date || !eventStartDate.value || !eventEndDate.value) {
    return false
  }

  const dateMs = date.getTime()
  return dateMs < eventStartDate.value.getTime() || dateMs > eventEndDate.value.getTime()
}

const normalizeTransferContent = (content: unknown) => {
  const obj = toObject(content)
  const arrival = toObject(obj.arrival)
  const ret = toObject(obj.return)

  const read = (section: Record<string, unknown>, key: string, fallbackKey: string) =>
    readContent(section, key) || readContent(obj, fallbackKey)

  return {
    arrivalPickupTime: read(arrival, 'pickupTime', 'arrivalPickupTime'),
    arrivalPickupPlace: read(arrival, 'pickupPlace', 'arrivalPickupPlace'),
    arrivalDropoffPlace: read(arrival, 'dropoffPlace', 'arrivalDropoffPlace'),
    arrivalVehicle: read(arrival, 'vehicle', 'arrivalVehicle'),
    arrivalPlate: read(arrival, 'plate', 'arrivalPlate'),
    arrivalDriverInfo: read(arrival, 'driverInfo', 'arrivalDriverInfo'),
    arrivalNote: read(arrival, 'note', 'arrivalNote'),
    returnPickupTime: read(ret, 'pickupTime', 'returnPickupTime'),
    returnPickupPlace: read(ret, 'pickupPlace', 'returnPickupPlace'),
    returnDropoffPlace: read(ret, 'dropoffPlace', 'returnDropoffPlace'),
    returnVehicle: read(ret, 'vehicle', 'returnVehicle'),
    returnPlate: read(ret, 'plate', 'returnPlate'),
    returnDriverInfo: read(ret, 'driverInfo', 'returnDriverInfo'),
    returnNote: read(ret, 'note', 'returnNote'),
  }
}

const normalizeCustomContent = (content: unknown) => {
  const obj = toObject(content)
  const textValue = typeof obj.text === 'string' ? obj.text : ''

  const fields: { id: string; label: string; value: string }[] = []
  const rawFields = obj.fields

  if (Array.isArray(rawFields)) {
    rawFields.forEach((item) => {
      if (!item || typeof item !== 'object') return
      const label = typeof item.label === 'string' ? item.label : ''
      const value = typeof item.value === 'string' ? item.value : item.value?.toString?.() ?? ''
      if (!label && !value) return
      fields.push({ id: `field-${fieldCounter++}`, label, value })
    })
  } else {
    Object.entries(obj)
      .filter(([key]) => !['text', 'html', 'fields'].includes(key))
      .forEach(([key, value]) => {
        if (value === null || value === undefined) return
        const label = key
          .replace(/_/g, ' ')
          .replace(/([a-z0-9])([A-Z])/g, '$1 $2')
          .replace(/\b\w/g, (char) => char.toUpperCase())
        const stringValue = typeof value === 'string' ? value : value.toString?.() ?? ''
        if (!label || !stringValue) return
        fields.push({ id: `field-${fieldCounter++}`, label, value: stringValue })
      })
  }

  return { text: textValue, fields }
}

const ensureDefaultField = () => {
  if (form.customFields.length === 0) {
    form.customFields.push({ id: `field-${fieldCounter++}`, label: '', value: '' })
  }
}

const resetForm = () => {
  form.title = ''
  form.type = 'Hotel'
  form.customType = ''
  form.sortOrder = 1
  form.isActive = true
  form.hotelName = ''
  form.hotelAddress = ''
  form.hotelPhone = ''
  form.hotelCheckInDate = ''
  form.hotelCheckOutDate = ''
  form.hotelCheckInNote = ''
  form.hotelCheckOutNote = ''
  form.transferArrivalPickupTime = ''
  form.transferArrivalPickupPlace = ''
  form.transferArrivalDropoffPlace = ''
  form.transferArrivalVehicle = ''
  form.transferArrivalPlate = ''
  form.transferArrivalDriverInfo = ''
  form.transferArrivalNote = ''
  form.transferReturnPickupTime = ''
  form.transferReturnPickupPlace = ''
  form.transferReturnDropoffPlace = ''
  form.transferReturnVehicle = ''
  form.transferReturnPlate = ''
  form.transferReturnDriverInfo = ''
  form.transferReturnNote = ''
  form.customText = ''
  form.customFields = []
  rawJson.value = ''
  rawJsonError.value = null
  showAdvanced.value = false
  ensureDefaultField()
  syncRawJson()
  formError.value = null
}

const openCreateAccommodation = () => {
  editingTab.value = null
  resetForm()
  form.type = 'Hotel'
  form.title = t('admin.docs.types.hotel')
  form.customType = ''
  form.sortOrder = nextSortOrder.value
  modalOpen.value = true
}

const openCreateCustom = () => {
  editingTab.value = null
  resetForm()
  form.type = 'Custom'
  form.title = ''
  form.customType = 'Custom'
  form.sortOrder = nextSortOrder.value
  modalOpen.value = true
}

const openPreview = (tab: EventDocTabDto) => {
  previewTab.value = tab
  previewOpen.value = true
}

const closePreview = () => {
  previewOpen.value = false
  previewTab.value = null
}

const openEdit = (tab: EventDocTabDto) => {
  editingTab.value = tab
  resetForm()
  form.title = tab.title
  form.sortOrder = tab.sortOrder
  form.isActive = tab.isActive
  const normalized = tab.type.toLowerCase()
  if (normalized === 'hotel') {
    form.type = 'Hotel'
  } else if (normalized === 'insurance') {
    form.type = 'Insurance'
  } else if (normalized === 'transfer') {
    form.type = 'Transfer'
  } else {
    form.type = 'Custom'
    form.customType = tab.type
  }
  const content = toObject(tab.content)
  if (form.type === 'Hotel') {
    form.hotelName = readContent(content, 'hotelName')
    form.hotelAddress = readContent(content, 'address')
    form.hotelPhone = readContent(content, 'phone')
    form.hotelCheckInDate = readContent(content, 'checkInDate')
    form.hotelCheckOutDate = readContent(content, 'checkOutDate')
    form.hotelCheckInNote = readContent(content, 'checkInNote')
    form.hotelCheckOutNote = readContent(content, 'checkOutNote')
  } else if (form.type === 'Transfer') {
    const normalized = normalizeTransferContent(tab.content)
    form.transferArrivalPickupTime = normalized.arrivalPickupTime
    form.transferArrivalPickupPlace = normalized.arrivalPickupPlace
    form.transferArrivalDropoffPlace = normalized.arrivalDropoffPlace
    form.transferArrivalVehicle = normalized.arrivalVehicle
    form.transferArrivalPlate = normalized.arrivalPlate
    form.transferArrivalDriverInfo = normalized.arrivalDriverInfo
    form.transferArrivalNote = normalized.arrivalNote
    form.transferReturnPickupTime = normalized.returnPickupTime
    form.transferReturnPickupPlace = normalized.returnPickupPlace
    form.transferReturnDropoffPlace = normalized.returnDropoffPlace
    form.transferReturnVehicle = normalized.returnVehicle
    form.transferReturnPlate = normalized.returnPlate
    form.transferReturnDriverInfo = normalized.returnDriverInfo
    form.transferReturnNote = normalized.returnNote
  } else {
    const normalized = normalizeCustomContent(tab.content)
    form.customText = normalized.text
    form.customFields = normalized.fields
    ensureDefaultField()
    syncRawJson()
  }
  modalOpen.value = true
}

const addCustomField = () => {
  form.customFields.push({ id: `field-${fieldCounter++}`, label: '', value: '' })
}

const removeCustomField = (id: string) => {
  form.customFields = form.customFields.filter((field) => field.id !== id)
  if (form.customFields.length === 0) {
    ensureDefaultField()
  }
}

const buildCustomContent = () => {
  const text = form.customText.trim()
  const fields = form.customFields
    .map((field) => ({
      label: field.label.trim(),
      value: field.value.trim(),
    }))
    .filter((field) => field.label && field.value)

  const content: Record<string, unknown> = {}
  if (text) {
    content.text = text
  }
  if (fields.length > 0) {
    content.fields = fields
  }
  return content
}

const applyRawJson = () => {
  rawJsonError.value = null
  if (!rawJson.value.trim()) {
    return true
  }
  try {
    const parsed = JSON.parse(rawJson.value)
    if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)) {
      rawJsonError.value = t('admin.docs.validation.jsonInvalid')
      return false
    }
    const normalized = normalizeCustomContent(parsed)
    form.customText = normalized.text
    form.customFields = normalized.fields
    ensureDefaultField()
    return true
  } catch {
    rawJsonError.value = t('admin.docs.validation.jsonInvalid')
    return false
  }
}

const syncRawJson = () => {
  rawJson.value = JSON.stringify(buildCustomContent(), null, 2)
}

const setCustomEditorMode = (mode: string) => {
  showAdvanced.value = mode === 'advanced'
  rawJsonError.value = null
  if (showAdvanced.value) {
    syncRawJson()
  }
}

const buildContent = () => {
  if (form.type === 'Hotel') {
    return {
      hotelName: form.hotelName.trim(),
      address: form.hotelAddress.trim(),
      phone: form.hotelPhone.trim(),
      checkInDate: form.hotelCheckInDate.trim(),
      checkOutDate: form.hotelCheckOutDate.trim(),
      checkInNote: form.hotelCheckInNote.trim(),
      checkOutNote: form.hotelCheckOutNote.trim(),
    }
  }
  if (form.type === 'Insurance') {
    return {}
  }
  if (form.type === 'Transfer') {
    return {
      arrival: {
        pickupTime: form.transferArrivalPickupTime.trim(),
        pickupPlace: form.transferArrivalPickupPlace.trim(),
        dropoffPlace: form.transferArrivalDropoffPlace.trim(),
        vehicle: form.transferArrivalVehicle.trim(),
        plate: form.transferArrivalPlate.trim(),
        driverInfo: form.transferArrivalDriverInfo.trim(),
        note: form.transferArrivalNote.trim(),
      },
      return: {
        pickupTime: form.transferReturnPickupTime.trim(),
        pickupPlace: form.transferReturnPickupPlace.trim(),
        dropoffPlace: form.transferReturnDropoffPlace.trim(),
        vehicle: form.transferReturnVehicle.trim(),
        plate: form.transferReturnPlate.trim(),
        driverInfo: form.transferReturnDriverInfo.trim(),
        note: form.transferReturnNote.trim(),
      },
    }
  }

  try {
    return buildCustomContent()
  } catch {
    return null
  }
}

const validateForm = () => {
  if (!form.title.trim()) {
    formError.value = t('admin.docs.validation.titleRequired')
    return false
  }
  if (!resolvedType.value) {
    formError.value = t('admin.docs.validation.typeRequired')
    return false
  }
  if (form.sortOrder < 1) {
    formError.value = t('admin.docs.validation.sortOrder')
    return false
  }
  if (form.type === 'Hotel') {
    const parsedCheckInDate = form.hotelCheckInDate.trim() ? tryParseIsoDate(form.hotelCheckInDate) : null
    const parsedCheckOutDate = form.hotelCheckOutDate.trim() ? tryParseIsoDate(form.hotelCheckOutDate) : null

    if (form.hotelCheckInDate.trim() && !parsedCheckInDate) {
      formError.value = t('admin.docs.validation.hotelCheckInDateInvalid')
      return false
    }
    if (form.hotelCheckOutDate.trim() && !parsedCheckOutDate) {
      formError.value = t('admin.docs.validation.hotelCheckOutDateInvalid')
      return false
    }

    if (isOutsideEventDateRange(parsedCheckInDate) || isOutsideEventDateRange(parsedCheckOutDate)) {
      formError.value = t('admin.docs.validation.hotelDateOutsideEventRange')
      return false
    }

    if (parsedCheckInDate && parsedCheckOutDate && parsedCheckOutDate.getTime() < parsedCheckInDate.getTime()) {
      formError.value = t('admin.docs.validation.hotelDateRange')
      return false
    }
  }
  if (form.type === 'Custom') {
    if (showAdvanced.value && !applyRawJson()) {
      formError.value = rawJsonError.value
      return false
    }
    const parsed = buildContent()
    if (!parsed) {
      formError.value = t('admin.docs.validation.jsonInvalid')
      return false
    }
  }
  formError.value = null
  return true
}

const fetchEvent = async () => {
  eventError.value = null
  try {
    event.value = await apiGet<TripEvent>(`/api/events/${eventId.value}`)
  } catch (err) {
    eventError.value = err instanceof Error ? err.message : t('errors.generic')
  }
}

const fetchTabs = async () => {
  loading.value = true
  errorMessage.value = null
  try {
    const response = await apiGet<EventDocTabDto[]>(`/api/events/${eventId.value}/docs/tabs`)
    tabs.value = [...response].sort((a, b) => a.sortOrder - b.sortOrder)
  } catch (err) {
    errorMessage.value = err instanceof Error ? err.message : t('errors.generic')
  } finally {
    loading.value = false
  }
}

const saveTab = async () => {
  if (!validateForm()) return
  const content = buildContent()
  if (!content) {
    formError.value = t('admin.docs.validation.jsonInvalid')
    return
  }
  saving.value = true
  try {
    const payload = {
      title: form.title.trim(),
      type: resolvedType.value,
      sortOrder: form.sortOrder,
      isActive: form.isActive,
      content,
    }
    if (editingTab.value) {
      await apiPut<EventDocTabDto>(
        `/api/events/${eventId.value}/docs/tabs/${editingTab.value.id}`,
        payload
      )
    } else {
      await apiPost<EventDocTabDto>(`/api/events/${eventId.value}/docs/tabs`, payload)
    }
    pushToast({ key: 'common.saved', tone: 'success' })
    modalOpen.value = false
    await fetchTabs()
  } catch (err) {
    pushToast({ key: 'errors.generic', tone: 'error' })
  } finally {
    saving.value = false
  }
}

const toggleActive = async (tab: EventDocTabDto) => {
  if (updatingId.value) return
  updatingId.value = tab.id
  const next = !tab.isActive
  const previous = tab.isActive
  tab.isActive = next
  try {
    const updated = await apiPut<EventDocTabDto>(
      `/api/events/${eventId.value}/docs/tabs/${tab.id}`,
      { isActive: next }
    )
    const index = tabs.value.findIndex((item) => item.id === tab.id)
    if (index >= 0) {
      tabs.value[index] = updated
    }
  } catch (err) {
    tab.isActive = previous
    pushToast({ key: 'errors.generic', tone: 'error' })
  } finally {
    updatingId.value = null
  }
}

const deleteTab = async (tab: EventDocTabDto) => {
  const confirmed = window.confirm(t('admin.docs.deleteConfirm'))
  if (!confirmed) return
  try {
    await apiDelete(`/api/events/${eventId.value}/docs/tabs/${tab.id}`)
    pushToast({ key: 'common.saved', tone: 'success' })
    tabs.value = tabs.value.filter((item) => item.id !== tab.id)
  } catch (err) {
    pushToast({ key: 'errors.generic', tone: 'error' })
  }
}

onMounted(() => {
  void fetchEvent()
  void fetchTabs()
})
</script>

<template>
  <div class="mx-auto flex w-full max-w-5xl flex-col gap-6 px-4 py-6 sm:px-6">
    <RouterLink class="text-sm text-slate-500 hover:text-slate-700" :to="`/admin/events/${eventId}`">
      {{ t('admin.logs.backToEvent') }}
    </RouterLink>

    <div class="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
      <div>
        <h1 class="text-2xl font-semibold text-slate-900">{{ t('admin.docs.title') }}</h1>
        <p class="text-sm text-slate-500">{{ event?.name || eventError }}</p>
      </div>
      <div class="flex flex-wrap items-center gap-2">
        <button
          class="inline-flex items-center justify-center rounded-full bg-slate-900 px-4 py-2 text-sm font-semibold text-white hover:bg-slate-800"
          type="button"
          @click="openCreateAccommodation"
        >
          {{ t('admin.docs.accommodations.add') }}
        </button>
        <button
          class="inline-flex items-center justify-center rounded-full border border-slate-300 bg-white px-4 py-2 text-sm font-semibold text-slate-700 hover:border-slate-400"
          type="button"
          @click="openCreateCustom"
        >
          {{ t('admin.docs.customTabs.add') }}
        </button>
      </div>
    </div>

    <LoadingState v-if="loading" message-key="common.loading" />

    <ErrorState
      v-else-if="errorMessage"
      :message="errorMessage"
      @retry="fetchTabs"
    />

    <div v-else class="space-y-4">
      <div v-if="tabs.length === 0" class="rounded-2xl border border-slate-200 bg-white p-6 text-sm text-slate-500 shadow-sm">
        {{ t('admin.docs.empty') }}
      </div>

      <section class="rounded-2xl border border-slate-200 bg-white shadow-sm">
        <header class="flex items-center justify-between border-b border-slate-200 px-4 py-3">
          <h2 class="text-sm font-semibold text-slate-900">{{ t('admin.docs.accommodations.title') }}</h2>
          <span class="text-xs text-slate-500">{{ accommodationTabs.length }} {{ t('common.total') }}</span>
        </header>
        <div v-if="accommodationTabs.length === 0" class="px-4 py-5 text-sm text-slate-500">
          {{ t('admin.docs.accommodations.empty') }}
        </div>
        <div v-else class="divide-y divide-slate-200">
          <div v-for="(tab, index) in accommodationTabs" :key="tab.id" class="flex flex-col gap-3 px-4 py-3 md:flex-row md:items-center md:justify-between">
            <div class="min-w-0">
              <div class="flex flex-wrap items-center gap-2">
                <span class="text-xs font-semibold text-slate-500">#{{ index + 1 }}</span>
                <span class="font-medium text-slate-900">{{ displayTitle(tab) }}</span>
                <span class="rounded-full border border-sky-200 bg-sky-50 px-2 py-0.5 text-[11px] font-medium text-sky-700">
                  {{ t('admin.docs.participantView') }}
                </span>
              </div>
              <div class="mt-1 text-xs text-slate-500">{{ formatType(tab.type) }}</div>
            </div>
            <div class="flex flex-wrap items-center gap-2">
              <label class="inline-flex items-center gap-2 text-xs text-slate-600">
                <input
                  type="checkbox"
                  class="h-4 w-4 rounded border-slate-300"
                  :checked="tab.isActive"
                  :disabled="updatingId === tab.id"
                  @change="toggleActive(tab)"
                />
                {{ tab.isActive ? t('common.show') : t('common.hide') }}
              </label>
              <button class="rounded border border-slate-200 bg-slate-50 px-2.5 py-1 text-xs font-semibold text-slate-700" type="button" @click="openPreview(tab)">
                {{ t('admin.docs.preview') }}
              </button>
              <button class="rounded border border-slate-300 bg-white px-2.5 py-1 text-xs font-semibold text-slate-900" type="button" @click="openEdit(tab)">
                {{ t('common.edit') }}
              </button>
              <button class="rounded border border-rose-200 px-2.5 py-1 text-xs font-semibold text-rose-600" type="button" @click="deleteTab(tab)">
                {{ t('common.delete') }}
              </button>
            </div>
          </div>
        </div>
      </section>

      <section class="rounded-2xl border border-slate-200 bg-white shadow-sm">
        <header class="flex items-center justify-between border-b border-slate-200 px-4 py-3">
          <h2 class="text-sm font-semibold text-slate-900">{{ t('admin.docs.systemTabs.title') }}</h2>
          <span class="text-xs text-slate-500">{{ systemTabs.length }} {{ t('common.total') }}</span>
        </header>
        <div class="px-4 py-3">
          <p class="text-xs text-slate-500">{{ t('admin.docs.systemTabs.helper') }}</p>
          <p v-for="warning in systemTabWarnings" :key="warning" class="mt-2 text-xs text-amber-700">{{ warning }}</p>
        </div>
        <div v-if="systemTabs.length === 0" class="px-4 pb-4 text-sm text-slate-500">
          {{ t('admin.docs.systemTabs.empty') }}
        </div>
        <div v-else class="divide-y divide-slate-200">
          <div v-for="tab in systemTabs" :key="tab.id" class="flex flex-col gap-3 px-4 py-3 md:flex-row md:items-center md:justify-between">
            <div class="min-w-0">
              <div class="flex flex-wrap items-center gap-2">
                <span class="font-medium text-slate-900">{{ displayTitle(tab) }}</span>
                <span class="rounded-full border border-slate-200 bg-slate-100 px-2 py-0.5 text-[11px] font-medium text-slate-600">
                  {{ t('admin.docs.systemTabs.badge') }}
                </span>
              </div>
              <div class="mt-1 text-xs text-slate-500">{{ formatType(tab.type) }}</div>
            </div>
            <div class="flex flex-wrap items-center gap-2">
              <label class="inline-flex items-center gap-2 text-xs text-slate-600">
                <input
                  type="checkbox"
                  class="h-4 w-4 rounded border-slate-300"
                  :checked="tab.isActive"
                  :disabled="updatingId === tab.id"
                  @change="toggleActive(tab)"
                />
                {{ tab.isActive ? t('common.show') : t('common.hide') }}
              </label>
              <button class="rounded border border-slate-200 bg-slate-50 px-2.5 py-1 text-xs font-semibold text-slate-700" type="button" @click="openPreview(tab)">
                {{ t('admin.docs.preview') }}
              </button>
              <button class="rounded border border-slate-300 bg-white px-2.5 py-1 text-xs font-semibold text-slate-900" type="button" @click="openEdit(tab)">
                {{ t('common.edit') }}
              </button>
            </div>
          </div>
        </div>
      </section>

      <section class="rounded-2xl border border-slate-200 bg-white shadow-sm">
        <header class="flex items-center justify-between border-b border-slate-200 px-4 py-3">
          <h2 class="text-sm font-semibold text-slate-900">{{ t('admin.docs.customTabs.title') }}</h2>
          <span class="text-xs text-slate-500">{{ customTabs.length }} {{ t('common.total') }}</span>
        </header>
        <div v-if="customTabs.length === 0" class="px-4 py-5 text-sm text-slate-500">
          {{ t('admin.docs.customTabs.empty') }}
        </div>
        <div v-else class="divide-y divide-slate-200">
          <div v-for="tab in customTabs" :key="tab.id" class="flex flex-col gap-3 px-4 py-3 md:flex-row md:items-center md:justify-between">
            <div class="min-w-0">
              <div class="flex flex-wrap items-center gap-2">
                <span class="text-xs font-semibold text-slate-500">#{{ tab.sortOrder }}</span>
                <span class="font-medium text-slate-900">{{ tab.title }}</span>
              </div>
              <div class="mt-1 text-xs text-slate-500">{{ formatType(tab.type) }}</div>
            </div>
            <div class="flex flex-wrap items-center gap-2">
              <label class="inline-flex items-center gap-2 text-xs text-slate-600">
                <input
                  type="checkbox"
                  class="h-4 w-4 rounded border-slate-300"
                  :checked="tab.isActive"
                  :disabled="updatingId === tab.id"
                  @change="toggleActive(tab)"
                />
                {{ tab.isActive ? t('common.show') : t('common.hide') }}
              </label>
              <button class="rounded border border-slate-200 bg-slate-50 px-2.5 py-1 text-xs font-semibold text-slate-700" type="button" @click="openPreview(tab)">
                {{ t('admin.docs.preview') }}
              </button>
              <button class="rounded border border-slate-300 bg-white px-2.5 py-1 text-xs font-semibold text-slate-900" type="button" @click="openEdit(tab)">
                {{ t('common.edit') }}
              </button>
              <button class="rounded border border-rose-200 px-2.5 py-1 text-xs font-semibold text-rose-600" type="button" @click="deleteTab(tab)">
                {{ t('common.delete') }}
              </button>
            </div>
          </div>
        </div>
      </section>
    </div>
  </div>

  <AppModalShell :open="modalOpen" @close="modalOpen = false">
    <template #default="{ panelClass }">
      <form
        :class="[panelClass, 'flex w-full max-w-2xl max-h-[90vh] flex-col overflow-hidden rounded-2xl bg-white p-5 shadow-xl']"
        @submit.prevent="saveTab"
      >
        <h3 class="text-lg font-semibold text-slate-900">
          {{ editingTab ? t('admin.docs.editTab') : t('admin.docs.newTab') }}
        </h3>
        <div class="mt-4 grid min-h-0 flex-1 gap-3 overflow-y-auto pr-1 md:grid-cols-2">
          <label class="grid gap-1 text-sm md:col-span-2">
            <span class="text-slate-600">{{ t('admin.docs.tabTitle') }}</span>
            <input
              v-model.trim="form.title"
              type="text"
              class="rounded border border-slate-200 px-3 py-2 text-sm"
              :disabled="saving"
            />
          </label>
          <div class="grid gap-1 text-sm">
            <span class="text-slate-600">{{ t('admin.docs.tabType') }}</span>
            <div class="rounded border border-slate-200 bg-slate-50 px-3 py-2 text-sm text-slate-800">
              {{ formatType(resolvedType) }}
            </div>
          </div>
          <label class="grid gap-1 text-sm">
            <span class="text-slate-600">{{ t('admin.docs.sortOrder') }}</span>
            <input
              v-model.number="form.sortOrder"
              type="number"
              min="1"
              class="rounded border border-slate-200 px-3 py-2 text-sm"
              :disabled="saving || isLockedFormType"
            />
          </label>
          <label class="inline-flex items-center gap-2 text-sm text-slate-600 md:col-span-2">
            <input
              v-model="form.isActive"
              type="checkbox"
              class="h-4 w-4 rounded border-slate-300"
              :disabled="saving"
            />
            {{ t('admin.docs.active') }}
          </label>
          <p v-if="isLockedFormType" class="text-xs text-slate-500 md:col-span-2">
            {{ t('admin.docs.systemTabs.badge') }}
          </p>

          <div v-if="form.type === 'Hotel'" class="md:col-span-2 grid gap-3">
            <div class="text-xs font-semibold uppercase tracking-wide text-slate-500">{{ t('admin.docs.hotelSection') }}</div>
            <label class="grid gap-1 text-sm">
              <span class="text-slate-600">{{ t('admin.docs.hotelName') }}</span>
              <input v-model.trim="form.hotelName" type="text" class="rounded border border-slate-200 px-3 py-2 text-sm" />
            </label>
            <label class="grid gap-1 text-sm">
              <span class="text-slate-600">{{ t('admin.docs.hotelAddress') }}</span>
              <input v-model.trim="form.hotelAddress" type="text" class="rounded border border-slate-200 px-3 py-2 text-sm" />
            </label>
            <label class="grid gap-1 text-sm">
              <span class="text-slate-600">{{ t('admin.docs.hotelPhone') }}</span>
              <input v-model.trim="form.hotelPhone" type="text" class="rounded border border-slate-200 px-3 py-2 text-sm" />
            </label>
            <label class="grid gap-1 text-sm">
              <span class="text-slate-600">{{ t('admin.docs.checkInDate') }}</span>
              <input
                v-model="form.hotelCheckInDate"
                type="date"
                class="rounded border border-slate-200 px-3 py-2 text-sm"
                :min="event?.startDate || undefined"
                :max="form.hotelCheckOutDate || event?.endDate || undefined"
              />
            </label>
            <label class="grid gap-1 text-sm">
              <span class="text-slate-600">{{ t('admin.docs.checkOutDate') }}</span>
              <input
                v-model="form.hotelCheckOutDate"
                type="date"
                class="rounded border border-slate-200 px-3 py-2 text-sm"
                :min="form.hotelCheckInDate || event?.startDate || undefined"
                :max="event?.endDate || undefined"
              />
            </label>
            <label class="grid gap-1 text-sm">
              <span class="text-slate-600">{{ t('admin.docs.checkInNote') }}</span>
              <textarea v-model.trim="form.hotelCheckInNote" rows="2" class="rounded border border-slate-200 px-3 py-2 text-sm"></textarea>
            </label>
            <label class="grid gap-1 text-sm">
              <span class="text-slate-600">{{ t('admin.docs.checkOutNote') }}</span>
              <textarea v-model.trim="form.hotelCheckOutNote" rows="2" class="rounded border border-slate-200 px-3 py-2 text-sm"></textarea>
            </label>
          </div>

          <div v-if="form.type === 'Insurance'" class="md:col-span-2 grid gap-2">
            <div class="text-xs font-semibold uppercase tracking-wide text-slate-500">{{ t('admin.docs.insuranceSection') }}</div>
            <p class="text-sm text-slate-600">
              {{ t('admin.docs.insurancePersonalNote') }}
            </p>
          </div>

          <div v-if="form.type === 'Transfer'" class="md:col-span-2 grid gap-4">
            <div class="text-xs font-semibold uppercase tracking-wide text-slate-500">
              {{ t('admin.docs.transferSection') }}
            </div>
            <div class="grid gap-3 rounded-xl border border-slate-200 bg-slate-50 p-3">
              <div class="text-xs font-semibold text-slate-600">{{ t('admin.docs.transferArrivalSection') }}</div>
              <label class="grid gap-1 text-sm">
                <span class="text-slate-600">{{ t('admin.docs.transferPickupTime') }}</span>
                <input v-model.trim="form.transferArrivalPickupTime" type="text" class="rounded border border-slate-200 px-3 py-2 text-sm" />
              </label>
              <label class="grid gap-1 text-sm">
                <span class="text-slate-600">{{ t('admin.docs.transferPickupPlace') }}</span>
                <input v-model.trim="form.transferArrivalPickupPlace" type="text" class="rounded border border-slate-200 px-3 py-2 text-sm" />
              </label>
              <label class="grid gap-1 text-sm">
                <span class="text-slate-600">{{ t('admin.docs.transferDropoffPlace') }}</span>
                <input v-model.trim="form.transferArrivalDropoffPlace" type="text" class="rounded border border-slate-200 px-3 py-2 text-sm" />
              </label>
              <label class="grid gap-1 text-sm">
                <span class="text-slate-600">{{ t('admin.docs.transferVehicle') }}</span>
                <input v-model.trim="form.transferArrivalVehicle" type="text" class="rounded border border-slate-200 px-3 py-2 text-sm" />
              </label>
              <label class="grid gap-1 text-sm">
                <span class="text-slate-600">{{ t('admin.docs.transferPlate') }}</span>
                <input v-model.trim="form.transferArrivalPlate" type="text" class="rounded border border-slate-200 px-3 py-2 text-sm" />
              </label>
              <label class="grid gap-1 text-sm">
                <span class="text-slate-600">{{ t('admin.docs.transferDriver') }}</span>
                <input v-model.trim="form.transferArrivalDriverInfo" type="text" class="rounded border border-slate-200 px-3 py-2 text-sm" />
              </label>
              <label class="grid gap-1 text-sm">
                <span class="text-slate-600">{{ t('admin.docs.transferNote') }}</span>
                <textarea v-model.trim="form.transferArrivalNote" rows="2" class="rounded border border-slate-200 px-3 py-2 text-sm"></textarea>
              </label>
            </div>

            <div class="grid gap-3 rounded-xl border border-slate-200 bg-slate-50 p-3">
              <div class="text-xs font-semibold text-slate-600">{{ t('admin.docs.transferReturnSection') }}</div>
              <label class="grid gap-1 text-sm">
                <span class="text-slate-600">{{ t('admin.docs.transferPickupTime') }}</span>
                <input v-model.trim="form.transferReturnPickupTime" type="text" class="rounded border border-slate-200 px-3 py-2 text-sm" />
              </label>
              <label class="grid gap-1 text-sm">
                <span class="text-slate-600">{{ t('admin.docs.transferPickupPlace') }}</span>
                <input v-model.trim="form.transferReturnPickupPlace" type="text" class="rounded border border-slate-200 px-3 py-2 text-sm" />
              </label>
              <label class="grid gap-1 text-sm">
                <span class="text-slate-600">{{ t('admin.docs.transferDropoffPlace') }}</span>
                <input v-model.trim="form.transferReturnDropoffPlace" type="text" class="rounded border border-slate-200 px-3 py-2 text-sm" />
              </label>
              <label class="grid gap-1 text-sm">
                <span class="text-slate-600">{{ t('admin.docs.transferVehicle') }}</span>
                <input v-model.trim="form.transferReturnVehicle" type="text" class="rounded border border-slate-200 px-3 py-2 text-sm" />
              </label>
              <label class="grid gap-1 text-sm">
                <span class="text-slate-600">{{ t('admin.docs.transferPlate') }}</span>
                <input v-model.trim="form.transferReturnPlate" type="text" class="rounded border border-slate-200 px-3 py-2 text-sm" />
              </label>
              <label class="grid gap-1 text-sm">
                <span class="text-slate-600">{{ t('admin.docs.transferDriver') }}</span>
                <input v-model.trim="form.transferReturnDriverInfo" type="text" class="rounded border border-slate-200 px-3 py-2 text-sm" />
              </label>
              <label class="grid gap-1 text-sm">
                <span class="text-slate-600">{{ t('admin.docs.transferNote') }}</span>
                <textarea v-model.trim="form.transferReturnNote" rows="2" class="rounded border border-slate-200 px-3 py-2 text-sm"></textarea>
              </label>
            </div>
          </div>

          <div v-if="form.type === 'Custom'" class="md:col-span-2 grid gap-4">
            <AppSegmentedControl
              :model-value="showAdvanced ? 'advanced' : 'form'"
              :options="customEditorModeOptions"
              size="sm"
              :aria-label="t('admin.docs.advancedJson')"
              class-name="w-full sm:w-auto"
              @update:model-value="setCustomEditorMode"
            />

            <Transition name="app-section-reveal" mode="out-in">
              <div v-if="!showAdvanced" key="custom-form" class="grid gap-4">
                <div>
                  <div class="text-xs font-semibold uppercase tracking-wide text-slate-500">
                    {{ t('admin.docs.customText') }}
                  </div>
                  <textarea
                    v-model.trim="form.customText"
                    rows="4"
                    class="mt-2 rounded border border-slate-200 px-3 py-2 text-sm"
                    :placeholder="t('admin.docs.customTextPlaceholder')"
                  ></textarea>
                </div>

                <div>
                  <div class="flex items-center justify-between">
                    <div class="text-xs font-semibold uppercase tracking-wide text-slate-500">
                      {{ t('admin.docs.customFields') }}
                    </div>
                    <button
                      type="button"
                      class="rounded border border-slate-200 px-2 py-1 text-xs font-semibold text-slate-600 hover:border-slate-300"
                      @click="addCustomField"
                    >
                      {{ t('admin.docs.addField') }}
                    </button>
                  </div>
                  <div class="mt-2 space-y-2">
                    <div
                      v-for="field in form.customFields"
                      :key="field.id"
                      class="grid gap-2 md:grid-cols-[1fr,1fr,auto]"
                    >
                      <input
                        v-model.trim="field.label"
                        type="text"
                        class="rounded border border-slate-200 px-3 py-2 text-sm"
                        :placeholder="t('admin.docs.fieldLabel')"
                      />
                      <input
                        v-model.trim="field.value"
                        type="text"
                        class="rounded border border-slate-200 px-3 py-2 text-sm"
                        :placeholder="t('admin.docs.fieldValue')"
                      />
                      <button
                        type="button"
                        class="rounded border border-rose-200 px-2 py-1 text-xs font-semibold text-rose-600 hover:border-rose-300"
                        @click="removeCustomField(field.id)"
                      >
                        {{ t('admin.docs.removeField') }}
                      </button>
                    </div>
                  </div>
                </div>
              </div>

              <div v-else key="custom-advanced" class="rounded border border-slate-200 bg-slate-50 p-3">
                <p class="text-xs text-slate-500">{{ t('admin.docs.advancedHint') }}</p>
                <textarea
                  v-model.trim="rawJson"
                  rows="6"
                  class="mt-3 rounded border border-slate-200 px-3 py-2 text-xs font-mono"
                ></textarea>
                <div class="mt-3 flex items-center gap-2">
                  <button
                    type="button"
                    class="rounded border border-slate-200 px-2 py-1 text-xs font-semibold text-slate-600 hover:border-slate-300"
                    @click="applyRawJson"
                  >
                    {{ t('admin.docs.applyJson') }}
                  </button>
                  <span v-if="rawJsonError" class="text-xs text-rose-600">{{ rawJsonError }}</span>
                </div>
              </div>
            </Transition>
          </div>
        </div>

        <p v-if="formError" class="mt-3 text-sm text-rose-600">{{ formError }}</p>

        <div class="mt-6 flex justify-end gap-2">
          <button
            class="rounded border border-slate-200 px-3 py-2 text-sm"
            type="button"
            :disabled="saving"
            @click="modalOpen = false"
          >
            {{ t('common.cancel') }}
          </button>
          <button
            class="rounded bg-slate-900 px-3 py-2 text-sm text-white"
            type="submit"
            :disabled="saving"
          >
            {{ saving ? t('common.saving') : t('common.save') }}
          </button>
        </div>
      </form>
    </template>
  </AppModalShell>

  <EventDocPreviewDrawer
    :open="previewOpen"
    :tab="previewTab"
    :event-title="event?.name ?? null"
    @close="closePreview"
  />
</template>
