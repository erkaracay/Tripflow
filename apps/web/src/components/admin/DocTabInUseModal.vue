<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import AppModalShell from '../ui/AppModalShell.vue'
import { apiDelete } from '../../lib/api'
import { useToast } from '../../lib/toast'
import { formatDateRange } from '../../lib/formatters'
import type { DocTabInUseSegment } from '../../types'

const props = defineProps<{
  open: boolean
  eventId: string
  tabId: string
  tabTitle: string
  segments: DocTabInUseSegment[]
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

watch(
  () => props.open,
  (isOpen) => {
    if (isOpen) {
      localSegments.value = [...props.segments]
      deletingSegmentId.value = null
      deletingTab.value = false
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

const handleClose = () => {
  if (deletingSegmentId.value || deletingTab.value) return
  emit('close')
}

const handleDeleteSegment = async (segment: DocTabInUseSegment) => {
  if (deletingSegmentId.value) return
  deletingSegmentId.value = segment.id
  try {
    await apiDelete(`/api/events/${props.eventId}/accommodation-segments/${segment.id}`)
    localSegments.value = localSegments.value.filter((item) => item.id !== segment.id)
    pushToast({ key: 'admin.docs.deleteInUse.segmentDeleted', tone: 'success' })
  } catch {
    pushToast({ key: 'errors.generic', tone: 'error' })
  } finally {
    deletingSegmentId.value = null
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
              class="flex items-start justify-between gap-3 px-4 py-3"
            >
              <div class="min-w-0">
                <div class="text-sm font-semibold text-slate-900">
                  {{ formatDateRange(segment.startDate, segment.endDate) }}
                </div>
                <div v-if="segment.participantCount > 0" class="mt-0.5 text-xs text-amber-700">
                  {{ t('admin.docs.deleteInUse.segmentDeleteWarning', { count: segment.participantCount }) }}
                </div>
              </div>
              <button
                type="button"
                class="shrink-0 rounded border border-rose-200 px-3 py-1.5 text-xs font-semibold text-rose-600 transition hover:bg-rose-50 disabled:cursor-not-allowed disabled:opacity-60"
                :disabled="deletingSegmentId !== null || deletingTab"
                @click="handleDeleteSegment(segment)"
              >
                {{ deletingSegmentId === segment.id ? '…' : t('admin.docs.deleteInUse.deleteSegment') }}
              </button>
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
              :disabled="deletingSegmentId !== null || deletingTab"
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
