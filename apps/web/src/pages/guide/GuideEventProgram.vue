<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { apiGet } from '../../lib/api'
import LoadingState from '../../components/ui/LoadingState.vue'
import ErrorState from '../../components/ui/ErrorState.vue'
import RichTextContent from '../../components/editor/RichTextContent.vue'
import type { EventListItem, EventSchedule, EventScheduleDay } from '../../types'

const route = useRoute()
const { t } = useI18n()

const eventId = computed(() => route.params.eventId as string)

const event = ref<EventListItem | null>(null)
const schedule = ref<EventSchedule | null>(null)
const selectedDayIndex = ref(0)

const loading = ref(true)
const errorKey = ref<string | null>(null)
const errorMessage = ref<string | null>(null)

const scheduleDays = computed<EventScheduleDay[]>(() => schedule.value?.days ?? [])
const selectedDay = computed(() => scheduleDays.value[selectedDayIndex.value] ?? null)

const parseDate = (value?: string | null) => {
  if (!value) return null
  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(value)
  if (!match) return null

  const year = Number(match[1])
  const month = Number(match[2])
  const day = Number(match[3])

  if (!Number.isFinite(year) || !Number.isFinite(month) || !Number.isFinite(day)) {
    return null
  }

  return new Date(year, month - 1, day)
}

const toDateOnly = (date: Date) => new Date(date.getFullYear(), date.getMonth(), date.getDate())

const setDefaultDay = () => {
  if (scheduleDays.value.length === 0) {
    selectedDayIndex.value = 0
    return
  }

  const today = toDateOnly(new Date())
  const index = scheduleDays.value.findIndex((day) => {
    const date = parseDate(day.date)
    return date && toDateOnly(date).getTime() === today.getTime()
  })

  selectedDayIndex.value = index >= 0 ? index : 0
}

const formatActivityTime = (activity: { startTime?: string | null; endTime?: string | null }) => {
  const start = activity.startTime?.trim()
  const end = activity.endTime?.trim()
  if (start && end) {
    return `${start} – ${end}`
  }
  if (start) {
    return start
  }
  return t('portal.schedule.timeTba')
}

const buildMapsLink = (activity: { locationName?: string | null; address?: string | null }) => {
  const query = activity.address?.trim() || activity.locationName?.trim()
  if (!query) {
    return ''
  }
  return `https://maps.google.com/?q=${encodeURIComponent(query)}`
}

const formatActivityType = (type?: string | null) => {
  if (type?.toLowerCase() === 'meal') return t('portal.schedule.typeMeal')
  if (type?.toLowerCase() === 'program') return t('portal.schedule.typeProgram')
  return t('portal.schedule.typeOther')
}

const programExpanded = ref<Record<string, boolean>>({})
const toggleProgram = (activityId: string) => {
  programExpanded.value[activityId] = !programExpanded.value[activityId]
  programExpanded.value = { ...programExpanded.value }
}

const loadData = async () => {
  loading.value = true
  errorKey.value = null
  errorMessage.value = null
  try {
    const [eventsResponse, scheduleResponse] = await Promise.all([
      apiGet<EventListItem[]>('/api/guide/events'),
      apiGet<EventSchedule>(`/api/guide/events/${eventId.value}/schedule`),
    ])

    event.value = eventsResponse.find((item) => item.id === eventId.value) ?? null
    schedule.value = scheduleResponse
    setDefaultDay()
  } catch (err) {
    errorMessage.value = err instanceof Error ? err.message : null
    if (!errorMessage.value) {
      errorKey.value = 'errors.guideEvents.load'
    }
  } finally {
    loading.value = false
  }
}

const selectDay = (index: number) => {
  selectedDayIndex.value = index
}

onMounted(loadData)
</script>

