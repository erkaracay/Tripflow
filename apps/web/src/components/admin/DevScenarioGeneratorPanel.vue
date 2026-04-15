<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { apiPost } from '../../lib/api'
import {
  formatTimeZoneOffsetPreview,
  getAllTimeZoneOptions,
  getBrowserTimeZone,
  getRecommendedTimeZoneValues,
} from '../../lib/timezones'
import AppCombobox from '../ui/AppCombobox.vue'
import AppSegmentedControl from '../ui/AppSegmentedControl.vue'
import type { CreateScenarioEventRequest, CreateScenarioEventResponse, ScenarioPresetDto } from '../../types'

const props = defineProps<{
  presets: ScenarioPresetDto[]
}>()

const emit = defineEmits<{
  (event: 'generated', value: CreateScenarioEventResponse): void
}>()

type FieldErrorState = {
  generic: string | null
  startDate: string | null
  timeZoneId: string | null
  dayCount: string | null
  accommodationCount: string | null
  participantCount: string | null
  equipmentTypeCount: string | null
  mealSelectionCoveragePercent: string | null
  eventCheckInCoveragePercent: string | null
  participantNamePrefix: string | null
}

const { t, te } = useI18n()
const browserTimeZone = getBrowserTimeZone()
const allTimeZoneOptions = getAllTimeZoneOptions()
const recommendedTimeZoneValues = getRecommendedTimeZoneValues(browserTimeZone)
const panelOpen = ref(false)
const advancedOpen = ref(false)
const submitting = ref(false)
const initialized = ref(false)

const numericInputClass =
  'h-11 rounded border border-slate-200 bg-white px-3 py-2 text-sm [appearance:textfield] focus:border-slate-400 focus:outline-none [&::-webkit-inner-spin-button]:appearance-none [&::-webkit-outer-spin-button]:appearance-none'

const form = reactive({
  preset: 'balanced' as ScenarioPresetDto['id'],
  name: '',
  startDate: getTodayLocalDateString(),
  timeZoneId: browserTimeZone ?? 'Europe/Istanbul',
  dayCount: 3,
  accommodationCount: 2,
  participantCount: 40,
  equipmentTypeCount: 2,
  mealMode: 'breakfast_only' as NonNullable<CreateScenarioEventRequest['mealMode']>,
  flightLegMode: 'mixed' as NonNullable<CreateScenarioEventRequest['flightLegMode']>,
  includeFlights: true,
  activityDensity: 'normal' as NonNullable<CreateScenarioEventRequest['activityDensity']>,
  mealSelectionCoveragePercent: 70,
  eventCheckInCoveragePercent: 20,
  participantNamingMode: 'random' as NonNullable<CreateScenarioEventRequest['participantNamingMode']>,
  participantNamePrefix: '',
  randomSeed: '',
})

const errors = reactive<FieldErrorState>({
  generic: null,
  startDate: null,
  timeZoneId: null,
  dayCount: null,
  accommodationCount: null,
  participantCount: null,
  equipmentTypeCount: null,
  mealSelectionCoveragePercent: null,
  eventCheckInCoveragePercent: null,
  participantNamePrefix: null,
})

const presetOptions = computed(() =>
  props.presets.map((preset) => ({
    value: preset.id,
    label: te(`admin.devTools.scenario.presets.${preset.id}`)
      ? t(`admin.devTools.scenario.presets.${preset.id}`)
      : preset.label,
  }))
)

const timeZonePreview = computed(() => formatTimeZoneOffsetPreview(form.timeZoneId))

const activityDensityOptions = computed(() => [
  { value: 'light', label: t('admin.devTools.scenario.activityDensityOptions.light') },
  { value: 'normal', label: t('admin.devTools.scenario.activityDensityOptions.normal') },
  { value: 'dense', label: t('admin.devTools.scenario.activityDensityOptions.dense') },
])

const mealModeOptions = computed(() => [
  { value: 'none', label: t('admin.devTools.scenario.mealModeOptions.none') },
  { value: 'breakfast_only', label: t('admin.devTools.scenario.mealModeOptions.breakfastOnly') },
  { value: 'breakfast_and_dinner', label: t('admin.devTools.scenario.mealModeOptions.breakfastAndDinner') },
])

