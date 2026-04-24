<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import AppSegmentedControl from '../ui/AppSegmentedControl.vue'
import { apiGet, bulkMatchFlightTicket } from '../../lib/api'
import { pushToast } from '../../lib/toast'
import { digitsOnly } from '../../lib/parsePasteTable'
import type {
  BulkMatchFlightTicketDirection,
  BulkMatchFlightTicketOverwriteMode,
  Participant,
} from '../../types'

type ParticipantLookup = {
  id: string
  tcNo: string
  fullName: string
}

const props = defineProps<{
  eventId: string
}>()

const emit = defineEmits<{
  (e: 'applied'): void
  (e: 'close'): void
}>()

const { t } = useI18n()

const direction = ref<BulkMatchFlightTicketDirection>('Arrival')
const overwriteMode = ref<BulkMatchFlightTicketOverwriteMode>('overwrite')
const tcPaste = ref('')
const ticketPaste = ref('')
const submitting = ref(false)
const error = ref<string | null>(null)

const loadingParticipants = ref(false)
const participantsError = ref<string | null>(null)
const participants = ref<ParticipantLookup[]>([])

const serverUnmatchedTcNos = ref<string[]>([])
const serverNoSegmentsTcNos = ref<string[]>([])

defineExpose({ submitting })

const splitLines = (text: string): string[] =>
  text
    .replace(/\r\n/g, '\n')
    .replace(/\r/g, '\n')
    .split('\n')
    .map((line) => line.trim())
    .filter((line) => line.length > 0)

const tcLines = computed(() => splitLines(tcPaste.value))
const ticketLines = computed(() => splitLines(ticketPaste.value))

const participantsByTcNo = computed(() => {
  const map = new Map<string, ParticipantLookup>()
  for (const participant of participants.value) {
    const tc = digitsOnly(participant.tcNo ?? '')
    if (tc.length !== 11) {
      continue
    }
    if (map.has(tc)) {
      map.delete(tc)
      continue
    }
    map.set(tc, participant)
  }
  return map
})

type MatchedRow = {
  lineNumber: number
  tcNo: string
  ticketNo: string
  matched: ParticipantLookup
}

type UnmatchedReason =
  | 'invalid_tc'
  | 'empty_ticket'
  | 'duplicate_tc'
  | 'no_participant'
  | 'no_segments'

type UnmatchedRow = {
  lineNumber: number | null
  tcNo: string
  ticketNo: string
  reason: UnmatchedReason
}

const ticketPairs = computed(() => {
  const matched: MatchedRow[] = []
  const unmatched: UnmatchedRow[] = []
  const seen = new Set<string>()

  const pairCount = Math.min(tcLines.value.length, ticketLines.value.length)

  for (let i = 0; i < pairCount; i++) {
    const rawTc = tcLines.value[i] ?? ''
    const rawTicket = ticketLines.value[i] ?? ''
    const tcNo = digitsOnly(rawTc)
    const ticketNo = rawTicket.trim()

    if (tcNo.length !== 11) {
      unmatched.push({ lineNumber: i + 1, tcNo: rawTc, ticketNo, reason: 'invalid_tc' })
      continue
    }
    if (ticketNo.length === 0) {
      unmatched.push({ lineNumber: i + 1, tcNo, ticketNo, reason: 'empty_ticket' })
      continue
    }
    if (seen.has(tcNo)) {
      unmatched.push({ lineNumber: i + 1, tcNo, ticketNo, reason: 'duplicate_tc' })
      continue
    }
    const participant = participantsByTcNo.value.get(tcNo)
    if (!participant) {
      unmatched.push({ lineNumber: i + 1, tcNo, ticketNo, reason: 'no_participant' })
      continue
    }

    seen.add(tcNo)
    matched.push({ lineNumber: i + 1, tcNo, ticketNo, matched: participant })
  }

  return { matched, unmatched }
})

const hasLengthMismatch = computed(
  () =>
    (tcLines.value.length > 0 || ticketLines.value.length > 0)
    && tcLines.value.length !== ticketLines.value.length
)

const canSubmit = computed(
  () => !submitting.value && !loadingParticipants.value && ticketPairs.value.matched.length > 0
)

