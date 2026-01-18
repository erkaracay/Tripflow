<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'

const props = defineProps<{
  message?: string
  messageKey?: string
  messageParams?: Record<string, string | number>
}>()

const { t } = useI18n()

const resolvedMessage = computed(() => {
  if (props.messageKey) {
    return t(props.messageKey, props.messageParams ?? {})
  }

  return props.message ?? t('common.loading')
})
</script>

<template>
  <div class="rounded-2xl border border-dashed border-slate-200 bg-white p-4">
    <div class="flex items-center gap-3 text-sm text-slate-500">
      <span class="h-4 w-4 animate-spin rounded-full border-2 border-slate-200 border-t-slate-500"></span>
      <span>{{ resolvedMessage }}</span>
    </div>
  </div>
</template>
