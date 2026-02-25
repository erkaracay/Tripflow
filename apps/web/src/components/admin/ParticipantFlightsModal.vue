<script setup lang="ts">
import { computed, onUnmounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { apiGet, apiPut } from '../../lib/api'
import { formatBaggage, formatDate, formatTime } from '../../lib/formatters'
import { useToast } from '../../lib/toast'
import LoadingState from '../ui/LoadingState.vue'
import type { FlightSegment, ParticipantDetails, ParticipantProfile } from '../../types'

type FlightTab = 'arrival' | 'return'
type ModalMode = 'view' | 'edit'
type LegacyField = { labelKey: string; value: string }

const props = withDefaults(
  defineProps<{
    eventId: string
    participantId: string
    participantName?: string | null
    buttonLabel?: string
    buttonClass?: string
  }>(),
  {
    participantName: null,
    buttonLabel: '',
    buttonClass:
      'rounded-lg bg-slate-900 px-3 py-2 text-sm font-medium text-white hover:bg-slate-800',
  }
)

const emit = defineEmits<{
  (e: 'saved'): void
}>()

const { t } = useI18n()
const { pushToast } = useToast()

const modalOpen = ref(false)
const mode = ref<ModalMode>('view')
const activeTab = ref<FlightTab>('arrival')

const loading = ref(false)
const loadError = ref<string | null>(null)
const saving = ref(false)
const saveError = ref<string | null>(null)

const arrivalSegments = ref<FlightSegment[]>([])
const returnSegments = ref<FlightSegment[]>([])
const draftArrivalSegments = ref<FlightSegment[]>([])
const draftReturnSegments = ref<FlightSegment[]>([])
const participantDetails = ref<ParticipantDetails | null>(null)

const previousBodyOverflow = ref<string | null>(null)

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
      (typeof segment.baggagePieces === 'number' && segment.baggagePieces > 0) ||
      (typeof segment.baggageTotalKg === 'number' && segment.baggageTotalKg > 0)
  )
}

const sortSegments = (segments?: FlightSegment[] | null) =>
  [...(segments ?? [])]
    .filter((segment) => segment && segmentHasValue(segment))
    .sort((a, b) => (a.segmentIndex ?? 0) - (b.segmentIndex ?? 0))

const cloneSegments = (segments: FlightSegment[]) =>
  segments.map((segment, index) => ({
    segmentIndex: index + 1,
    airline: segment.airline ?? '',
    departureAirport: segment.departureAirport ?? '',
    arrivalAirport: segment.arrivalAirport ?? '',
    flightCode: segment.flightCode ?? '',
    departureDate: segment.departureDate ?? '',
    departureTime: segment.departureTime ?? '',
    arrivalDate: segment.arrivalDate ?? '',
    arrivalTime: segment.arrivalTime ?? '',
    pnr: segment.pnr ?? '',
    ticketNo: segment.ticketNo ?? '',
    baggagePieces: segment.baggagePieces ?? null,
    baggageTotalKg: segment.baggageTotalKg ?? null,
  }))

const buttonText = computed(() => props.buttonLabel || t('admin.participant.flights.editButton'))

const readOnlyVisibleSegments = computed(() =>
  activeTab.value === 'arrival' ? arrivalSegments.value : returnSegments.value
)

const editVisibleSegments = computed(() =>
  activeTab.value === 'arrival' ? draftArrivalSegments.value : draftReturnSegments.value
)

const pushLegacyField = (
  target: LegacyField[],
  labelKey: string,
  value: string | null | undefined
) => {
  if (!value) {
    return
  }

  const normalized = value.trim()
  if (!normalized || normalized === '—') {
    return
  }

  target.push({ labelKey, value: normalized })
}