<template>
  <div class="space-y-6 sm:space-y-8">
    <section class="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:p-6">
      <RouterLink
        class="inline-block text-sm text-slate-600 underline-offset-2 hover:text-slate-900"
        to="/guide/events"
      >
        {{ t('nav.backToGuideEvents') }}
      </RouterLink>
      <nav class="mt-3 flex flex-wrap items-center gap-2" aria-label="Event sections">
        <RouterLink
          :to="`/guide/events/${eventId}/checkin`"
          active-class="bg-slate-100 border-slate-300 font-medium text-slate-900"
          class="whitespace-nowrap rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm text-slate-700 transition-colors hover:border-slate-300 hover:bg-slate-50"
        >
          {{ t('common.checkIn') }}
        </RouterLink>
        <RouterLink
          :to="`/guide/events/${eventId}/activities/checkin`"
          active-class="bg-slate-100 border-slate-300 font-medium text-slate-900"
          class="whitespace-nowrap rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm text-slate-700 transition-colors hover:border-slate-300 hover:bg-slate-50"
        >
          {{ t('admin.eventDetail.activityCheckIn') }}
        </RouterLink>
        <RouterLink
          :to="`/guide/events/${eventId}/equipment`"
          active-class="bg-slate-100 border-slate-300 font-medium text-slate-900"
          class="whitespace-nowrap rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm text-slate-700 transition-colors hover:border-slate-300 hover:bg-slate-50"
        >
          {{ t('admin.eventDetail.equipment') }}
        </RouterLink>
        <RouterLink
          :to="`/guide/events/${eventId}/program`"
          active-class="bg-slate-100 border-slate-300 font-medium text-slate-900"
          class="whitespace-nowrap rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm text-slate-700 transition-colors hover:border-slate-300 hover:bg-slate-50"
        >
          {{ t('admin.eventDetail.openProgram') }}
        </RouterLink>
      </nav>
      <div class="mt-4 flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
        <div>
          <h1 class="text-2xl font-semibold text-slate-900">{{ event?.name ?? t('common.event') }}</h1>
          <p v-if="event" class="mt-1 text-sm text-slate-500">
            {{ t('common.dateRange', { start: event.startDate, end: event.endDate }) }}
          </p>
          <p class="mt-2 text-sm text-slate-500">{{ t('guide.program.subtitle') }}</p>
        </div>
        <RouterLink
          :to="`/guide/events/${eventId}/program/edit`"
          class="whitespace-nowrap rounded-xl bg-slate-900 px-4 py-2 text-sm font-medium text-white hover:bg-slate-800 transition-colors"
        >
          {{ t('guide.program.edit') }}
        </RouterLink>
      </div>
    </section>

    <LoadingState v-if="loading" message-key="guide.program.loading" />
    <ErrorState
      v-else-if="errorKey || errorMessage"
      :message="errorMessage ?? undefined"
      :message-key="errorKey ?? undefined"
      @retry="loadData"
    />

    <section
      v-else
      class="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:p-6"
    >
      <h2 class="text-lg font-semibold">{{ t('guide.program.title') }}</h2>
      <p v-if="scheduleDays.length === 0" class="mt-2 text-sm text-slate-500">
        {{ t('guide.program.empty') }}
      </p>

      <template v-else>
        <div class="mt-4 flex gap-2 overflow-x-auto pb-2">
          <button
            v-for="(day, index) in scheduleDays"
            :key="day.id"
            class="min-w-[140px] rounded-xl border px-3 py-2 text-left text-sm transition"
            :class="
              index === selectedDayIndex
                ? 'border-slate-900 bg-slate-900 text-white'
                : 'border-slate-200 bg-white text-slate-600 hover:border-slate-300'
            "
            type="button"
            @click="selectDay(index)"
          >
            <div class="text-xs uppercase tracking-wide opacity-80">{{ day.date }}</div>
            <div class="font-medium">
              {{ day.title || t('portal.schedule.dayFallback', { day: index + 1 }) }}
            </div>
          </button>
        </div>

        <div v-if="selectedDay" class="mt-6">
          <div class="flex items-center justify-between">
            <div>
              <h3 class="text-lg font-semibold">
                {{ selectedDay.title || t('portal.schedule.dayFallback', { day: selectedDayIndex + 1 }) }}
              </h3>
              <p class="text-sm text-slate-500">{{ selectedDay.date }}</p>
            </div>
          </div>

          <p v-if="selectedDay.activities.length === 0" class="mt-3 text-sm text-slate-500">
            {{ t('portal.schedule.noActivities') }}
          </p>

          <div v-else class="mt-4 space-y-4">
            <article
              v-for="activity in selectedDay.activities"
              :key="activity.id"
              class="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm"
            >
              <div class="flex flex-wrap items-start justify-between gap-3">
                <div>
                  <div class="text-xs font-semibold uppercase tracking-wide text-slate-500">
                    {{ formatActivityTime(activity) }}
                  </div>
                  <div class="mt-1 text-base font-semibold text-slate-900">
                    {{ activity.title }}
                  </div>
                  <div class="mt-2 flex flex-wrap gap-2">
                    <span
                      class="rounded-full border px-2 py-0.5 text-xs"
                      :class="
                        activity.type === 'Meal'
                          ? 'border-amber-200 bg-amber-50 text-amber-700'
                          : activity.type === 'Program'
                            ? 'border-sky-200 bg-sky-50 text-sky-700'
                            : 'border-slate-200 bg-white text-slate-600'
                      "
                    >
                      {{ formatActivityType(activity.type) }}
                    </span>
                    <span
                      v-if="activity.checkInEnabled"
                      class="rounded-full border border-emerald-200 bg-emerald-50 px-2 py-0.5 text-xs text-emerald-700"
                    >
                      {{ t('guide.program.checkInEnabled') }}
                    </span>
                  </div>
                </div>
              </div>

              <div v-if="activity.locationName || activity.address" class="mt-3 text-sm text-slate-600">
                <div class="font-medium text-slate-700" v-if="activity.locationName">
                  {{ activity.locationName }}
                </div>
                <div v-if="activity.address">{{ activity.address }}</div>
                <a
                  v-if="buildMapsLink(activity)"
                  class="mt-2 inline-flex items-center gap-2 text-sm font-medium text-slate-700 underline"
                  :href="buildMapsLink(activity)"
                  rel="noreferrer"
                  target="_blank"
                >
                  {{ t('portal.schedule.openMap') }}
                </a>
              </div>

              <div v-if="activity.directions" class="mt-2 text-sm text-slate-500">
                {{ activity.directions }}
              </div>

              <div
                v-if="activity.menuText"
                class="mt-3 rounded-xl border border-amber-100 bg-amber-50 px-3 py-2 text-sm text-amber-800"
              >
                <div class="text-xs font-semibold uppercase tracking-wide text-amber-700">
                  {{ t('portal.schedule.menuLabel') }}
                </div>
                <RichTextContent :content="activity.menuText" class="mt-1" />
              </div>

              <div
                v-if="activity.type === 'Program' && activity.programContent"
                class="mt-3 rounded-xl border border-sky-100 bg-sky-50 px-3 py-2 text-sm text-sky-800"
              >
                <button
                  type="button"
                  class="flex w-full cursor-pointer items-center justify-between gap-2 border-0 bg-transparent p-0 text-left"
                  @click="toggleProgram(activity.id)"
                >
                  <span class="text-xs font-semibold uppercase tracking-wide text-sky-700">
                    {{ t('portal.schedule.programContent') }}
                  </span>
                  <span class="text-xs font-semibold text-sky-700 underline">
                    {{ programExpanded[activity.id] ? t('portal.schedule.menuHide') : t('portal.schedule.menuView') }}
                  </span>
                </button>
                <div
                  v-if="!programExpanded[activity.id]"
                  class="mt-2 overflow-hidden border-t border-sky-200/50 pt-2 text-sky-800 line-clamp-5"
                >
                  <RichTextContent :content="activity.programContent" />
                </div>
                <Transition name="program-expand">
                  <div
                    v-if="programExpanded[activity.id]"
                    class="mt-2 overflow-hidden border-t border-sky-200/50 pt-2 text-sky-800"
                  >
                    <RichTextContent :content="activity.programContent" />
                  </div>
                </Transition>
              </div>

              <div v-if="activity.notes" class="mt-3 text-sm text-slate-600">
                <RichTextContent :content="activity.notes" />
              </div>

              <a
                v-if="activity.surveyUrl"
                class="mt-3 inline-flex items-center gap-2 text-sm font-medium text-slate-700 underline"
                :href="activity.surveyUrl"
                rel="noreferrer"
                target="_blank"
              >
                {{ t('portal.schedule.openSurvey') }}
              </a>
            </article>
          </div>
        </div>
      </template>
    </section>
  </div>
</template>

<style scoped>
.program-expand-enter-active,
.program-expand-leave-active {
  transition: opacity 0.3s ease-out, max-height 0.3s ease-out;
}
.program-expand-enter-from,
.program-expand-leave-to {
  opacity: 0;
  max-height: 0;
}
.program-expand-enter-to,
.program-expand-leave-from {
  opacity: 1;
  max-height: 2000px;
}
</style>
