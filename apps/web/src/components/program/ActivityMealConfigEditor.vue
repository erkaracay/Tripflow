<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { apiDelete, apiGet, apiPost, apiPut } from '../../lib/api'
import { useToast } from '../../lib/toast'
import ConfirmDialog from '../ui/ConfirmDialog.vue'
import type {
  CreateMealGroupPayload,
  CreateMealOptionPayload,
  MealGroup,
  MealGroupsResponse,
  MealOption,
  UpdateMealGroupPayload,
  UpdateMealOptionPayload,
} from '../../types'

const props = withDefaults(defineProps<{
  mode: 'admin' | 'guide'
  eventId: string
  activityId: string | null
  activityType: string
  disabled?: boolean
}>(), {
  disabled: false,
})

const emit = defineEmits<{
  (event: 'changed'): void
  (event: 'error', code?: string): void
}>()

const { t } = useI18n()
const { pushToast } = useToast()

const loading = ref(false)
const loadError = ref<string | null>(null)
const groups = ref<MealGroup[]>([])
const openGroups = ref<Record<string, boolean>>({})
const creatingGroup = ref(false)
const savingGroupIds = ref<string[]>([])
const deletingGroupId = ref<string | null>(null)
const creatingOptionGroupIds = ref<string[]>([])
const savingOptionIds = ref<string[]>([])
const deletingOptionId = ref<string | null>(null)
const confirmDeleteGroupOpen = ref(false)
const confirmDeleteOptionOpen = ref(false)
const deleteTargetGroup = ref<MealGroup | null>(null)
const deleteTargetOption = ref<{ groupId: string; option: MealOption } | null>(null)

const newGroup = ref({
  title: '',
  sortOrder: '',
  allowOther: true,
  allowNote: true,
  isActive: true,
})

const newOptions = ref<Record<string, { label: string; sortOrder: string; isActive: boolean }>>({})

const isMeal = computed(() => props.activityType === 'Meal')
const canEditExisting = computed(() => isMeal.value && Boolean(props.activityId))
const apiBase = computed(() => (props.mode === 'guide' ? '/api/guide/events' : '/api/events'))

const toOptionalPositiveInt = (value: string | number) => {
  if (typeof value === 'number') {
    return Number.isInteger(value) && value >= 1 ? value : null
  }

  const trimmed = value.trim()
  if (!trimmed) {
    return undefined
  }

  const parsed = Number(trimmed)
  return Number.isInteger(parsed) && parsed >= 1 ? parsed : null
}

const ensureOptionDraft = (groupId: string) => {
  if (!newOptions.value[groupId]) {
    newOptions.value[groupId] = {
      label: '',
      sortOrder: '',
      isActive: true,
    }
  }

  return newOptions.value[groupId]
}

const normalizeCode = (err: unknown) => {
  if (!err || typeof err !== 'object') return null
  const payload = (err as { payload?: unknown }).payload
  if (!payload || typeof payload !== 'object' || !('code' in payload)) return null
  return String((payload as { code?: unknown }).code ?? '') || null
}

const handleMealError = (err: unknown) => {
  const code = normalizeCode(err)
  emit('error', code ?? undefined)

  if (code === 'not_meal_activity') {
    pushToast({ key: 'admin.program.meal.errors.notMealActivity', tone: 'error' })
    return
  }
  if (code === 'group_in_use') {
    pushToast({ key: 'admin.program.meal.errors.groupInUse', tone: 'error' })
    return
  }
  if (code === 'option_in_use') {
    pushToast({ key: 'admin.program.meal.errors.optionInUse', tone: 'error' })
    return
  }
  if (code === 'invalid_sort_order') {
    pushToast({ key: 'admin.program.meal.errors.invalidSortOrder', tone: 'error' })
    return
  }

  pushToast({ key: 'errors.generic', tone: 'error' })
}

const loadGroups = async () => {
  if (!canEditExisting.value) {
    groups.value = []
    loadError.value = null
    return
  }

  loading.value = true
  loadError.value = null
  try {
    const response = await apiGet<MealGroupsResponse>(`${apiBase.value}/${props.eventId}/activities/${props.activityId}/meal-groups`)
    const previousOpen = openGroups.value
    groups.value = response.groups
    openGroups.value = Object.fromEntries(
      response.groups.map((group) => [group.id, previousOpen[group.id] ?? true])
    )
    response.groups.forEach((group) => {
      ensureOptionDraft(group.id)
    })
  } catch (err) {
    loadError.value = err instanceof Error ? err.message : t('errors.generic')
  } finally {
    loading.value = false
  }
}