const directionOptions = computed(() => [
  { value: 'Arrival', label: t('admin.flightPanelHelper.ticketMatch.directionArrival') },
  { value: 'Return', label: t('admin.flightPanelHelper.ticketMatch.directionReturn') },
])

const handleDirectionChange = (value: string) => {
  if (value === 'Arrival' || value === 'Return') {
    direction.value = value
  }
}

const maskTcNo = (tcNo: string) => {
  const tc = digitsOnly(tcNo)
  if (tc.length !== 11) {
    return tcNo
  }
  return `${tc.slice(0, 3)}••••${tc.slice(-2)}`
}

const unmatchedReasonLabel = (reason: UnmatchedReason) => {
  if (reason === 'invalid_tc') return t('admin.flightPanelHelper.ticketMatch.unmatchedInvalidTc')
  if (reason === 'empty_ticket') return t('admin.flightPanelHelper.ticketMatch.unmatchedEmptyTicket')
  if (reason === 'duplicate_tc') return t('admin.flightPanelHelper.ticketMatch.unmatchedDuplicateTc')
  if (reason === 'no_segments') return t('admin.flightPanelHelper.ticketMatch.unmatchedNoSegments')
  return t('admin.flightPanelHelper.ticketMatch.unmatchedNoParticipant')
}

const fetchAllParticipants = async () => {
  loadingParticipants.value = true
  participantsError.value = null
  try {
    const list = await apiGet<Participant[]>(`/api/events/${props.eventId}/participants`)
    participants.value = list.map((item) => ({
      id: item.id,
      tcNo: item.tcNo,
      fullName: item.fullName,
    }))
  } catch (err) {
    participantsError.value = err instanceof Error ? err.message : t('errors.generic')
  } finally {
    loadingParticipants.value = false
  }
}

const mergedUnmatched = computed<UnmatchedRow[]>(() => {
  const localRows = ticketPairs.value.unmatched
  if (serverUnmatchedTcNos.value.length === 0 && serverNoSegmentsTcNos.value.length === 0) {
    return localRows
  }
  const rows: UnmatchedRow[] = [...localRows]
  const matchedByTc = new Map<string, MatchedRow>(
    ticketPairs.value.matched.map((row) => [row.tcNo, row])
  )
  for (const tc of serverUnmatchedTcNos.value) {
    const row = matchedByTc.get(tc)
    rows.push({
      lineNumber: row?.lineNumber ?? null,
      tcNo: tc,
      ticketNo: row?.ticketNo ?? '',
      reason: 'no_participant',
    })
  }
  for (const tc of serverNoSegmentsTcNos.value) {
    const row = matchedByTc.get(tc)
    rows.push({
      lineNumber: row?.lineNumber ?? null,
      tcNo: tc,
      ticketNo: row?.ticketNo ?? '',
      reason: 'no_segments',
    })
  }
  return rows
})

const submit = async () => {
  if (!canSubmit.value) {
    return
  }
  error.value = null
  serverUnmatchedTcNos.value = []
  serverNoSegmentsTcNos.value = []
  submitting.value = true
  try {
    const response = await bulkMatchFlightTicket(props.eventId, {
      direction: direction.value,
      overwriteMode: overwriteMode.value,
      entries: ticketPairs.value.matched.map((row) => ({
        tcNo: row.tcNo,
        ticketNo: row.ticketNo,
      })),
    })
    serverUnmatchedTcNos.value = response.unmatchedTcNos ?? []
    serverNoSegmentsTcNos.value = response.noSegmentsTcNos ?? []
    pushToast({
      key: 'admin.flightPanelHelper.ticketMatch.successToast',
      params: {
        participants: response.appliedParticipantCount,
        segments: response.appliedSegmentCount,
      },
      tone: 'success',
    })
    emit('applied')
    if (serverUnmatchedTcNos.value.length === 0 && serverNoSegmentsTcNos.value.length === 0) {
      tcPaste.value = ''
      ticketPaste.value = ''
      emit('close')
    } else {
      tcPaste.value = ''
      ticketPaste.value = ''
    }
  } catch (err) {
    error.value = err instanceof Error ? err.message : t('admin.flightPanelHelper.ticketMatch.genericError')
  } finally {
    submitting.value = false
  }
}

