<script setup lang="ts">
import { computed, onMounted, onUnmounted, reactive, ref, watch } from 'vue'
import { RouterLink, useRoute } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { apiGet, guideGetAccommodationSegmentParticipants, guideGetAccommodationSegments } from '../../lib/api'
import { formatDateRange } from '../../lib/formatters'
import LoadingState from '../../components/ui/LoadingState.vue'
import ErrorState from '../../components/ui/ErrorState.vue'
import AppCombobox from '../../components/ui/AppCombobox.vue'
import type {
  AccommodationSegment,
  AppComboboxOption,
  Event as EventDto,
  GuideAccommodationOption,
  GuideAccommodationParticipant,
} from '../../types'

const route = useRoute()
const { t } = useI18n()

const eventId = computed(() => route.params.eventId as string)

const event = ref<EventDto | null>(null)
const segments = ref<AccommodationSegment[]>([])
const rows = ref<GuideAccommodationParticipant[]>([])
const availableAccommodations = ref<GuideAccommodationOption[]>([])
const selectedSegmentId = ref('')
const total = ref(0)
const page = ref(1)
const pageSize = ref(50)
const searchInput = ref('')

const filters = reactive({
  query: '',
  accommodationFilter: 'all',
})

const loading = ref(true)
const tableLoading = ref(false)
const errorKey = ref<string | null>(null)
const errorMessage = ref<string | null>(null)

let searchDebounceTimer: ReturnType<typeof setTimeout> | null = null

const selectedSegment = computed(
  () => segments.value.find((segment) => segment.id === selectedSegmentId.value) ?? null
)

const accommodationOptions = computed<AppComboboxOption[]>(() => [
  { value: 'all', label: t('guide.accommodation.filters.accommodationAll') },
  ...availableAccommodations.value.map((item) => ({ value: item.id, label: item.title })),
])

const totalLabel = computed(() => t('guide.accommodation.total', { count: total.value }))
const hasPreviousPage = computed(() => page.value > 1)
const hasNextPage = computed(() => page.value * pageSize.value < total.value)

const loadRows = async () => {
  if (!selectedSegmentId.value) {
    rows.value = []
    total.value = 0
    availableAccommodations.value = []
    return
  }

  tableLoading.value = true
  errorKey.value = null
  errorMessage.value = null

  try {
    const params = new URLSearchParams()
    if (filters.query.trim()) {
      params.set('query', filters.query.trim())
    }
    if (filters.accommodationFilter !== 'all') {
      params.set('accommodationFilter', filters.accommodationFilter)
    }
    params.set('page', String(page.value))
    params.set('pageSize', String(pageSize.value))

    const response = await guideGetAccommodationSegmentParticipants(eventId.value, selectedSegmentId.value, params)
    rows.value = response.items
    total.value = response.total
    availableAccommodations.value = response.availableAccommodations ?? []
  } catch (err) {
    errorMessage.value = err instanceof Error ? err.message : null
    if (!errorMessage.value) {
      errorKey.value = 'guide.accommodation.loadError'
    }
  } finally {
    tableLoading.value = false
  }
}

const loadData = async () => {
  loading.value = true
  errorKey.value = null
  errorMessage.value = null

  try {
    const [eventResponse, segmentsResponse] = await Promise.all([
      apiGet<EventDto>(`/api/guide/events/${eventId.value}`),
      guideGetAccommodationSegments(eventId.value),
    ])

    event.value = eventResponse
    segments.value = segmentsResponse
    selectedSegmentId.value = segmentsResponse[0]?.id ?? ''
    page.value = 1
    filters.query = ''
    filters.accommodationFilter = 'all'
    searchInput.value = ''

    if (selectedSegmentId.value) {
      await loadRows()
    } else {
      rows.value = []
      total.value = 0
      availableAccommodations.value = []
    }
  } catch (err) {
    errorMessage.value = err instanceof Error ? err.message : null
    if (!errorMessage.value) {
      errorKey.value = 'guide.accommodation.loadError'
    }
  } finally {
    loading.value = false
  }
}