const isGroupOpen = (groupId: string) => openGroups.value[groupId] ?? true

const toggleGroup = (groupId: string) => {
  openGroups.value[groupId] = !isGroupOpen(groupId)
}

watch(
  () => [props.activityId, props.activityType, props.mode, props.eventId] as const,
  () => {
    void loadGroups()
  },
  { immediate: true }
)

const createGroup = async () => {
  const title = newGroup.value.title.trim()
  if (!title || !canEditExisting.value || props.disabled || creatingGroup.value) {
    if (!title) {
      pushToast({ key: 'admin.program.meal.errors.groupTitleRequired', tone: 'error' })
    }
    return
  }

  const payload: CreateMealGroupPayload = {
    title,
    sortOrder: undefined,
    allowOther: newGroup.value.allowOther,
    allowNote: newGroup.value.allowNote,
    isActive: newGroup.value.isActive,
  }

  const sortOrder = toOptionalPositiveInt(newGroup.value.sortOrder)
  if (sortOrder === null) {
    pushToast({ key: 'admin.program.meal.errors.invalidSortOrder', tone: 'error' })
    return
  }
  payload.sortOrder = sortOrder

  creatingGroup.value = true
  try {
    await apiPost(`${apiBase.value}/${props.eventId}/activities/${props.activityId}/meal-groups`, payload)
    newGroup.value = {
      title: '',
      sortOrder: '',
      allowOther: true,
      allowNote: true,
      isActive: true,
    }
    await loadGroups()
    emit('changed')
    pushToast({ key: 'toast.saved', tone: 'success' })
  } catch (err) {
    handleMealError(err)
  } finally {
    creatingGroup.value = false
  }
}

const saveGroup = async (group: MealGroup) => {
  if (props.disabled || savingGroupIds.value.includes(group.id)) return
  if (!group.title.trim()) {
    pushToast({ key: 'admin.program.meal.errors.groupTitleRequired', tone: 'error' })
    return
  }

  savingGroupIds.value = [...savingGroupIds.value, group.id]
  const payload: UpdateMealGroupPayload = {
    title: group.title.trim(),
    sortOrder: group.sortOrder,
    allowOther: group.allowOther,
    allowNote: group.allowNote,
    isActive: group.isActive,
  }

  const sortOrder = toOptionalPositiveInt(group.sortOrder)
  if (sortOrder === null || sortOrder === undefined) {
    pushToast({ key: 'admin.program.meal.errors.invalidSortOrder', tone: 'error' })
    return
  }
  payload.sortOrder = sortOrder

  try {
    await apiPut(`${apiBase.value}/${props.eventId}/meal-groups/${group.id}`, payload)
    await loadGroups()
    emit('changed')
    pushToast({ key: 'toast.saved', tone: 'success' })
  } catch (err) {
    handleMealError(err)
  } finally {
    savingGroupIds.value = savingGroupIds.value.filter((id) => id !== group.id)
  }
}

const askDeleteGroup = (group: MealGroup) => {
  deleteTargetGroup.value = group
  confirmDeleteGroupOpen.value = true
}

const deleteGroup = async () => {
  const group = deleteTargetGroup.value
  if (!group) return

  deletingGroupId.value = group.id
  try {
    await apiDelete(`${apiBase.value}/${props.eventId}/meal-groups/${group.id}`)
    confirmDeleteGroupOpen.value = false
    deleteTargetGroup.value = null
    await loadGroups()
    emit('changed')
    pushToast({ key: 'toast.deleted', tone: 'success' })
  } catch (err) {
    handleMealError(err)
  } finally {
    deletingGroupId.value = null
  }
}

