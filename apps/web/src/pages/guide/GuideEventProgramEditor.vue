<script setup lang="ts">
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import EventProgramEditor from '../../components/program/EventProgramEditor.vue'

const props = defineProps<{ eventId?: string }>()
const route = useRoute()
const eventId = computed(() => (props.eventId ?? route.params.eventId) as string)
</script>

<template>
  <EventProgramEditor :event-id="eventId" mode="guide" />
</template>
