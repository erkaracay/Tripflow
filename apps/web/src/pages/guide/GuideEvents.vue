<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { apiGet } from '../../lib/api'
import LoadingState from '../../components/ui/LoadingState.vue'
import ErrorState from '../../components/ui/ErrorState.vue'
import type { EventListItem } from '../../types'

const { t } = useI18n()
const events = ref<EventListItem[]>([])
const loading = ref(true)
const errorKey = ref<string | null>(null)
const errorMessage = ref<string | null>(null)

const loadEvents = async () => {
  loading.value = true
  errorKey.value = null
  errorMessage.value = null
  try {
    events.value = await apiGet<EventListItem[]>('/api/guide/events')
  } catch (err) {
    errorMessage.value = err instanceof Error ? err.message : null
    if (!errorMessage.value) {
      errorKey.value = 'errors.guideEvents.load'
    }
  } finally {
    loading.value = false
  }
}

onMounted(loadEvents)
</script>

<template>
  <div class="space-y-6 sm:space-y-8">
    <section class="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:p-6">
      <h1 class="text-2xl font-semibold">{{ t('guide.events.title') }}</h1>
      <p class="mt-1 text-sm text-slate-500">{{ t('guide.events.subtitle') }}</p>
      <button
        class="mt-4 rounded-xl border border-slate-200 bg-white px-4 py-2 text-sm font-medium text-slate-700 hover:border-slate-300"
        type="button"
        @click="loadEvents"
      >
        {{ t('common.refreshList') }}
      </button>
    </section>

    <LoadingState v-if="loading && events.length === 0" message-key="guide.events.loading" />
    <ErrorState
      v-if="errorKey || errorMessage"
      :message="errorMessage ?? undefined"
      :message-key="errorKey ?? undefined"
      @retry="loadEvents"
    />

    <div
      v-if="!loading && events.length === 0"
      class="rounded-2xl border border-dashed border-slate-200 bg-white p-4 text-sm text-slate-500"
    >
      {{ t('guide.events.empty') }}
    </div>

    <ul v-if="events.length > 0" class="space-y-3">
      <li
        v-for="event in events"
        :key="event.id"
        class="flex flex-col gap-3 rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:flex-row sm:items-center sm:justify-between"
      >
        <div>
          <div class="text-base font-medium">{{ event.name }}</div>
          <div class="text-xs text-slate-500">
            {{ t('common.dateRange', { start: event.startDate, end: event.endDate }) }}
          </div>
          <div class="mt-2 inline-flex items-center rounded-full bg-slate-100 px-3 py-1 text-xs text-slate-700">
            {{ t('common.arrivedSummary', { arrived: event.arrivedCount, total: event.totalCount }) }}
          </div>
        </div>
        <div class="flex w-full flex-col gap-2 sm:w-auto sm:flex-row sm:items-center">
          <RouterLink
            class="w-full rounded-xl bg-slate-900 px-4 py-2 text-center text-sm font-medium text-white hover:bg-slate-800 sm:w-auto"
            :to="`/guide/events/${event.id}/checkin`"
          >
            {{ t('guide.events.openCheckIn') }}
          </RouterLink>
          <RouterLink
            class="w-full rounded-xl border border-slate-200 bg-white px-4 py-2 text-center text-sm font-medium text-slate-700 hover:border-slate-300 sm:w-auto"
            :to="`/guide/events/${event.id}/program`"
          >
            {{ t('guide.events.openProgram') }}
          </RouterLink>
        </div>
      </li>
    </ul>
  </div>
</template>