const createOption = async (groupId: string) => {
  const draft = ensureOptionDraft(groupId)
  const label = draft.label.trim()
  if (!label || props.disabled || creatingOptionGroupIds.value.includes(groupId)) {
    if (!label) {
      pushToast({ key: 'admin.program.meal.errors.optionLabelRequired', tone: 'error' })
    }
    return
  }

  creatingOptionGroupIds.value = [...creatingOptionGroupIds.value, groupId]
  const payload: CreateMealOptionPayload = {
    label,
    sortOrder: undefined,
    isActive: draft.isActive,
  }

  const sortOrder = toOptionalPositiveInt(draft.sortOrder)
  if (sortOrder === null) {
    pushToast({ key: 'admin.program.meal.errors.invalidSortOrder', tone: 'error' })
    return
  }
  payload.sortOrder = sortOrder

  try {
    await apiPost(`${apiBase.value}/${props.eventId}/meal-groups/${groupId}/options`, payload)
    newOptions.value[groupId] = { label: '', sortOrder: '', isActive: true }
    await loadGroups()
    emit('changed')
    pushToast({ key: 'toast.saved', tone: 'success' })
  } catch (err) {
    handleMealError(err)
  } finally {
    creatingOptionGroupIds.value = creatingOptionGroupIds.value.filter((id) => id !== groupId)
  }
}

const saveOption = async (option: MealOption) => {
  if (props.disabled || savingOptionIds.value.includes(option.id)) return
  if (!option.label.trim()) {
    pushToast({ key: 'admin.program.meal.errors.optionLabelRequired', tone: 'error' })
    return
  }

  savingOptionIds.value = [...savingOptionIds.value, option.id]
  const payload: UpdateMealOptionPayload = {
    label: option.label.trim(),
    sortOrder: option.sortOrder,
    isActive: option.isActive,
  }

  const sortOrder = toOptionalPositiveInt(option.sortOrder)
  if (sortOrder === null || sortOrder === undefined) {
    pushToast({ key: 'admin.program.meal.errors.invalidSortOrder', tone: 'error' })
    return
  }
  payload.sortOrder = sortOrder

  try {
    await apiPut(`${apiBase.value}/${props.eventId}/meal-options/${option.id}`, payload)
    await loadGroups()
    emit('changed')
    pushToast({ key: 'toast.saved', tone: 'success' })
  } catch (err) {
    handleMealError(err)
  } finally {
    savingOptionIds.value = savingOptionIds.value.filter((id) => id !== option.id)
  }
}

const askDeleteOption = (groupId: string, option: MealOption) => {
  deleteTargetOption.value = { groupId, option }
  confirmDeleteOptionOpen.value = true
}

const deleteOption = async () => {
  const target = deleteTargetOption.value
  if (!target) return

  deletingOptionId.value = target.option.id
  try {
    await apiDelete(`${apiBase.value}/${props.eventId}/meal-options/${target.option.id}`)
    confirmDeleteOptionOpen.value = false
    deleteTargetOption.value = null
    await loadGroups()
    emit('changed')
    pushToast({ key: 'toast.deleted', tone: 'success' })
  } catch (err) {
    handleMealError(err)
  } finally {
    deletingOptionId.value = null
  }
}
</script>

