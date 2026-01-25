<script setup lang="ts">
import { nextTick, onUnmounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { BrowserQRCodeReader, type IScannerControls } from '@zxing/browser'

type Props = {
  open: boolean
}

const props = defineProps<Props>()
const emit = defineEmits<{
  (event: 'close'): void
  (event: 'result', value: string): void
}>()

const { t } = useI18n()
const videoRef = ref<HTMLVideoElement | null>(null)
const errorKey = ref<string | null>(null)
const isStarting = ref(false)
let controls: IScannerControls | null = null
let reader: BrowserQRCodeReader | null = null

const stopScanner = () => {
  controls?.stop()
  controls = null
  reader?.reset()
  reader = null
}

const resolveErrorKey = (err: unknown) => {
  if (err instanceof DOMException) {
    if (err.name === 'NotAllowedError' || err.name === 'SecurityError') {
      return 'guide.checkIn.cameraPermissionDenied'
    }
    if (err.name === 'NotFoundError' || err.name === 'OverconstrainedError') {
      return 'guide.checkIn.cameraNotFound'
    }
  }

  return 'guide.checkIn.cameraError'
}

const startScanner = async () => {
  if (!props.open || isStarting.value) {
    return
  }

  errorKey.value = null
  isStarting.value = true
  await nextTick()

  const video = videoRef.value
  if (!video) {
    isStarting.value = false
    return
  }

  try {
    reader = new BrowserQRCodeReader()
    controls = await reader.decodeFromVideoDevice(undefined, video, (result) => {
      if (!result) {
        return
      }
      stopScanner()
      emit('result', result.getText())
    })
  } catch (err) {
    errorKey.value = resolveErrorKey(err)
  } finally {
    isStarting.value = false
  }
}

watch(
  () => props.open,
  (open) => {
    if (open) {
      void startScanner()
    } else {
      stopScanner()
    }
  },
  { immediate: true }
)

onUnmounted(() => {
  stopScanner()
})
</script>

<template>
  <Teleport to="body">
    <div v-if="open" class="fixed inset-0 z-50 flex items-center justify-center bg-black/70 p-4">
      <div class="w-full max-w-md rounded-2xl bg-white p-4 shadow-xl">
        <div class="flex items-center justify-between">
          <h3 class="text-base font-semibold text-slate-900">{{ t('guide.checkIn.scanQr') }}</h3>
          <button
            class="rounded-full border border-slate-200 bg-white px-2 py-1 text-xs font-semibold text-slate-600 hover:border-slate-300"
            type="button"
            @click="emit('close')"
          >
            {{ t('common.dismiss') }}
          </button>
        </div>

        <div class="mt-4 overflow-hidden rounded-2xl bg-black">
          <video ref="videoRef" class="h-64 w-full object-cover" playsinline muted></video>
        </div>

        <p class="mt-3 text-sm text-slate-600">
          {{ errorKey ? t(errorKey) : t('guide.checkIn.scanning') }}
        </p>

        <div class="mt-4 flex flex-wrap items-center gap-2">
          <button
            class="rounded-full border border-slate-200 bg-white px-3 py-1.5 text-xs font-semibold text-slate-700 hover:border-slate-300"
            type="button"
            @click="startScanner"
          >
            {{ t('common.retry') }}
          </button>
          <button
            class="rounded-full border border-slate-200 bg-white px-3 py-1.5 text-xs font-semibold text-slate-700 hover:border-slate-300"
            type="button"
            @click="emit('close')"
          >
            {{ t('guide.checkIn.useManualCode') }}
          </button>
        </div>
      </div>
    </div>
  </Teleport>
</template>
