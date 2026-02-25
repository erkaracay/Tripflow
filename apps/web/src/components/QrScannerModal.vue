<script setup lang="ts">
import { nextTick, onUnmounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'

type ScannerControls = {
  stop: () => void
}

type ScanResult = {
  getText: () => string
}

type BrowserQrReader = {
  decodeFromVideoDevice: (
    deviceId: string | undefined,
    video: HTMLVideoElement,
    callback: (result: ScanResult | undefined) => void
  ) => Promise<ScannerControls>
}

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
const isScanning = ref(false)
let controls: ScannerControls | null = null
let reader: BrowserQrReader | null = null

const createReader = async (): Promise<BrowserQrReader> => {
  const module = await import('@zxing/browser')
  return new module.BrowserQRCodeReader() as BrowserQrReader
}

const stopScanner = () => {
  try {
    controls?.stop()
  } finally {
    controls = null
  }

  const video = videoRef.value
  const stream = video?.srcObject as MediaStream | null
  stream?.getTracks().forEach((track) => track.stop())
  if (video) {
    video.srcObject = null
  }

  reader = null
  isScanning.value = false
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

  // Always restart from a clean state to avoid parallel decode sessions / leaked tracks.
  stopScanner()

  errorKey.value = null
  isStarting.value = true
  isScanning.value = false
  await nextTick()

  if (!props.open) {
    isStarting.value = false
    return
  }

  if (!globalThis.navigator?.mediaDevices?.getUserMedia) {
    errorKey.value = 'guide.checkIn.cameraNotSupported'
    isStarting.value = false
    return
  }

  const video = videoRef.value
  if (!video) {
    isStarting.value = false
    return
  }

  try {
    reader = await createReader()
    controls = await reader.decodeFromVideoDevice(undefined, video, (result) => {
      if (!result) {
        return
      }
      stopScanner()
      emit('result', result.getText())
    })
    if (!props.open) {
      stopScanner()
      return
    }
    isScanning.value = true
  } catch (err) {
    errorKey.value = resolveErrorKey(err)
    stopScanner()
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

const handleClose = () => {
  stopScanner()
  emit('close')
}
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
            @click="handleClose"
          >
            {{ t('common.dismiss') }}
          </button>
        </div>

        <div class="mt-4 overflow-hidden rounded-2xl bg-black">
          <video ref="videoRef" class="h-64 w-full object-cover" playsinline muted></video>
        </div>

        <p class="mt-3 text-sm text-slate-600">
          {{
            errorKey
              ? t(errorKey)
              : isScanning
                ? t('guide.checkIn.scanningActive')
                : t('guide.checkIn.scanning')
          }}
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
            @click="handleClose"
          >
            {{ t('guide.checkIn.useManualCode') }}
          </button>
        </div>
      </div>
    </div>
  </Teleport>
</template>
