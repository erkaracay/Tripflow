<script setup lang="ts">
import { computed } from 'vue'

const props = withDefaults(
  defineProps<{
    open: boolean
    closeOnOverlay?: boolean
    panelClass?: string
    overlayClass?: string
    contentClass?: string
    labelledBy?: string
  }>(),
  {
    closeOnOverlay: true,
    panelClass: '',
    overlayClass: '',
    contentClass: '',
    labelledBy: '',
  }
)

const emit = defineEmits<{
  (event: 'close'): void
}>()

const panelClassName = computed(() => ['app-modal-panel relative', props.panelClass])
const overlayClassName = computed(() => ['app-modal-overlay absolute inset-0 bg-slate-900/40', props.overlayClass])
const containerClassName = computed(() => ['fixed inset-0 z-50 flex items-center justify-center px-4', props.contentClass])

const handleOverlayClick = () => {
  if (!props.closeOnOverlay) {
    return
  }

  emit('close')
}
</script>

<template>
  <Teleport to="body">
    <Transition name="app-modal">
      <div v-if="open" :class="containerClassName" tabindex="-1">
        <div :class="overlayClassName" @click="handleOverlayClick" />
        <slot :panel-class="panelClassName" :labelled-by="labelledBy" />
      </div>
    </Transition>
  </Teleport>
</template>
