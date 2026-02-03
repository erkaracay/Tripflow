<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { apiDelete, apiGet, apiPost, apiPut } from '../../lib/api'
import { useToast } from '../../lib/toast'
import LoadingState from '../../components/ui/LoadingState.vue'
import ErrorState from '../../components/ui/ErrorState.vue'
import ConfirmDialog from '../../components/ui/ConfirmDialog.vue'
import type { Event, EventActivity, EventDay } from '../../types'

const route = useRoute()
const { t } = useI18n()
const { pushToast } = useToast()

const eventId = computed(() => route.params.eventId as string)

const event = ref<Event | null>(null)
const days = ref<EventDay[]>([])
const selectedDayId = ref<string | null>(null)
const activities = ref<EventActivity[]>([])

const loading = ref(true)
const errorKey = ref<string | null>(null)
const errorMessage = ref<string | null>(null)
const activitiesLoading = ref(false)

const dayModalOpen = ref(false)
const activityModalOpen = ref(false)
const deleteDayOpen = ref(false)
const deleteActivityOpen = ref(false)
const savingDay = ref(false)
const savingActivity = ref(false)

const editingDayId = ref<string | null>(null)
const editingActivityId = ref<string | null>(null)
const dayForm = ref({
  date: '',
  title: '',
  notes: '',
  sortOrder: '',
  isActive: true,
})
const activityForm = ref({
  title: '',
  type: 'Other',
  startTime: '',
  endTime: '',
  locationName: '',
  address: '',
  directions: '',
  notes: '',
  checkInEnabled: false,
  checkInMode: 'EntryOnly',
  menuText: '',
  surveyUrl: '',
})

const selectedDay = computed(() => days.value.find((day) => day.id === selectedDayId.value) ?? null)

const resetDayForm = (day?: EventDay) => {
  dayForm.value = {
    date: day?.date ?? '',
    title: day?.title ?? '',
    notes: day?.notes ?? '',
    sortOrder: day?.sortOrder?.toString() ?? '',
    isActive: day?.isActive ?? true,
  }
}

const resetActivityForm = (activity?: EventActivity) => {
  activityForm.value = {
    title: activity?.title ?? '',
    type: activity?.type ?? 'Other',
    startTime: activity?.startTime ?? '',
    endTime: activity?.endTime ?? '',
    locationName: activity?.locationName ?? '',
    address: activity?.address ?? '',
    directions: activity?.directions ?? '',
    notes: activity?.notes ?? '',
    checkInEnabled: activity?.checkInEnabled ?? false,
    checkInMode: activity?.checkInMode ?? 'EntryOnly',
    menuText: activity?.menuText ?? '',
    surveyUrl: activity?.surveyUrl ?? '',
  }
}

const loadEvent = async () => {
  event.value = await apiGet<Event>(`/api/events/${eventId.value}`)
}

const loadDays = async () => {
  days.value = await apiGet<EventDay[]>(`/api/events/${eventId.value}/days`)
  if (!selectedDayId.value) {
    const firstDay = days.value[0]
    if (firstDay) {
      selectedDayId.value = firstDay.id
    }
  }
}

const loadActivities = async (dayId: string) => {
  activitiesLoading.value = true
  try {
    activities.value = await apiGet<EventActivity[]>(
      `/api/events/${eventId.value}/days/${dayId}/activities`
    )
  } finally {
    activitiesLoading.value = false
  }
}

const loadAll = async () => {
  loading.value = true
  errorKey.value = null
  errorMessage.value = null
  try {
    await loadEvent()
    await loadDays()
    if (selectedDayId.value) {
      await loadActivities(selectedDayId.value)
    }
  } catch (err) {
    errorMessage.value = err instanceof Error ? err.message : null
    if (!errorMessage.value) {
      errorKey.value = 'errors.events.load'
    }
  } finally {
    loading.value = false
  }
}

const openCreateDay = () => {
  editingDayId.value = null
  resetDayForm(selectedDay.value ?? undefined)
  dayModalOpen.value = true
}

const openEditDay = (day: EventDay) => {
  editingDayId.value = day.id
  resetDayForm(day)
  dayModalOpen.value = true
}

