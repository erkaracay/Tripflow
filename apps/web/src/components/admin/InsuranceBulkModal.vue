<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import AppModalShell from '../ui/AppModalShell.vue'
import AppSegmentedControl from '../ui/AppSegmentedControl.vue'
import { bulkApplyCommonInsurance, bulkMatchInsurancePolicy } from '../../lib/api'
import { pushToast } from '../../lib/toast'
import { digitsOnly } from '../../lib/parsePasteTable'
import type {
  BulkApplyCommonInsuranceOverwriteMode,
  BulkApplyCommonInsuranceScope,
  Participant,
} from '../../types'

type ParticipantLookup = {
  id: string
  tcNo: string
  fullName: string
  existingPolicyNo?: string | null
}

type BulkTab = 'common' | 'policy'

const props = withDefaults(
  defineProps<{
    open: boolean
    eventId: string
    participants: Participant[] | ParticipantLookup[]
    eventStartDate?: string | null
    eventEndDate?: string | null
    defaultTab?: BulkTab
  }>(),
  { defaultTab: 'common' }
)

const emit = defineEmits<{
  (e: 'close'): void
  (e: 'applied'): void
}>()

const { t } = useI18n()

const titleId = 'insurance-bulk-modal-title'
const activeTab = ref<BulkTab>(props.defaultTab)

// ───────────── Common fields state ─────────────
const companyName = ref('')
const startDate = ref('')
const endDate = ref('')
const scope = ref<BulkApplyCommonInsuranceScope>('all')
const overwriteMode = ref<BulkApplyCommonInsuranceOverwriteMode>('overwrite')
const commonSubmitting = ref(false)
const commonError = ref<string | null>(null)

const isDateRangeInvalid = computed(() => {
  if (!startDate.value || !endDate.value) {
    return false
  }
  return endDate.value < startDate.value
})

const hasAnyCommonField = computed(
  () => companyName.value.trim().length > 0 || startDate.value.length > 0 || endDate.value.length > 0
)

const canSubmitCommon = computed(
  () => !commonSubmitting.value && hasAnyCommonField.value && !isDateRangeInvalid.value
)

const canFillFromEvent = computed(() => !!(props.eventStartDate || props.eventEndDate))

const fillFromEvent = () => {
  if (commonSubmitting.value) {
    return
  }
  if (props.eventStartDate) {
    startDate.value = props.eventStartDate
  }
  if (props.eventEndDate) {
    endDate.value = props.eventEndDate
  }
}

const resetCommon = () => {
  companyName.value = ''
  startDate.value = ''
  endDate.value = ''
  scope.value = 'all'
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
    const response = await bulkApplyCommonInsurance(props.eventId, {
      companyName: companyName.value.trim() || null,
      startDate: startDate.value || null,
      endDate: endDate.value || null,
      scope: scope.value,
      overwriteMode: overwriteMode.value,
    })
    pushToast({
      key: 'admin.events.insuranceCommon.successToast',
      params: { count: response.affectedCount },
      tone: 'success',
    })
    emit('applied')
    emit('close')
  } catch (err) {
    commonError.value = err instanceof Error ? err.message : t('admin.events.insuranceCommon.genericError')
  } finally {
    commonSubmitting.value = false
  }
}

// ───────────── Policy match state ─────────────
const tcPaste = ref('')
const policyPaste = ref('')
const policySubmitting = ref(false)
const policyError = ref<string | null>(null)

const splitLines = (text: string): string[] =>
  text
    .replace(/\r\n/g, '\n')
    .replace(/\r/g, '\n')
    .split('\n')
    .map((line) => line.trim())
    .filter((line) => line.length > 0)

const tcLines = computed(() => splitLines(tcPaste.value))
const policyLines = computed(() => splitLines(policyPaste.value))

const participantsByTcNo = computed(() => {
  const map = new Map<string, ParticipantLookup>()
  for (const participant of props.participants) {
    const tc = digitsOnly(participant.tcNo ?? '')
    if (tc.length !== 11) {
      continue
    }

    const lookup: ParticipantLookup = 'existingPolicyNo' in participant
      ? (participant as ParticipantLookup)
      : {
          id: participant.id,
          tcNo: tc,
          fullName: (participant as Participant).fullName,
          existingPolicyNo: (participant as Participant).details?.insurancePolicyNo ?? null,
        }

    // Defensive: if two participants share a TC, drop from lookup so we flag it as unmatched.
    if (map.has(tc)) {
      map.delete(tc)
      continue
    }
    map.set(tc, lookup)
  }
  return map
})

