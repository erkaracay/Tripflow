<script setup lang="ts">
import { onBeforeUnmount, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'

type Props = {
  open: boolean
  title: string
  email?: string | null
  name?: string | null
  loading?: boolean
  errorKey?: string | null
  errorMessage?: string | null
}

const props = defineProps<Props>()
const emit = defineEmits<{
  (e: 'close'): void
  (e: 'submit', payload: { password: string; confirm: string }): void
}>()

const { t } = useI18n()

const password = ref('')
const confirm = ref('')
const showPassword = ref(false)
const showConfirm = ref(false)

const resetForm = () => {
  password.value = ''
  confirm.value = ''
  showPassword.value = false
  showConfirm.value = false
}

watch(
  () => props.open,
  (isOpen) => {
    if (isOpen) {
      resetForm()
    }
  }
)

const onClose = () => {
  emit('close')
}

const onSubmit = () => {
  emit('submit', { password: password.value, confirm: confirm.value })
}

const onKeydown = (event: KeyboardEvent) => {
  if (!props.open) {
    return
  }
  if (event.key === 'Escape') {
    event.preventDefault()
    onClose()
  }
}

if (typeof window !== 'undefined') {
  window.addEventListener('keydown', onKeydown)
}

onBeforeUnmount(() => {
  if (typeof window !== 'undefined') {
    window.removeEventListener('keydown', onKeydown)
  }
})
</script>

<template>
  <Teleport to="body">
    <div
      v-if="open"
      class="fixed inset-0 z-50 flex items-center justify-center bg-black/40 px-4"
      @click.self="onClose"
    >
      <div class="w-full max-w-lg rounded-2xl bg-white p-6 shadow-xl">
        <div class="flex items-start justify-between gap-4">
          <div>
            <h3 class="text-lg font-semibold text-slate-900">{{ title }}</h3>
            <p v-if="name || email" class="mt-1 text-sm text-slate-500">
              <span v-if="name">{{ name }}</span>
              <span v-if="name && email"> · </span>
              <span v-if="email">{{ email }}</span>
            </p>
          </div>
          <button
            type="button"
            class="rounded-full p-1 text-slate-500 hover:bg-slate-100"
            :aria-label="t('common.dismiss')"
            @click="onClose"
          >
            ✕
          </button>
        </div>

        <div class="mt-4 grid gap-4">
          <label class="grid gap-1 text-sm">
            <span class="text-slate-600">{{ t('common.newPassword') }}</span>
            <div class="flex items-center gap-2">
              <input
                v-model="password"
                class="w-full rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
                :type="showPassword ? 'text' : 'password'"
                :placeholder="t('common.newPassword')"
              />
              <button
                type="button"
                class="shrink-0 rounded border border-slate-200 px-3 py-2 text-xs font-semibold text-slate-600 hover:bg-slate-50"
                @click="showPassword = !showPassword"
              >
                {{ showPassword ? t('common.hide') : t('common.show') }}
              </button>
            </div>
          </label>

          <label class="grid gap-1 text-sm">
            <span class="text-slate-600">{{ t('common.confirmPassword') }}</span>
            <div class="flex items-center gap-2">
              <input
                v-model="confirm"
                class="w-full rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
                :type="showConfirm ? 'text' : 'password'"
                :placeholder="t('common.confirmPassword')"
              />
              <button
                type="button"
                class="shrink-0 rounded border border-slate-200 px-3 py-2 text-xs font-semibold text-slate-600 hover:bg-slate-50"
                @click="showConfirm = !showConfirm"
              >
                {{ showConfirm ? t('common.hide') : t('common.show') }}
              </button>
            </div>
          </label>

          <p v-if="errorKey" class="text-xs text-rose-600">{{ t(errorKey) }}</p>
          <p v-else-if="errorMessage" class="text-xs text-rose-600">{{ errorMessage }}</p>
        </div>

        <div class="mt-6 flex flex-wrap items-center justify-end gap-2">
          <button
            type="button"
            class="rounded border border-slate-200 px-4 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50"
            @click="onClose"
          >
            {{ t('common.cancel') }}
          </button>
          <button
            type="button"
            class="inline-flex items-center gap-2 rounded bg-slate-900 px-4 py-2 text-sm font-medium text-white hover:bg-slate-800 disabled:cursor-not-allowed disabled:opacity-60"
            :disabled="loading"
            @click="onSubmit"
          >
            <span v-if="loading" class="h-3 w-3 animate-spin rounded-full border border-white/60 border-t-transparent"></span>
            {{ loading ? t('common.saving') : t('common.save') }}
          </button>
        </div>
      </div>
    </div>
  </Teleport>
</template>
