<script setup lang="ts">
const props = defineProps<{ message?: string }>()
</script>

<template>
  <div class="rounded-2xl border border-dashed border-slate-200 bg-white p-4">
    <div class="flex items-center gap-3 text-sm text-slate-500">
      <span class="h-4 w-4 animate-spin rounded-full border-2 border-slate-200 border-t-slate-500"></span>
      <span>{{ props.message ?? 'Loading...' }}</span>
    </div>
  </div>
</template>