type MatchedRow = {
  lineNumber: number
  tcNo: string
  policyNo: string
  matched: ParticipantLookup
}

type UnmatchedReason = 'invalid_tc' | 'empty_policy' | 'no_participant' | 'duplicate_tc'

type UnmatchedRow = {
  lineNumber: number
  tcNo: string
  policyNo: string
  reason: UnmatchedReason
}

const policyPairs = computed(() => {
  const matched: MatchedRow[] = []
  const unmatched: UnmatchedRow[] = []
  const seen = new Set<string>()

  const pairCount = Math.min(tcLines.value.length, policyLines.value.length)

  for (let i = 0; i < pairCount; i++) {
    const rawTc = tcLines.value[i] ?? ''
    const rawPolicy = policyLines.value[i] ?? ''
    const tcNo = digitsOnly(rawTc)
    const policyNo = rawPolicy.trim()

    if (tcNo.length !== 11) {
      unmatched.push({ lineNumber: i + 1, tcNo: rawTc, policyNo, reason: 'invalid_tc' })
      continue
    }
    if (policyNo.length === 0) {
      unmatched.push({ lineNumber: i + 1, tcNo, policyNo, reason: 'empty_policy' })
      continue
    }
    if (seen.has(tcNo)) {
      unmatched.push({ lineNumber: i + 1, tcNo, policyNo, reason: 'duplicate_tc' })
      continue
    }
    const participant = participantsByTcNo.value.get(tcNo)
    if (!participant) {
      unmatched.push({ lineNumber: i + 1, tcNo, policyNo, reason: 'no_participant' })
      continue
    }

    seen.add(tcNo)
    matched.push({ lineNumber: i + 1, tcNo, policyNo, matched: participant })
  }

  return { matched, unmatched }
})

const hasLengthMismatch = computed(
  () =>
    (tcLines.value.length > 0 || policyLines.value.length > 0)
    && tcLines.value.length !== policyLines.value.length
)

const canSubmitPolicy = computed(
  () => !policySubmitting.value && policyPairs.value.matched.length > 0
)

const resetPolicy = () => {
  tcPaste.value = ''
  policyPaste.value = ''
  policyError.value = null
}