const getLegacyFields = (direction: FlightTab): LegacyField[] => {
  const details = participantDetails.value
  if (!details) {
    return []
  }

  const fields: LegacyField[] = []

  if (direction === 'arrival') {
    pushLegacyField(fields, 'admin.participants.details.arrivalAirline', details.arrivalAirline)
    pushLegacyField(
      fields,
      'admin.participants.details.arrivalDepartureAirport',
      details.arrivalDepartureAirport
    )
    pushLegacyField(
      fields,
      'admin.participants.details.arrivalArrivalAirport',
      details.arrivalArrivalAirport
    )
    pushLegacyField(fields, 'admin.participants.details.arrivalFlightCode', details.arrivalFlightCode)
    pushLegacyField(
      fields,
      'admin.participants.details.arrivalFlightDate',
      formatDate(details.arrivalFlightDate)
    )
    pushLegacyField(
      fields,
      'admin.participants.details.arrivalDepartureTime',
      formatTime(details.arrivalDepartureTime)
    )
    pushLegacyField(
      fields,
      'admin.participants.details.arrivalArrivalTime',
      formatTime(details.arrivalArrivalTime)
    )
    pushLegacyField(fields, 'admin.participants.details.arrivalPnr', details.arrivalPnr)
    pushLegacyField(
      fields,
      'admin.participants.details.arrivalTicketNo',
      details.arrivalTicketNo ?? details.ticketNo
    )
    const arrivalBaggage = formatBaggage(
      details.arrivalBaggagePieces,
      details.arrivalBaggageTotalKg,
      details.arrivalBaggageAllowance
    )
    if (arrivalBaggage !== '—') {
      fields.push({
        labelKey: 'admin.participants.details.arrivalBaggageAllowance',
        value: arrivalBaggage,
      })
    }
    pushLegacyField(fields, 'admin.participants.details.cabinBaggage', details.arrivalCabinBaggage)
    return fields
  }

  pushLegacyField(fields, 'admin.participants.details.returnAirline', details.returnAirline)
  pushLegacyField(
    fields,
    'admin.participants.details.returnDepartureAirport',
    details.returnDepartureAirport
  )
  pushLegacyField(
    fields,
    'admin.participants.details.returnArrivalAirport',
    details.returnArrivalAirport
  )
  pushLegacyField(fields, 'admin.participants.details.returnFlightCode', details.returnFlightCode)
  pushLegacyField(fields, 'admin.participants.details.returnFlightDate', formatDate(details.returnFlightDate))
  pushLegacyField(
    fields,
    'admin.participants.details.returnDepartureTime',
    formatTime(details.returnDepartureTime)
  )
  pushLegacyField(
    fields,
    'admin.participants.details.returnArrivalTime',
    formatTime(details.returnArrivalTime)
  )
  pushLegacyField(fields, 'admin.participants.details.returnPnr', details.returnPnr)
  pushLegacyField(fields, 'admin.participants.details.returnTicketNo', details.returnTicketNo)
  const returnBaggage = formatBaggage(
    details.returnBaggagePieces,
    details.returnBaggageTotalKg,
    details.returnBaggageAllowance
  )
  if (returnBaggage !== '—') {
    fields.push({
      labelKey: 'admin.participants.details.returnBaggageAllowance',
      value: returnBaggage,
    })
  }
  pushLegacyField(fields, 'admin.participants.details.cabinBaggage', details.returnCabinBaggage)
  return fields
}

const legacyVisibleFields = computed(() => getLegacyFields(activeTab.value))

const loadParticipantFlights = async () => {
  loading.value = true
  loadError.value = null
  participantDetails.value = null
  try {
    const participant = await apiGet<ParticipantProfile>(
      `/api/events/${props.eventId}/participants/${props.participantId}`
    )
    participantDetails.value = participant.details ?? null
    arrivalSegments.value = sortSegments(participant.arrivalSegments)
    returnSegments.value = sortSegments(participant.returnSegments)
  } catch (err) {
    loadError.value = err instanceof Error ? err.message : t('errors.generic')
  } finally {
    loading.value = false
  }
}

const openModal = async () => {
  modalOpen.value = true
  mode.value = 'view'
  activeTab.value = 'arrival'
  saveError.value = null
  await loadParticipantFlights()
}

const closeModal = () => {
  if (saving.value) {
    return
  }
  modalOpen.value = false
}

const startEdit = () => {
  draftArrivalSegments.value = cloneSegments(arrivalSegments.value)
  draftReturnSegments.value = cloneSegments(returnSegments.value)
  saveError.value = null
  mode.value = 'edit'
}

const addSegment = () => {
  const target = editVisibleSegments.value
  target.push({
    segmentIndex: target.length + 1,
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
  })
}

const removeSegment = (index: number) => {
  const target = editVisibleSegments.value
  if (index < 0 || index >= target.length) {
    return
  }
  target.splice(index, 1)
}

