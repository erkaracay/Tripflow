import { ref } from 'vue'

export type ToastTone = 'success' | 'error' | 'info'

export type ToastAction = {
  label?: string
  labelKey?: string
  labelParams?: Record<string, string | number>
  onClick: () => void
}

export type ToastPayload = {
  key: string
  params?: Record<string, string | number>
  tone?: ToastTone
  timeout?: number
  action?: ToastAction
}

export type ToastOptions = {
  timeout?: number
  action?: ToastAction
}

export type ToastItem = {
  id: string
  message?: string
  key?: string
  params?: Record<string, string | number>
  tone: ToastTone
  action?: ToastAction
}

const toasts = ref<ToastItem[]>([])

const createId = () => {
  const random = globalThis.crypto?.randomUUID?.()
  return random ?? `${Date.now()}-${Math.random().toString(16).slice(2)}`
}

const isToastPayload = (value: string | ToastPayload): value is ToastPayload =>
  typeof value === 'object' && value !== null && 'key' in value

export const pushToast = (
  payload: string | ToastPayload,
  tone: ToastTone = 'info',
  timeoutOrOptions: number | ToastOptions = 2600
) => {
  const id = createId()

  if (isToastPayload(payload)) {
    const timeout = payload.timeout ?? 2600
    const item: ToastItem = {
      id,
      key: payload.key,
      params: payload.params,
      tone: payload.tone ?? 'info',
      action: payload.action,
    }

    toasts.value = [...toasts.value, item]

    if (timeout > 0) {
      globalThis.setTimeout(() => {
        removeToast(id)
      }, timeout)
    }

    return id
  }

  const options = typeof timeoutOrOptions === 'number' ? { timeout: timeoutOrOptions } : timeoutOrOptions
  const timeout = options.timeout ?? 2600

  toasts.value = [...toasts.value, { id, message: payload, tone, action: options.action }]

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