const saveDay = async () => {
  const payload = {
    date: dayForm.value.date,
    title: dayForm.value.title,
    notes: dayForm.value.notes,
    sortOrder: dayForm.value.sortOrder ? Number(dayForm.value.sortOrder) : undefined,
    isActive: dayForm.value.isActive,
  }

  if (!payload.date) {
    pushToast({ key: 'admin.program.days.validation.dateRequired', tone: 'error' })
    return
  }

  savingDay.value = true
  try {
    if (editingDayId.value) {
      await apiPut(`/api/events/${eventId.value}/days/${editingDayId.value}`, payload)
    } else {
      await apiPost(`/api/events/${eventId.value}/days`, payload)
    }
    dayModalOpen.value = false
    await loadDays()
    if (selectedDayId.value) {
      await loadActivities(selectedDayId.value)
    }
    pushToast({ key: 'toast.saved', tone: 'success' })
  } catch (err) {
    pushToast({ key: 'errors.generic', tone: 'error' })
  } finally {
    savingDay.value = false
  }
}

const confirmDeleteDay = (day: EventDay) => {
  editingDayId.value = day.id
  deleteDayOpen.value = true
}

const deleteDay = async () => {
  if (!editingDayId.value) return
  try {
    await apiDelete(`/api/events/${eventId.value}/days/${editingDayId.value}`)
    deleteDayOpen.value = false
    if (selectedDayId.value === editingDayId.value) {
      selectedDayId.value = null
    }
    await loadDays()
    if (selectedDayId.value) {
      await loadActivities(selectedDayId.value)
    }
    pushToast({ key: 'toast.deleted', tone: 'success' })
  } catch {
    pushToast({ key: 'errors.generic', tone: 'error' })
  }
}

const moveDay = async (index: number, direction: -1 | 1) => {
  const target = days.value[index]
  const swap = days.value[index + direction]
  if (!target || !swap) return

  await Promise.all([
    apiPut(`/api/events/${eventId.value}/days/${target.id}`, { sortOrder: swap.sortOrder }),
    apiPut(`/api/events/${eventId.value}/days/${swap.id}`, { sortOrder: target.sortOrder }),
  ])

  const current = selectedDayId.value
  await loadDays()
  selectedDayId.value = current
}

const openCreateActivity = () => {
  editingActivityId.value = null
  resetActivityForm()
  activityModalOpen.value = true
}

const openEditActivity = (activity: EventActivity) => {
  editingActivityId.value = activity.id
  resetActivityForm(activity)
  activityModalOpen.value = true
}

const duplicateActivity = (activity: EventActivity) => {
  editingActivityId.value = null
  resetActivityForm(activity)
  activityModalOpen.value = true
}

const parseTimeToMinutes = (value?: string | null) => {
  if (!value) return null
  const [h, m] = value.split(':')
  const hours = Number(h)
  const minutes = Number(m)
  if (!Number.isFinite(hours) || !Number.isFinite(minutes)) return null
  return hours * 60 + minutes
}

const buildMapsLink = (activity: { locationName?: string | null; address?: string | null }) => {
  const query = activity.address?.trim() || activity.locationName?.trim()
  if (!query) {
    return ''
  }
  return `https://maps.google.com/?q=${encodeURIComponent(query)}`
}

const timeError = computed(() => {
  const start = parseTimeToMinutes(activityForm.value.startTime)
  const end = parseTimeToMinutes(activityForm.value.endTime)
  if (start !== null && end !== null && end < start) {
    return t('admin.program.activities.validation.timeRange')
  }
  return ''
})

const canSaveActivity = computed(() => {
  if (!activityForm.value.title.trim()) {
    return false
  }
  return !timeError.value
})