const trimOptional = (value?: string | null) => {
  if (!value) return null
  const trimmed = value.trim()
  return trimmed.length > 0 ? trimmed : null
}

const normalizeNumber = (value: number | null | undefined) => {
  if (value == null || Number.isNaN(value)) return null
  return Number(value)
}

const buildPayloadSegments = (segments: FlightSegment[], direction: FlightTab) => {
  const normalized: FlightSegment[] = []
  for (const segment of segments) {
    const candidate: FlightSegment = {
      segmentIndex: normalized.length + 1,
      airline: trimOptional(segment.airline),
      departureAirport: trimOptional(segment.departureAirport),
      arrivalAirport: trimOptional(segment.arrivalAirport),
      flightCode: trimOptional(segment.flightCode),
      departureDate: trimOptional(segment.departureDate),
      departureTime: trimOptional(segment.departureTime),
      arrivalDate: trimOptional(segment.arrivalDate),
      arrivalTime: trimOptional(segment.arrivalTime),
      pnr: trimOptional(segment.pnr),
      ticketNo: trimOptional(segment.ticketNo),
      baggagePieces: normalizeNumber(segment.baggagePieces),
      baggageTotalKg: normalizeNumber(segment.baggageTotalKg),
    }

    if (!segmentHasValue(candidate)) {
      continue
    }

    if (
      candidate.baggagePieces != null &&
      (!Number.isInteger(candidate.baggagePieces) || candidate.baggagePieces <= 0)
    ) {
      throw new Error(
        t('admin.participant.flights.validation.baggagePieces', {
          direction: t(`admin.participant.flights.tabs.${direction}`),
        })
      )
    }

    if (
      candidate.baggageTotalKg != null &&
      (!Number.isInteger(candidate.baggageTotalKg) || candidate.baggageTotalKg <= 0)
    ) {
      throw new Error(
        t('admin.participant.flights.validation.baggageTotalKg', {
          direction: t(`admin.participant.flights.tabs.${direction}`),
        })
      )
    }

    normalized.push(candidate)
  }

  return normalized.map((segment, index) => ({ ...segment, segmentIndex: index + 1 }))
}

const saveFlights = async () => {
  if (saving.value) {
    return
  }

  saving.value = true
  saveError.value = null

  try {
    const arrivalPayload = buildPayloadSegments(draftArrivalSegments.value, 'arrival')
    const returnPayload = buildPayloadSegments(draftReturnSegments.value, 'return')

    await apiPut(`/api/events/${props.eventId}/participants/${props.participantId}/flights`, {
      arrivalSegments: arrivalPayload,
      returnSegments: returnPayload,
    })

    await loadParticipantFlights()
    mode.value = 'view'
    modalOpen.value = false
    pushToast({ key: 'admin.participant.flights.saveSuccess', tone: 'success' })
    emit('saved')
  } catch (err) {
    saveError.value = err instanceof Error ? err.message : t('errors.generic')
    pushToast({ key: 'admin.participant.flights.saveError', tone: 'error' })
  } finally {
    saving.value = false
  }
}

watch(modalOpen, (open) => {
  if (open) {
    if (previousBodyOverflow.value === null) {
      previousBodyOverflow.value = document.body.style.overflow
    }
    document.body.style.overflow = 'hidden'
    return
  }

  document.body.style.overflow = previousBodyOverflow.value ?? ''
  previousBodyOverflow.value = null
})

onUnmounted(() => {
  document.body.style.overflow = previousBodyOverflow.value ?? ''
  previousBodyOverflow.value = null
})
</script>