onMounted(() => {
  void fetchAllParticipants()
})
</script>

<template>
  <div class="space-y-4">
    <p class="text-sm text-slate-600">
      {{ t('admin.flightPanelHelper.ticketMatch.description') }}
    </p>

    <div class="grid gap-3 sm:grid-cols-2">
      <div>
        <div class="mb-1 text-xs font-semibold uppercase tracking-wide text-slate-500">
          {{ t('admin.flightPanelHelper.ticketMatch.directionLabel') }}
        </div>
        <AppSegmentedControl
          :model-value="direction"
          :options="directionOptions"
          size="sm"
          full-width
          :aria-label="t('admin.flightPanelHelper.ticketMatch.directionLabel')"
          @update:model-value="handleDirectionChange"
        />
      </div>
      <fieldset class="grid gap-1 rounded-lg border border-slate-200 px-3 py-2 text-sm">
        <legend class="px-1 text-xs font-semibold uppercase tracking-wide text-slate-500">
          {{ t('admin.flightPanelHelper.ticketMatch.overwriteLabel') }}
        </legend>
        <label class="flex items-start gap-2">
          <input
            v-model="overwriteMode"
            type="radio"
            value="overwrite"
            class="mt-0.5"
            :disabled="submitting"
          />
          <span>{{ t('admin.flightPanelHelper.ticketMatch.overwriteAll') }}</span>
        </label>
        <label class="flex items-start gap-2">
          <input
            v-model="overwriteMode"
            type="radio"
            value="only_empty"
            class="mt-0.5"
            :disabled="submitting"
          />
          <span>{{ t('admin.flightPanelHelper.ticketMatch.overwriteOnlyEmpty') }}</span>
        </label>
      </fieldset>
    </div>

    <div class="grid gap-3 sm:grid-cols-2">
      <label class="grid gap-1 text-sm">
        <span class="flex items-center justify-between">
          <span class="font-medium text-slate-700">{{ t('admin.flightPanelHelper.ticketMatch.tcLabel') }}</span>
          <span class="text-xs text-slate-400">{{ tcLines.length }}</span>
        </span>
        <textarea
          v-model="tcPaste"
          rows="8"
          class="rounded border border-slate-200 bg-slate-50 px-3 py-2 font-mono text-xs leading-relaxed focus:border-slate-400 focus:bg-white focus:outline-none"
          :placeholder="t('admin.flightPanelHelper.ticketMatch.tcPlaceholder')"
          spellcheck="false"
          autocorrect="off"
          autocapitalize="off"
          :disabled="submitting"
        />
      </label>
      <label class="grid gap-1 text-sm">
        <span class="flex items-center justify-between">
          <span class="font-medium text-slate-700">{{ t('admin.flightPanelHelper.ticketMatch.ticketLabel') }}</span>
          <span class="text-xs text-slate-400">{{ ticketLines.length }}</span>
        </span>
        <textarea
          v-model="ticketPaste"
          rows="8"
          class="rounded border border-slate-200 bg-slate-50 px-3 py-2 font-mono text-xs leading-relaxed focus:border-slate-400 focus:bg-white focus:outline-none"
          :placeholder="t('admin.flightPanelHelper.ticketMatch.ticketPlaceholder')"
          spellcheck="false"
          autocorrect="off"
          autocapitalize="off"
          :disabled="submitting"
        />
      </label>
    </div>

    <div
      v-if="hasLengthMismatch"
      class="rounded border border-amber-200 bg-amber-50 px-3 py-2 text-xs text-amber-800"
    >
      {{
        t('admin.flightPanelHelper.ticketMatch.lengthMismatchWarning', {
          tc: tcLines.length,
          ticket: ticketLines.length,
        })
      }}
    </div>

    <div v-if="loadingParticipants" class="text-xs text-slate-500">
      {{ t('common.loading') }}
    </div>
    <div
      v-else-if="participantsError"
      class="rounded border border-rose-200 bg-rose-50 px-3 py-2 text-sm text-rose-700"
    >
      {{ participantsError }}
    </div>
    <div v-else class="text-xs text-slate-500">
      {{ t('admin.flightPanelHelper.ticketMatch.participantsLoaded', { count: participants.length }) }}
    </div>

    <div
      v-if="ticketPairs.matched.length > 0 || mergedUnmatched.length > 0"
      class="flex flex-wrap items-center gap-2 text-xs"
    >
      <span class="inline-flex items-center gap-1 rounded-full bg-emerald-50 px-2.5 py-1 font-medium text-emerald-700">
        ✓ {{ t('admin.flightPanelHelper.ticketMatch.matchedCount', { count: ticketPairs.matched.length }) }}
      </span>
      <span
        v-if="mergedUnmatched.length > 0"
        class="inline-flex items-center gap-1 rounded-full bg-amber-50 px-2.5 py-1 font-medium text-amber-700"
      >
        ⚠ {{ t('admin.flightPanelHelper.ticketMatch.unmatchedCount', { count: mergedUnmatched.length }) }}
      </span>
    </div>

    <div
      v-if="mergedUnmatched.length > 0"
      class="rounded border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-800"
    >
      <p class="font-medium">{{ t('admin.flightPanelHelper.ticketMatch.unmatchedWarning') }}</p>
      <ul class="mt-1 list-disc space-y-0.5 pl-5 font-mono text-xs">
        <li
          v-for="(entry, index) in mergedUnmatched"
          :key="`${entry.lineNumber ?? 'srv'}-${entry.tcNo}-${index}`"
        >
          <span v-if="entry.lineNumber !== null" class="text-amber-600">#{{ entry.lineNumber }}</span>
          {{ entry.tcNo || '—' }} — {{ entry.ticketNo || '—' }}
          <span class="text-amber-600/80">({{ unmatchedReasonLabel(entry.reason) }})</span>
        </li>
      </ul>
    </div>

    <div
      v-if="ticketPairs.matched.length > 0"
      class="overflow-hidden rounded-lg border border-slate-200"
    >
      <div class="max-h-72 overflow-y-auto">
        <table class="min-w-full divide-y divide-slate-200 text-xs">
          <thead class="bg-slate-50 text-left text-[10px] font-semibold uppercase tracking-wide text-slate-500">
            <tr>
              <th class="px-3 py-2">{{ t('admin.flightPanelHelper.ticketMatch.colParticipant') }}</th>
              <th class="px-3 py-2">{{ t('admin.flightPanelHelper.ticketMatch.colNew') }}</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-slate-100 bg-white">
            <tr v-for="entry in ticketPairs.matched" :key="entry.tcNo">
              <td class="px-3 py-1.5">
                <div class="font-medium text-slate-800">{{ entry.matched.fullName }}</div>
                <div class="font-mono text-[10px] text-slate-400">{{ maskTcNo(entry.tcNo) }}</div>
              </td>
              <td class="px-3 py-1.5 font-mono font-medium text-slate-900">
                {{ entry.ticketNo }}
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>

    <p v-if="error" class="rounded border border-rose-200 bg-rose-50 px-3 py-2 text-sm text-rose-700">
      {{ error }}
    </p>

    <div class="flex items-center justify-end gap-2 border-t border-slate-100 pt-3">
      <button
        type="button"
        class="rounded border border-slate-200 bg-white px-4 py-2 text-sm font-medium text-slate-700 hover:border-slate-300 disabled:cursor-not-allowed disabled:opacity-50"
        :disabled="submitting"
        @click="emit('close')"
      >
        {{ t('common.close') }}
      </button>
      <button
        type="button"
        class="inline-flex items-center gap-2 rounded-full bg-slate-900 px-4 py-2 text-sm font-semibold text-white hover:bg-slate-800 disabled:cursor-not-allowed disabled:bg-slate-400"
        :disabled="!canSubmit"
        @click="submit"
      >
        <span
          v-if="submitting"
          class="h-3 w-3 animate-spin rounded-full border border-white/60 border-t-transparent"
        />
        <span>
          {{
            submitting
              ? t('admin.flightPanelHelper.ticketMatch.submitting')
              : t('admin.flightPanelHelper.ticketMatch.submit', { count: ticketPairs.matched.length })
          }}
        </span>
      </button>
    </div>
  </div>
</template>
