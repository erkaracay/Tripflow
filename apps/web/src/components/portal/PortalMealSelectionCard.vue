<script setup lang="ts">
import { computed, onUnmounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { portalGetMeal, portalSaveMealSelections } from '../../lib/api'
import { useToast } from '../../lib/toast'
import type {
  EventActivity,
  MealSelectionDraft,
  PortalMealGroup,
  PortalMealResponse,
  PortalMealSelectionsUpsertRequest,
} from '../../types'

const props = defineProps<{
  activity: EventActivity
  eventTitle?: string
  sessionExpired?: boolean
}>()

const emit = defineEmits<{
  (event: 'session-expired'): void
  (event: 'saved', activityId: string): void
  (event: 'error', error: unknown): void
}>()

const { t } = useI18n()
const { pushToast } = useToast()

const loading = ref(false)
const loadError = ref<string | null>(null)
const groups = ref<PortalMealGroup[]>([])
const drafts = ref<Record<string, MealSelectionDraft>>({})
const openGroups = ref<Record<string, boolean>>({})
const sectionOpen = ref(false)
const saving = ref(false)
const savedBadgeVisible = ref(false)
let savedBadgeTimer: ReturnType<typeof setTimeout> | null = null

const isMeal = computed(() => props.activity.type === 'Meal')
const hasGroups = computed(() => groups.value.length > 0)

const buildDraft = (group: PortalMealGroup): MealSelectionDraft => ({
  selectedKind: group.selection?.optionId ? 'option' : group.selection?.otherText ? 'other' : null,
  optionId: group.selection?.optionId ?? null,
  otherText: group.selection?.otherText ?? '',
  note: group.selection?.note ?? '',
  errorKey: null,
})

const hasSelection = (group: PortalMealGroup) => Boolean(group.selection?.optionId || group.selection?.otherText)

const getDefaultGroupOpen = (group: PortalMealGroup) => !hasSelection(group)

const hasPendingSelection = (mealGroups: PortalMealGroup[]) => mealGroups.some((group) => !hasSelection(group))

const resetDrafts = (response: PortalMealResponse) => {
  groups.value = response.groups
  drafts.value = Object.fromEntries(response.groups.map((group) => [group.groupId, buildDraft(group)]))
  openGroups.value = Object.fromEntries(
    response.groups.map((group) => [
      group.groupId,
      getDefaultGroupOpen(group),
    ])
  )
  sectionOpen.value = hasPendingSelection(response.groups)
}

const clearSavedBadge = () => {
  savedBadgeVisible.value = false
  if (savedBadgeTimer) {
    globalThis.clearTimeout(savedBadgeTimer)
    savedBadgeTimer = null
  }
}

const handlePortalAuthError = (error: unknown) => {
  if (!error || typeof error !== 'object' || !('status' in error)) {
    return false
  }

  const status = Number((error as { status?: unknown }).status)
  if (status !== 401 && status !== 403) {
    return false
  }

  emit('session-expired')
  return true
}

const loadMeal = async () => {
  if (!isMeal.value || props.sessionExpired) {
    groups.value = []
    drafts.value = {}
    loadError.value = null
    return
  }

  loading.value = true
  loadError.value = null
  try {
    const response = await portalGetMeal(props.activity.id)
    resetDrafts(response)
  } catch (error) {
    if (handlePortalAuthError(error)) {
      return
    }

    loadError.value = error instanceof Error ? error.message : t('portal.meal.loadError')
    emit('error', error)
  } finally {
    loading.value = false
  }
}

watch(
  () => [props.activity.id, props.activity.type, props.sessionExpired] as const,
  () => {
    clearSavedBadge()
    void loadMeal()
  },
  { immediate: true }
)

const ensureDraft = (groupId: string) => {
  if (!drafts.value[groupId]) {
    drafts.value[groupId] = {
      selectedKind: null,
      optionId: null,
      otherText: '',
      note: '',
      errorKey: null,
    }
  }

  return drafts.value[groupId]
}

const isGroupOpen = (groupId: string) => openGroups.value[groupId] ?? false

const toggleGroup = (groupId: string) => {
  openGroups.value[groupId] = !isGroupOpen(groupId)
}

const selectOption = (groupId: string, optionId: string) => {
  const draft = ensureDraft(groupId)
  draft.selectedKind = 'option'
  draft.optionId = optionId
  draft.errorKey = null
}

const selectOther = (groupId: string) => {
  const draft = ensureDraft(groupId)
  draft.selectedKind = 'other'
  draft.optionId = null
  draft.errorKey = null
}

const selectedOptionValue = (groupId: string) => {
  const draft = ensureDraft(groupId)
  if (draft.selectedKind === 'other') {
    return 'other'
  }
  return draft.optionId ?? ''
}

const selectedGroupCount = computed(() =>
  groups.value.filter((group) => {
    const draft = ensureDraft(group.groupId)
    return Boolean(
      (draft.selectedKind === 'option' && draft.optionId) ||
        (draft.selectedKind === 'other' && draft.otherText.trim())
    )
  }).length
)

const groupBadges = (group: PortalMealGroup) => {
  const draft = ensureDraft(group.groupId)
  const badges: string[] = []

  if (draft.selectedKind === 'option' && draft.optionId) {
    const option = group.options.find((entry) => entry.id === draft.optionId)
    if (option?.label) {
      badges.push(option.label)
    }
  } else if (draft.selectedKind === 'other' && draft.otherText.trim()) {
    badges.push(t('portal.meal.other'))
  }

  if (group.allowNote && draft.note.trim()) {
    badges.push(t('portal.meal.note'))
  }

  return badges
}

const groupSummary = (group: PortalMealGroup) => {
  const draft = ensureDraft(group.groupId)
  const parts: string[] = []

  if (draft.selectedKind === 'option' && draft.optionId) {
    const option = group.options.find((entry) => entry.id === draft.optionId)
    if (option?.label) {
      parts.push(option.label)
    }
  } else if (draft.selectedKind === 'other' && draft.otherText.trim()) {
    parts.push(`${t('portal.meal.other')}: ${draft.otherText.trim()}`)
  }

  if (group.allowNote && draft.note.trim()) {
    parts.push(t('portal.meal.note'))
  }

  return parts.join(' · ')
}

const validateDrafts = () => {
  let hasError = false

  groups.value.forEach((group) => {
    const draft = ensureDraft(group.groupId)
    draft.errorKey = null

    const normalizedOther = draft.otherText.trim()
    const normalizedNote = draft.note.trim()

    if (draft.selectedKind === 'other' && !normalizedOther) {
      draft.errorKey = 'portal.meal.otherRequired'
      sectionOpen.value = true
      openGroups.value[group.groupId] = true
      hasError = true
      return
    }

    if (!draft.selectedKind && normalizedNote) {
      draft.errorKey = 'portal.meal.selectionRequired'
      sectionOpen.value = true
      openGroups.value[group.groupId] = true
      hasError = true
    }
  })

  return !hasError
}

const buildPayload = (): PortalMealSelectionsUpsertRequest => {
  const selections: PortalMealSelectionsUpsertRequest['selections'] = []

  groups.value.forEach((group) => {
    const draft = ensureDraft(group.groupId)
    const normalizedNote = group.allowNote ? draft.note.trim() || null : null

    if (draft.selectedKind === 'option' && draft.optionId) {
      selections.push({
        groupId: group.groupId,
        optionId: draft.optionId,
        otherText: null,
        note: normalizedNote,
      })
      return
    }

    if (draft.selectedKind === 'other') {
      const normalizedOther = draft.otherText.trim()
      if (!normalizedOther) {
        return
      }

      selections.push({
        groupId: group.groupId,
        optionId: null,
        otherText: normalizedOther,
        note: normalizedNote,
      })
    }
  })

  return { selections }
}

const prepareAnimatedElement = (element: HTMLElement) => {
  element.style.overflow = 'hidden'
  element.style.willChange = 'height, opacity'
  element.style.transition = 'height 180ms ease, opacity 180ms ease'
}

const cleanupAnimatedElement = (element: HTMLElement) => {
  element.style.height = ''
  element.style.opacity = ''
  element.style.overflow = ''
  element.style.willChange = ''
  element.style.transition = ''
}

const animateExpandEnter = (el: Element) => {
  const element = el as HTMLElement
  prepareAnimatedElement(element)
  element.style.height = '0px'
  element.style.opacity = '0'
  requestAnimationFrame(() => {
    element.style.height = `${element.scrollHeight}px`
    element.style.opacity = '1'
  })
}

const animateExpandAfterEnter = (el: Element) => {
  cleanupAnimatedElement(el as HTMLElement)
}

const animateExpandBeforeLeave = (el: Element) => {
  const element = el as HTMLElement
  prepareAnimatedElement(element)
  element.style.height = `${element.scrollHeight}px`
  element.style.opacity = '1'
}

const animateExpandLeave = (el: Element) => {
  const element = el as HTMLElement
  requestAnimationFrame(() => {
    element.style.height = '0px'
    element.style.opacity = '0'
  })
}

const animateExpandAfterLeave = (el: Element) => {
  cleanupAnimatedElement(el as HTMLElement)
}

const saveSelections = async () => {
  if (saving.value || !hasGroups.value) {
    return
  }

  if (!validateDrafts()) {
    return
  }

  saving.value = true
  try {
    const response = await portalSaveMealSelections(props.activity.id, buildPayload())
    resetDrafts(response)
    clearSavedBadge()
    savedBadgeVisible.value = true
    savedBadgeTimer = globalThis.setTimeout(() => {
      savedBadgeVisible.value = false
      savedBadgeTimer = null
    }, 3200)
    emit('saved', props.activity.id)
    pushToast({ key: 'portal.meal.saved', tone: 'success' })
  } catch (error) {
    if (handlePortalAuthError(error)) {
      return
    }

    emit('error', error)
    pushToast({ key: 'portal.meal.saveError', tone: 'error' })
  } finally {
    saving.value = false
  }
}

onUnmounted(() => {
  clearSavedBadge()
})
</script>

<template>
  <section v-if="isMeal" class="mt-4 rounded-2xl border border-slate-200 bg-white p-4 shadow-sm">
    <button
      class="flex w-full items-start justify-between gap-3 text-left"
      type="button"
      :aria-expanded="sectionOpen"
      @click="sectionOpen = !sectionOpen"
    >
      <div class="min-w-0">
        <h4 class="text-sm font-semibold text-slate-900">{{ t('portal.meal.title') }}</h4>
        <div v-if="hasGroups" class="mt-2 flex flex-wrap gap-2">
          <span class="inline-flex items-center rounded-full border border-slate-200 bg-slate-50 px-2.5 py-1 text-xs font-medium text-slate-600">
            {{ selectedGroupCount }}/{{ groups.length }}
          </span>
        </div>
      </div>
      <div class="flex items-center gap-2">
        <span
          v-if="savedBadgeVisible"
          class="rounded border border-emerald-200 bg-emerald-50 px-2 py-1 text-xs font-medium text-emerald-700"
        >
          {{ t('portal.meal.saved') }}
        </span>
        <span
          class="inline-flex h-8 w-8 shrink-0 items-center justify-center rounded-full border border-slate-200 bg-white text-slate-500 transition"
          :class="sectionOpen ? 'rotate-180' : ''"
          aria-hidden="true"
        >
          <svg class="h-4 w-4" viewBox="0 0 20 20" fill="none">
            <path d="M5 8l5 5 5-5" stroke="currentColor" stroke-linecap="round" stroke-linejoin="round" stroke-width="1.75" />
          </svg>
        </span>
      </div>
    </button>

    <Transition
      @enter="animateExpandEnter"
      @after-enter="animateExpandAfterEnter"
      @before-leave="animateExpandBeforeLeave"
      @leave="animateExpandLeave"
      @after-leave="animateExpandAfterLeave"
    >
      <div v-if="sectionOpen" class="mt-4 space-y-4">
        <div
          v-if="loading"
          class="rounded-xl border border-dashed border-slate-200 bg-slate-50 p-4 text-sm text-slate-500"
        >
          {{ t('portal.meal.loading') }}
        </div>

        <div
          v-else-if="loadError"
          class="rounded-xl border border-rose-200 bg-rose-50 p-4 text-sm text-rose-700"
        >
          <div>{{ loadError }}</div>
          <button
            class="mt-3 rounded-full border border-rose-200 bg-white px-3 py-2 text-xs font-semibold text-rose-700 hover:border-rose-300"
            type="button"
            :disabled="saving"
            @click="loadMeal"
          >
            {{ t('common.retry') }}
          </button>
        </div>

        <p v-else-if="!hasGroups" class="text-sm text-slate-500">
          {{ t('portal.meal.empty') }}
        </p>

        <div v-else class="space-y-4">
          <article
            v-for="group in groups"
            :key="group.groupId"
            class="rounded-2xl border border-slate-200 bg-slate-50 p-4"
          >
            <button
              class="flex w-full items-start justify-between gap-3 text-left"
              type="button"
              :aria-expanded="isGroupOpen(group.groupId)"
              @click="toggleGroup(group.groupId)"
            >
              <div class="min-w-0">
                <h5 class="text-sm font-semibold text-slate-900">{{ group.title }}</h5>
                <div v-if="groupBadges(group).length" class="mt-2 flex flex-wrap gap-2">
                  <span
                    v-for="badge in groupBadges(group)"
                    :key="badge"
                    class="inline-flex items-center rounded-full border border-sky-200 bg-sky-50 px-2.5 py-1 text-xs font-medium text-sky-700"
                  >
                    {{ badge }}
                  </span>
                </div>
                <p v-else-if="groupSummary(group)" class="mt-1 truncate text-xs text-slate-500">
                  {{ groupSummary(group) }}
                </p>
              </div>
              <span
                class="mt-0.5 inline-flex h-8 w-8 shrink-0 items-center justify-center rounded-full border border-slate-200 bg-white text-slate-500 transition"
                :class="isGroupOpen(group.groupId) ? 'rotate-180' : ''"
                aria-hidden="true"
              >
                <svg class="h-4 w-4" viewBox="0 0 20 20" fill="none">
                  <path d="M5 8l5 5 5-5" stroke="currentColor" stroke-linecap="round" stroke-linejoin="round" stroke-width="1.75" />
                </svg>
              </span>
            </button>

            <Transition
              @enter="animateExpandEnter"
              @after-enter="animateExpandAfterEnter"
              @before-leave="animateExpandBeforeLeave"
              @leave="animateExpandLeave"
              @after-leave="animateExpandAfterLeave"
            >
              <div v-if="isGroupOpen(group.groupId)" class="mt-4 space-y-4">
                <div class="space-y-2">
                  <label
                    v-for="option in group.options"
                    :key="option.id"
                    class="flex cursor-pointer items-start gap-3 rounded-xl border border-slate-200 bg-white px-3 py-3 text-sm text-slate-700"
                  >
                    <input
                      :checked="selectedOptionValue(group.groupId) === option.id"
                      class="mt-0.5 h-4 w-4 border-slate-300 text-slate-900 focus:ring-slate-500"
                      type="radio"
                      :name="`portal-meal-${group.groupId}`"
                      :value="option.id"
                      :disabled="saving"
                      @change="selectOption(group.groupId, option.id)"
                    />
                    <span>{{ option.label }}</span>
                  </label>

                  <label
                    v-if="group.allowOther"
                    class="flex cursor-pointer items-start gap-3 rounded-xl border border-slate-200 bg-white px-3 py-3 text-sm text-slate-700"
                  >
                    <input
                      :checked="selectedOptionValue(group.groupId) === 'other'"
                      class="mt-0.5 h-4 w-4 border-slate-300 text-slate-900 focus:ring-slate-500"
                      type="radio"
                      :name="`portal-meal-${group.groupId}`"
                      value="other"
                      :disabled="saving"
                      @change="selectOther(group.groupId)"
                    />
                    <div class="min-w-0 flex-1">
                      <div>{{ t('portal.meal.other') }}</div>
                      <input
                        v-if="ensureDraft(group.groupId).selectedKind === 'other'"
                        v-model="ensureDraft(group.groupId).otherText"
                        class="mt-3 w-full rounded-xl border border-slate-200 px-3 py-2 text-sm text-slate-900"
                        :class="ensureDraft(group.groupId).errorKey === 'portal.meal.otherRequired' ? 'border-rose-300 bg-rose-50' : ''"
                        type="text"
                        :disabled="saving"
                        :placeholder="t('portal.meal.otherPlaceholder')"
                        @input="ensureDraft(group.groupId).errorKey = null"
                      />
                    </div>
                  </label>
                </div>

                <label v-if="group.allowNote" class="grid gap-1 text-sm">
                  <span class="text-slate-600">{{ t('portal.meal.note') }}</span>
                  <textarea
                    v-model="ensureDraft(group.groupId).note"
                    rows="3"
                    class="w-full rounded-xl border border-slate-200 px-3 py-2 text-sm text-slate-900"
                    :class="ensureDraft(group.groupId).errorKey === 'portal.meal.selectionRequired' ? 'border-rose-300 bg-rose-50' : ''"
                    :disabled="saving"
                    :placeholder="t('portal.meal.notePlaceholder')"
                    @input="ensureDraft(group.groupId).errorKey = null"
                  />
                </label>

                <p v-if="ensureDraft(group.groupId).errorKey" class="text-sm text-rose-600">
                  {{ t(ensureDraft(group.groupId).errorKey!) }}
                </p>
              </div>
            </Transition>
          </article>

          <div class="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
            <div class="text-xs text-slate-500">
              {{ t('portal.meal.helper') }}
            </div>
            <button
              class="inline-flex items-center justify-center rounded-full bg-slate-900 px-4 py-2 text-sm font-semibold text-white transition hover:bg-slate-800 disabled:cursor-not-allowed disabled:opacity-60"
              type="button"
              :disabled="saving"
              @click="saveSelections"
            >
              {{ saving ? t('portal.meal.saving') : t('portal.meal.save') }}
            </button>
          </div>
        </div>
      </div>
    </Transition>
  </section>
</template>