const submitPolicy = async () => {
  if (!canSubmitPolicy.value) {
    return
  }
  policyError.value = null
  policySubmitting.value = true
  try {
    const response = await bulkMatchInsurancePolicy(props.eventId, {
      entries: policyPairs.value.matched.map((row) => ({ tcNo: row.tcNo, policyNo: row.policyNo })),
    })
    pushToast({
      key: 'admin.events.insurancePolicyMatch.successToast',
      params: { count: response.appliedCount },
      tone: 'success',
    })
    emit('applied')
    if (response.unmatchedTcNos.length === 0) {
      emit('close')
    } else {
      tcPaste.value = ''
      policyPaste.value = ''
    }
  } catch (err) {
    policyError.value = err instanceof Error ? err.message : t('admin.events.insurancePolicyMatch.genericError')
  } finally {
    policySubmitting.value = false
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
  if (reason === 'invalid_tc') return t('admin.events.insurancePolicyMatch.unmatchedInvalidTc')
  if (reason === 'empty_policy') return t('admin.events.insurancePolicyMatch.unmatchedEmptyPolicy')
  if (reason === 'duplicate_tc') return t('admin.events.insurancePolicyMatch.unmatchedDuplicateTc')
  return t('admin.events.insurancePolicyMatch.unmatchedNoParticipant')
}

// ───────────── Shared lifecycle ─────────────
const submitting = computed(() => commonSubmitting.value || policySubmitting.value)

watch(
  () => props.open,
  (open) => {
    if (open) {
      activeTab.value = props.defaultTab
      commonError.value = null
      policyError.value = null
    } else {
      resetCommon()
      resetPolicy()
    }
  }
)

const closeModal = () => {
  if (submitting.value) {
    return
  }
  emit('close')
}

const tabOptions = computed(() => [
  { value: 'common', label: t('admin.events.insuranceBulk.tabCommon') },
  { value: 'policy', label: t('admin.events.insuranceBulk.tabPolicy') },
])

const handleTabChange = (value: string) => {
  if (value === 'common' || value === 'policy') {
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
        :class="[panelClass, 'w-full max-w-2xl overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-xl']"
      >
        <div class="flex items-start justify-between gap-3 border-b border-slate-200 px-4 py-4 sm:px-5">
          <div>
            <h3 :id="titleId" class="text-lg font-semibold text-slate-900">
              {{ t('admin.events.insuranceBulk.title') }}
            </h3>
            <p class="mt-1 text-sm text-slate-600">
              {{ t('admin.events.insuranceBulk.description') }}
            </p>
          </div>
          <button
            type="button"
            class="rounded border border-slate-200 bg-white px-3 py-1.5 text-xs font-medium text-slate-700 hover:border-slate-300"
            :disabled="submitting"
            @click="closeModal"
          >
            {{ t('admin.events.insuranceBulk.cancel') }}
          </button>
        </div>

        <div class="border-b border-slate-100 px-4 py-3 sm:px-5">
          <AppSegmentedControl
            :model-value="activeTab"
            :options="tabOptions"
            :aria-label="t('admin.events.insuranceBulk.title')"
            full-width
            @update:model-value="handleTabChange"
          />
        </div>

        <!-- ═════════════ Tab 1: Common fields ═════════════ -->
        <form
          v-if="activeTab === 'common'"
          class="space-y-4 px-4 py-4 sm:px-5"
          @submit.prevent="submitCommon"
        >
          <label class="grid gap-1 text-sm">
            <span class="font-medium text-slate-700">{{ t('admin.events.insuranceCommon.companyLabel') }}</span>
            <input
              v-model="companyName"
              type="text"
              class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
              :placeholder="t('admin.events.insuranceCommon.companyPlaceholder')"
              maxlength="200"
              :disabled="commonSubmitting"
            />
          </label>

          <div class="grid gap-4 sm:grid-cols-2">
            <label class="grid gap-1 text-sm">
              <span class="font-medium text-slate-700">{{ t('admin.events.insuranceCommon.startLabel') }}</span>
              <input
                v-model="startDate"
                type="date"
                class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
                :disabled="commonSubmitting"
              />
            </label>
            <label class="grid gap-1 text-sm">
              <span class="font-medium text-slate-700">{{ t('admin.events.insuranceCommon.endLabel') }}</span>
              <input
                v-model="endDate"
                type="date"
                class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
                :disabled="commonSubmitting"
              />
            </label>
          </div>

          <div v-if="canFillFromEvent" class="flex justify-end">
            <button
              type="button"
              class="inline-flex items-center gap-1.5 rounded-full border border-slate-200 bg-white px-3 py-1.5 text-xs font-medium text-slate-700 hover:border-slate-300 disabled:cursor-not-allowed disabled:opacity-50"
              :disabled="commonSubmitting"
              @click="fillFromEvent"
            >
              <svg class="h-3.5 w-3.5 text-slate-500" viewBox="0 0 24 24" fill="none" aria-hidden="true">
                <rect x="3.5" y="5" width="17" height="15" rx="2" stroke="currentColor" stroke-width="1.6" />
                <path d="M3.5 9.5h17" stroke="currentColor" stroke-width="1.6" />
                <path d="M8 3.5v3M16 3.5v3" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" />
              </svg>
              <span>{{ t('admin.events.insuranceCommon.fillFromEvent') }}</span>
            </button>
          </div>

          <p v-if="isDateRangeInvalid" class="text-xs text-rose-600">
            {{ t('admin.events.insuranceCommon.invalidDateRange') }}
          </p>

          <fieldset class="grid gap-2 rounded-lg border border-slate-200 px-3 py-3 text-sm">
            <legend class="px-1 text-xs font-semibold uppercase tracking-wide text-slate-500">
              {{ t('admin.events.insuranceCommon.scopeLabel') }}
            </legend>
            <label class="flex items-start gap-2">
              <input
                v-model="scope"
                type="radio"
                value="all"
                class="mt-0.5"
                :disabled="commonSubmitting"
              />
              <span>{{ t('admin.events.insuranceCommon.scopeAll') }}</span>
            </label>
            <label class="flex items-start gap-2">
              <input
                v-model="scope"
                type="radio"
                value="missing_policy"
                class="mt-0.5"
                :disabled="commonSubmitting"
              />
              <span>{{ t('admin.events.insuranceCommon.scopeMissing') }}</span>
            </label>
          </fieldset>

          <fieldset class="grid gap-2 rounded-lg border border-slate-200 px-3 py-3 text-sm">
            <legend class="px-1 text-xs font-semibold uppercase tracking-wide text-slate-500">
              {{ t('admin.events.insuranceCommon.overwriteLabel') }}
            </legend>
            <label class="flex items-start gap-2">
              <input
                v-model="overwriteMode"
                type="radio"
                value="overwrite"
                class="mt-0.5"
                :disabled="commonSubmitting"
              />
              <span>{{ t('admin.events.insuranceCommon.overwriteAll') }}</span>
            </label>
            <label class="flex items-start gap-2">
              <input
                v-model="overwriteMode"
                type="radio"
                value="only_empty"
                class="mt-0.5"
                :disabled="commonSubmitting"
              />
              <span>{{ t('admin.events.insuranceCommon.overwriteOnlyEmpty') }}</span>
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
              {{ t('admin.events.insuranceBulk.cancel') }}
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
              <span>{{ commonSubmitting ? t('admin.events.insuranceCommon.submitting') : t('admin.events.insuranceCommon.submit') }}</span>
            </button>
          </div>
        </form>

        <!-- ═════════════ Tab 2: Policy match ═════════════ -->
        <div v-else class="space-y-4 px-4 py-4 sm:px-5">
          <p class="text-sm text-slate-600">
            {{ t('admin.events.insurancePolicyMatch.pairDescription') }}
          </p>

          <div class="grid gap-3 sm:grid-cols-2">
            <label class="grid gap-1 text-sm">
              <span class="flex items-center justify-between">
                <span class="font-medium text-slate-700">{{ t('admin.events.insurancePolicyMatch.tcLabel') }}</span>
                <span class="text-xs text-slate-400">{{ tcLines.length }}</span>
              </span>
              <textarea
                v-model="tcPaste"
                rows="8"
                class="rounded border border-slate-200 bg-slate-50 px-3 py-2 font-mono text-xs leading-relaxed focus:border-slate-400 focus:bg-white focus:outline-none"
                :placeholder="t('admin.events.insurancePolicyMatch.tcPlaceholder')"
                spellcheck="false"
                autocorrect="off"
                autocapitalize="off"
                :disabled="policySubmitting"
              />
            </label>
            <label class="grid gap-1 text-sm">
              <span class="flex items-center justify-between">
                <span class="font-medium text-slate-700">{{ t('admin.events.insurancePolicyMatch.policyLabel') }}</span>
                <span class="text-xs text-slate-400">{{ policyLines.length }}</span>
              </span>
              <textarea
                v-model="policyPaste"
                rows="8"
                class="rounded border border-slate-200 bg-slate-50 px-3 py-2 font-mono text-xs leading-relaxed focus:border-slate-400 focus:bg-white focus:outline-none"
                :placeholder="t('admin.events.insurancePolicyMatch.policyPlaceholder')"
                spellcheck="false"
                autocorrect="off"
                autocapitalize="off"
                :disabled="policySubmitting"
              />
            </label>
          </div>

          <div
            v-if="hasLengthMismatch"
            class="rounded border border-amber-200 bg-amber-50 px-3 py-2 text-xs text-amber-800"
          >
            {{
              t('admin.events.insurancePolicyMatch.lengthMismatchWarning', {
                tc: tcLines.length,
                policy: policyLines.length,
              })
            }}
          </div>

          <div
            v-if="policyPairs.matched.length > 0 || policyPairs.unmatched.length > 0"
            class="flex flex-wrap items-center gap-2 text-xs"
          >
            <span class="inline-flex items-center gap-1 rounded-full bg-emerald-50 px-2.5 py-1 font-medium text-emerald-700">
              ✓ {{ t('admin.events.insurancePolicyMatch.matchedCount', { count: policyPairs.matched.length }) }}
            </span>
            <span
              v-if="policyPairs.unmatched.length > 0"
              class="inline-flex items-center gap-1 rounded-full bg-amber-50 px-2.5 py-1 font-medium text-amber-700"
            >
              ⚠ {{ t('admin.events.insurancePolicyMatch.unmatchedCount', { count: policyPairs.unmatched.length }) }}
            </span>
          </div>

          <div
            v-if="policyPairs.unmatched.length > 0"
            class="rounded border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-800"
          >
            <p class="font-medium">{{ t('admin.events.insurancePolicyMatch.unmatchedWarning') }}</p>
            <ul class="mt-1 list-disc space-y-0.5 pl-5 font-mono text-xs">
              <li v-for="entry in policyPairs.unmatched" :key="`${entry.lineNumber}-${entry.tcNo}`">
                <span class="text-amber-600">#{{ entry.lineNumber }}</span>
                {{ entry.tcNo || '—' }} — {{ entry.policyNo || '—' }}
                <span class="text-amber-600/80">({{ unmatchedReasonLabel(entry.reason) }})</span>
              </li>
            </ul>
          </div>

          <div
            v-if="policyPairs.matched.length > 0"
            class="overflow-hidden rounded-lg border border-slate-200"
          >
            <div class="max-h-72 overflow-y-auto">
              <table class="min-w-full divide-y divide-slate-200 text-xs">
                <thead class="bg-slate-50 text-left text-[10px] font-semibold uppercase tracking-wide text-slate-500">
                  <tr>
                    <th class="px-3 py-2">{{ t('admin.events.insurancePolicyMatch.colParticipant') }}</th>
                    <th class="px-3 py-2">{{ t('admin.events.insurancePolicyMatch.colCurrent') }}</th>
                    <th class="px-3 py-2">{{ t('admin.events.insurancePolicyMatch.colNew') }}</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-slate-100 bg-white">
                  <tr v-for="entry in policyPairs.matched" :key="entry.tcNo">
                    <td class="px-3 py-1.5">
                      <div class="font-medium text-slate-800">{{ entry.matched.fullName }}</div>
                      <div class="font-mono text-[10px] text-slate-400">{{ maskTcNo(entry.tcNo) }}</div>
                    </td>
                    <td class="px-3 py-1.5 font-mono text-slate-500">
                      {{ entry.matched.existingPolicyNo || '—' }}
                    </td>
                    <td class="px-3 py-1.5 font-mono font-medium text-slate-900">
                      {{ entry.policyNo }}
                    </td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>

          <p v-if="policyError" class="rounded border border-rose-200 bg-rose-50 px-3 py-2 text-sm text-rose-700">
            {{ policyError }}
          </p>

          <div class="flex items-center justify-end gap-2 border-t border-slate-100 pt-3">
            <button
              type="button"
              class="rounded border border-slate-200 bg-white px-4 py-2 text-sm font-medium text-slate-700 hover:border-slate-300 disabled:cursor-not-allowed disabled:opacity-50"
              :disabled="policySubmitting"
              @click="closeModal"
            >
              {{ t('admin.events.insuranceBulk.cancel') }}
            </button>
            <button
              type="button"
              class="inline-flex items-center gap-2 rounded-full bg-slate-900 px-4 py-2 text-sm font-semibold text-white hover:bg-slate-800 disabled:cursor-not-allowed disabled:bg-slate-400"
              :disabled="!canSubmitPolicy"
              @click="submitPolicy"
            >
              <span
                v-if="policySubmitting"
                class="h-3 w-3 animate-spin rounded-full border border-white/60 border-t-transparent"
              />
              <span>
                {{
                  policySubmitting
                    ? t('admin.events.insurancePolicyMatch.submitting')
                    : t('admin.events.insurancePolicyMatch.submit', { count: policyPairs.matched.length })
                }}
              </span>
            </button>
          </div>
        </div>
      </div>
    </template>
  </AppModalShell>
</template>
