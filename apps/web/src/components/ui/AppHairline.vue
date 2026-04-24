<script setup lang="ts">
withDefaults(defineProps<{ vertical?: boolean }>(), {
  vertical: false,
})
</script>

<template>
  <div
    :class="[
      'bg-line',
      vertical ? 'w-px self-stretch' : 'h-px w-full',
    ]"
    role="separator"
    aria-hidden="true"
  />
</template>
