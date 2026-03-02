<script setup lang="ts">
import { computed } from 'vue'

type SegmentedOption = {
  value: string
  label: string
  icon?: string
  disabled?: boolean
}

const props = withDefaults(
  defineProps<{
    modelValue: string
    options: SegmentedOption[]
    size?: 'sm' | 'md'
    fullWidth?: boolean
    ariaLabel?: string
    className?: string
  }>(),
  {
    size: 'md',
    fullWidth: false,
    ariaLabel: '',
    className: '',
  }
)

const emit = defineEmits<{
  (event: 'update:modelValue', value: string): void
}>()

const activeIndex = computed(() => {
  const index = props.options.findIndex((option) => option.value === props.modelValue)
  return index >= 0 ? index : 0
})

const rootClass = computed(() => [
  'app-segmented',
  props.fullWidth ? 'w-full' : '',
  props.className,
])

const buttonSizeClass = computed(() =>
  props.size === 'sm' ? 'px-2.5 py-1 text-xs font-semibold' : 'px-3 py-2 text-sm font-semibold'
)

const rootStyle = computed(() => ({
  '--segment-count': String(Math.max(props.options.length, 1)),
  '--segment-index': String(activeIndex.value),
  gridTemplateColumns: `repeat(${Math.max(props.options.length, 1)}, minmax(0, 1fr))`,
}))

const select = (value: string, disabled?: boolean) => {
  if (disabled || value === props.modelValue) {
    return
  }

  emit('update:modelValue', value)
}
</script>

<template>
  <div
    :class="rootClass"
    :style="rootStyle"
    role="tablist"
    :aria-label="ariaLabel || undefined"
  >
    <span class="app-segmented-indicator" aria-hidden="true" />
    <button
      v-for="option in options"
      :key="option.value"
      class="app-segmented-button"
      :class="[
        buttonSizeClass,
        option.value === modelValue ? 'app-segmented-button-active' : '',
        option.disabled ? 'cursor-not-allowed opacity-50' : '',
      ]"
      type="button"
      role="tab"
      :aria-selected="option.value === modelValue"
      :disabled="option.disabled"
      @click="select(option.value, option.disabled)"
    >
      <span v-if="option.icon" class="mr-1.5 text-[0.95em]" aria-hidden="true">{{ option.icon }}</span>
      <span>{{ option.label }}</span>
    </button>
  </div>
</template>
