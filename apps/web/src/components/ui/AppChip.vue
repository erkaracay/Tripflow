<script setup lang="ts">
import { computed } from 'vue'

export type ChipTone = 'neutral' | 'ember' | 'sage' | 'sky' | 'coral' | 'dark'

const props = withDefaults(defineProps<{ tone?: ChipTone }>(), {
  tone: 'neutral',
})

const toneClass = computed(() => {
  switch (props.tone) {
    case 'ember':
      return 'bg-ember-soft text-ember-deep border-ember-line'
    case 'sage':
      return 'bg-sage-soft text-sage border-sage/30'
    case 'sky':
      return 'bg-sky-soft text-sky border-sky/30'
    case 'coral':
      return 'bg-coral-soft text-coral border-coral/30'
    case 'dark':
      return 'bg-ink text-paper border-ink'
    case 'neutral':
    default:
      return 'bg-paper-warm text-ink-soft border-line'
  }
})
</script>

<template>
  <span
    :class="[
      'inline-flex items-center gap-1 rounded-full border px-[7px] py-[3px] font-mono text-[10.5px] font-medium uppercase tracking-[0.11em] leading-none',
      toneClass,
    ]"
  >
    <slot />
  </span>
</template>
