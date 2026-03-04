<script setup lang="ts">
import { computed, onUnmounted, ref, watch } from 'vue'

const props = withDefaults(
  defineProps<{
    open: boolean
    closeOnOverlay?: boolean
    panelClass?: string
    overlayClass?: string
    contentClass?: string
    labelledBy?: string
    desktopWidth?: 'sm' | 'md' | 'lg' | 'xl'
    desktopBreakpoint?: 'md' | 'lg' | 'xl'
    swipeToClose?: boolean
    swipeThresholdPx?: number
    swipeVelocityPxMs?: number
    swipeHandleSelector?: string
  }>(),
  {
    closeOnOverlay: true,
    panelClass: '',
    overlayClass: '',
    contentClass: '',
    labelledBy: '',
    desktopWidth: 'lg',
    desktopBreakpoint: 'lg',
    swipeToClose: false,
    swipeThresholdPx: 88,
    swipeVelocityPxMs: 0.45,
    swipeHandleSelector: '[data-drawer-swipe-handle]',
  }
)

const emit = defineEmits<{
  (event: 'close'): void
}>()

const getScrollLockState = () => {
  const stateKey = '__tripflowDrawerScrollLockState' as const
  const globalRecord = globalThis as typeof globalThis & {
    __tripflowDrawerScrollLockState?: { count: number; overflow: string }
  }

  if (!globalRecord[stateKey]) {
    globalRecord[stateKey] = { count: 0, overflow: '' }
  }

  return globalRecord[stateKey]!
}

const desktopPanelClass = computed(() => {
  if (props.desktopBreakpoint === 'xl') {
    return props.desktopWidth === 'sm'
      ? 'xl:h-full xl:max-w-sm xl:rounded-none xl:rounded-l-3xl'
      : props.desktopWidth === 'md'
        ? 'xl:h-full xl:max-w-md xl:rounded-none xl:rounded-l-3xl'
        : props.desktopWidth === 'xl'
          ? 'xl:h-full xl:max-w-2xl xl:rounded-none xl:rounded-l-3xl'
          : 'xl:h-full xl:max-w-xl xl:rounded-none xl:rounded-l-3xl'
  }

  if (props.desktopBreakpoint === 'md') {
    return props.desktopWidth === 'sm'
      ? 'md:h-full md:max-w-sm md:rounded-none md:rounded-l-3xl'
      : props.desktopWidth === 'md'
        ? 'md:h-full md:max-w-md md:rounded-none md:rounded-l-3xl'
        : props.desktopWidth === 'xl'
          ? 'md:h-full md:max-w-2xl md:rounded-none md:rounded-l-3xl'
          : 'md:h-full md:max-w-xl md:rounded-none md:rounded-l-3xl'
  }

  return props.desktopWidth === 'sm'
    ? 'lg:h-full lg:max-w-sm lg:rounded-none lg:rounded-l-3xl'
    : props.desktopWidth === 'md'
      ? 'lg:h-full lg:max-w-md lg:rounded-none lg:rounded-l-3xl'
      : props.desktopWidth === 'xl'
        ? 'lg:h-full lg:max-w-2xl lg:rounded-none lg:rounded-l-3xl'
        : 'lg:h-full lg:max-w-xl lg:rounded-none lg:rounded-l-3xl'
})

const desktopContainerClass = computed(() =>
  props.desktopBreakpoint === 'xl'
    ? 'xl:items-stretch xl:justify-end'
    : props.desktopBreakpoint === 'md'
      ? 'md:items-stretch md:justify-end'
      : 'lg:items-stretch lg:justify-end'
)

const panelClassName = computed(() => [
  'app-drawer-panel relative z-10 flex h-[90vh] w-full flex-col rounded-t-3xl bg-white shadow-2xl',
  props.desktopBreakpoint === 'xl'
    ? 'app-drawer-breakpoint-xl'
    : props.desktopBreakpoint === 'md'
      ? 'app-drawer-breakpoint-md'
      : 'app-drawer-breakpoint-lg',
  desktopPanelClass.value,
  props.panelClass,
])

const overlayClassName = computed(() => [
  'app-drawer-overlay absolute inset-0 bg-slate-900/30',
  props.overlayClass,
])

const containerClassName = computed(() => [
  'fixed inset-0 z-50 flex items-end px-0',
  desktopContainerClass.value,
  props.contentClass,
])

const containerRef = ref<HTMLElement | null>(null)
const activePointerId = ref<number | null>(null)
const pointerStartY = ref(0)
const pointerStartAt = ref(0)
const currentDeltaY = ref(0)
const snapBackTimer = ref<ReturnType<typeof setTimeout> | null>(null)
const closingBySwipe = ref(false)

const resolvePanelElement = () => containerRef.value?.querySelector<HTMLElement>('.app-drawer-panel') ?? null
const resolveOverlayElement = () => containerRef.value?.querySelector<HTMLElement>('.app-drawer-overlay') ?? null

const clearSnapBackTimer = () => {
  if (snapBackTimer.value) {
    clearTimeout(snapBackTimer.value)
    snapBackTimer.value = null
  }
}

const clearInlineStyles = () => {
  const panel = resolvePanelElement()
  const overlay = resolveOverlayElement()
  if (panel) {
    panel.style.opacity = ''
    panel.style.transform = ''
    panel.style.transition = ''
  }
  if (overlay) {
    overlay.style.opacity = ''
    overlay.style.transition = ''
  }
}