const flightLegModeOptions = computed(() => [
  { value: 'mixed', label: t('admin.devTools.scenario.flightLegModeOptions.mixed') },
  { value: 'direct_only', label: t('admin.devTools.scenario.flightLegModeOptions.directOnly') },
  { value: 'layover_heavy', label: t('admin.devTools.scenario.flightLegModeOptions.layoverHeavy') },
])

const participantNamingOptions = computed(() => [
  { value: 'random', label: t('admin.devTools.scenario.namingOptions.random') },
  { value: 'prefix', label: t('admin.devTools.scenario.namingOptions.prefix') },
])

const resolveMaxAccommodationPlanCount = (dayCountRaw: unknown) => {
  const parsed = typeof dayCountRaw === 'number' && Number.isFinite(dayCountRaw) ? Math.trunc(dayCountRaw) : Number.NaN
  if (!Number.isFinite(parsed) || parsed <= 1) {
    return 1
  }

  return Math.max(1, Math.min(6, parsed - 1))
}

const resolveAccommodationCount = (raw: unknown, dayCountFallback: number) => {
  const maxCount = resolveMaxAccommodationPlanCount(dayCountFallback)
  const parsed = typeof raw === 'number' && Number.isFinite(raw) ? Math.trunc(raw) : Number.NaN
  if (Number.isFinite(parsed)) {
    return Math.min(maxCount, Math.max(1, parsed))
  }

  const defaultCount = dayCountFallback >= 5 ? 3 : dayCountFallback >= 3 ? 2 : 1
  return Math.min(defaultCount, maxCount)
}

const maxAccommodationPlanCount = computed(() => resolveMaxAccommodationPlanCount(form.dayCount))
const effectiveAccommodationPlanCount = computed(() => resolveAccommodationCount(form.accommodationCount, form.dayCount))

const ensureAccommodationCountDefault = () => {
  form.accommodationCount = resolveAccommodationCount(form.accommodationCount, form.dayCount)
}

const applyPreset = (presetId: ScenarioPresetDto['id']) => {
  const preset = props.presets.find((option) => option.id === presetId)
  if (!preset) {
    return
  }

  form.preset = preset.id
  form.dayCount = preset.defaults.dayCount
  form.accommodationCount = resolveAccommodationCount(
    (preset.defaults as { accommodationCount?: number }).accommodationCount,
    form.dayCount
  )
  form.participantCount = preset.defaults.participantCount
  form.equipmentTypeCount = preset.defaults.equipmentTypeCount
  form.activityDensity = preset.defaults.activityDensity
  form.mealMode = preset.defaults.mealMode
  form.flightLegMode = preset.defaults.flightLegMode
  form.includeFlights = preset.defaults.includeFlights
  form.mealSelectionCoveragePercent = preset.defaults.mealSelectionCoveragePercent
  form.eventCheckInCoveragePercent = preset.defaults.eventCheckInCoveragePercent
  form.participantNamingMode = preset.defaults.participantNamingMode
  if (form.participantNamingMode !== 'prefix') {
    form.participantNamePrefix = ''
  }
  ensureAccommodationCountDefault()
}

const handlePresetChange = (value: string) => {
  applyPreset(value as ScenarioPresetDto['id'])
}

watch(
  () => props.presets,
  (presets) => {
    if (!presets.length) {
      return
    }

    const defaultPreset = presets.find((preset) => preset.id === 'balanced') ?? presets[0]
    if (!defaultPreset) {
      return
    }

    if (!initialized.value) {
      applyPreset(defaultPreset.id)
      initialized.value = true
      return
    }

    if (!presets.some((preset) => preset.id === form.preset)) {
      applyPreset(defaultPreset.id)
    }
  },
  { immediate: true }
)

watch(
  () => form.participantNamingMode,
  (value) => {
    errors.participantNamePrefix = null
    if (value !== 'prefix') {
      form.participantNamePrefix = ''
    }
  }
)

watch(
  () => panelOpen.value,
  (isOpen) => {
    if (!isOpen) {
      return
    }

    ensureAccommodationCountDefault()
  },
  { immediate: true }
)

