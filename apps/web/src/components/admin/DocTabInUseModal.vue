<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import AppModalShell from '../ui/AppModalShell.vue'
import AppCombobox from '../ui/AppCombobox.vue'
import { apiDelete, apiPut } from '../../lib/api'
import { useToast } from '../../lib/toast'
import { formatDateRange } from '../../lib/formatters'
import type { AppComboboxOption, AppComboboxValue, DocTabInUseSegment, EventDocTabDto } from '../../types'

const props = defineProps<{
  open: boolean
  eventId: string
  tabId: string
  tabTitle: string
  segments: DocTabInUseSegment[]
  candidateHotels: EventDocTabDto[]
}>()

const emit = defineEmits<{
  (event: 'close'): void
  (event: 'deleted'): void
}>()

const { t } = useI18n()
const { pushToast } = useToast()

const localSegments = ref<DocTabInUseSegment[]>([])
const deletingSegmentId = ref<string | null>(null)
const deletingTab = ref(false)
const reassigningSegmentId = ref<string | null>(null)
const reassignTargetId = ref<string | null>(null)
const applyingSegmentId = ref<string | null>(null)

watch(
  () => props.open,
  (isOpen) => {
    if (isOpen) {
      localSegments.value = [...props.segments]
      deletingSegmentId.value = null
      deletingTab.value = false
      reassigningSegmentId.value = null
      reassignTargetId.value = null
      applyingSegmentId.value = null
    }
  },
  { immediate: true }
)

watch(
  () => props.segments,
  (segments) => {
    if (props.open) {
      localSegments.value = [...segments]
    }
  }
)

const remainingCount = computed(() => localSegments.value.length)
const canDeleteTab = computed(() => remainingCount.value === 0 && !deletingTab.value)
const hasCandidateHotels = computed(() => props.candidateHotels.length > 0)

const candidateOptions = computed<AppComboboxOption[]>(() =>
  props.candidateHotels.map((tab) => ({
    value: tab.id,
    label: tab.title,
  }))
)

const anyRowBusy = computed(
  () => deletingSegmentId.value !== null || applyingSegmentId.value !== null || deletingTab.value
)

const handleClose = () => {
  if (anyRowBusy.value) return
  emit('close')
}

const handleDeleteSegment = async (segment: DocTabInUseSegment) => {
  if (anyRowBusy.value) return
  deletingSegmentId.value = segment.id
  try {
    await apiDelete(`/api/events/${props.eventId}/accommodation-segments/${segment.id}`)
    localSegments.value = localSegments.value.filter((item) => item.id !== segment.id)
    if (reassigningSegmentId.value === segment.id) {
      reassigningSegmentId.value = null
      reassignTargetId.value = null
    }
    pushToast({ key: 'admin.docs.deleteInUse.segmentDeleted', tone: 'success' })
  } catch {
    pushToast({ key: 'errors.generic', tone: 'error' })
  } finally {
    deletingSegmentId.value = null
  }
}

const handleStartReassign = (segment: DocTabInUseSegment) => {
  if (anyRowBusy.value) return
  if (!hasCandidateHotels.value) return
  reassigningSegmentId.value = segment.id
  reassignTargetId.value = props.candidateHotels[0]?.id ?? null
}

const handleCancelReassign = () => {
  if (applyingSegmentId.value !== null) return
  reassigningSegmentId.value = null
  reassignTargetId.value = null
}

const handleReassignTargetChange = (value: AppComboboxValue) => {
  reassignTargetId.value = typeof value === 'string' ? value : String(value)
}

const handleApplyReassign = async (segment: DocTabInUseSegment) => {
  if (applyingSegmentId.value !== null) return
  const targetId = reassignTargetId.value
  if (!targetId) return
  applyingSegmentId.value = segment.id
  try {
    await apiPut(`/api/events/${props.eventId}/accommodation-segments/${segment.id}`, {
      defaultAccommodationDocTabId: targetId,
      startDate: segment.startDate,
      endDate: segment.endDate,
      sortOrder: segment.sortOrder,
    })
    localSegments.value = localSegments.value.filter((item) => item.id !== segment.id)
    reassigningSegmentId.value = null
    reassignTargetId.value = null
    pushToast({ key: 'admin.docs.deleteInUse.reassignSuccess', tone: 'success' })
  } catch {
    pushToast({ key: 'errors.generic', tone: 'error' })
  } finally {
    applyingSegmentId.value = null
  }
}

const handleDeleteTab = async () => {
  if (!canDeleteTab.value) return
  deletingTab.value = true
  try {
    await apiDelete(`/api/events/${props.eventId}/docs/tabs/${props.tabId}`)
    pushToast({ key: 'admin.docs.deleteInUse.tabDeleted', tone: 'success' })
    emit('deleted')
  } catch {
    pushToast({ key: 'errors.generic', tone: 'error' })
    deletingTab.value = false
  }
}
</script>

