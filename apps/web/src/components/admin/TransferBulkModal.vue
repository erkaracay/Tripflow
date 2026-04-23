<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import AppModalShell from '../ui/AppModalShell.vue'
import AppSegmentedControl from '../ui/AppSegmentedControl.vue'
import { bulkApplyCommonTransfer, bulkMatchTransferSeats } from '../../lib/api'
import { pushToast } from '../../lib/toast'
import { digitsOnly, parsePasteTable } from '../../lib/parsePasteTable'
import type {
  BulkApplyCommonTransferOverwriteMode,
  BulkMatchTransferSeatsEntry,
  BulkTransferCommonLeg,
  Participant,
} from '../../types'

type ParticipantLookup = {
  id: string
  tcNo: string
  fullName: string
}

type BulkTab = 'common' | 'seats'

const props = withDefaults(
  defineProps<{
    open: boolean
    eventId: string
    participants: Participant[] | ParticipantLookup[]
    defaultTab?: BulkTab
  }>(),
  { defaultTab: 'common' }
)

const emit = defineEmits<{
  (e: 'close'): void
  (e: 'applied'): void
}>()

const { t } = useI18n()

const titleId = 'transfer-bulk-modal-title'
const activeTab = ref<BulkTab>(props.defaultTab)

// ───────────── Common fields state ─────────────
const arrivalPickupTime = ref('')
const arrivalPickupPlace = ref('')
const arrivalDropoffPlace = ref('')
const arrivalVehicle = ref('')
const arrivalPlate = ref('')
const arrivalDriverInfo = ref('')
const arrivalNote = ref('')
const returnPickupTime = ref('')
const returnPickupPlace = ref('')
const returnDropoffPlace = ref('')
const returnVehicle = ref('')
const returnPlate = ref('')
const returnDriverInfo = ref('')
const returnNote = ref('')
const overwriteMode = ref<BulkApplyCommonTransferOverwriteMode>('overwrite')
const commonSubmitting = ref(false)
const commonError = ref<string | null>(null)

const trimOrNull = (value: string): string | null => {
  const trimmed = value.trim()
  return trimmed.length > 0 ? trimmed : null
}

const buildLeg = (
  pickupTime: string,
  pickupPlace: string,
  dropoffPlace: string,
  vehicle: string,
  plate: string,
  driverInfo: string,
  note: string
): BulkTransferCommonLeg | null => {
  const leg: BulkTransferCommonLeg = {
    pickupTime: trimOrNull(pickupTime),
    pickupPlace: trimOrNull(pickupPlace),
    dropoffPlace: trimOrNull(dropoffPlace),
    vehicle: trimOrNull(vehicle),
    plate: trimOrNull(plate),
    driverInfo: trimOrNull(driverInfo),
    note: trimOrNull(note),
  }
  const hasAny = Object.values(leg).some((v) => v !== null)
  return hasAny ? leg : null
}

const arrivalLeg = computed(() =>
  buildLeg(
    arrivalPickupTime.value,
    arrivalPickupPlace.value,
    arrivalDropoffPlace.value,
    arrivalVehicle.value,
    arrivalPlate.value,
    arrivalDriverInfo.value,
    arrivalNote.value
  )
)

const returnLeg = computed(() =>
  buildLeg(
    returnPickupTime.value,
    returnPickupPlace.value,
    returnDropoffPlace.value,
    returnVehicle.value,
    returnPlate.value,
    returnDriverInfo.value,
    returnNote.value
  )
)

const hasAnyCommonField = computed(() => arrivalLeg.value !== null || returnLeg.value !== null)

const canSubmitCommon = computed(() => !commonSubmitting.value && hasAnyCommonField.value)

const resetCommon = () => {
  arrivalPickupTime.value = ''
  arrivalPickupPlace.value = ''
  arrivalDropoffPlace.value = ''
  arrivalVehicle.value = ''
  arrivalPlate.value = ''
  arrivalDriverInfo.value = ''
  arrivalNote.value = ''
  returnPickupTime.value = ''
  returnPickupPlace.value = ''
  returnDropoffPlace.value = ''
  returnVehicle.value = ''
  returnPlate.value = ''
  returnDriverInfo.value = ''
  returnNote.value = ''
  overwriteMode.value = 'overwrite'
  commonError.value = null
}