const saveActivity = async () => {
  if (!selectedDayId.value) {
    return
  }

  if (!activityForm.value.title.trim()) {
    pushToast({ key: 'admin.program.activities.validation.titleRequired', tone: 'error' })
    return
  }

  if (timeError.value) {
    pushToast({ key: 'admin.program.activities.validation.timeRange', tone: 'error' })
    return
  }

  const payload = {
    title: activityForm.value.title,
    type: activityForm.value.type,
    startTime: activityForm.value.startTime || null,
    endTime: activityForm.value.endTime || null,
    locationName: activityForm.value.locationName || null,
    address: activityForm.value.address || null,
    directions: activityForm.value.directions || null,
    notes: activityForm.value.notes || null,
    checkInEnabled: activityForm.value.checkInEnabled,
    checkInMode: activityForm.value.checkInMode || 'EntryOnly',
    menuText: activityForm.value.menuText || null,
    surveyUrl: activityForm.value.surveyUrl || null,
  }

  savingActivity.value = true
  try {
    if (editingActivityId.value) {
      await apiPut(`/api/events/${eventId.value}/activities/${editingActivityId.value}`, payload)
    } else {
      await apiPost(
        `/api/events/${eventId.value}/days/${selectedDayId.value}/activities`,
        payload
      )
    }
    activityModalOpen.value = false
    await loadActivities(selectedDayId.value)
    pushToast({ key: 'toast.saved', tone: 'success' })
  } catch {
    pushToast({ key: 'errors.generic', tone: 'error' })
  } finally {
    savingActivity.value = false
  }
}

const confirmDeleteActivity = (activity: EventActivity) => {
  editingActivityId.value = activity.id
  deleteActivityOpen.value = true
}

const deleteActivity = async () => {
  if (!editingActivityId.value) return
  try {
    await apiDelete(`/api/events/${eventId.value}/activities/${editingActivityId.value}`)
    deleteActivityOpen.value = false
    if (selectedDayId.value) {
      await loadActivities(selectedDayId.value)
    }
    pushToast({ key: 'toast.deleted', tone: 'success' })
  } catch {
    pushToast({ key: 'errors.generic', tone: 'error' })
  }
}

watch(selectedDayId, (value) => {
  if (value) {
    loadActivities(value)
  }
})

onMounted(loadAll)
</script>

