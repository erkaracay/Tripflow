<script setup lang="ts">
import { computed, onUnmounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { apiGet } from '../../lib/api'
import { formatUtcDateTimeLocal } from '../../lib/formatters'
import { formatPhoneDisplay } from '../../lib/normalize'
import { useToast } from '../../lib/toast'
import LoadingState from '../ui/LoadingState.vue'
import ErrorState from '../ui/ErrorState.vue'
import type {
  MealChoiceListItem,
  MealChoiceListResponse,
  MealReportMode,
  MealSummaryCount,
  MealSummaryGroup,
  MealSummaryResponse,
} from '../../types'

const props = withDefaults(defineProps<{
  eventId: string
  activityId: string
  mode: MealReportMode
  activityTitle?: string | null
  panel?: boolean
}>(), {
  activityTitle: null,
  panel: false,
})

const emit = defineEmits<{
  (event: 'close'): void
}>()

const { t } = useI18n()
const { pushToast } = useToast()

const summary = ref<MealSummaryResponse | null>(null)
const summaryLoading = ref(false)
const summaryErrorMessage = ref<string | null>(null)

const choicesOpen = ref(false)
const activeGroup = ref<MealSummaryGroup | null>(null)
const activeCount = ref<MealSummaryCount | null>(null)
const choices = ref<MealChoiceListResponse | null>(null)
const choicesLoading = ref(false)
const choicesErrorMessage = ref<string | null>(null)
const searchQuery = ref('')
const onlyNotes = ref(false)
const page = ref(1)
const pageSize = ref(20)
const expandedItems = ref<Record<string, boolean>>({})
let searchTimer: ReturnType<typeof setTimeout> | null = null
let bodyOverflow: string | null = null

const apiBase = computed(() => (props.mode === 'guide' ? '/api/guide/events' : '/api/events'))
const pageBackRoute = computed(() => (
  props.mode === 'guide'
    ? `/guide/events/${props.eventId}/program`
    : `/admin/events/${props.eventId}/program`
))
const resolvedActivityTitle = computed(() => props.activityTitle?.trim() || t('mealReport.title'))
const isOtherContext = computed(() => activeCount.value?.optionId == null)
const canToggleOnlyOther = computed(() => isOtherContext.value)
const effectiveOnlyOther = computed(() => isOtherContext.value)
const choiceDrawerTitle = computed(() => {
  if (!activeGroup.value) {
    return t('mealReport.panelTitle')
  }

  const optionLabel = activeCount.value?.optionId == null
    ? t('mealReport.other')
    : activeCount.value?.label

  return optionLabel
    ? `${activeGroup.value.title} · ${optionLabel}`
    : activeGroup.value.title
})
const anyOverlayOpen = computed(() => props.panel || choicesOpen.value)

const normalizeOtherLabel = (count: MealSummaryCount) =>
  count.optionId == null ? t('mealReport.other') : count.label

const normalizePhoneHref = (value?: string | null) => {
  const trimmed = value?.trim() ?? ''
  if (!trimmed) {
    return ''
  }

  const digits = trimmed.replace(/[^\d+]/g, '')
  return digits ? `tel:${digits}` : ''
}

const choiceItemKey = (item: MealChoiceListItem) => `${item.participant.id}:${item.updatedAt}:${item.optionId ?? 'other'}`

const isLongText = (value?: string | null) => (value?.trim().length ?? 0) > 120

const isExpanded = (item: MealChoiceListItem, field: 'otherText' | 'note') =>
  Boolean(expandedItems.value[`${choiceItemKey(item)}:${field}`])

const toggleExpanded = (item: MealChoiceListItem, field: 'otherText' | 'note') => {
  const key = `${choiceItemKey(item)}:${field}`
  expandedItems.value = {
    ...expandedItems.value,
    [key]: !expandedItems.value[key],
  }
}

const resetChoiceState = () => {
  choices.value = null
  choicesErrorMessage.value = null
  choicesLoading.value = false
  searchQuery.value = ''
  onlyNotes.value = false
  page.value = 1
  expandedItems.value = {}
}

const loadSummary = async () => {
  summaryLoading.value = true
  summaryErrorMessage.value = null
  try {
    summary.value = await apiGet<MealSummaryResponse>(`${apiBase.value}/${props.eventId}/activities/${props.activityId}/meal/summary`)
  } catch (error) {
    summaryErrorMessage.value = error instanceof Error ? error.message : t('errors.generic')
  } finally {
    summaryLoading.value = false
  }
}

const buildChoicesPath = () => {
  if (!activeGroup.value) {
    return ''
  }

  const params = new URLSearchParams({
    groupId: activeGroup.value.groupId,
    page: String(page.value),
    pageSize: String(pageSize.value),
  })

  if (activeCount.value) {
    params.set('optionId', activeCount.value.optionId ?? 'other')
  }

  if (searchQuery.value.trim()) {
    params.set('q', searchQuery.value.trim())
  }

  if (onlyNotes.value) {
    params.set('onlyNotes', 'true')
  }

  if (effectiveOnlyOther.value) {
    params.set('onlyOther', 'true')
  }

  return `${apiBase.value}/${props.eventId}/activities/${props.activityId}/meal/choices?${params.toString()}`
}

const loadChoices = async () => {
  if (!choicesOpen.value || !activeGroup.value || !activeCount.value) {
    return
  }

  choicesLoading.value = true
  choicesErrorMessage.value = null
  try {
    choices.value = await apiGet<MealChoiceListResponse>(buildChoicesPath())
  } catch (error) {
    choicesErrorMessage.value = error instanceof Error ? error.message : t('errors.generic')
  } finally {
    choicesLoading.value = false
  }
}

const openChoices = (group: MealSummaryGroup, count: MealSummaryCount) => {
  activeGroup.value = group
  activeCount.value = count
  choicesOpen.value = true
  resetChoiceState()
  void loadChoices()
}

const closeChoices = () => {
  choicesOpen.value = false
  activeGroup.value = null
  activeCount.value = null
  resetChoiceState()
}

const setPage = (nextPage: number) => {
  if (!choices.value) {
    return
  }

  const maxPage = Math.max(1, Math.ceil(choices.value.total / choices.value.pageSize))
  const clamped = Math.min(Math.max(1, nextPage), maxPage)
  if (clamped === page.value) {
    return
  }

  page.value = clamped
  void loadChoices()
}

const copySummary = async () => {
  const groups = summary.value?.groups ?? []
  const text = groups
    .map((group) => {
      const countsText = group.counts
        .map((count) => `${normalizeOtherLabel(count)} ${count.count}`)
        .join(', ')

      return countsText ? `${group.title}: ${countsText}` : `${group.title}: ${t('common.noData')}`
    })
    .join('\n')
    .trim()

  if (!text) {
    pushToast({ key: 'mealReport.copyError', tone: 'error' })
    return
  }

  try {
    await globalThis.navigator.clipboard.writeText(text)
    pushToast({ key: 'mealReport.copySuccess', tone: 'success' })
  } catch {
    pushToast({ key: 'mealReport.copyError', tone: 'error' })
  }
}

watch(
  () => [props.eventId, props.activityId, props.mode] as const,
  () => {
    closeChoices()
    void loadSummary()
  },
  { immediate: true }
)

watch(searchQuery, () => {
  if (!choicesOpen.value) {
    return
  }

  if (searchTimer) {
    globalThis.clearTimeout(searchTimer)
    searchTimer = null
  }

  searchTimer = globalThis.setTimeout(() => {
    page.value = 1
    void loadChoices()
  }, 250)
})

watch(onlyNotes, () => {
  if (!choicesOpen.value) {
    return
  }

  page.value = 1
  void loadChoices()
})

watch(anyOverlayOpen, (open) => {
  if (typeof document === 'undefined') {
    return
  }

  if (open) {
    if (bodyOverflow === null) {
      bodyOverflow = document.body.style.overflow
    }
    document.body.style.overflow = 'hidden'
    return
  }

  document.body.style.overflow = bodyOverflow ?? ''
  bodyOverflow = null
})

onUnmounted(() => {
  if (searchTimer) {
    globalThis.clearTimeout(searchTimer)
  }

  if (typeof document !== 'undefined') {
    document.body.style.overflow = bodyOverflow ?? ''
  }
  bodyOverflow = null
})
</script>

<template>
  <div v-if="!panel" class="space-y-6">
    <section class="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:p-6">
      <div class="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
        <div>
          <RouterLink
            class="inline-flex items-center gap-2 text-sm text-slate-600 underline-offset-2 hover:text-slate-900"
            :to="pageBackRoute"
          >
            {{ t('mealReport.back') }}
          </RouterLink>
          <h1 class="mt-3 text-2xl font-semibold text-slate-900">{{ resolvedActivityTitle }}</h1>
          <p class="mt-1 text-sm text-slate-500">{{ t('mealReport.subtitle') }}</p>
        </div>
        <button
          class="inline-flex items-center justify-center rounded-full border border-slate-200 px-4 py-2 text-sm font-medium text-slate-700 transition hover:border-slate-300 hover:bg-slate-50"
          type="button"
          @click="copySummary"
        >
          {{ t('mealReport.copySummary') }}
        </button>
      </div>
    </section>

    <LoadingState v-if="summaryLoading" message-key="mealReport.loading" />
    <ErrorState
      v-else-if="summaryErrorMessage"
      :message="summaryErrorMessage"
      @retry="loadSummary"
    />
    <section
      v-else
      class="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:p-6"
    >
      <p v-if="(summary?.groups.length ?? 0) === 0" class="text-sm text-slate-500">
        {{ t('mealReport.empty') }}
      </p>
      <div v-else class="space-y-4">
        <article
          v-for="group in summary?.groups ?? []"
          :key="group.groupId"
          class="rounded-2xl border border-slate-200 bg-slate-50 p-4"
        >
          <div class="flex flex-wrap items-start justify-between gap-2">
            <h2 class="text-lg font-semibold text-slate-900">{{ group.title }}</h2>
            <span v-if="group.noteCount > 0" class="rounded-full border border-slate-200 bg-white px-3 py-1 text-xs font-medium text-slate-600">
              {{ t('mealReport.noteCount', { count: group.noteCount }) }}
            </span>
          </div>

          <div v-if="group.counts.length > 0" class="mt-4 grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
            <button
              v-for="count in group.counts"
              :key="`${group.groupId}-${count.optionId ?? 'other'}`"
              class="rounded-2xl border border-slate-200 bg-white p-4 text-left transition hover:border-slate-300 hover:bg-slate-50"
              type="button"
              @click="openChoices(group, count)"
            >
              <div class="text-sm font-medium text-slate-600">{{ normalizeOtherLabel(count) }}</div>
              <div class="mt-2 text-2xl font-semibold text-slate-900">{{ count.count }}</div>
            </button>
          </div>

          <p v-else class="mt-4 text-sm text-slate-500">{{ t('mealReport.empty') }}</p>
        </article>
      </div>
    </section>
  </div>

  <Transition name="meal-panel" appear>
    <div v-if="panel" class="meal-panel-shell fixed inset-0 z-50 flex items-end bg-slate-900/30 sm:items-stretch sm:justify-end">
      <button class="absolute inset-0 h-full w-full cursor-default" type="button" @click="emit('close')" />
      <section class="meal-drawer-surface relative z-10 flex h-[90vh] w-full flex-col rounded-t-3xl bg-white shadow-2xl sm:h-full sm:max-w-3xl sm:rounded-none sm:rounded-l-3xl">
        <div class="flex items-start justify-between gap-4 border-b border-slate-200 px-4 py-4 sm:px-6">
          <div>
            <div class="text-xs font-semibold uppercase tracking-[0.2em] text-slate-500">{{ t('mealReport.panelTitle') }}</div>
            <h2 class="mt-1 text-xl font-semibold text-slate-900">{{ resolvedActivityTitle }}</h2>
          </div>
          <div class="flex items-center gap-2">
            <button
              class="rounded-full border border-slate-200 px-3 py-2 text-xs font-medium text-slate-700 transition hover:border-slate-300 hover:bg-slate-50"
              type="button"
              @click="copySummary"
            >
              {{ t('mealReport.copySummary') }}
            </button>
            <button
              class="rounded-full border border-slate-200 px-3 py-2 text-xs font-medium text-slate-700 transition hover:border-slate-300 hover:bg-slate-50"
              type="button"
              @click="emit('close')"
            >
              {{ t('common.dismiss') }}
            </button>
          </div>
        </div>

        <div class="min-h-0 flex-1 overflow-y-auto px-4 py-4 sm:px-6">
          <div v-if="summaryLoading" class="rounded-2xl border border-dashed border-slate-200 bg-slate-50 p-4 text-sm text-slate-500">
            {{ t('mealReport.loading') }}
          </div>
          <div v-else-if="summaryErrorMessage" class="rounded-2xl border border-rose-200 bg-rose-50 p-4 text-sm text-rose-700">
            <div>{{ summaryErrorMessage }}</div>
            <button class="mt-3 rounded-full border border-rose-200 bg-white px-3 py-2 text-xs font-semibold text-rose-700" type="button" @click="loadSummary">
              {{ t('common.retry') }}
            </button>
          </div>
          <div v-else-if="(summary?.groups.length ?? 0) === 0" class="rounded-2xl border border-dashed border-slate-200 bg-slate-50 p-4 text-sm text-slate-500">
            {{ t('mealReport.empty') }}
          </div>
          <div v-else class="space-y-4">
            <article
              v-for="group in summary?.groups ?? []"
              :key="group.groupId"
              class="rounded-2xl border border-slate-200 bg-slate-50 p-4"
            >
              <div class="flex flex-wrap items-start justify-between gap-2">
                <h3 class="text-base font-semibold text-slate-900">{{ group.title }}</h3>
                <span v-if="group.noteCount > 0" class="rounded-full border border-slate-200 bg-white px-3 py-1 text-xs font-medium text-slate-600">
                  {{ t('mealReport.noteCount', { count: group.noteCount }) }}
                </span>
              </div>
              <div v-if="group.counts.length > 0" class="mt-4 grid gap-3 sm:grid-cols-2">
                <button
                  v-for="count in group.counts"
                  :key="`${group.groupId}-${count.optionId ?? 'other'}`"
                  class="rounded-2xl border border-slate-200 bg-white p-4 text-left transition hover:border-slate-300 hover:bg-slate-50"
                  type="button"
                  @click="openChoices(group, count)"
                >
                  <div class="text-sm font-medium text-slate-600">{{ normalizeOtherLabel(count) }}</div>
                  <div class="mt-2 text-2xl font-semibold text-slate-900">{{ count.count }}</div>
                </button>
              </div>
              <p v-else class="mt-4 text-sm text-slate-500">{{ t('mealReport.empty') }}</p>
            </article>
          </div>
        </div>
      </section>
    </div>
  </Transition>

  <teleport to="body">
    <Transition name="meal-panel" appear>
      <div v-if="choicesOpen" class="meal-panel-shell fixed inset-0 z-[60] flex items-end bg-slate-900/30 sm:items-stretch sm:justify-end">
        <button class="absolute inset-0 h-full w-full cursor-default" type="button" @click="closeChoices" />
        <aside class="meal-drawer-surface relative z-10 flex h-[88vh] w-full flex-col rounded-t-3xl bg-white shadow-2xl sm:h-full sm:max-w-xl sm:rounded-none sm:rounded-l-3xl">
          <div class="flex items-start justify-between gap-3 border-b border-slate-200 px-4 py-4 sm:px-5">
            <div>
              <div class="text-xs font-semibold uppercase tracking-[0.2em] text-slate-500">{{ t('mealReport.panelTitle') }}</div>
              <h3 class="mt-1 text-lg font-semibold text-slate-900">{{ choiceDrawerTitle }}</h3>
            </div>
            <button
              class="rounded-full border border-slate-200 px-3 py-2 text-xs font-medium text-slate-700 transition hover:border-slate-300 hover:bg-slate-50"
              type="button"
              @click="closeChoices"
            >
              {{ t('common.dismiss') }}
            </button>
          </div>

          <div class="space-y-4 border-b border-slate-200 px-4 py-4 sm:px-5">
            <input
              v-model="searchQuery"
              type="search"
              :placeholder="t('mealReport.searchPlaceholder')"
              class="w-full rounded-2xl border border-slate-200 px-4 py-3 text-sm text-slate-900"
            />
            <div class="flex flex-wrap gap-2">
              <button
                class="rounded-full border px-3 py-1.5 text-xs font-medium transition"
                :class="onlyNotes ? 'border-slate-900 bg-slate-900 text-white' : 'border-slate-200 bg-white text-slate-700 hover:border-slate-300'"
                type="button"
                @click="onlyNotes = !onlyNotes"
              >
                {{ t('mealReport.onlyNotes') }}
              </button>
              <span
                v-if="canToggleOnlyOther"
                class="rounded-full border border-slate-900 bg-slate-900 px-3 py-1.5 text-xs font-medium text-white"
              >
                {{ t('mealReport.onlyOther') }}
              </span>
            </div>
          </div>

          <div class="min-h-0 flex-1 overflow-y-auto px-4 py-4 sm:px-5">
            <div v-if="choicesLoading" class="rounded-2xl border border-dashed border-slate-200 bg-slate-50 p-4 text-sm text-slate-500">
              {{ t('mealReport.loading') }}
            </div>
            <div v-else-if="choicesErrorMessage" class="rounded-2xl border border-rose-200 bg-rose-50 p-4 text-sm text-rose-700">
              <div>{{ choicesErrorMessage }}</div>
              <button class="mt-3 rounded-full border border-rose-200 bg-white px-3 py-2 text-xs font-semibold text-rose-700" type="button" @click="loadChoices">
                {{ t('common.retry') }}
              </button>
            </div>
            <div v-else-if="(choices?.items.length ?? 0) === 0" class="rounded-2xl border border-dashed border-slate-200 bg-slate-50 p-4 text-sm text-slate-500">
              {{ t('mealReport.empty') }}
            </div>
            <div v-else class="space-y-3">
              <article
                v-for="item in choices?.items ?? []"
                :key="choiceItemKey(item)"
                class="rounded-2xl border border-slate-200 bg-slate-50 p-4"
              >
                <div class="flex flex-wrap items-start justify-between gap-3">
                  <div>
                    <h4 class="text-sm font-semibold text-slate-900">{{ item.participant.fullName }}</h4>
                    <div class="mt-1 flex flex-wrap items-center gap-x-3 gap-y-1 text-xs text-slate-500">
                      <span v-if="item.participant.roomNo">{{ t('mealReport.participantRoom', { room: item.participant.roomNo }) }}</span>
                      <a
                        v-if="item.participant.phone"
                        class="text-slate-600 underline-offset-2 hover:text-slate-900 hover:underline"
                        :href="normalizePhoneHref(item.participant.phone)"
                      >
                        {{ formatPhoneDisplay(item.participant.phone) || item.participant.phone }}
                      </a>
                    </div>
                  </div>
                  <span class="rounded-full border border-slate-200 bg-white px-3 py-1 text-[11px] font-medium text-slate-500">
                    {{ formatUtcDateTimeLocal(item.updatedAt) }}
                  </span>
                </div>

                <div v-if="item.otherText" class="mt-3 rounded-xl border border-slate-200 bg-white px-3 py-3 text-sm text-slate-700">
                  <div class="text-xs font-semibold uppercase tracking-wide text-slate-500">{{ t('mealReport.other') }}</div>
                  <p :class="!isExpanded(item, 'otherText') && isLongText(item.otherText) ? 'mt-2 line-clamp-3' : 'mt-2 whitespace-pre-wrap'">
                    {{ item.otherText }}
                  </p>
                  <button
                    v-if="isLongText(item.otherText)"
                    class="mt-2 text-xs font-semibold text-slate-700 underline"
                    type="button"
                    @click="toggleExpanded(item, 'otherText')"
                  >
                    {{ isExpanded(item, 'otherText') ? t('mealReport.less') : t('mealReport.more') }}
                  </button>
                </div>

                <div v-if="item.note" class="mt-3 rounded-xl border border-slate-200 bg-white px-3 py-3 text-sm text-slate-700">
                  <div class="text-xs font-semibold uppercase tracking-wide text-slate-500">{{ t('mealReport.note') }}</div>
                  <p :class="!isExpanded(item, 'note') && isLongText(item.note) ? 'mt-2 line-clamp-3' : 'mt-2 whitespace-pre-wrap'">
                    {{ item.note }}
                  </p>
                  <button
                    v-if="isLongText(item.note)"
                    class="mt-2 text-xs font-semibold text-slate-700 underline"
                    type="button"
                    @click="toggleExpanded(item, 'note')"
                  >
                    {{ isExpanded(item, 'note') ? t('mealReport.less') : t('mealReport.more') }}
                  </button>
                </div>
              </article>
            </div>
          </div>

          <div class="border-t border-slate-200 px-4 py-4 sm:px-5">
            <div class="flex items-center justify-between gap-3 text-sm text-slate-600">
              <span>{{ t('mealReport.results', { count: choices?.total ?? 0 }) }}</span>
              <span>{{ t('common.pageOf', { page: choices?.page ?? 1, total: Math.max(1, Math.ceil((choices?.total ?? 0) / (choices?.pageSize ?? pageSize))) }) }}</span>
            </div>
            <div class="mt-3 flex items-center justify-between gap-3">
              <button
                class="rounded-full border border-slate-200 px-4 py-2 text-sm font-medium text-slate-700 transition hover:border-slate-300 hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-50"
                type="button"
                :disabled="(choices?.page ?? 1) <= 1 || choicesLoading"
                @click="setPage((choices?.page ?? 1) - 1)"
              >
                {{ t('mealReport.previous') }}
              </button>
              <button
                class="rounded-full border border-slate-200 px-4 py-2 text-sm font-medium text-slate-700 transition hover:border-slate-300 hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-50"
                type="button"
                :disabled="(choices?.page ?? 1) >= Math.max(1, Math.ceil((choices?.total ?? 0) / (choices?.pageSize ?? pageSize))) || choicesLoading"
                @click="setPage((choices?.page ?? 1) + 1)"
              >
                {{ t('mealReport.next') }}
              </button>
            </div>
          </div>
        </aside>
      </div>
    </Transition>
  </teleport>
</template>

<style scoped>
.meal-panel-enter-active,
.meal-panel-leave-active {
  transition: opacity 180ms ease;
}

.meal-panel-enter-from,
.meal-panel-leave-to {
  opacity: 0;
}

.meal-panel-enter-active .meal-drawer-surface,
.meal-panel-leave-active .meal-drawer-surface {
  transition: transform 220ms ease, opacity 220ms ease;
}

.meal-panel-enter-from .meal-drawer-surface,
.meal-panel-leave-to .meal-drawer-surface {
  transform: translateY(100%);
  opacity: 0.98;
}

@media (min-width: 640px) {
  .meal-panel-enter-from .meal-drawer-surface,
  .meal-panel-leave-to .meal-drawer-surface {
    transform: translateX(100%);
  }
}
</style>