<template>
  <button type="button" :class="props.buttonClass" @click="openModal">
    {{ buttonText }}
  </button>

  <teleport to="body">
    <div
      v-if="modalOpen"
      class="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/50 px-4 py-5"
      role="dialog"
      aria-modal="true"
      :aria-label="t('admin.participant.flights.modalTitle')"
    >
      <div class="flex w-full max-w-4xl max-h-[90vh] flex-col overflow-hidden rounded-2xl bg-white shadow-xl">
        <div class="border-b border-slate-200 px-5 py-4">
          <h3 class="text-lg font-semibold text-slate-900">{{ t('admin.participant.flights.modalTitle') }}</h3>
          <p class="mt-1 text-sm text-slate-500">{{ t('admin.participant.flights.modalSubtitle') }}</p>
          <p v-if="props.participantName" class="mt-1 text-xs text-slate-500">{{ props.participantName }}</p>
        </div>

        <div class="flex flex-wrap items-center gap-2 border-b border-slate-200 px-5 py-3">
          <button
            type="button"
            class="rounded-md px-3 py-1.5 text-sm font-medium transition"
            :class="activeTab === 'arrival' ? 'bg-slate-900 text-white' : 'bg-slate-100 text-slate-700 hover:bg-slate-200'"
            :disabled="saving || loading"
            @click="activeTab = 'arrival'"
          >
            {{ t('admin.participant.flights.tabs.arrival') }}
          </button>
          <button
            type="button"
            class="rounded-md px-3 py-1.5 text-sm font-medium transition"
            :class="activeTab === 'return' ? 'bg-slate-900 text-white' : 'bg-slate-100 text-slate-700 hover:bg-slate-200'"
            :disabled="saving || loading"
            @click="activeTab = 'return'"
          >
            {{ t('admin.participant.flights.tabs.return') }}
          </button>

          <button
            v-if="mode === 'view'"
            type="button"
            class="ml-auto rounded-md border border-slate-200 px-3 py-1.5 text-sm text-slate-700 hover:border-slate-300"
            :disabled="saving || loading || !!loadError"
            @click="startEdit"
          >
            {{ t('common.edit') }}
          </button>
          <button
            v-else
            type="button"
            class="ml-auto rounded-md border border-slate-200 px-3 py-1.5 text-sm text-slate-700 hover:border-slate-300"
            :disabled="saving"
            @click="addSegment"
          >
            {{ t('admin.participant.flights.addSegment') }}
          </button>
        </div>

        <div class="min-h-0 flex-1 overflow-y-auto px-5 py-4">
          <LoadingState v-if="loading" message-key="common.loading" />

          <div v-else-if="loadError" class="rounded-md border border-rose-200 bg-rose-50 px-3 py-2 text-sm text-rose-700">
            <div>{{ loadError }}</div>
            <button
              type="button"
              class="mt-2 rounded border border-rose-300 px-2 py-1 text-xs text-rose-700 hover:border-rose-400"
              :disabled="saving"
              @click="loadParticipantFlights"
            >
              {{ t('common.retry') }}
            </button>
          </div>

          <template v-else-if="mode === 'view'">
            <div
              v-if="readOnlyVisibleSegments.length === 0 && legacyVisibleFields.length === 0"
              class="rounded-lg border border-dashed border-slate-300 px-4 py-3 text-sm text-slate-500"
            >
              {{ t('admin.participant.flights.empty') }}
            </div>
            <div
              v-else-if="readOnlyVisibleSegments.length === 0"
              class="rounded-lg border border-amber-200 bg-amber-50/70 p-4"
            >
              <div class="text-xs font-semibold text-amber-800">
                {{ t('admin.participants.details.legacyFlightsTitle') }}
              </div>
              <p class="mt-1 text-xs text-amber-700">
                {{ t('admin.participants.details.legacyFlightsHint') }}
              </p>
              <div class="mt-3 rounded-md border border-amber-200 bg-white p-3">
                <div class="mb-2 text-xs font-semibold uppercase tracking-wide text-slate-600">
                  {{ t(`admin.participant.flights.tabs.${activeTab}`) }}
                </div>
                <div class="space-y-1 text-sm">
                  <div
                    v-for="field in legacyVisibleFields"
                    :key="`${activeTab}-${field.labelKey}`"
                    class="flex items-start justify-between gap-2"
                  >
                    <span class="text-slate-500">{{ t(field.labelKey) }}</span>
                    <span class="text-right text-slate-900">{{ field.value }}</span>
                  </div>
                </div>
              </div>
            </div>

            <template v-for="(segment, index) in readOnlyVisibleSegments" :key="`view-${activeTab}-${segment.segmentIndex}-${index}`">
              <div class="rounded-xl border border-slate-200 bg-slate-50 p-4">
                <div class="mb-2 text-xs font-semibold uppercase tracking-wide text-slate-500">
                  {{ t('admin.participant.flights.segmentTitle', { index: segment.segmentIndex }) }}
                </div>

                <div class="grid gap-2 text-sm sm:grid-cols-2">
                  <div v-if="segment.airline">
                    <span class="text-slate-500">{{ t('admin.participant.flights.fields.airline') }}:</span>
                    <span class="ml-1 text-slate-900">{{ segment.airline }}</span>
                  </div>
                  <div v-if="segment.flightCode">
                    <span class="text-slate-500">{{ t('admin.participant.flights.fields.flightCode') }}:</span>
                    <span class="ml-1 text-slate-900">{{ segment.flightCode }}</span>
                  </div>
                  <div v-if="segment.departureAirport || segment.arrivalAirport">
                    <span class="text-slate-500">{{ t('admin.participant.flights.fields.route') }}:</span>
                    <span class="ml-1 text-slate-900">
                      {{ [segment.departureAirport, segment.arrivalAirport].filter(Boolean).join(' → ') }}
                    </span>
                  </div>
                  <div v-if="segment.departureDate || segment.departureTime || segment.arrivalDate || segment.arrivalTime">
                    <span class="text-slate-500">{{ t('admin.participant.flights.fields.schedule') }}:</span>
                    <span class="ml-1 text-slate-900">
                      {{
                        [formatDate(segment.departureDate), formatTime(segment.departureTime), formatDate(segment.arrivalDate), formatTime(segment.arrivalTime)]
                          .filter((x) => x && x !== '—')
                          .join(' • ')
                      }}
                    </span>
                  </div>
                  <div v-if="segment.ticketNo">
                    <span class="text-slate-500">{{ t('admin.participant.flights.fields.ticketNo') }}:</span>
                    <span class="ml-1 text-slate-900">{{ segment.ticketNo }}</span>
                  </div>
                  <div v-if="segment.pnr">
                    <span class="text-slate-500">{{ t('admin.participant.flights.fields.pnr') }}:</span>
                    <span class="ml-1 text-slate-900">{{ segment.pnr }}</span>
                  </div>
                  <div v-if="formatBaggage(segment.baggagePieces, segment.baggageTotalKg) !== '—'">
                    <span class="text-slate-500">{{ t('admin.participant.flights.fields.baggage') }}:</span>
                    <span class="ml-1 text-slate-900">{{ formatBaggage(segment.baggagePieces, segment.baggageTotalKg) }}</span>
                  </div>
                </div>
              </div>

              <div v-if="index < readOnlyVisibleSegments.length - 1" class="flex items-center gap-3 py-1 text-xs font-semibold text-slate-500">
                <span aria-hidden="true" class="h-px flex-1 bg-slate-200" />
                <span>{{ t('common.transferDivider') }}</span>
                <span aria-hidden="true" class="h-px flex-1 bg-slate-200" />
              </div>
            </template>
          </template>

          <template v-else>
            <p v-if="saveError" class="mb-3 rounded-md border border-rose-200 bg-rose-50 px-3 py-2 text-sm text-rose-700">
              {{ saveError }}
            </p>

            <div v-if="editVisibleSegments.length === 0" class="rounded-lg border border-dashed border-slate-300 px-4 py-3 text-sm text-slate-500">
              {{ t('admin.participant.flights.emptyEditor') }}
            </div>

            <div v-for="(segment, index) in editVisibleSegments" :key="`edit-${activeTab}-${index}`" class="mb-4 rounded-xl border border-slate-200 p-4">
              <div class="mb-3 flex items-center justify-between">
                <h4 class="text-sm font-semibold text-slate-900">
                  {{ t('admin.participant.flights.segmentTitle', { index: index + 1 }) }}
                </h4>
                <button
                  type="button"
                  class="rounded border border-slate-200 px-2 py-1 text-xs text-slate-600 hover:border-slate-300"
                  :disabled="saving"
                  @click="removeSegment(index)"
                >
                  {{ t('common.remove') }}
                </button>
              </div>

              <div class="grid gap-3 sm:grid-cols-2">
                <label class="grid gap-1 text-sm">
                  <span class="text-slate-600">{{ t('admin.participant.flights.fields.airline') }}</span>
                  <input v-model.trim="segment.airline" type="text" class="rounded border border-slate-200 px-3 py-2 text-sm" :disabled="saving" />
                </label>
                <label class="grid gap-1 text-sm">
                  <span class="text-slate-600">{{ t('admin.participant.flights.fields.flightCode') }}</span>
                  <input v-model.trim="segment.flightCode" type="text" class="rounded border border-slate-200 px-3 py-2 text-sm" :disabled="saving" />
                </label>
                <label class="grid gap-1 text-sm">
                  <span class="text-slate-600">{{ t('admin.participant.flights.fields.departureAirport') }}</span>
                  <input v-model.trim="segment.departureAirport" type="text" class="rounded border border-slate-200 px-3 py-2 text-sm" :disabled="saving" />
                </label>
                <label class="grid gap-1 text-sm">
                  <span class="text-slate-600">{{ t('admin.participant.flights.fields.arrivalAirport') }}</span>
                  <input v-model.trim="segment.arrivalAirport" type="text" class="rounded border border-slate-200 px-3 py-2 text-sm" :disabled="saving" />
                </label>
                <label class="grid gap-1 text-sm">
                  <span class="text-slate-600">{{ t('admin.participant.flights.fields.departureDate') }}</span>
                  <input v-model="segment.departureDate" type="date" class="rounded border border-slate-200 px-3 py-2 text-sm" :disabled="saving" />
                </label>
                <label class="grid gap-1 text-sm">
                  <span class="text-slate-600">{{ t('admin.participant.flights.fields.departureTime') }}</span>
                  <input v-model="segment.departureTime" type="time" class="rounded border border-slate-200 px-3 py-2 text-sm" :disabled="saving" />
                </label>
                <label class="grid gap-1 text-sm">
                  <span class="text-slate-600">{{ t('admin.participant.flights.fields.arrivalDate') }}</span>
                  <input v-model="segment.arrivalDate" type="date" class="rounded border border-slate-200 px-3 py-2 text-sm" :disabled="saving" />
                </label>
                <label class="grid gap-1 text-sm">
                  <span class="text-slate-600">{{ t('admin.participant.flights.fields.arrivalTime') }}</span>
                  <input v-model="segment.arrivalTime" type="time" class="rounded border border-slate-200 px-3 py-2 text-sm" :disabled="saving" />
                </label>
                <label class="grid gap-1 text-sm">
                  <span class="text-slate-600">{{ t('admin.participant.flights.fields.ticketNo') }}</span>
                  <input v-model.trim="segment.ticketNo" type="text" class="rounded border border-slate-200 px-3 py-2 text-sm" :disabled="saving" />
                </label>
                <label class="grid gap-1 text-sm">
                  <span class="text-slate-600">{{ t('admin.participant.flights.fields.pnr') }}</span>
                  <input v-model.trim="segment.pnr" type="text" class="rounded border border-slate-200 px-3 py-2 text-sm" :disabled="saving" />
                </label>
                <label class="grid gap-1 text-sm">
                  <span class="text-slate-600">{{ t('admin.participant.flights.fields.baggagePieces') }}</span>
                  <input v-model.number="segment.baggagePieces" type="number" min="1" step="1" class="rounded border border-slate-200 px-3 py-2 text-sm" :disabled="saving" />
                </label>
                <label class="grid gap-1 text-sm">
                  <span class="text-slate-600">{{ t('admin.participant.flights.fields.baggageTotalKg') }}</span>
                  <input v-model.number="segment.baggageTotalKg" type="number" min="1" step="1" class="rounded border border-slate-200 px-3 py-2 text-sm" :disabled="saving" />
                </label>
              </div>
            </div>
          </template>
        </div>

        <div class="sticky bottom-0 border-t border-slate-200 bg-white px-5 py-3">
          <div class="flex flex-wrap items-center justify-end gap-2">
            <button
              type="button"
              class="rounded-lg border border-slate-200 bg-white px-4 py-2 text-sm font-medium text-slate-700 hover:border-slate-300 disabled:opacity-50"
              :disabled="saving"
              @click="closeModal"
            >
              {{ t('common.cancel') }}
            </button>
            <button
              v-if="mode === 'edit'"
              type="button"
              class="rounded-lg bg-slate-900 px-4 py-2 text-sm font-medium text-white hover:bg-slate-800 disabled:opacity-50"
              :disabled="saving"
              @click="saveFlights"
            >
              {{ saving ? t('common.saving') : t('common.save') }}
            </button>
          </div>
        </div>
      </div>
    </div>
  </teleport>
</template>
