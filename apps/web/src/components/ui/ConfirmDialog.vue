<script setup lang="ts">
import { onMounted, onUnmounted } from 'vue'

type Tone = 'default' | 'danger'

const props = withDefaults(
  defineProps<{
    open: boolean
    title: string
    message: string
    confirmLabel?: string
    cancelLabel?: string
    tone?: Tone
  }>(),
  {
    confirmLabel: 'Confirm',
    cancelLabel: 'Cancel',
    tone: 'default',
  }
)

const emit = defineEmits<{
  (event: 'confirm'): void
  (event: 'cancel'): void
  (event: 'update:open', value: boolean): void
}>()

const close = () => {
  emit('update:open', false)
  emit('cancel')
}

const confirm = () => {
  emit('update:open', false)
  emit('confirm')
}

const handleEscape = (event: KeyboardEvent) => {
  if (event.key === 'Escape' && props.open) {
    close()
  }
}

onMounted(() => {
  globalThis.addEventListener('keydown', handleEscape)
})

onUnmounted(() => {
  globalThis.removeEventListener('keydown', handleEscape)
})
</script>

<template>
  <teleport to="body">
    <div
      v-if="open"
      class="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/40 px-4"
      @click.self="close"
      tabindex="-1"
    >
      <div class="w-full max-w-md rounded-2xl bg-white p-5 shadow-xl">
        <div class="text-lg font-semibold text-slate-900">{{ title }}</div>
        <p class="mt-2 text-sm text-slate-600">{{ message }}</p>
        <div class="mt-6 flex flex-wrap justify-end gap-2">
          <button
            class="rounded-lg border border-slate-200 bg-white px-4 py-2 text-sm font-medium text-slate-700 hover:border-slate-300"
            type="button"
            @click="close"
          >
            {{ cancelLabel }}
          </button>
          <button
            class="rounded-lg px-4 py-2 text-sm font-medium text-white"
            :class="tone === 'danger' ? 'bg-rose-600 hover:bg-rose-500' : 'bg-slate-900 hover:bg-slate-800'"
            type="button"
            @click="confirm"
          >
            {{ confirmLabel }}
          </button>
        </div>
      </div>
    </div>
  </teleport>
</template>
