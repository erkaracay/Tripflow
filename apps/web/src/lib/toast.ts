import { ref } from 'vue'

export type ToastTone = 'success' | 'error' | 'info'

export type ToastAction = {
  label: string
  onClick: () => void
}

export type ToastOptions = {
  timeout?: number
  action?: ToastAction
}

export type ToastItem = {
  id: string
  message: string
  tone: ToastTone
  action?: ToastAction
}

const toasts = ref<ToastItem[]>([])

const createId = () => {
  const random = globalThis.crypto?.randomUUID?.()
  return random ?? `${Date.now()}-${Math.random().toString(16).slice(2)}`
}

export const pushToast = (
  message: string,
  tone: ToastTone = 'info',
  timeoutOrOptions: number | ToastOptions = 2600
) => {
  const id = createId()
  const options = typeof timeoutOrOptions === 'number' ? { timeout: timeoutOrOptions } : timeoutOrOptions
  const timeout = options.timeout ?? 2600

  toasts.value = [...toasts.value, { id, message, tone, action: options.action }]

  if (timeout > 0) {
    globalThis.setTimeout(() => {
      removeToast(id)
    }, timeout)
  }

  return id
}

export const removeToast = (id: string) => {
  toasts.value = toasts.value.filter((toast) => toast.id !== id)
}

export const useToast = () => ({
  toasts,
  pushToast,
  removeToast,
})