<template>
  <AppModalShell :open="props.open" :close-on-overlay="false" @close="handleClose">
    <template #default="{ panelClass }">
      <div
        :class="[panelClass, 'flex w-full max-w-2xl max-h-[90vh] flex-col overflow-hidden rounded-2xl bg-white p-5 shadow-xl']"
      >
        <div class="flex items-start justify-between gap-3">
          <div>
            <h3 class="text-lg font-semibold text-slate-900">
              {{ t('admin.docs.deleteInUse.title') }}
            </h3>
            <p class="mt-1 text-sm text-slate-600">{{ tabTitle }}</p>
          </div>
        </div>

        <p class="mt-3 text-sm text-slate-600">
          {{ t('admin.docs.deleteInUse.description') }}
        </p>

        <div class="mt-4 flex min-h-0 flex-1 flex-col overflow-y-auto rounded border border-slate-200">
          <ul v-if="localSegments.length > 0" class="divide-y divide-slate-100">
            <li
              v-for="segment in localSegments"
              :key="segment.id"
              class="flex flex-col gap-2 px-4 py-3"
            >
              <div class="flex items-start justify-between gap-3">
                <div class="min-w-0">
                  <div class="text-sm font-semibold text-slate-900">
                    {{ formatDateRange(segment.startDate, segment.endDate) }}
                  </div>
                  <div v-if="segment.participantCount > 0" class="mt-0.5 text-xs text-amber-700">
                    {{ t('admin.docs.deleteInUse.segmentDeleteWarning', { count: segment.participantCount }) }}
                  </div>
                </div>
                <div class="flex shrink-0 items-center gap-2">
                  <button
                    type="button"
                    class="rounded border border-slate-200 px-3 py-1.5 text-xs font-semibold text-slate-700 transition hover:border-slate-300 hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-60"
                    :disabled="anyRowBusy || !hasCandidateHotels || reassigningSegmentId === segment.id"
                    :title="!hasCandidateHotels ? t('admin.docs.deleteInUse.reassignNoCandidates') : ''"
                    @click="handleStartReassign(segment)"
                  >
                    {{ t('admin.docs.deleteInUse.reassignSegment') }}
                  </button>
                  <button
                    type="button"
                    class="rounded border border-rose-200 px-3 py-1.5 text-xs font-semibold text-rose-600 transition hover:bg-rose-50 disabled:cursor-not-allowed disabled:opacity-60"
                    :disabled="anyRowBusy"
                    @click="handleDeleteSegment(segment)"
                  >
                    {{ deletingSegmentId === segment.id ? '…' : t('admin.docs.deleteInUse.deleteSegment') }}
                  </button>
                </div>
              </div>

              <div
                v-if="reassigningSegmentId === segment.id"
                class="flex flex-col gap-2 rounded bg-slate-50 p-3 sm:flex-row sm:items-center"
              >
                <div class="min-w-0 flex-1">
                  <label class="mb-1 block text-xs font-medium text-slate-600">
                    {{ t('admin.docs.deleteInUse.reassignPickHotel') }}
                  </label>
                  <AppCombobox
                    :model-value="reassignTargetId"
                    :options="candidateOptions"
                    :disabled="applyingSegmentId === segment.id"
                    :aria-label="t('admin.docs.deleteInUse.reassignPickHotel')"
                    :teleport-panel="true"
                    @update:model-value="handleReassignTargetChange"
                  />
                </div>
                <div class="flex shrink-0 items-center gap-2">
                  <button
                    type="button"
                    class="rounded border border-slate-200 bg-white px-3 py-1.5 text-xs font-semibold text-slate-700 transition hover:border-slate-300 disabled:cursor-not-allowed disabled:opacity-60"
                    :disabled="applyingSegmentId === segment.id"
                    @click="handleCancelReassign"
                  >
                    {{ t('admin.docs.deleteInUse.reassignCancel') }}
                  </button>
                  <button
                    type="button"
                    class="rounded bg-slate-900 px-3 py-1.5 text-xs font-semibold text-white transition hover:bg-slate-800 disabled:cursor-not-allowed disabled:opacity-60"
                    :disabled="applyingSegmentId === segment.id || !reassignTargetId"
                    @click="handleApplyReassign(segment)"
                  >
                    {{ applyingSegmentId === segment.id ? '…' : t('admin.docs.deleteInUse.reassignApply') }}
                  </button>
                </div>
              </div>
            </li>
          </ul>
          <div v-else class="px-4 py-6 text-center text-sm text-emerald-700">
            {{ t('admin.docs.deleteInUse.allClear') }}
          </div>
        </div>

        <div class="mt-4 flex flex-col-reverse gap-2 sm:flex-row sm:items-center sm:justify-between">
          <span v-if="remainingCount > 0" class="text-sm text-slate-600">
            {{ t('admin.docs.deleteInUse.segmentsRemaining', { count: remainingCount }) }}
          </span>
          <span v-else class="text-sm font-medium text-emerald-700">
            {{ t('admin.docs.deleteInUse.allClear') }}
          </span>
          <div class="flex items-center gap-2">
            <button
              type="button"
              class="rounded-full border border-slate-300 bg-white px-4 py-2 text-sm font-semibold text-slate-700 transition hover:border-slate-400 disabled:cursor-not-allowed disabled:opacity-60"
              :disabled="anyRowBusy"
              @click="handleClose"
            >
              {{ t('admin.docs.deleteInUse.close') }}
            </button>
            <button
              type="button"
              class="rounded-full bg-rose-600 px-4 py-2 text-sm font-semibold text-white transition hover:bg-rose-500 disabled:cursor-not-allowed disabled:opacity-60"
              :disabled="!canDeleteTab"
              @click="handleDeleteTab"
            >
              {{ t('admin.docs.deleteInUse.deleteTab') }}
            </button>
          </div>
        </div>
      </div>
    </template>
  </AppModalShell>
</template>