const submitCommon = async () => {
  if (!canSubmitCommon.value) {
    return
  }
  commonError.value = null
  commonSubmitting.value = true
  try {
    const response = await bulkApplyCommonTransfer(props.eventId, {
      arrival: arrivalLeg.value,
      return: returnLeg.value,
      scope: 'all',
      overwriteMode: overwriteMode.value,
    })
    pushToast({
      key: 'admin.events.transferBulk.commonSuccessToast',
      params: { count: response.affectedCount },
      tone: 'success',
    })
    emit('applied')
    emit('close')
  } catch (err) {
    commonError.value = err instanceof Error ? err.message : t('admin.events.transferBulk.genericError')
  } finally {
    commonSubmitting.value = false
  }
}

// ───────────── Seat grid state ─────────────
type SeatRowInitial = {
  arrivalSeatNo: string
  arrivalCompartmentNo: string
  returnSeatNo: string
  returnCompartmentNo: string
}

type SeatRow = SeatRowInitial & {
  id: string
  fullName: string
  tcNo: string
  initial: SeatRowInitial
}

const rows = ref<SeatRow[]>([])
const searchQuery = ref('')
const pasteHelperOpen = ref(false)
const pasteHelperText = ref('')
const pasteFeedback = ref<{ applied: number; unmatched: string[] } | null>(null)
const seatsSubmitting = ref(false)
const seatsError = ref<string | null>(null)

const hydrateRows = () => {
  const seenTcNos = new Set<string>()
  const duplicateTcNos = new Set<string>()
  for (const participant of props.participants) {
    const tc = digitsOnly(participant.tcNo ?? '')
    if (tc.length !== 11) continue
    if (seenTcNos.has(tc)) {
      duplicateTcNos.add(tc)
      continue
    }
    seenTcNos.add(tc)
  }

  const out: SeatRow[] = []
  for (const participant of props.participants) {
    const tc = digitsOnly(participant.tcNo ?? '')
    if (tc.length !== 11) continue
    if (duplicateTcNos.has(tc)) continue

    const details = 'details' in participant ? (participant as Participant).details : null
    const initial: SeatRowInitial = {
      arrivalSeatNo: details?.arrivalTransferSeatNo ?? '',
      arrivalCompartmentNo: details?.arrivalTransferCompartmentNo ?? '',
      returnSeatNo: details?.returnTransferSeatNo ?? '',
      returnCompartmentNo: details?.returnTransferCompartmentNo ?? '',
    }

    out.push({
      id: participant.id,
      fullName: participant.fullName,
      tcNo: tc,
      ...initial,
      initial,
    })
  }

  rows.value = out
}

const filteredRows = computed(() => {
  const q = searchQuery.value.trim().toLowerCase()
  if (!q) return rows.value
  return rows.value.filter(
    (r) => r.fullName.toLowerCase().includes(q) || r.tcNo.includes(q)
  )
})

const dirtyEntries = computed<BulkMatchTransferSeatsEntry[]>(() => {
  const out: BulkMatchTransferSeatsEntry[] = []
  for (const r of rows.value) {
    const arrivalSeat = r.arrivalSeatNo.trim()
    const arrivalComp = r.arrivalCompartmentNo.trim()
    const returnSeat = r.returnSeatNo.trim()
    const returnComp = r.returnCompartmentNo.trim()

    const changed = {
      arrivalSeat: arrivalSeat !== r.initial.arrivalSeatNo.trim(),
      arrivalComp: arrivalComp !== r.initial.arrivalCompartmentNo.trim(),
      returnSeat: returnSeat !== r.initial.returnSeatNo.trim(),
      returnComp: returnComp !== r.initial.returnCompartmentNo.trim(),
    }

    if (!changed.arrivalSeat && !changed.arrivalComp && !changed.returnSeat && !changed.returnComp) {
      continue
    }

    out.push({
      tcNo: r.tcNo,
      arrivalSeatNo: changed.arrivalSeat ? arrivalSeat : null,
      arrivalCompartmentNo: changed.arrivalComp ? arrivalComp : null,
      returnSeatNo: changed.returnSeat ? returnSeat : null,
      returnCompartmentNo: changed.returnComp ? returnComp : null,
    })
  }
  return out
})

