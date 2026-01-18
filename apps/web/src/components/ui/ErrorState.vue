<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'

const props = defineProps<{
  message?: string
  messageKey?: string
  messageParams?: Record<string, string | number>
  actionLabel?: string
  actionKey?: string
  actionParams?: Record<string, string | number>
}>()
const emit = defineEmits<{ (event: 'retry'): void }>()

const { t } = useI18n()

const resolvedMessage = computed(() => {
  if (props.messageKey) {
    return t(props.messageKey, props.messageParams ?? {})
  }

  return props.message ?? t('errors.generic')
})

const resolvedActionLabel = computed(() => {
  if (props.actionKey) {
    return t(props.actionKey, props.actionParams ?? {})
  }

  return props.actionLabel ?? t('common.retry')
})
</script>

<template>
  <div class="rounded-2xl border border-rose-200 bg-rose-50 p-4">
    <p class="text-sm text-rose-700">{{ resolvedMessage }}</p>
    <button
      class="mt-3 rounded-xl border border-rose-200 bg-white px-3 py-2 text-xs font-medium text-rose-700 hover:border-rose-300"
      type="button"
      @click="emit('retry')"
    >
      {{ resolvedActionLabel }}
    </button>
  </div>
</template>