<template>
  <section v-if="isMeal" class="rounded-2xl border border-slate-200 bg-slate-50 p-4 md:col-span-2">
    <div class="flex flex-col gap-1 sm:flex-row sm:items-start sm:justify-between">
      <div>
        <h4 class="text-sm font-semibold text-slate-900">{{ t('admin.program.meal.title') }}</h4>
        <p class="text-xs text-slate-500">{{ t('admin.program.meal.helper') }}</p>
      </div>
    </div>

    <div v-if="!activityId" class="mt-4 rounded-xl border border-dashed border-slate-300 bg-white p-4 text-sm text-slate-500">
      {{ t('admin.program.meal.unsavedHelper') }}
    </div>

    <template v-else>
      <div v-if="loading" class="mt-4 rounded-xl border border-slate-200 bg-white p-4 text-sm text-slate-500">
        {{ t('admin.program.meal.loading') }}
      </div>

      <div v-else-if="loadError" class="mt-4 rounded-xl border border-rose-200 bg-rose-50 p-4 text-sm text-rose-700">
        <div>{{ loadError }}</div>
        <button
          class="mt-2 rounded-lg border border-rose-200 bg-white px-3 py-2 text-xs font-medium text-rose-700"
          type="button"
          :disabled="disabled"
          @click="loadGroups"
        >
          {{ t('common.retry') }}
        </button>
      </div>

      <div v-else class="mt-4 space-y-4">
        <div class="rounded-xl border border-slate-200 bg-white p-4">
          <div class="grid gap-3 md:grid-cols-[minmax(0,1fr)_minmax(120px,160px)]">
            <label class="grid min-w-0 gap-1 text-sm">
              <span class="text-slate-600">{{ t('admin.program.meal.groupTitle') }}</span>
              <input v-model="newGroup.title" type="text" class="rounded border border-slate-200 px-3 py-2 text-sm" :disabled="disabled || creatingGroup" />
            </label>
            <label class="grid min-w-0 gap-1 text-sm">
              <span class="text-slate-600">{{ t('admin.program.meal.sortOrder') }}</span>
              <input v-model="newGroup.sortOrder" type="number" min="1" class="w-full rounded border border-slate-200 px-3 py-2 text-sm" :disabled="disabled || creatingGroup" />
            </label>
          </div>
          <div class="mt-3 flex flex-wrap gap-4">
            <label class="inline-flex min-w-0 items-start gap-2 text-sm leading-snug text-slate-600">
              <input v-model="newGroup.allowOther" type="checkbox" class="h-4 w-4 rounded border-slate-300" :disabled="disabled || creatingGroup" />
              <span>{{ t('admin.program.meal.allowOther') }}</span>
            </label>
            <label class="inline-flex min-w-0 items-start gap-2 text-sm leading-snug text-slate-600">
              <input v-model="newGroup.allowNote" type="checkbox" class="h-4 w-4 rounded border-slate-300" :disabled="disabled || creatingGroup" />
              <span>{{ t('admin.program.meal.allowNote') }}</span>
            </label>
          </div>
          <div class="mt-3 flex flex-wrap items-center justify-between gap-3">
            <label class="inline-flex items-center gap-2 text-sm text-slate-600">
              <input v-model="newGroup.isActive" type="checkbox" class="h-4 w-4 rounded border-slate-300" :disabled="disabled || creatingGroup" />
              {{ t('admin.program.meal.active') }}
            </label>
            <button class="rounded-lg bg-slate-900 px-3 py-2 text-sm font-medium text-white hover:bg-slate-800 disabled:opacity-50" type="button" :disabled="disabled || creatingGroup" @click="createGroup">
              {{ creatingGroup ? t('common.saving') : t('admin.program.meal.addGroup') }}
            </button>
          </div>
        </div>

        <p v-if="groups.length === 0" class="rounded-xl border border-dashed border-slate-300 bg-white p-4 text-sm text-slate-500">
          {{ t('admin.program.meal.empty') }}
        </p>

        <article v-for="group in groups" :key="group.id" class="rounded-xl border border-slate-200 bg-white p-4 shadow-sm">
          <button
            class="flex w-full items-start justify-between gap-3 text-left"
            type="button"
            :aria-expanded="isGroupOpen(group.id)"
            @click="toggleGroup(group.id)"
          >
            <div class="min-w-0">
              <h5 class="text-sm font-semibold text-slate-900">{{ group.title || t('admin.program.meal.groupTitle') }}</h5>
              <div class="mt-2 flex flex-wrap gap-2">
                <span class="inline-flex items-center rounded-full border border-slate-200 bg-slate-50 px-2.5 py-1 text-xs font-medium text-slate-600">
                  #{{ group.sortOrder }}
                </span>
                <span
                  class="inline-flex items-center rounded-full px-2.5 py-1 text-xs font-medium"
                  :class="group.isActive ? 'border border-emerald-200 bg-emerald-50 text-emerald-700' : 'border border-slate-200 bg-slate-50 text-slate-500'"
                >
                  {{ group.isActive ? t('admin.program.meal.active') : t('common.inactive') }}
                </span>
                <span class="inline-flex items-center rounded-full border border-slate-200 bg-slate-50 px-2.5 py-1 text-xs font-medium text-slate-600">
                  {{ group.options.length }} {{ t('admin.program.meal.optionsTitle').toLowerCase() }}
                </span>
              </div>
            </div>
            <span
              class="mt-0.5 inline-flex h-8 w-8 shrink-0 items-center justify-center rounded-full border border-slate-200 bg-white text-slate-500 transition"
              :class="isGroupOpen(group.id) ? 'rotate-180' : ''"
              aria-hidden="true"
            >
              <svg class="h-4 w-4" viewBox="0 0 20 20" fill="none">
                <path d="M5 8l5 5 5-5" stroke="currentColor" stroke-linecap="round" stroke-linejoin="round" stroke-width="1.75" />
              </svg>
            </span>
          </button>

          <div v-if="isGroupOpen(group.id)" class="mt-4">
            <div class="grid gap-3 md:grid-cols-[minmax(0,1fr)_minmax(120px,160px)]">
              <label class="grid min-w-0 gap-1 text-sm">
                <span class="text-slate-600">{{ t('admin.program.meal.groupTitle') }}</span>
                <input v-model="group.title" type="text" class="rounded border border-slate-200 px-3 py-2 text-sm" :disabled="disabled || savingGroupIds.includes(group.id) || deletingGroupId === group.id" />
              </label>
              <label class="grid min-w-0 gap-1 text-sm">
                <span class="text-slate-600">{{ t('admin.program.meal.sortOrder') }}</span>
                <input v-model.number="group.sortOrder" type="number" min="1" class="w-full rounded border border-slate-200 px-3 py-2 text-sm" :disabled="disabled || savingGroupIds.includes(group.id) || deletingGroupId === group.id" />
              </label>
            </div>
            <div class="mt-3 flex flex-wrap gap-4">
              <label class="inline-flex min-w-0 items-start gap-2 text-sm leading-snug text-slate-600">
                <input v-model="group.allowOther" type="checkbox" class="h-4 w-4 rounded border-slate-300" :disabled="disabled || savingGroupIds.includes(group.id) || deletingGroupId === group.id" />
                <span>{{ t('admin.program.meal.allowOther') }}</span>
              </label>
              <label class="inline-flex min-w-0 items-start gap-2 text-sm leading-snug text-slate-600">
                <input v-model="group.allowNote" type="checkbox" class="h-4 w-4 rounded border-slate-300" :disabled="disabled || savingGroupIds.includes(group.id) || deletingGroupId === group.id" />
                <span>{{ t('admin.program.meal.allowNote') }}</span>
              </label>
            </div>

            <div class="mt-3 flex flex-wrap items-center justify-between gap-3">
              <label class="inline-flex items-center gap-2 text-sm text-slate-600">
                <input v-model="group.isActive" type="checkbox" class="h-4 w-4 rounded border-slate-300" :disabled="disabled || savingGroupIds.includes(group.id) || deletingGroupId === group.id" />
                {{ t('admin.program.meal.active') }}
              </label>
              <div class="flex flex-wrap gap-2">
                <button class="rounded-lg border border-slate-200 px-3 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-50" type="button" :disabled="disabled || savingGroupIds.includes(group.id) || deletingGroupId === group.id" @click="saveGroup(group)">
                  {{ savingGroupIds.includes(group.id) ? t('common.saving') : t('common.save') }}
                </button>
                <button class="rounded-lg border border-rose-200 px-3 py-2 text-sm font-medium text-rose-700 hover:bg-rose-50 disabled:opacity-50" type="button" :disabled="disabled || savingGroupIds.includes(group.id) || deletingGroupId === group.id" @click="askDeleteGroup(group)">
                  {{ t('common.delete') }}
                </button>
              </div>
            </div>

            <div class="mt-4 border-t border-slate-200 pt-4">
              <div class="flex items-center justify-between gap-2">
                <h5 class="text-sm font-semibold text-slate-900">{{ t('admin.program.meal.optionsTitle') }}</h5>
                <span class="text-xs text-slate-500">{{ t('admin.program.meal.savedInstantly') }}</span>
              </div>

              <div class="mt-3 space-y-3">
                <div v-for="option in group.options" :key="option.id" class="rounded-xl border border-slate-200 bg-slate-50 p-3">
                  <div class="grid gap-3 md:grid-cols-[minmax(0,1fr)_minmax(120px,180px)]">
                    <label class="grid min-w-0 gap-1 text-sm">
                      <span class="text-slate-600">{{ t('admin.program.meal.optionLabel') }}</span>
                      <input v-model="option.label" type="text" class="rounded border border-slate-200 bg-white px-3 py-2 text-sm" :disabled="disabled || savingOptionIds.includes(option.id) || deletingOptionId === option.id" />
                    </label>
                    <label class="grid min-w-0 gap-1 text-sm">
                      <span class="text-slate-600">{{ t('admin.program.meal.sortOrder') }}</span>
                      <input v-model.number="option.sortOrder" type="number" min="1" class="w-full rounded border border-slate-200 bg-white px-3 py-2 text-sm" :disabled="disabled || savingOptionIds.includes(option.id) || deletingOptionId === option.id" />
                    </label>
                  </div>
                  <div class="mt-3 flex flex-wrap items-start justify-between gap-3">
                    <label class="inline-flex items-center gap-2 text-sm text-slate-600">
                      <input v-model="option.isActive" type="checkbox" class="h-4 w-4 rounded border-slate-300" :disabled="disabled || savingOptionIds.includes(option.id) || deletingOptionId === option.id" />
                      {{ t('admin.program.meal.active') }}
                    </label>
                    <div class="flex flex-wrap items-end gap-2">
                      <button class="rounded-lg border border-slate-200 px-3 py-2 text-sm font-medium text-slate-700 hover:bg-white disabled:opacity-50" type="button" :disabled="disabled || savingOptionIds.includes(option.id) || deletingOptionId === option.id" @click="saveOption(option)">
                        {{ savingOptionIds.includes(option.id) ? t('common.saving') : t('common.save') }}
                      </button>
                      <button class="rounded-lg border border-rose-200 px-3 py-2 text-sm font-medium text-rose-700 hover:bg-rose-50 disabled:opacity-50" type="button" :disabled="disabled || savingOptionIds.includes(option.id) || deletingOptionId === option.id" @click="askDeleteOption(group.id, option)">
                        {{ t('common.delete') }}
                      </button>
                    </div>
                  </div>
                </div>

              </div>

              <div class="mt-3 rounded-xl border border-dashed border-slate-300 bg-white p-3">
                <div class="grid gap-3 md:grid-cols-[minmax(0,1fr)_minmax(120px,180px)]">
                  <label class="grid min-w-0 gap-1 text-sm">
                    <span class="text-slate-600">{{ t('admin.program.meal.optionLabel') }}</span>
                    <input v-model="ensureOptionDraft(group.id).label" type="text" class="rounded border border-slate-200 px-3 py-2 text-sm" :disabled="disabled || creatingOptionGroupIds.includes(group.id)" />
                  </label>
                  <label class="grid min-w-0 gap-1 text-sm">
                    <span class="text-slate-600">{{ t('admin.program.meal.sortOrder') }}</span>
                    <input v-model="ensureOptionDraft(group.id).sortOrder" type="number" min="1" class="w-full rounded border border-slate-200 px-3 py-2 text-sm" :disabled="disabled || creatingOptionGroupIds.includes(group.id)" />
                  </label>
                </div>
                <div class="mt-3 flex flex-wrap items-start justify-between gap-3">
                  <label class="inline-flex items-center gap-2 text-sm text-slate-600">
                    <input v-model="ensureOptionDraft(group.id).isActive" type="checkbox" class="h-4 w-4 rounded border-slate-300" :disabled="disabled || creatingOptionGroupIds.includes(group.id)" />
                    {{ t('admin.program.meal.active') }}
                  </label>
                  <div class="flex flex-col items-end gap-2">
                    <button class="rounded-lg bg-slate-900 px-3 py-2 text-sm font-medium text-white hover:bg-slate-800 disabled:opacity-50" type="button" :disabled="disabled || creatingOptionGroupIds.includes(group.id)" @click="createOption(group.id)">
                      {{ creatingOptionGroupIds.includes(group.id) ? t('common.saving') : t('admin.program.meal.addOption') }}
                    </button>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </article>
      </div>
    </template>
  </section>

  <ConfirmDialog
    v-model:open="confirmDeleteGroupOpen"
    :title="t('admin.program.meal.deleteGroupTitle')"
    :message="t('admin.program.meal.deleteGroupConfirm', { title: deleteTargetGroup?.title ?? '' })"
    tone="danger"
    :confirm-disabled="deletingGroupId !== null"
    @confirm="deleteGroup"
  />

  <ConfirmDialog
    v-model:open="confirmDeleteOptionOpen"
    :title="t('admin.program.meal.deleteOptionTitle')"
    :message="t('admin.program.meal.deleteOptionConfirm', { label: deleteTargetOption?.option.label ?? '' })"
    tone="danger"
    :confirm-disabled="deletingOptionId !== null"
    @confirm="deleteOption"
  />
</template>
