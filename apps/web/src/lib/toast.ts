import { ref } from 'vue'

export type ToastTone = 'success' | 'error' | 'info'

export type ToastItem = {
  id: string
  message: string
  tone: ToastTone
}

const toasts = ref<ToastItem[]>([])

const createId = () => {
  const random = globalThis.crypto?.randomUUID?.()
  return random ?? `${Date.now()}-${Math.random().toString(16).slice(2)}`
}

export const pushToast = (message: string, tone: ToastTone = 'info', timeout = 2600) => {
  const id = createId()
  toasts.value = [...toasts.value, { id, message, tone }]

  if (timeout > 0) {
    globalThis.setTimeout(() => {
      removeToast(id)
    }, timeout)
  }
}

export const removeToast = (id: string) => {
  toasts.value = toasts.value.filter((toast) => toast.id !== id)
}

export const useToast = () => ({
  toasts,
  pushToast,
  removeToast,
})