<template>
  <div class="space-y-6">
    <div class="flex flex-col gap-3 rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:flex-row sm:items-center sm:justify-between sm:p-6">
      <div>
        <RouterLink class="text-sm text-slate-600 underline" :to="`/admin/events/${eventId}`">
          {{ t('nav.backToEvent') }}
        </RouterLink>
        <h1 class="mt-2 text-2xl font-semibold">{{ event?.name ?? t('common.event') }}</h1>
        <p class="text-sm text-slate-500" v-if="event">
          {{ t('common.dateRange', { start: event.startDate, end: event.endDate }) }}
        </p>
      </div>
      <div class="text-sm text-slate-600">
        {{ t('admin.program.subtitle') }}
      </div>
    </div>

    <LoadingState v-if="loading" message-key="admin.program.loading" />
    <ErrorState
      v-else-if="errorKey || errorMessage"
      :message="errorMessage ?? undefined"
      :message-key="errorKey ?? undefined"
      @retry="loadAll"
    />

    <div v-else class="grid gap-6 lg:grid-cols-[280px_1fr]">
      <section class="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm">
        <div class="flex items-center justify-between">
          <h2 class="text-sm font-semibold text-slate-900">{{ t('admin.program.days.title') }}</h2>
          <button
            class="rounded-lg border border-slate-200 bg-white px-2 py-1 text-xs text-slate-700 hover:border-slate-300"
            type="button"
            @click="openCreateDay"
          >
            {{ t('admin.program.days.add') }}
          </button>
        </div>
        <p class="mt-2 text-xs text-slate-500" v-if="days.length === 0">
          {{ t('admin.program.days.empty') }}
        </p>
        <ul v-else class="mt-4 space-y-2">
          <li
            v-for="(day, index) in days"
            :key="day.id"
            class="rounded-xl border px-3 py-2 text-xs"
            :class="selectedDayId === day.id ? 'border-slate-900 bg-slate-900 text-white' : 'border-slate-200 text-slate-700'"
          >
            <button class="w-full text-left" type="button" @click="selectedDayId = day.id">
              <div class="font-semibold">{{ day.title || t('admin.program.days.fallback', { day: index + 1 }) }}</div>
              <div class="text-[11px] opacity-70">{{ day.date }}</div>
              <div class="mt-1 text-[11px] opacity-70">
                {{ t('admin.program.days.count', { count: day.activityCount }) }}
              </div>
            </button>
            <div class="mt-2 flex flex-wrap gap-1">
              <button
                class="rounded border border-white/30 px-2 py-1 text-[10px]"
                type="button"
                :disabled="index === 0"
                @click="moveDay(index, -1)"
              >
                {{ t('admin.program.days.up') }}
              </button>
              <button
                class="rounded border border-white/30 px-2 py-1 text-[10px]"
                type="button"
                :disabled="index === days.length - 1"
                @click="moveDay(index, 1)"
              >
                {{ t('admin.program.days.down') }}
              </button>
              <button
                class="rounded border border-white/30 px-2 py-1 text-[10px]"
                type="button"
                @click="openEditDay(day)"
              >
                {{ t('admin.program.days.edit') }}
              </button>
              <button
                class="rounded border border-white/30 px-2 py-1 text-[10px]"
                type="button"
                @click="confirmDeleteDay(day)"
              >
                {{ t('admin.program.days.delete') }}
              </button>
            </div>
          </li>
        </ul>
      </section>

      <section class="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm">
        <div class="flex flex-wrap items-center justify-between gap-2">
          <div>
            <h2 class="text-sm font-semibold text-slate-900">{{ t('admin.program.activities.title') }}</h2>
            <p class="text-xs text-slate-500" v-if="selectedDay">{{ selectedDay.date }}</p>
          </div>
          <button
            class="rounded-lg bg-slate-900 px-3 py-2 text-xs font-semibold text-white hover:bg-slate-800"
            type="button"
            :disabled="!selectedDayId"
            @click="openCreateActivity"
          >
            {{ t('admin.program.activities.add') }}
          </button>
        </div>

        <p class="mt-4 text-xs text-slate-500" v-if="!selectedDayId">
          {{ t('admin.program.activities.selectDay') }}
        </p>
        <p class="mt-4 text-xs text-slate-500" v-else-if="activities.length === 0">
          {{ t('admin.program.activities.empty') }}
        </p>

        <div v-else class="mt-4 space-y-3">
          <div
            v-for="activity in activities"
            :key="activity.id"
            class="rounded-xl border border-slate-200 bg-slate-50 p-4"
          >
            <div class="flex items-start justify-between gap-3">
              <div>
                <div class="text-xs uppercase tracking-[0.2em] text-slate-400">
                  {{ activity.startTime && activity.endTime ? `${activity.startTime} – ${activity.endTime}` : activity.startTime || t('admin.program.activities.timeTba') }}
                </div>
                <div class="mt-1 text-sm font-semibold text-slate-900">{{ activity.title }}</div>
                <div class="mt-1 text-xs text-slate-500" v-if="activity.locationName || activity.address">
                  <span v-if="activity.locationName">{{ activity.locationName }}</span>
                  <span v-if="activity.address"> · {{ activity.address }}</span>
                  <a
                    v-if="buildMapsLink(activity)"
                    :href="buildMapsLink(activity)"
                    rel="noreferrer"
                    target="_blank"
                    class="ml-2 inline-flex items-center text-[10px] font-medium text-slate-600 underline"
                  >
                    {{ t('portal.schedule.openMap') }}
                  </a>
                </div>
              </div>
              <span class="rounded-full border px-2 py-1 text-[10px] text-slate-600">
                {{ activity.type === 'Meal' ? t('admin.program.activities.typeMeal') : t('admin.program.activities.typeOther') }}
              </span>
            </div>
            <div class="mt-2 text-xs text-slate-500" v-if="activity.notes">{{ activity.notes }}</div>
            <div class="mt-3 flex flex-wrap gap-2">
              <button class="rounded border border-slate-200 px-2 py-1 text-[10px]" type="button" @click="openEditActivity(activity)">
                {{ t('admin.program.activities.edit') }}
              </button>
              <button class="rounded border border-slate-200 px-2 py-1 text-[10px]" type="button" @click="duplicateActivity(activity)">
                {{ t('admin.program.activities.duplicate') }}
              </button>
              <button class="rounded border border-slate-200 px-2 py-1 text-[10px]" type="button" @click="confirmDeleteActivity(activity)">
                {{ t('admin.program.activities.delete') }}
              </button>
            </div>
          </div>
        </div>
        <LoadingState v-if="activitiesLoading" message-key="admin.program.activities.loading" />
      </section>
    </div>
  </div>

  <ConfirmDialog
    v-model:open="deleteDayOpen"
    :title="t('admin.program.days.delete')"
    :message="t('admin.program.days.deleteConfirm')"
    tone="danger"
    @confirm="deleteDay"
  />

  <ConfirmDialog
    v-model:open="deleteActivityOpen"
    :title="t('admin.program.activities.delete')"
    :message="t('admin.program.activities.deleteConfirm')"
    tone="danger"
    @confirm="deleteActivity"
  />

  <teleport to="body">
    <div v-if="dayModalOpen" class="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/40 px-4" @click.self="dayModalOpen = false">
      <div class="w-full max-w-lg rounded-2xl bg-white p-5 shadow-xl">
        <h3 class="text-lg font-semibold text-slate-900">
          {{ editingDayId ? t('admin.program.days.edit') : t('admin.program.days.add') }}
        </h3>
        <div class="mt-4 grid gap-3">
          <label class="grid gap-1 text-sm">
            <span class="text-slate-600">{{ t('admin.program.days.form.date') }}</span>
            <input v-model="dayForm.date" type="date" class="rounded border border-slate-200 px-3 py-2 text-sm" />
          </label>
          <label class="grid gap-1 text-sm">
            <span class="text-slate-600">{{ t('admin.program.days.form.title') }}</span>
            <input v-model="dayForm.title" type="text" class="rounded border border-slate-200 px-3 py-2 text-sm" />
          </label>
          <label class="grid gap-1 text-sm">
            <span class="text-slate-600">{{ t('admin.program.days.form.notes') }}</span>
            <textarea v-model="dayForm.notes" rows="3" class="rounded border border-slate-200 px-3 py-2 text-sm"></textarea>
          </label>
          <label class="grid gap-1 text-sm">
            <span class="text-slate-600">{{ t('admin.program.days.form.sortOrder') }}</span>
            <input v-model="dayForm.sortOrder" type="number" min="1" class="rounded border border-slate-200 px-3 py-2 text-sm" />
          </label>
          <label class="inline-flex items-center gap-2 text-sm text-slate-600">
            <input v-model="dayForm.isActive" type="checkbox" class="h-4 w-4 rounded border-slate-300" />
            {{ t('admin.program.days.form.active') }}
          </label>
        </div>
        <div class="mt-6 flex justify-end gap-2">
          <button class="rounded border border-slate-200 px-3 py-2 text-sm" type="button" @click="dayModalOpen = false">
            {{ t('common.cancel') }}
          </button>
          <button class="rounded bg-slate-900 px-3 py-2 text-sm text-white" type="button" :disabled="savingDay" @click="saveDay">
            {{ savingDay ? t('common.saving') : t('common.save') }}
          </button>
        </div>
      </div>
    </div>
  </teleport>

  <teleport to="body">
    <div v-if="activityModalOpen" class="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/40 px-4" @click.self="activityModalOpen = false">
      <div class="w-full max-w-2xl rounded-2xl bg-white p-5 shadow-xl">
        <h3 class="text-lg font-semibold text-slate-900">
          {{ editingActivityId ? t('admin.program.activities.edit') : t('admin.program.activities.add') }}
        </h3>
        <div class="mt-4 grid gap-3 md:grid-cols-2">
          <label class="grid gap-1 text-sm md:col-span-2">
            <span class="text-slate-600">{{ t('admin.program.activities.form.title') }}</span>
            <input v-model="activityForm.title" type="text" class="rounded border border-slate-200 px-3 py-2 text-sm" />
          </label>
          <label class="grid gap-1 text-sm">
            <span class="text-slate-600">{{ t('admin.program.activities.form.type') }}</span>
            <select v-model="activityForm.type" class="rounded border border-slate-200 px-3 py-2 text-sm">
              <option value="Meal">{{ t('admin.program.activities.typeMeal') }}</option>
              <option value="Other">{{ t('admin.program.activities.typeOther') }}</option>
            </select>
          </label>
          <label class="grid gap-1 text-sm">
            <span class="text-slate-600">{{ t('admin.program.activities.form.startTime') }}</span>
            <input v-model="activityForm.startTime" type="time" class="rounded border border-slate-200 px-3 py-2 text-sm" />
          </label>
          <label class="grid gap-1 text-sm">
            <span class="text-slate-600">{{ t('admin.program.activities.form.endTime') }}</span>
            <input v-model="activityForm.endTime" type="time" class="rounded border border-slate-200 px-3 py-2 text-sm" />
          </label>
          <label class="grid gap-1 text-sm md:col-span-2">
            <span class="text-slate-600">{{ t('admin.program.activities.form.locationName') }}</span>
            <input v-model="activityForm.locationName" type="text" class="rounded border border-slate-200 px-3 py-2 text-sm" />
          </label>
          <label class="grid gap-1 text-sm md:col-span-2">
            <span class="text-slate-600">{{ t('admin.program.activities.form.address') }}</span>
            <input v-model="activityForm.address" type="text" class="rounded border border-slate-200 px-3 py-2 text-sm" />
          </label>
          <label class="grid gap-1 text-sm md:col-span-2">
            <span class="text-slate-600">{{ t('admin.program.activities.form.directions') }}</span>
            <textarea v-model="activityForm.directions" rows="2" class="rounded border border-slate-200 px-3 py-2 text-sm"></textarea>
          </label>
          <label class="grid gap-1 text-sm md:col-span-2">
            <span class="text-slate-600">{{ t('admin.program.activities.form.notes') }}</span>
            <textarea v-model="activityForm.notes" rows="2" class="rounded border border-slate-200 px-3 py-2 text-sm"></textarea>
          </label>
          <label class="grid gap-1 text-sm md:col-span-2" v-if="activityForm.type === 'Meal'">
            <span class="text-slate-600">{{ t('admin.program.activities.form.menuText') }}</span>
            <textarea
              v-model="activityForm.menuText"
              rows="4"
              class="min-h-24 rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
            ></textarea>
          </label>
          <label class="grid gap-1 text-sm md:col-span-2">
            <span class="text-slate-600">{{ t('admin.program.activities.form.surveyUrl') }}</span>
            <input v-model="activityForm.surveyUrl" type="url" class="rounded border border-slate-200 px-3 py-2 text-sm" />
          </label>
          <label class="inline-flex items-center gap-2 text-sm text-slate-600 md:col-span-2">
            <input v-model="activityForm.checkInEnabled" type="checkbox" class="h-4 w-4 rounded border-slate-300" />
            {{ t('admin.program.activities.form.checkInEnabled') }}
          </label>
          <label class="grid gap-1 text-sm md:col-span-2" v-if="activityForm.checkInEnabled">
            <span class="text-slate-600">{{ t('admin.program.activities.form.checkInMode') }}</span>
            <select v-model="activityForm.checkInMode" class="rounded border border-slate-200 px-3 py-2 text-sm">
              <option value="EntryOnly">{{ t('admin.program.activities.checkInModeEntry') }}</option>
              <option value="EntryExit">{{ t('admin.program.activities.checkInModeEntryExit') }}</option>
            </select>
          </label>
        </div>
        <p v-if="timeError" class="mt-3 text-xs text-rose-600">{{ timeError }}</p>
        <div class="mt-6 flex justify-end gap-2">
          <button class="rounded border border-slate-200 px-3 py-2 text-sm" type="button" @click="activityModalOpen = false">
            {{ t('common.cancel') }}
          </button>
          <button
            class="rounded bg-slate-900 px-3 py-2 text-sm text-white"
            type="button"
            :disabled="savingActivity || !canSaveActivity"
            @click="saveActivity"
          >
            {{ savingActivity ? t('common.saving') : t('common.save') }}
          </button>
        </div>
      </div>
    </div>
  </teleport>
</template>
