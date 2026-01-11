<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { apiGet } from '../../lib/api'
import LoadingState from '../../components/ui/LoadingState.vue'
import ErrorState from '../../components/ui/ErrorState.vue'
import type { TourListItem } from '../../types'

const tours = ref<TourListItem[]>([])
const loading = ref(true)
const error = ref<string | null>(null)

const loadTours = async () => {
  loading.value = true
  error.value = null
  try {
    tours.value = await apiGet<TourListItem[]>('/api/guide/tours')
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Failed to load tours.'
  } finally {
    loading.value = false
  }
}

onMounted(loadTours)
</script>

<template>
  <div class="space-y-6 sm:space-y-8">
    <section class="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:p-6">
      <h1 class="text-2xl font-semibold">Guide Tours</h1>
      <p class="mt-1 text-sm text-slate-500">Check-in your assigned tours.</p>
      <button
        class="mt-4 rounded-xl border border-slate-200 bg-white px-4 py-2 text-sm font-medium text-slate-700 hover:border-slate-300"
        type="button"
        @click="loadTours"
      >
        Refresh list
      </button>
    </section>

    <LoadingState v-if="loading" message="Loading tours..." />
    <ErrorState v-else-if="error" :message="error" @retry="loadTours" />

    <div
      v-else-if="tours.length === 0"
      class="rounded-2xl border border-dashed border-slate-200 bg-white p-4 text-sm text-slate-500"
    >
      No tours assigned yet.
    </div>

    <ul v-else class="space-y-3">
      <li
        v-for="tour in tours"
        :key="tour.id"
        class="flex flex-col gap-3 rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:flex-row sm:items-center sm:justify-between"
      >
        <div>
          <div class="text-base font-medium">{{ tour.name }}</div>
          <div class="text-xs text-slate-500">{{ tour.startDate }} to {{ tour.endDate }}</div>
          <div class="mt-2 inline-flex items-center rounded-full bg-slate-100 px-3 py-1 text-xs text-slate-700">
            Arrived {{ tour.arrivedCount }} / {{ tour.totalCount }}
          </div>
        </div>
        <RouterLink
          class="w-full rounded-xl bg-slate-900 px-4 py-2 text-center text-sm font-medium text-white hover:bg-slate-800 sm:w-auto"
          :to="`/guide/tours/${tour.id}/checkin`"
        >
          Open check-in
        </RouterLink>
      </li>
    </ul>
  </div>
</template>
