<script setup lang="ts">
import { computed } from 'vue'
import { useToast } from '../../lib/toast'

const { toasts, removeToast } = useToast()

const toneClass = (tone: string) => {
  switch (tone) {
    case 'success':
      return 'border-emerald-200 bg-emerald-50 text-emerald-800'
    case 'error':
      return 'border-rose-200 bg-rose-50 text-rose-800'
    default:
      return 'border-slate-200 bg-white text-slate-700'
  }
}

const stackedToasts = computed(() => toasts.value.slice(-4))

const handleAction = (toast: { id: string; action?: { onClick: () => void } }) => {
  toast.action?.onClick()
  removeToast(toast.id)
}
</script>

<template>
  <div class="pointer-events-none fixed inset-x-0 bottom-4 z-50 flex justify-center px-4 sm:bottom-6">
    <div class="flex w-full max-w-sm flex-col gap-2">
      <div
        v-for="toast in stackedToasts"
        :key="toast.id"
        class="pointer-events-auto flex items-start justify-between gap-3 rounded-2xl border px-4 py-3 text-sm shadow-sm"
        :class="toneClass(toast.tone)"
      >
        <span class="flex-1">{{ toast.message }}</span>
        <div class="flex items-center gap-2">
          <button
            v-if="toast.action"
            class="text-xs font-semibold text-slate-700 hover:text-slate-900"
            type="button"
            @click="handleAction(toast)"
          >
            {{ toast.action.label }}
          </button>
          <button
            class="text-xs font-medium text-slate-500 hover:text-slate-700"
            type="button"
            @click="removeToast(toast.id)"
          >
            Dismiss
          </button>
        </div>
      </div>
    </div>
  </div>
</template>
