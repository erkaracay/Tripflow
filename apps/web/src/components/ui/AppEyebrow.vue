<script setup lang="ts">
import { computed } from 'vue'

export type EyebrowTone = 'mute' | 'mute-soft' | 'ember'

const props = withDefaults(defineProps<{ tone?: EyebrowTone; size?: 'sm' | 'md' }>(), {
  tone: 'mute',
  size: 'sm',
})

const toneClass = computed(() => {
  switch (props.tone) {
    case 'mute-soft':
      return 'text-mute-soft'
    case 'ember':
      return 'text-ember-deep'
    case 'mute':
    default:
      return 'text-mute'
  }
})

const sizeClass = computed(() =>
  props.size === 'md'
    ? 'text-[12px] tracking-[0.13em]'
    : 'text-[10.5px] tracking-[0.11em]'
)
</script>

<template>
  <div
    :class="[
      'font-mono font-medium uppercase leading-none',
      toneClass,
      sizeClass,
    ]"
  >
    <slot />
  </div>
</template>
