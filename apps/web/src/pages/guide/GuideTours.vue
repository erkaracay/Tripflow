<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { apiGet } from '../../lib/api'
import LoadingState from '../../components/ui/LoadingState.vue'
import ErrorState from '../../components/ui/ErrorState.vue'
import type { TourListItem } from '../../types'

const { t } = useI18n()
const tours = ref<TourListItem[]>([])
const loading = ref(true)
const errorKey = ref<string | null>(null)
const errorMessage = ref<string | null>(null)

const loadTours = async () => {
  loading.value = true
  errorKey.value = null
  errorMessage.value = null
  try {
    tours.value = await apiGet<TourListItem[]>('/api/guide/tours')
  } catch (err) {
    errorMessage.value = err instanceof Error ? err.message : null
    if (!errorMessage.value) {
      errorKey.value = 'errors.guideTours.load'
    }
  } finally {
    loading.value = false
  }
}

onMounted(loadTours)
</script>

<template>
  <div class="space-y-6 sm:space-y-8">
    <section class="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:p-6">
      <h1 class="text-2xl font-semibold">{{ t('guide.tours.title') }}</h1>
      <p class="mt-1 text-sm text-slate-500">{{ t('guide.tours.subtitle') }}</p>
      <button
        class="mt-4 rounded-xl border border-slate-200 bg-white px-4 py-2 text-sm font-medium text-slate-700 hover:border-slate-300"
        type="button"
        @click="loadTours"
      >
        {{ t('common.refreshList') }}
      </button>
    </section>

    <LoadingState v-if="loading && tours.length === 0" message-key="guide.tours.loading" />
    <ErrorState
      v-if="errorKey || errorMessage"
      :message="errorMessage ?? undefined"
      :message-key="errorKey ?? undefined"
      @retry="loadTours"
    />

    <div
      v-if="!loading && tours.length === 0"
      class="rounded-2xl border border-dashed border-slate-200 bg-white p-4 text-sm text-slate-500"
    >
      {{ t('guide.tours.empty') }}
    </div>

    <ul v-if="tours.length > 0" class="space-y-3">
      <li
        v-for="tour in tours"
        :key="tour.id"
        class="flex flex-col gap-3 rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:flex-row sm:items-center sm:justify-between"
      >
        <div>
          <div class="text-base font-medium">{{ tour.name }}</div>
          <div class="text-xs text-slate-500">
            {{ t('common.dateRange', { start: tour.startDate, end: tour.endDate }) }}
          </div>
          <div class="mt-2 inline-flex items-center rounded-full bg-slate-100 px-3 py-1 text-xs text-slate-700">
            {{ t('common.arrivedSummary', { arrived: tour.arrivedCount, total: tour.totalCount }) }}
          </div>
        </div>
        <RouterLink
          class="w-full rounded-xl bg-slate-900 px-4 py-2 text-center text-sm font-medium text-white hover:bg-slate-800 sm:w-auto"
          :to="`/guide/tours/${tour.id}/checkin`"
        >
          {{ t('guide.tours.openCheckIn') }}
        </RouterLink>
      </li>
    </ul>
  </div>
</template>