const canSubmitSeats = computed(() => !seatsSubmitting.value && dirtyEntries.value.length > 0)

const applyPaste = () => {
  const parsed = parsePasteTable(pasteHelperText.value)
  const rowByTc = new Map(rows.value.map((r) => [r.tcNo, r]))
  let applied = 0
  const unmatched: string[] = []

  for (const parsedCells of parsed) {
    // Seat/compartment values never contain whitespace, so if the row came back
    // as a single cell (single-space separated paste), re-split on any whitespace.
    const cells =
      parsedCells.length === 1 && /\s/.test(parsedCells[0] ?? '')
        ? parsedCells[0]!.split(/\s+/).filter((c) => c.length > 0)
        : parsedCells
    const rawTc = cells[0] ?? ''
    const tc = digitsOnly(rawTc)
    if (tc.length !== 11) {
      unmatched.push(rawTc || '—')
      continue
    }
    const row = rowByTc.get(tc)
    if (!row) {
      unmatched.push(tc)
      continue
    }
    if (cells[1]) row.arrivalSeatNo = cells[1]
    if (cells[2]) row.arrivalCompartmentNo = cells[2]
    if (cells[3]) row.returnSeatNo = cells[3]
    if (cells[4]) row.returnCompartmentNo = cells[4]
    applied++
  }

  pasteFeedback.value = { applied, unmatched }
  if (applied > 0) {
    pushToast({
      key: 'admin.events.transferBulk.seatsPasteAppliedToast',
      params: { count: applied },
      tone: 'success',
    })
    pasteHelperText.value = ''
  }
}

const closePasteHelper = () => {
  pasteHelperOpen.value = false
  pasteHelperText.value = ''
  pasteFeedback.value = null
}

const resetSeats = () => {
  rows.value = []
  searchQuery.value = ''
  pasteHelperOpen.value = false
  pasteHelperText.value = ''
  pasteFeedback.value = null
  seatsError.value = null
}

const submitSeats = async () => {
  if (!canSubmitSeats.value) return
  seatsError.value = null
  seatsSubmitting.value = true
  try {
    const entries = dirtyEntries.value
    const response = await bulkMatchTransferSeats(props.eventId, { entries })
    pushToast({
      key: 'admin.events.transferBulk.seatsSuccessToast',
      params: { count: response.appliedCount },
      tone: 'success',
    })
    emit('applied')
    emit('close')
  } catch (err) {
    seatsError.value = err instanceof Error ? err.message : t('admin.events.transferBulk.genericError')
  } finally {
    seatsSubmitting.value = false
  }
}

const maskTcNo = (tcNo: string) => {
  const tc = digitsOnly(tcNo)
  if (tc.length !== 11) return tcNo
  return `${tc.slice(0, 3)}••••${tc.slice(-2)}`
}

// ───────────── Shared lifecycle ─────────────
const submitting = computed(() => commonSubmitting.value || seatsSubmitting.value)

watch(
  () => props.open,
  (open) => {
    if (open) {
      activeTab.value = props.defaultTab
      commonError.value = null
      seatsError.value = null
      hydrateRows()
    } else {
      resetCommon()
      resetSeats()
    }
  }
)

watch(
  () => props.participants,
  () => {
    if (props.open) hydrateRows()
  }
)

const closeModal = () => {
  if (submitting.value) return
  emit('close')
}

