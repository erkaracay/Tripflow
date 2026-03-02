<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import { apiGet } from '../../lib/api'
import MealReportView from '../../components/meal/MealReportView.vue'
import type { EventActivity, EventDay } from '../../types'

const props = defineProps<{ eventId?: string; activityId?: string }>()
const route = useRoute()

const eventId = computed(() => (props.eventId ?? route.params.eventId) as string)
const activityId = computed(() => (props.activityId ?? route.params.activityId) as string)
const activityTitle = ref<string | null>(null)

const loadActivityTitle = async () => {
  activityTitle.value = null

  try {
    const days = await apiGet<EventDay[]>(`/api/events/${eventId.value}/days`)
    const activitiesByDay = await Promise.all(
      days.map(async (day) => ({
        dayId: day.id,
        items: await apiGet<EventActivity[]>(`/api/events/${eventId.value}/days/${day.id}/activities`),
      }))
    )

    const found = activitiesByDay.flatMap((entry) => entry.items).find((item) => item.id === activityId.value)
    activityTitle.value = found?.title ?? null
  } catch {
    activityTitle.value = null
  }
}

watch(
  () => [eventId.value, activityId.value] as const,
  () => {
    void loadActivityTitle()
  },
  { immediate: true }
)
</script>

<template>
  <MealReportView
    :activity-id="activityId"
    :activity-title="activityTitle"
    :event-id="eventId"
    mode="admin"
  />
</template>