const applyDragStyles = (deltaY: number) => {
  const panel = resolvePanelElement()
  const overlay = resolveOverlayElement()
  if (!panel || !overlay) {
    return
  }

  clearSnapBackTimer()
  panel.style.transition = 'none'
  overlay.style.transition = 'none'
  panel.style.transform = `translateY(${deltaY}px)`
  const opacity = Math.max(0.4, 1 - deltaY / 220)
  overlay.style.opacity = `${opacity}`
}

const animateSnapBack = () => {
  const panel = resolvePanelElement()
  const overlay = resolveOverlayElement()
  if (!panel || !overlay) {
    clearInlineStyles()
    return
  }

  panel.style.transition = 'transform 180ms ease-out'
  overlay.style.transition = 'opacity 180ms ease-out'
  panel.style.transform = ''
  overlay.style.opacity = ''

  clearSnapBackTimer()
  snapBackTimer.value = setTimeout(() => {
    clearInlineStyles()
  }, 200)
}

const removePointerListeners = () => {
  if (typeof window === 'undefined') {
    return
  }

  window.removeEventListener('pointermove', handlePointerMove)
  window.removeEventListener('pointerup', handlePointerUp)
  window.removeEventListener('pointercancel', handlePointerCancel)
}

const finishGesture = (mode: 'close' | 'snap') => {
  activePointerId.value = null
  currentDeltaY.value = 0
  removePointerListeners()

  if (mode === 'close') {
    closingBySwipe.value = true
    emit('close')
    return
  }

  animateSnapBack()
}

function handlePointerMove(event: PointerEvent) {
  if (activePointerId.value === null || event.pointerId !== activePointerId.value) {
    return
  }

  const deltaY = Math.max(0, event.clientY - pointerStartY.value)
  currentDeltaY.value = deltaY
  applyDragStyles(deltaY)
}

function handlePointerUp(event: PointerEvent) {
  if (activePointerId.value === null || event.pointerId !== activePointerId.value) {
    return
  }

  const deltaY = Math.max(0, event.clientY - pointerStartY.value)
  const elapsedMs = Math.max(1, performance.now() - pointerStartAt.value)
  const velocity = deltaY / elapsedMs
  const meetsDistance = deltaY >= props.swipeThresholdPx
  const meetsVelocity = deltaY >= 24 && velocity >= props.swipeVelocityPxMs

  finishGesture(meetsDistance || meetsVelocity ? 'close' : 'snap')
}

function handlePointerCancel(event?: PointerEvent) {
  if (activePointerId.value === null) {
    return
  }
  if (event && event.pointerId !== activePointerId.value) {
    return
  }
  finishGesture('snap')
}

const handlePointerDown = (event: PointerEvent) => {
  if (!props.swipeToClose || event.isPrimary === false) {
    return
  }

  if (event.pointerType === 'mouse' && event.button !== 0) {
    return
  }

  if (!(event.target instanceof Element)) {
    return
  }

  if (!event.target.closest(props.swipeHandleSelector)) {
    return
  }

  pointerStartY.value = event.clientY
  pointerStartAt.value = performance.now()
  currentDeltaY.value = 0
  activePointerId.value = event.pointerId

  if (typeof window !== 'undefined') {
    window.addEventListener('pointermove', handlePointerMove)
    window.addEventListener('pointerup', handlePointerUp)
    window.addEventListener('pointercancel', handlePointerCancel)
  }
}

const lockBodyScroll = () => {
  if (typeof document === 'undefined') {
    return
  }

  const state = getScrollLockState()
  if (state.count === 0) {
    state.overflow = document.body.style.overflow
  }
  state.count += 1
  document.body.style.overflow = 'hidden'
}

const unlockBodyScroll = () => {
  if (typeof document === 'undefined') {
    return
  }

  const state = getScrollLockState()
  if (state.count === 0) {
    return
  }

  state.count -= 1
  if (state.count === 0) {
    document.body.style.overflow = state.overflow
    state.overflow = ''
  }
}

const handleOverlayClick = () => {
  if (!props.closeOnOverlay) {
    return
  }

  emit('close')
}

watch(
  () => props.open,
  (open, previousOpen) => {
    if (open && !previousOpen) {
      closingBySwipe.value = false
      lockBodyScroll()
      clearInlineStyles()
      return
    }

    if (!open && previousOpen) {
      handlePointerCancel()
      if (!closingBySwipe.value) {
        clearInlineStyles()
      }
      closingBySwipe.value = false
      unlockBodyScroll()
    }
  }
)

onUnmounted(() => {
  handlePointerCancel()
  clearSnapBackTimer()
  clearInlineStyles()
  if (props.open) {
    unlockBodyScroll()
  }
})
</script>

<template>
  <Teleport to="body">
    <Transition name="app-drawer">
      <div
        v-if="open"
        ref="containerRef"
        :class="containerClassName"
        tabindex="-1"
        @pointerdown.capture="handlePointerDown"
      >
        <div :class="overlayClassName" @click="handleOverlayClick" />
        <slot :panel-class="panelClassName" :labelled-by="labelledBy" />
      </div>
    </Transition>
  </Teleport>
</template>