watch(
  () => form.dayCount,
  () => {
    ensureAccommodationCountDefault()
  }
)

const coverageCount = (total: number, percent: number) => Math.max(0, Math.min(total, Math.round(total * (percent / 100))))

const breakfastMealCount = computed(() => (form.mealMode === 'none' ? 0 : form.dayCount))
const dinnerMealCount = computed(() => (form.mealMode === 'breakfast_and_dinner' ? form.dayCount : 0))
const mealActivityCount = computed(() => breakfastMealCount.value + dinnerMealCount.value)
const baseNonMealActivityCount = computed(() => {
  if (form.activityDensity === 'light') {
    return 2 * form.dayCount
  }

  if (form.activityDensity === 'normal') {
    return 3 * form.dayCount
  }

  return 4 * form.dayCount
})
const activityCount = computed(() => baseNonMealActivityCount.value + mealActivityCount.value)
const mealSelectionCount = computed(() =>
  coverageCount(form.participantCount, form.mealSelectionCoveragePercent) * mealActivityCount.value
)
const eventCheckInCount = computed(() =>
  coverageCount(form.participantCount, form.eventCheckInCoveragePercent)
)
const layoverParticipantCount = computed(() => {
  if (!form.includeFlights) {
    return 0
  }

  if (form.flightLegMode === 'direct_only') {
    return 0
  }

  if (form.flightLegMode === 'layover_heavy') {
    return Math.floor(form.participantCount / 2)
  }

  return Math.floor(form.participantCount / 5)
})
const directParticipantCount = computed(() =>
  form.includeFlights ? Math.max(0, form.participantCount - layoverParticipantCount.value) : 0
)
const flightSegmentCount = computed(() => {
  if (!form.includeFlights) {
    return 0
  }

  if (form.flightLegMode === 'direct_only') {
    return form.participantCount * 2
  }

  return directParticipantCount.value * 2 + layoverParticipantCount.value * 4
})

const resetErrors = () => {
  errors.generic = null
  errors.startDate = null
  errors.timeZoneId = null
  errors.dayCount = null
  errors.accommodationCount = null
  errors.participantCount = null
  errors.equipmentTypeCount = null
  errors.mealSelectionCoveragePercent = null
  errors.eventCheckInCoveragePercent = null
  errors.participantNamePrefix = null
}

const setFieldErrorFromApi = (field?: string | null, code?: string | null, message?: string | null) => {
  if (field && field in errors) {
    const typedField = field as keyof FieldErrorState
    errors[typedField] = code ? `admin.devTools.scenario.errors.${code}` : (message ?? null)
    return
  }

  if (code) {
    errors.generic = `admin.devTools.scenario.errors.${code}`
    return
  }

  errors.generic = message ?? null
}

const submit = async () => {
  resetErrors()

  if (!form.startDate) {
    errors.startDate = 'admin.devTools.scenario.errors.startDateRequired'
    panelOpen.value = true
    return
  }

  if (!form.timeZoneId.trim()) {
    errors.timeZoneId = 'admin.devTools.scenario.errors.invalid_time_zone_id'
    panelOpen.value = true
    return
  }

  if (!Number.isFinite(form.accommodationCount) || form.accommodationCount < 1 || form.accommodationCount > 6) {
    errors.accommodationCount = 'admin.devTools.scenario.errors.invalid_accommodation_count'
    panelOpen.value = true
    return
  }

  if (form.participantNamingMode === 'prefix' && !form.participantNamePrefix.trim()) {
    errors.participantNamePrefix = 'admin.devTools.scenario.errors.invalid_participant_name_prefix'
    panelOpen.value = true
    advancedOpen.value = true
    return
  }

  submitting.value = true
  try {
    const payload: CreateScenarioEventRequest = {
      name: form.name.trim() || null,
      startDate: form.startDate,
      dayCount: form.dayCount,
      accommodationCount: form.accommodationCount,
      timeZoneId: form.timeZoneId.trim(),
      preset: form.preset,
      activityDensity: form.activityDensity,
      mealMode: form.mealMode,
      flightLegMode: form.includeFlights ? form.flightLegMode : null,
      participantCount: form.participantCount,
      equipmentTypeCount: form.equipmentTypeCount,
      includeFlights: form.includeFlights,
      mealSelectionCoveragePercent: form.mealSelectionCoveragePercent,
      eventCheckInCoveragePercent: form.eventCheckInCoveragePercent,
      participantNamingMode: form.participantNamingMode,
      participantNamePrefix: form.participantNamingMode === 'prefix' ? form.participantNamePrefix.trim() : null,
      randomSeed: form.randomSeed.trim() ? Number(form.randomSeed) : null,
    }

    const response = await apiPost<CreateScenarioEventResponse>('/api/dev/scenario-events', payload)
    emit('generated', response)
  } catch (error: unknown) {
    const payload =
      error && typeof error === 'object' && 'payload' in error
        ? (error as { payload?: { code?: string; field?: string; message?: string } }).payload
        : undefined

    setFieldErrorFromApi(payload?.field ?? null, payload?.code ?? null, payload?.message ?? (error instanceof Error ? error.message : null))
    panelOpen.value = true
    if (payload?.field === 'participantNamePrefix') {
      advancedOpen.value = true
    }
  } finally {
    submitting.value = false
  }
}

