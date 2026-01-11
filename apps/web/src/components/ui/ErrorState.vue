<script setup lang="ts">
const props = defineProps<{ message?: string; actionLabel?: string }>()
const emit = defineEmits<{ (event: 'retry'): void }>()
</script>

<template>
  <div class="rounded-2xl border border-rose-200 bg-rose-50 p-4">
    <p class="text-sm text-rose-700">{{ props.message ?? 'Something went wrong.' }}</p>
    <button
      class="mt-3 rounded-xl border border-rose-200 bg-white px-3 py-2 text-xs font-medium text-rose-700 hover:border-rose-300"
      type="button"
      @click="emit('retry')"
    >
      {{ props.actionLabel ?? 'Retry' }}
    </button>
  </div>
</template>