const selectSegment = (segmentId: string) => {
  if (!segmentId || segmentId === selectedSegmentId.value) {
    return
  }

  selectedSegmentId.value = segmentId
}

watch(
  () => selectedSegmentId.value,
  () => {
    filters.accommodationFilter = 'all'
    page.value = 1
    if (!loading.value) {
      void loadRows()
    }
  }
)

watch(
  () => filters.accommodationFilter,
  () => {
    page.value = 1
    if (!loading.value) {
      void loadRows()
    }
  }
)

watch(page, () => {
  if (!loading.value) {
    void loadRows()
  }
})

watch(searchInput, (value) => {
  if (searchDebounceTimer) {
    globalThis.clearTimeout(searchDebounceTimer)
  }

  searchDebounceTimer = globalThis.setTimeout(() => {
    filters.query = value.trim()
    page.value = 1
    if (!loading.value) {
      void loadRows()
    }
    searchDebounceTimer = null
  }, 250)
})

onMounted(() => {
  void loadData()
})

onUnmounted(() => {
  if (searchDebounceTimer) {
    globalThis.clearTimeout(searchDebounceTimer)
  }
})
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
        <RouterLink
          :to="`/guide/events/${eventId}/accommodation`"
          active-class="bg-slate-100 border-slate-300 font-medium text-slate-900"
          class="whitespace-nowrap rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm text-slate-700 transition-colors hover:border-slate-300 hover:bg-slate-50"
        >
          {{ t('guide.accommodation.nav') }}
        </RouterLink>
      </nav>
      <div class="mt-4">
        <h1 class="text-2xl font-semibold text-slate-900">{{ event?.name ?? t('guide.accommodation.title') }}</h1>
        <p v-if="event" class="mt-1 text-sm text-slate-500">
          {{ formatDateRange(event.startDate, event.endDate) }}
        </p>
        <p class="mt-2 text-sm text-slate-500">{{ t('guide.accommodation.subtitle') }}</p>
      </div>
    </section>

    <LoadingState v-if="loading" message-key="guide.accommodation.loading" />
    <ErrorState
      v-else-if="errorKey || errorMessage"
      :message="errorMessage ?? undefined"
      :message-key="errorKey ?? undefined"
      @retry="loadData"
    />

    <template v-else>
      <section
        v-if="segments.length === 0"
        class="rounded-2xl border border-slate-200 bg-white p-6 text-center shadow-sm"
      >
        <h2 class="text-lg font-semibold text-slate-900">{{ t('guide.accommodation.title') }}</h2>
        <p class="mt-2 text-sm text-slate-500">{{ t('guide.accommodation.empty') }}</p>
      </section>

      <template v-else>
        <section class="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:p-6">
          <div class="flex flex-wrap gap-3">
            <button
              v-for="segment in segments"
              :key="segment.id"
              type="button"
              class="min-w-[220px] rounded-2xl border px-4 py-3 text-left transition"
              :class="
                segment.id === selectedSegmentId
                  ? 'border-slate-900 bg-slate-900 text-white shadow-sm'
                  : 'border-slate-200 bg-white text-slate-900 hover:border-slate-300 hover:bg-slate-50'
              "
              @click="selectSegment(segment.id)"
            >
              <div class="text-sm font-semibold">{{ segment.defaultAccommodationTitle }}</div>
              <div
                class="mt-2 text-xs"
                :class="segment.id === selectedSegmentId ? 'text-slate-200' : 'text-slate-500'"
              >
                {{ formatDateRange(segment.startDate, segment.endDate) }}
              </div>
            </button>
          </div>

          <div
            v-if="selectedSegment"
            class="mt-5 grid gap-3 rounded-2xl border border-slate-200 bg-slate-50 p-4 text-sm sm:grid-cols-2"
          >
            <div>
              <div class="text-xs text-slate-500">{{ t('guide.accommodation.defaultAccommodation') }}</div>
              <div class="mt-1 font-semibold text-slate-900">{{ selectedSegment.defaultAccommodationTitle }}</div>
            </div>
            <div>
              <div class="text-xs text-slate-500">{{ t('guide.accommodation.dateRange') }}</div>
              <div class="mt-1 font-semibold text-slate-900">{{ formatDateRange(selectedSegment.startDate, selectedSegment.endDate) }}</div>
            </div>
          </div>
        </section>

        <section class="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:p-6">
          <div class="flex flex-col gap-4 xl:flex-row xl:items-end xl:justify-between">
            <div class="grid flex-1 gap-3 xl:grid-cols-[minmax(0,1.4fr)_minmax(220px,0.8fr)]">
              <label class="grid gap-1 text-sm">
                <span class="text-slate-600">{{ t('guide.accommodation.filters.search') }}</span>
                <input
                  v-model.trim="searchInput"
                  class="rounded-xl border border-slate-300 px-3 py-2 text-sm outline-none transition focus:border-slate-500 focus:ring-2 focus:ring-slate-200"
                  :placeholder="t('guide.accommodation.searchPlaceholder')"
                  type="search"
                />
              </label>
              <label class="grid gap-1 text-sm">
                <span class="text-slate-600">{{ t('guide.accommodation.filters.accommodation') }}</span>
                <AppCombobox
                  v-model="filters.accommodationFilter"
                  :options="accommodationOptions"
                  :placeholder="t('guide.accommodation.filters.accommodation')"
                  :aria-label="t('guide.accommodation.filters.accommodation')"
                />
              </label>
            </div>
            <div class="text-sm font-medium text-slate-500">
              {{ totalLabel }}
            </div>
          </div>

          <div v-if="tableLoading && rows.length === 0" class="mt-6">
            <LoadingState message-key="guide.accommodation.loading" />
          </div>

          <template v-else>
            <div v-if="rows.length === 0" class="mt-6 rounded-2xl border border-dashed border-slate-300 bg-slate-50 px-5 py-8 text-center">
              <p class="text-sm text-slate-500">{{ t('guide.accommodation.emptyParticipants') }}</p>
            </div>

            <template v-else>
              <div class="mt-6 space-y-3 md:hidden">
                <article
                  v-for="row in rows"
                  :key="row.participantId"
                  class="rounded-2xl border border-slate-200 bg-slate-50 p-4"
                >
                  <div class="flex items-start justify-between gap-3">
                    <div>
                      <div class="text-sm font-semibold text-slate-900">{{ row.fullName }}</div>
                      <div class="mt-1 text-xs text-slate-500">{{ row.tcNo }}</div>
                    </div>
                    <span
                      v-if="row.usesOverride"
                      class="rounded-full border border-sky-200 bg-sky-50 px-2.5 py-1 text-[11px] font-semibold text-sky-700"
                    >
                      {{ t('guide.accommodation.overrideBadge') }}
                    </span>
                  </div>

                  <div class="mt-4 grid gap-2 text-sm">
                    <div class="flex items-start justify-between gap-3">
                      <span class="text-slate-500">{{ t('guide.accommodation.columns.accommodation') }}</span>
                      <span class="text-right font-medium text-slate-800">{{ row.effectiveAccommodationTitle }}</span>
                    </div>
                    <div class="flex items-start justify-between gap-3">
                      <span class="text-slate-500">{{ t('guide.accommodation.columns.roomNo') }}</span>
                      <span class="text-right font-medium text-slate-800">{{ row.roomNo || t('common.noData') }}</span>
                    </div>
                    <div class="flex items-start justify-between gap-3">
                      <span class="text-slate-500">{{ t('guide.accommodation.columns.roomType') }}</span>
                      <span class="text-right font-medium text-slate-800">{{ row.roomType || t('common.noData') }}</span>
                    </div>
                    <div class="flex items-start justify-between gap-3">
                      <span class="text-slate-500">{{ t('guide.accommodation.columns.boardType') }}</span>
                      <span class="text-right font-medium text-slate-800">{{ row.boardType || t('common.noData') }}</span>
                    </div>
                    <div class="flex items-start justify-between gap-3">
                      <span class="text-slate-500">{{ t('guide.accommodation.columns.personNo') }}</span>
                      <span class="text-right font-medium text-slate-800">{{ row.personNo || t('common.noData') }}</span>
                    </div>
                    <div class="flex items-start justify-between gap-3">
                      <span class="text-slate-500">{{ t('guide.accommodation.columns.roommates') }}</span>
                      <span class="text-right font-medium text-slate-800">
                        {{ row.roommates.length > 0 ? row.roommates.join(', ') : t('common.noData') }}
                      </span>
                    </div>
                  </div>
                </article>
              </div>

              <div class="mt-6 hidden overflow-x-auto md:block">
                <table class="min-w-full divide-y divide-slate-200 text-sm">
                  <thead class="bg-slate-50 text-left text-slate-600">
                    <tr>
                      <th class="px-3 py-3 font-medium">{{ t('guide.accommodation.columns.participant') }}</th>
                      <th class="px-3 py-3 font-medium">{{ t('guide.accommodation.columns.accommodation') }}</th>
                      <th class="px-3 py-3 font-medium">{{ t('guide.accommodation.columns.roomNo') }}</th>
                      <th class="px-3 py-3 font-medium">{{ t('guide.accommodation.columns.roomType') }}</th>
                      <th class="px-3 py-3 font-medium">{{ t('guide.accommodation.columns.boardType') }}</th>
                      <th class="px-3 py-3 font-medium">{{ t('guide.accommodation.columns.personNo') }}</th>
                      <th class="px-3 py-3 font-medium">{{ t('guide.accommodation.columns.roommates') }}</th>
                    </tr>
                  </thead>
                  <tbody class="divide-y divide-slate-100 bg-white">
                    <tr v-for="row in rows" :key="row.participantId">
                      <td class="px-3 py-3 align-top">
                        <div class="font-medium text-slate-900">{{ row.fullName }}</div>
                        <div class="mt-1 text-xs text-slate-500">{{ row.tcNo }}</div>
                      </td>
                      <td class="px-3 py-3 align-top">
                        <div class="font-medium text-slate-800">{{ row.effectiveAccommodationTitle }}</div>
                        <div v-if="row.usesOverride" class="mt-1 text-xs font-semibold text-sky-700">
                          {{ t('guide.accommodation.overrideBadge') }}
                        </div>
                      </td>
                      <td class="px-3 py-3 align-top text-slate-700">{{ row.roomNo || t('common.noData') }}</td>
                      <td class="px-3 py-3 align-top text-slate-700">{{ row.roomType || t('common.noData') }}</td>
                      <td class="px-3 py-3 align-top text-slate-700">{{ row.boardType || t('common.noData') }}</td>
                      <td class="px-3 py-3 align-top text-slate-700">{{ row.personNo || t('common.noData') }}</td>
                      <td class="px-3 py-3 align-top text-slate-700">
                        {{ row.roommates.length > 0 ? row.roommates.join(', ') : t('common.noData') }}
                      </td>
                    </tr>
                  </tbody>
                </table>
              </div>

              <div class="mt-4 flex items-center justify-end gap-2">
                <button
                  class="rounded-lg border border-slate-300 px-3 py-2 text-sm text-slate-700 transition hover:border-slate-400 hover:text-slate-900 disabled:cursor-not-allowed disabled:opacity-50"
                  :disabled="!hasPreviousPage || tableLoading"
                  type="button"
                  @click="page = Math.max(1, page - 1)"
                >
                  {{ t('common.previous') }}
                </button>
                <button
                  class="rounded-lg border border-slate-300 px-3 py-2 text-sm text-slate-700 transition hover:border-slate-400 hover:text-slate-900 disabled:cursor-not-allowed disabled:opacity-50"
                  :disabled="!hasNextPage || tableLoading"
                  type="button"
                  @click="page += 1"
                >
                  {{ t('common.next') }}
                </button>
              </div>
            </template>
          </template>
        </section>
      </template>
    </template>
  </div>
</template>