const tabOptions = computed(() => [
  { value: 'common', label: t('admin.events.transferBulk.tabCommon') },
  { value: 'seats', label: t('admin.events.transferBulk.tabSeats') },
])

const handleTabChange = (value: string) => {
  if (value === 'common' || value === 'seats') {
    activeTab.value = value
  }
}
</script>

<template>
  <AppModalShell :open="open" :close-on-overlay="false" content-class="py-6" @close="closeModal">
    <template #default="{ panelClass }">
      <div
        role="dialog"
        aria-modal="true"
        :aria-labelledby="titleId"
        :class="[panelClass, 'w-full max-w-3xl overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-xl']"
      >
        <div class="flex items-start justify-between gap-3 border-b border-slate-200 px-4 py-4 sm:px-5">
          <div>
            <h3 :id="titleId" class="text-lg font-semibold text-slate-900">
              {{ t('admin.events.transferBulk.title') }}
            </h3>
            <p class="mt-1 text-sm text-slate-600">
              {{ t('admin.events.transferBulk.description') }}
            </p>
          </div>
          <button
            type="button"
            class="rounded border border-slate-200 bg-white px-3 py-1.5 text-xs font-medium text-slate-700 hover:border-slate-300"
            :disabled="submitting"
            @click="closeModal"
          >
            {{ t('admin.events.transferBulk.cancel') }}
          </button>
        </div>

        <div class="border-b border-slate-100 px-4 py-3 sm:px-5">
          <AppSegmentedControl
            :model-value="activeTab"
            :options="tabOptions"
            :aria-label="t('admin.events.transferBulk.title')"
            full-width
            @update:model-value="handleTabChange"
          />
        </div>

        <!-- ═════════════ Tab 1: Common fields ═════════════ -->
        <form
          v-if="activeTab === 'common'"
          class="space-y-4 px-4 py-4 sm:px-5 max-h-[70vh] overflow-y-auto"
          @submit.prevent="submitCommon"
        >
          <p class="text-sm text-slate-600">{{ t('admin.events.transferBulk.commonDescription') }}</p>

          <div class="grid gap-4 md:grid-cols-2">
            <fieldset class="grid gap-3 rounded-lg border border-slate-200 px-3 py-3">
              <legend class="px-1 text-xs font-semibold uppercase tracking-wide text-slate-500">
                {{ t('admin.events.transferBulk.arrivalLeg') }}
              </legend>
              <label class="grid gap-1 text-sm">
                <span class="text-slate-600">{{ t('admin.events.transferBulk.pickupTime') }}</span>
                <input v-model="arrivalPickupTime" type="time" class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none" :disabled="commonSubmitting" />
              </label>
              <label class="grid gap-1 text-sm">
                <span class="text-slate-600">{{ t('admin.events.transferBulk.pickupPlace') }}</span>
                <input v-model="arrivalPickupPlace" type="text" maxlength="200" class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none" :disabled="commonSubmitting" />
              </label>
              <label class="grid gap-1 text-sm">
                <span class="text-slate-600">{{ t('admin.events.transferBulk.dropoffPlace') }}</span>
                <input v-model="arrivalDropoffPlace" type="text" maxlength="200" class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none" :disabled="commonSubmitting" />
              </label>
              <label class="grid gap-1 text-sm">
                <span class="text-slate-600">{{ t('admin.events.transferBulk.vehicle') }}</span>
                <input v-model="arrivalVehicle" type="text" maxlength="120" class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none" :disabled="commonSubmitting" />
              </label>
              <label class="grid gap-1 text-sm">
                <span class="text-slate-600">{{ t('admin.events.transferBulk.plate') }}</span>
                <input v-model="arrivalPlate" type="text" maxlength="32" class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none" :disabled="commonSubmitting" />
              </label>
              <label class="grid gap-1 text-sm">
                <span class="text-slate-600">{{ t('admin.events.transferBulk.driverInfo') }}</span>
                <input v-model="arrivalDriverInfo" type="text" maxlength="200" class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none" :disabled="commonSubmitting" />
              </label>
              <label class="grid gap-1 text-sm">
                <span class="text-slate-600">{{ t('admin.events.transferBulk.note') }}</span>
                <input v-model="arrivalNote" type="text" maxlength="400" class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none" :disabled="commonSubmitting" />
              </label>
            </fieldset>

            <fieldset class="grid gap-3 rounded-lg border border-slate-200 px-3 py-3">
              <legend class="px-1 text-xs font-semibold uppercase tracking-wide text-slate-500">
                {{ t('admin.events.transferBulk.returnLeg') }}
              </legend>
              <label class="grid gap-1 text-sm">
                <span class="text-slate-600">{{ t('admin.events.transferBulk.pickupTime') }}</span>
                <input v-model="returnPickupTime" type="time" class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none" :disabled="commonSubmitting" />
              </label>
              <label class="grid gap-1 text-sm">
                <span class="text-slate-600">{{ t('admin.events.transferBulk.pickupPlace') }}</span>
                <input v-model="returnPickupPlace" type="text" maxlength="200" class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none" :disabled="commonSubmitting" />
              </label>
              <label class="grid gap-1 text-sm">
                <span class="text-slate-600">{{ t('admin.events.transferBulk.dropoffPlace') }}</span>
                <input v-model="returnDropoffPlace" type="text" maxlength="200" class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none" :disabled="commonSubmitting" />
              </label>
              <label class="grid gap-1 text-sm">
                <span class="text-slate-600">{{ t('admin.events.transferBulk.vehicle') }}</span>
                <input v-model="returnVehicle" type="text" maxlength="120" class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none" :disabled="commonSubmitting" />
              </label>
              <label class="grid gap-1 text-sm">
                <span class="text-slate-600">{{ t('admin.events.transferBulk.plate') }}</span>
                <input v-model="returnPlate" type="text" maxlength="32" class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none" :disabled="commonSubmitting" />
              </label>
              <label class="grid gap-1 text-sm">
                <span class="text-slate-600">{{ t('admin.events.transferBulk.driverInfo') }}</span>
                <input v-model="returnDriverInfo" type="text" maxlength="200" class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none" :disabled="commonSubmitting" />
              </label>
              <label class="grid gap-1 text-sm">
                <span class="text-slate-600">{{ t('admin.events.transferBulk.note') }}</span>
                <input v-model="returnNote" type="text" maxlength="400" class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none" :disabled="commonSubmitting" />
              </label>
            </fieldset>
          </div>

          <fieldset class="grid gap-2 rounded-lg border border-slate-200 px-3 py-3 text-sm">
            <legend class="px-1 text-xs font-semibold uppercase tracking-wide text-slate-500">
              {{ t('admin.events.transferBulk.overwriteLabel') }}
            </legend>
            <label class="flex items-start gap-2">
              <input v-model="overwriteMode" type="radio" value="overwrite" class="mt-0.5" :disabled="commonSubmitting" />
              <span>{{ t('admin.events.transferBulk.overwriteAll') }}</span>
            </label>
            <label class="flex items-start gap-2">
              <input v-model="overwriteMode" type="radio" value="only_empty" class="mt-0.5" :disabled="commonSubmitting" />
              <span>{{ t('admin.events.transferBulk.overwriteOnlyEmpty') }}</span>
            </label>
          </fieldset>

          <p v-if="commonError" class="rounded border border-rose-200 bg-rose-50 px-3 py-2 text-sm text-rose-700">
            {{ commonError }}
          </p>

          <div class="flex items-center justify-end gap-2 border-t border-slate-100 pt-3">
            <button
              type="button"
              class="rounded border border-slate-200 bg-white px-4 py-2 text-sm font-medium text-slate-700 hover:border-slate-300 disabled:cursor-not-allowed disabled:opacity-50"
              :disabled="commonSubmitting"
              @click="closeModal"
            >
              {{ t('admin.events.transferBulk.cancel') }}
            </button>
            <button
              type="submit"
              class="inline-flex items-center gap-2 rounded-full bg-slate-900 px-4 py-2 text-sm font-semibold text-white hover:bg-slate-800 disabled:cursor-not-allowed disabled:bg-slate-400"
              :disabled="!canSubmitCommon"
            >
              <span
                v-if="commonSubmitting"
                class="h-3 w-3 animate-spin rounded-full border border-white/60 border-t-transparent"
              />
              <span>{{ commonSubmitting ? t('admin.events.transferBulk.submitting') : t('admin.events.transferBulk.commonSubmit') }}</span>
            </button>
          </div>
        </form>

        <!-- ═════════════ Tab 2: Seat grid ═════════════ -->
        <div v-else class="flex flex-col gap-4 px-4 py-4 sm:px-5 max-h-[70vh]">
          <p class="text-sm text-slate-600">{{ t('admin.events.transferBulk.seatsDescription') }}</p>

          <div class="flex flex-col gap-2 sm:flex-row sm:items-center">
            <input
              v-model="searchQuery"
              type="search"
              :placeholder="t('admin.events.transferBulk.seatsSearchPlaceholder')"
              class="min-w-0 flex-1 rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
              :disabled="seatsSubmitting"
            />
            <button
              type="button"
              class="rounded border border-slate-200 bg-white px-3 py-2 text-sm font-medium text-slate-700 hover:border-slate-300 disabled:opacity-50"
              :disabled="seatsSubmitting"
              @click="pasteHelperOpen = !pasteHelperOpen"
            >
              {{ t('admin.events.transferBulk.seatsPasteButton') }}
            </button>
          </div>

          <div
            v-if="pasteHelperOpen"
            class="flex flex-col gap-2 rounded-lg border border-slate-200 bg-slate-50 px-3 py-3"
          >
            <p class="text-xs text-slate-600">{{ t('admin.events.transferBulk.seatsPasteHint') }}</p>
            <textarea
              v-model="pasteHelperText"
              rows="4"
              class="rounded border border-slate-200 bg-white px-2 py-1.5 font-mono text-xs leading-relaxed focus:border-slate-400 focus:outline-none"
              spellcheck="false"
              autocorrect="off"
              autocapitalize="off"
              :disabled="seatsSubmitting"
            />
            <div
              v-if="pasteFeedback && pasteFeedback.unmatched.length > 0"
              class="rounded border border-amber-200 bg-amber-50 px-2 py-1.5 text-xs text-amber-800"
            >
              <p class="font-medium">{{ t('admin.events.transferBulk.seatsPasteUnmatched', { count: pasteFeedback.unmatched.length }) }}</p>
              <p class="mt-0.5 font-mono break-all">{{ pasteFeedback.unmatched.join(', ') }}</p>
            </div>
            <div class="flex items-center justify-end gap-2">
              <button
                type="button"
                class="rounded border border-slate-200 bg-white px-3 py-1.5 text-xs font-medium text-slate-700 hover:border-slate-300"
                @click="closePasteHelper"
              >
                {{ t('admin.events.transferBulk.cancel') }}
              </button>
              <button
                type="button"
                class="rounded-full bg-slate-900 px-3 py-1.5 text-xs font-semibold text-white hover:bg-slate-800 disabled:bg-slate-400"
                :disabled="!pasteHelperText.trim()"
                @click="applyPaste"
              >
                {{ t('admin.events.transferBulk.seatsPasteApply') }}
              </button>
            </div>
          </div>

          <div class="overflow-auto rounded-lg border border-slate-200">
            <table class="min-w-full text-sm">
              <thead class="bg-slate-50 text-xs uppercase tracking-wide text-slate-500">
                <tr>
                  <th class="px-3 py-2 text-left font-semibold">{{ t('admin.events.transferBulk.colParticipant') }}</th>
                  <th class="px-3 py-2 text-left font-semibold">{{ t('admin.events.transferBulk.colArrivalSeat') }}</th>
                  <th class="px-3 py-2 text-left font-semibold">{{ t('admin.events.transferBulk.colArrivalCompartment') }}</th>
                  <th class="px-3 py-2 text-left font-semibold">{{ t('admin.events.transferBulk.colReturnSeat') }}</th>
                  <th class="px-3 py-2 text-left font-semibold">{{ t('admin.events.transferBulk.colReturnCompartment') }}</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="row in filteredRows" :key="row.id" class="border-t border-slate-100">
                  <td class="px-3 py-2">
                    <div class="font-medium text-slate-800">{{ row.fullName }}</div>
                    <div class="font-mono text-[10px] text-slate-400">{{ maskTcNo(row.tcNo) }}</div>
                  </td>
                  <td class="px-2 py-1">
                    <input
                      v-model="row.arrivalSeatNo"
                      type="text"
                      maxlength="16"
                      class="w-20 rounded border border-slate-200 bg-white px-2 py-1 text-sm focus:border-slate-400 focus:outline-none"
                      :disabled="seatsSubmitting"
                    />
                  </td>
                  <td class="px-2 py-1">
                    <input
                      v-model="row.arrivalCompartmentNo"
                      type="text"
                      maxlength="16"
                      class="w-20 rounded border border-slate-200 bg-white px-2 py-1 text-sm focus:border-slate-400 focus:outline-none"
                      :disabled="seatsSubmitting"
                    />
                  </td>
                  <td class="px-2 py-1">
                    <input
                      v-model="row.returnSeatNo"
                      type="text"
                      maxlength="16"
                      class="w-20 rounded border border-slate-200 bg-white px-2 py-1 text-sm focus:border-slate-400 focus:outline-none"
                      :disabled="seatsSubmitting"
                    />
                  </td>
                  <td class="px-2 py-1">
                    <input
                      v-model="row.returnCompartmentNo"
                      type="text"
                      maxlength="16"
                      class="w-20 rounded border border-slate-200 bg-white px-2 py-1 text-sm focus:border-slate-400 focus:outline-none"
                      :disabled="seatsSubmitting"
                    />
                  </td>
                </tr>
                <tr v-if="filteredRows.length === 0">
                  <td colspan="5" class="px-3 py-8 text-center text-sm text-slate-500">
                    {{ t('admin.events.transferBulk.seatsNoMatch') }}
                  </td>
                </tr>
              </tbody>
            </table>
          </div>

          <p v-if="seatsError" class="rounded border border-rose-200 bg-rose-50 px-3 py-2 text-sm text-rose-700">
            {{ seatsError }}
          </p>

          <div class="flex flex-col gap-2 border-t border-slate-100 pt-3 sm:flex-row sm:items-center sm:justify-between">
            <span class="text-sm text-slate-500">
              {{ t('admin.events.transferBulk.dirtyCount', { count: dirtyEntries.length }) }}
            </span>
            <div class="flex items-center justify-end gap-2">
              <button
                type="button"
                class="rounded border border-slate-200 bg-white px-4 py-2 text-sm font-medium text-slate-700 hover:border-slate-300 disabled:cursor-not-allowed disabled:opacity-50"
                :disabled="seatsSubmitting"
                @click="closeModal"
              >
                {{ t('admin.events.transferBulk.cancel') }}
              </button>
              <button
                type="button"
                class="inline-flex items-center gap-2 rounded-full bg-slate-900 px-4 py-2 text-sm font-semibold text-white hover:bg-slate-800 disabled:cursor-not-allowed disabled:bg-slate-400"
                :disabled="!canSubmitSeats"
                @click="submitSeats"
              >
                <span
                  v-if="seatsSubmitting"
                  class="h-3 w-3 animate-spin rounded-full border border-white/60 border-t-transparent"
                />
                <span>
                  {{
                    seatsSubmitting
                      ? t('admin.events.transferBulk.submitting')
                      : t('admin.events.transferBulk.seatsSubmit', { count: dirtyEntries.length })
                  }}
                </span>
              </button>
            </div>
          </div>
        </div>
      </div>
    </template>
  </AppModalShell>
</template>