function getTodayLocalDateString() {
  const today = new Date()
  const year = today.getFullYear()
  const month = `${today.getMonth() + 1}`.padStart(2, '0')
  const day = `${today.getDate()}`.padStart(2, '0')
  return `${year}-${month}-${day}`
}
</script>

<template>
  <section class="rounded-lg border border-dashed border-slate-300 bg-slate-50/80 p-6 shadow-sm">
      <div class="flex flex-wrap items-start justify-between gap-3">
      <div class="min-w-0 flex-1 space-y-2">
        <div class="inline-flex items-center rounded-full border border-slate-300 bg-white px-2.5 py-1 text-[11px] font-semibold uppercase tracking-[0.14em] text-slate-500">
          {{ t('admin.devTools.badge') }}
        </div>
        <div>
          <h2 class="text-lg font-semibold text-slate-900">{{ t('admin.devTools.scenario.title') }}</h2>
          <p class="mt-1 max-w-3xl text-sm text-slate-600">{{ t('admin.devTools.scenario.subtitle') }}</p>
        </div>
      </div>

      <button
        class="shrink-0 self-start whitespace-nowrap rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm font-medium text-slate-700 hover:border-slate-300"
        type="button"
        @click="panelOpen = !panelOpen"
      >
        {{ panelOpen ? t('admin.devTools.hide') : t('admin.devTools.show') }}
      </button>
    </div>

    <Transition name="app-section-reveal">
      <div v-if="panelOpen" class="mt-5 space-y-5">
        <div class="rounded-2xl border border-white/70 bg-white p-4 shadow-sm">
          <div class="space-y-2">
            <div class="text-xs font-semibold uppercase tracking-[0.14em] text-slate-400">
              {{ t('admin.devTools.scenario.presetLabel') }}
            </div>
            <AppSegmentedControl
              :model-value="form.preset"
              :options="presetOptions"
              full-width
              :aria-label="t('admin.devTools.scenario.presetLabel')"
              @update:model-value="handlePresetChange"
            />
          </div>
        </div>

        <form class="grid gap-4 lg:grid-cols-2" @submit.prevent="submit">
          <label class="grid content-start gap-1 self-start text-sm">
            <span class="text-slate-600">{{ t('admin.devTools.scenario.nameLabel') }}</span>
            <input
              v-model.trim="form.name"
              class="h-11 rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
              :placeholder="t('admin.devTools.scenario.namePlaceholder')"
              type="text"
            />
            <div class="min-h-5">
              <p class="text-xs text-slate-500">{{ t('admin.devTools.scenario.nameHelper') }}</p>
            </div>
          </label>

          <label class="grid content-start gap-1 self-start text-sm">
            <span class="text-slate-600">{{ t('admin.devTools.scenario.startDateLabel') }}</span>
            <input
              v-model="form.startDate"
              class="h-11 rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
              :class="errors.startDate ? 'border-rose-400' : ''"
              type="date"
            />
            <div class="min-h-5">
              <p v-if="errors.startDate" class="text-xs text-rose-600">{{ t(errors.startDate) }}</p>
              <p v-else class="invisible text-xs">.</p>
            </div>
          </label>

          <div class="grid gap-1 text-sm lg:col-span-2">
            <span class="text-slate-600">{{ t('admin.devTools.scenario.timeZoneLabel') }}</span>
            <AppCombobox
              v-model="form.timeZoneId"
              :options="allTimeZoneOptions"
              :recommended-values="recommendedTimeZoneValues"
              :placeholder="t('admin.devTools.scenario.timeZonePlaceholder')"
              :search-placeholder="t('admin.devTools.scenario.timeZoneSearchPlaceholder')"
              :recommended-label="t('admin.devTools.scenario.timeZoneRecommended')"
              :all-label="t('admin.devTools.scenario.timeZoneAll')"
              :browse-all-label="t('admin.devTools.scenario.timeZoneBrowseAll')"
              :empty-label="t('admin.devTools.scenario.timeZoneEmpty')"
              :invalid="Boolean(errors.timeZoneId)"
              :aria-label="t('admin.devTools.scenario.timeZoneLabel')"
              :disabled="submitting"
              @update:model-value="errors.timeZoneId = null"
            />
            <p class="text-xs text-slate-500">
              {{ t('admin.devTools.scenario.timeZoneHelper', { offset: timeZonePreview || '—' }) }}
            </p>
            <p v-if="errors.timeZoneId" class="text-xs text-rose-600">{{ t(errors.timeZoneId) }}</p>
          </div>

          <label class="grid content-start self-start gap-1 text-sm">
            <span class="text-slate-600">{{ t('admin.devTools.scenario.dayCountLabel') }}</span>
            <input
              v-model.number="form.dayCount"
              :class="[numericInputClass, errors.dayCount ? 'border-rose-400' : '']"
              min="1"
              max="14"
              step="1"
              type="number"
            />
            <p v-if="errors.dayCount" class="text-xs text-rose-600">{{ t(errors.dayCount) }}</p>
          </label>

          <label class="grid content-start self-start gap-1 text-sm">
            <span class="text-slate-600">{{ t('admin.devTools.scenario.accommodationCountLabel') }}</span>
            <input
              v-model.number="form.accommodationCount"
              :class="[numericInputClass, errors.accommodationCount ? 'border-rose-400' : '']"
              min="1"
              :max="maxAccommodationPlanCount"
              step="1"
              type="number"
              @blur="ensureAccommodationCountDefault"
            />
            <div class="min-h-5">
              <p v-if="errors.accommodationCount" class="text-xs text-rose-600">{{ t(errors.accommodationCount) }}</p>
              <p v-else class="text-xs text-slate-500">
                {{ t('admin.devTools.scenario.accommodationCountHelper', { max: maxAccommodationPlanCount }) }}
              </p>
            </div>
          </label>

          <label class="grid content-start self-start gap-1 text-sm">
            <span class="text-slate-600">{{ t('admin.devTools.scenario.participantCountLabel') }}</span>
            <input
              v-model.number="form.participantCount"
              :class="[numericInputClass, errors.participantCount ? 'border-rose-400' : '']"
              min="0"
              max="500"
              step="1"
              type="number"
            />
            <p v-if="errors.participantCount" class="text-xs text-rose-600">{{ t(errors.participantCount) }}</p>
          </label>

          <label class="grid content-start self-start gap-1 text-sm">
            <span class="text-slate-600">{{ t('admin.devTools.scenario.equipmentTypeCountLabel') }}</span>
            <input
              v-model.number="form.equipmentTypeCount"
              :class="[numericInputClass, errors.equipmentTypeCount ? 'border-rose-400' : '']"
              min="0"
              max="10"
              step="1"
              type="number"
            />
            <p v-if="errors.equipmentTypeCount" class="text-xs text-rose-600">{{ t(errors.equipmentTypeCount) }}</p>
          </label>

          <label class="grid content-start self-start gap-1 text-sm">
            <span class="text-slate-600">{{ t('admin.devTools.scenario.mealModeLabel') }}</span>
            <AppCombobox
              v-model="form.mealMode"
              :options="mealModeOptions"
              :placeholder="t('admin.devTools.scenario.mealModeLabel')"
              :aria-label="t('admin.devTools.scenario.mealModeLabel')"
              :searchable="false"
            />
          </label>

          <label class="flex items-center gap-3 rounded border border-slate-200 bg-white px-3 py-2 text-sm text-slate-700 lg:col-span-2">
            <input v-model="form.includeFlights" type="checkbox" class="rounded border-slate-300" />
            <span>{{ t('admin.devTools.scenario.includeFlightsLabel') }}</span>
          </label>

          <div class="lg:col-span-2">
            <button
              class="text-sm font-medium text-slate-700 underline underline-offset-4"
              type="button"
              @click="advancedOpen = !advancedOpen"
            >
              {{ advancedOpen ? t('admin.devTools.scenario.hideAdvanced') : t('admin.devTools.scenario.advanced') }}
            </button>
          </div>

          <Transition name="app-section-reveal">
            <div v-if="advancedOpen" class="grid gap-4 rounded-2xl border border-slate-200 bg-white p-4 lg:col-span-2 lg:grid-cols-2">
              <label class="grid content-start self-start gap-1 text-sm">
                <span class="text-slate-600">{{ t('admin.devTools.scenario.activityDensityLabel') }}</span>
                <AppCombobox
                  v-model="form.activityDensity"
                  :options="activityDensityOptions"
                  :placeholder="t('admin.devTools.scenario.activityDensityLabel')"
                  :aria-label="t('admin.devTools.scenario.activityDensityLabel')"
                  :searchable="false"
                />
                <div class="min-h-5">
                  <p class="invisible text-xs">.</p>
                </div>
              </label>

              <label class="grid content-start self-start gap-1 text-sm">
                <span class="text-slate-600">{{ t('admin.devTools.scenario.flightLegModeLabel') }}</span>
                <AppCombobox
                  v-model="form.flightLegMode"
                  :options="flightLegModeOptions"
                  :placeholder="t('admin.devTools.scenario.flightLegModeLabel')"
                  :aria-label="t('admin.devTools.scenario.flightLegModeLabel')"
                  :disabled="!form.includeFlights"
                  :searchable="false"
                />
                <div class="min-h-5">
                  <p class="text-xs text-slate-500">{{ t('admin.devTools.scenario.flightLegModeHelper') }}</p>
                </div>
              </label>

              <label class="grid content-start self-start gap-1 text-sm">
                <span class="text-slate-600">{{ t('admin.devTools.scenario.namingModeLabel') }}</span>
                <AppCombobox
                  v-model="form.participantNamingMode"
                  :options="participantNamingOptions"
                  :placeholder="t('admin.devTools.scenario.namingModeLabel')"
                  :aria-label="t('admin.devTools.scenario.namingModeLabel')"
                  :searchable="false"
                />
                <div class="min-h-5">
                  <p class="invisible text-xs">.</p>
                </div>
              </label>

              <label class="grid content-start self-start gap-1 text-sm">
                <span class="text-slate-600">{{ t('admin.devTools.scenario.mealSelectionCoverageLabel') }}</span>
                <input
                  v-model.number="form.mealSelectionCoveragePercent"
                  :class="[numericInputClass, errors.mealSelectionCoveragePercent ? 'border-rose-400' : '']"
                  min="0"
                  max="100"
                  step="1"
                  type="number"
                />
                <div class="min-h-5">
                  <p v-if="errors.mealSelectionCoveragePercent" class="text-xs text-rose-600">
                    {{ t(errors.mealSelectionCoveragePercent) }}
                  </p>
                  <p v-else class="invisible text-xs">.</p>
                </div>
              </label>

              <label class="grid content-start self-start gap-1 text-sm">
                <span class="text-slate-600">{{ t('admin.devTools.scenario.eventCheckInCoverageLabel') }}</span>
                <input
                  v-model.number="form.eventCheckInCoveragePercent"
                  :class="[numericInputClass, errors.eventCheckInCoveragePercent ? 'border-rose-400' : '']"
                  min="0"
                  max="100"
                  step="1"
                  type="number"
                />
                <div class="min-h-5">
                  <p v-if="errors.eventCheckInCoveragePercent" class="text-xs text-rose-600">
                    {{ t(errors.eventCheckInCoveragePercent) }}
                  </p>
                  <p v-else class="invisible text-xs">.</p>
                </div>
              </label>

              <label class="grid content-start self-start gap-1 text-sm">
                <span class="text-slate-600">{{ t('admin.devTools.scenario.participantNamePrefixLabel') }}</span>
                <input
                  v-model.trim="form.participantNamePrefix"
                  class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
                  :class="errors.participantNamePrefix ? 'border-rose-400' : ''"
                  :disabled="form.participantNamingMode !== 'prefix'"
                  :placeholder="t('admin.devTools.scenario.participantNamePrefixPlaceholder')"
                  type="text"
                />
                <div class="min-h-5">
                  <p v-if="errors.participantNamePrefix" class="text-xs text-rose-600">{{ t(errors.participantNamePrefix) }}</p>
                  <p v-else class="invisible text-xs">.</p>
                </div>
              </label>

              <label class="grid content-start self-start gap-1 text-sm">
                <span class="text-slate-600">{{ t('admin.devTools.scenario.randomSeedLabel') }}</span>
                <input
                  v-model="form.randomSeed"
                  class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
                  :placeholder="t('admin.devTools.scenario.randomSeedPlaceholder')"
                  inputmode="numeric"
                  type="text"
                />
                <div class="min-h-5">
                  <p class="invisible text-xs">.</p>
                </div>
              </label>
            </div>
          </Transition>

          <div class="grid gap-3 rounded-2xl border border-slate-200 bg-white p-4 lg:col-span-2 lg:grid-cols-2">
            <div class="lg:col-span-2">
              <h3 class="text-sm font-semibold text-slate-900">{{ t('admin.devTools.scenario.summaryTitle') }}</h3>
            </div>
            <div class="space-y-1 text-sm text-slate-600">
              <p>{{ t('admin.devTools.scenario.summaryDays', { count: form.dayCount }) }}</p>
              <p>{{ t('admin.devTools.scenario.summaryAccommodations', { count: effectiveAccommodationPlanCount }) }}</p>
              <p>{{ t('admin.devTools.scenario.summaryActivities', { count: activityCount }) }}</p>
              <p>{{ t('admin.devTools.scenario.summaryMealActivities', { count: mealActivityCount }) }}</p>
              <p>{{ t('admin.devTools.scenario.summaryParticipants', { count: form.participantCount }) }}</p>
            </div>
            <div class="space-y-1 text-sm text-slate-600">
              <p>{{ t('admin.devTools.scenario.summaryEquipmentTypes', { count: form.equipmentTypeCount }) }}</p>
              <p>{{ t('admin.devTools.scenario.summaryMealSelections', { count: mealSelectionCount }) }}</p>
              <p>{{ t('admin.devTools.scenario.summaryEventCheckIns', { count: eventCheckInCount }) }}</p>
              <p>{{ t('admin.devTools.scenario.summaryFlightSegments', { count: flightSegmentCount }) }}</p>
            </div>
            <p v-if="form.includeFlights" class="lg:col-span-2 text-xs text-slate-500">
              {{ t('admin.devTools.scenario.summaryFlightMix', { direct: directParticipantCount, layover: layoverParticipantCount }) }}
            </p>
          </div>

          <div class="flex flex-wrap items-center gap-3 lg:col-span-2">
            <button
              class="rounded bg-slate-900 px-4 py-2 text-sm font-medium text-white hover:bg-slate-800 disabled:cursor-not-allowed disabled:bg-slate-400"
              :disabled="submitting"
              type="submit"
            >
              {{ submitting ? t('admin.devTools.scenario.generating') : t('admin.devTools.scenario.generate') }}
            </button>
            <span class="text-xs text-slate-500">{{ t('admin.devTools.scenario.helper') }}</span>
          </div>

          <p v-if="errors.generic" class="text-sm text-rose-600 lg:col-span-2">
            {{ errors.generic.startsWith('admin.devTools.scenario.errors.') ? t(errors.generic) : errors.generic }}
          </p>
        </form>
      </div>
    </Transition>
  </section>
</template>
