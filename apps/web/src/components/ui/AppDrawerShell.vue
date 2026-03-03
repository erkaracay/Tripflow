<script setup lang="ts">
import { computed, onUnmounted, watch } from 'vue'

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
  }>(),
  {
    closeOnOverlay: true,
    panelClass: '',
    overlayClass: '',
    contentClass: '',
    labelledBy: '',
    desktopWidth: 'lg',
    desktopBreakpoint: 'lg',
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
      lockBodyScroll()
      return
    }

    if (!open && previousOpen) {
      unlockBodyScroll()
    }
  }
)

onUnmounted(() => {
  if (props.open) {
    unlockBodyScroll()
  }
})
</script>

<template>
  <Teleport to="body">
    <Transition name="app-drawer">
      <div v-if="open" :class="containerClassName" tabindex="-1">
        <div :class="overlayClassName" @click="handleOverlayClick" />
        <slot :panel-class="panelClassName" :labelled-by="labelledBy" />
      </div>
    </Transition>
  </Teleport>
</template>
