<script setup lang="ts">
import { computed, nextTick, onMounted, onUnmounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import AppDrawerShell from './AppDrawerShell.vue'
import type { AppComboboxOption, AppComboboxValue } from '../../types'

type ComboboxRenderGroup = {
  key: string
  label: string | null
  options: Array<{
    option: AppComboboxOption
    index: number
  }>
}

const props = withDefaults(
  defineProps<{
    modelValue: AppComboboxValue[]
    options: AppComboboxOption[]
    placeholder?: string
    searchPlaceholder?: string
    emptyLabel?: string
    invalid?: boolean
    disabled?: boolean
    ariaLabel?: string
    mobileBreakpoint?: 'md' | 'lg'
    searchable?: boolean
    compact?: boolean
    maxVisibleChips?: number
    removeChipLabel?: string
  }>(),
  {
    placeholder: '',
    searchPlaceholder: '',
    emptyLabel: '',
    invalid: false,
    disabled: false,
    ariaLabel: '',
    mobileBreakpoint: 'md',
    searchable: true,
    compact: false,
    maxVisibleChips: 2,
    removeChipLabel: '',
  }
)

const emit = defineEmits<{
  (event: 'update:modelValue', value: AppComboboxValue[]): void
  (event: 'open'): void
  (event: 'close'): void
}>()

const { t } = useI18n()

const rootRef = ref<HTMLElement | null>(null)
const desktopPanelRef = ref<HTMLElement | null>(null)
const mobilePanelRef = ref<HTMLElement | null>(null)
const desktopSearchRef = ref<HTMLInputElement | null>(null)
const mobileSearchRef = ref<HTMLInputElement | null>(null)
const open = ref(false)
const search = ref('')
const highlightedIndex = ref(-1)
const isMobile = ref(false)

let mediaQueryList: MediaQueryList | null = null
let mediaQueryListener:
  | ((event: MediaQueryListEvent) => void)
  | null = null

const breakpointQuery = computed(() =>
  props.mobileBreakpoint === 'lg' ? '(max-width: 1023px)' : '(max-width: 767px)'
)

const normalizedSearch = computed(() => search.value.trim().toLowerCase())

const optionKey = (value: AppComboboxValue) => `${typeof value}:${String(value)}`

const selectedValueKeySet = computed(() => new Set(props.modelValue.map((value) => optionKey(value))))

const optionLookup = computed(() => {
  const map = new Map<string, AppComboboxOption>()
  props.options.forEach((option) => {
    map.set(optionKey(option.value), option)
  })
  return map
})

const selectedOptions = computed<AppComboboxOption[]>(() =>
  props.modelValue.map((value) => {
    const existing = optionLookup.value.get(optionKey(value))
    if (existing) {
      return existing
    }

    return {
      value,
      label: String(value),
      description: null,
    }
  })
)

const visibleChipOptions = computed(() => selectedOptions.value.slice(0, props.maxVisibleChips))
const overflowChipCount = computed(() => Math.max(0, selectedOptions.value.length - visibleChipOptions.value.length))

const matchesSearch = (option: AppComboboxOption, query: string) => {
  if (!query) {
    return true
  }

  const haystack = [
    option.label,
    option.description ?? '',
    option.groupLabel ?? '',
    String(option.value),
    ...(option.keywords ?? []),
  ]
    .join(' ')
    .toLowerCase()

  return haystack.includes(query)
}

const filteredOptions = computed(() => props.options.filter((option) => matchesSearch(option, normalizedSearch.value)))

const buildRenderGroups = (options: AppComboboxOption[]): ComboboxRenderGroup[] => {
  const groups: ComboboxRenderGroup[] = []
  const groupIndexByKey = new Map<string, number>()

  options.forEach((option, index) => {
    const label = option.groupLabel?.trim() || null
    const key = label ? `group:${label}` : 'group:__ungrouped__'
    const existingIndex = groupIndexByKey.get(key)
    const entry = { option, index }

    if (existingIndex === undefined) {
      groupIndexByKey.set(key, groups.length)
      groups.push({
        key,
        label,
        options: [entry],
      })
      return
    }

    const group = groups[existingIndex]
    if (group) {
      group.options.push(entry)
    }
  })

  return groups
}

const renderGroups = computed(() => buildRenderGroups(filteredOptions.value))
const desktopOpen = computed(() => open.value && !isMobile.value)
const mobileOpen = computed(() => open.value && isMobile.value)
const panelTitle = computed(() => props.ariaLabel || props.placeholder || t('common.search'))

const activePanelRef = () => (isMobile.value ? mobilePanelRef.value : desktopPanelRef.value)
const activeSearchRef = () => (isMobile.value ? mobileSearchRef.value : desktopSearchRef.value)

const syncMobileMode = () => {
  if (typeof window === 'undefined' || typeof window.matchMedia !== 'function') {
    isMobile.value = false
    return
  }

  const nextQueryList = window.matchMedia(breakpointQuery.value)
  isMobile.value = nextQueryList.matches

  if (mediaQueryList?.media === nextQueryList.media) {
    return
  }

  if (mediaQueryList && mediaQueryListener) {
    if (typeof mediaQueryList.removeEventListener === 'function') {
      mediaQueryList.removeEventListener('change', mediaQueryListener)
    } else {
      mediaQueryList.removeListener(mediaQueryListener)
    }
  }

  mediaQueryList = nextQueryList
  mediaQueryListener = (event: MediaQueryListEvent) => {
    isMobile.value = event.matches
  }

  if (typeof mediaQueryList.addEventListener === 'function') {
    mediaQueryList.addEventListener('change', mediaQueryListener)
  } else {
    mediaQueryList.addListener(mediaQueryListener)
  }
}

const focusSearchInput = async () => {
  if (!props.searchable) {
    return
  }

  await nextTick()
  activeSearchRef()?.focus()
}

const scrollHighlightedIntoView = async () => {
  await nextTick()
  const panel = activePanelRef()
  if (!panel || highlightedIndex.value < 0) {
    return
  }

  const option = panel.querySelector<HTMLElement>(`[data-option-index="${highlightedIndex.value}"]`)
  option?.scrollIntoView({ block: 'nearest' })
}

const syncHighlightedIndex = () => {
  if (!filteredOptions.value.length) {
    highlightedIndex.value = -1
    return
  }

  const selectedIndex = filteredOptions.value.findIndex((option) =>
    selectedValueKeySet.value.has(optionKey(option.value))
  )

  highlightedIndex.value = selectedIndex >= 0 ? selectedIndex : 0
}

const closePanel = () => {
  if (!open.value) {
    return
  }

  open.value = false
  search.value = ''
  highlightedIndex.value = -1
  emit('close')
}

const openPanel = async () => {
  if (props.disabled) {
    return
  }

  search.value = ''
  open.value = true
  syncHighlightedIndex()
  emit('open')

  await focusSearchInput()
  await scrollHighlightedIntoView()
}

const toggleOpen = () => {
  if (props.disabled) {
    return
  }

  if (open.value) {
    closePanel()
    return
  }

  void openPanel()
}

const removeValue = (value: AppComboboxValue) => {
  if (props.disabled) {
    return
  }

  emit(
    'update:modelValue',
    props.modelValue.filter((entry) => entry !== value)
  )
}

const toggleOption = (value: AppComboboxValue) => {
  if (props.disabled) {
    return
  }

  if (selectedValueKeySet.value.has(optionKey(value))) {
    removeValue(value)
    return
  }

  emit('update:modelValue', [...props.modelValue, value])
}

const toggleHighlighted = () => {
  const option = filteredOptions.value[highlightedIndex.value]
  if (option) {
    toggleOption(option.value)
  }
}

const moveHighlight = async (direction: 1 | -1) => {
  if (!filteredOptions.value.length) {
    return
  }

  if (highlightedIndex.value < 0) {
    highlightedIndex.value = direction > 0 ? 0 : filteredOptions.value.length - 1
  } else {
    highlightedIndex.value =
      (highlightedIndex.value + direction + filteredOptions.value.length) % filteredOptions.value.length
  }

  await scrollHighlightedIntoView()
}

const handleTriggerKeydown = async (event: KeyboardEvent) => {
  if (props.disabled) {
    return
  }

  if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
    event.preventDefault()
    if (!open.value) {
      await openPanel()
    }
    await moveHighlight(event.key === 'ArrowDown' ? 1 : -1)
    return
  }

  if (!props.searchable && open.value && (event.key === 'Enter' || event.key === ' ')) {
    event.preventDefault()
    toggleHighlighted()
    return
  }

  if (event.key === 'Enter' || event.key === ' ') {
    event.preventDefault()
    toggleOpen()
  }
}

const handleSearchKeydown = async (event: KeyboardEvent) => {
  if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
    event.preventDefault()
    await moveHighlight(event.key === 'ArrowDown' ? 1 : -1)
    return
  }

  if (event.key === 'Enter') {
    event.preventDefault()
    toggleHighlighted()
    return
  }

  if (event.key === 'Escape') {
    event.preventDefault()
    closePanel()
    return
  }

  if (event.key === 'Tab') {
    closePanel()
  }
}

const handleOutsidePointer = (event: MouseEvent | TouchEvent) => {
  if (!desktopOpen.value) {
    return
  }

  const target = event.target
  if (target instanceof Node && rootRef.value?.contains(target)) {
    return
  }

  closePanel()
}

const handleDocumentKeydown = (event: KeyboardEvent) => {
  if (!open.value || event.key !== 'Escape') {
    return
  }

  closePanel()
}

watch(filteredOptions, () => {
  syncHighlightedIndex()
})

watch(
  () => props.modelValue,
  () => {
    if (open.value) {
      syncHighlightedIndex()
      void scrollHighlightedIntoView()
    }
  }
)

watch(
  () => breakpointQuery.value,
  () => {
    syncMobileMode()
  }
)

onMounted(() => {
  syncMobileMode()
  document.addEventListener('mousedown', handleOutsidePointer)
  document.addEventListener('touchstart', handleOutsidePointer)
  document.addEventListener('keydown', handleDocumentKeydown)
})

onUnmounted(() => {
  document.removeEventListener('mousedown', handleOutsidePointer)
  document.removeEventListener('touchstart', handleOutsidePointer)
  document.removeEventListener('keydown', handleDocumentKeydown)

  if (mediaQueryList && mediaQueryListener) {
    if (typeof mediaQueryList.removeEventListener === 'function') {
      mediaQueryList.removeEventListener('change', mediaQueryListener)
    } else {
      mediaQueryList.removeListener(mediaQueryListener)
    }
  }
})
</script>

<template>
  <div ref="rootRef" class="relative">
    <div
      class="app-multicombobox-trigger"
      :class="{
        'app-multicombobox-trigger-invalid': invalid,
        'app-multicombobox-trigger-open': open,
        'app-multicombobox-trigger-compact': compact,
        'cursor-not-allowed opacity-60': disabled,
      }"
      role="combobox"
      :aria-label="ariaLabel || placeholder"
      :aria-expanded="open"
      :aria-disabled="disabled"
      :tabindex="disabled ? -1 : 0"
      @click="toggleOpen"
      @keydown="handleTriggerKeydown"
    >
      <div class="min-w-0 flex-1">
        <div v-if="selectedOptions.length > 0" class="flex min-w-0 items-center gap-1.5 overflow-hidden">
          <span
            v-for="option in visibleChipOptions"
            :key="optionKey(option.value)"
            class="app-multicombobox-chip"
          >
            <span class="truncate">{{ option.label }}</span>
            <button
              class="app-multicombobox-chip-remove"
              type="button"
              :disabled="disabled"
              :aria-label="removeChipLabel ? `${removeChipLabel}: ${option.label}` : option.label"
              @click.stop="removeValue(option.value)"
            >
              <svg class="h-3 w-3" viewBox="0 0 20 20" fill="none" aria-hidden="true">
                <path d="M6 6 14 14M14 6 6 14" stroke="currentColor" stroke-linecap="round" stroke-linejoin="round" stroke-width="1.75" />
              </svg>
            </button>
          </span>
          <span v-if="overflowChipCount > 0" class="app-multicombobox-chip shrink-0">
            +{{ overflowChipCount }}
          </span>
        </div>
        <span v-else class="block truncate text-sm text-slate-400">{{ placeholder }}</span>
      </div>
      <svg
        class="h-4 w-4 shrink-0 text-slate-400 transition-transform duration-200"
        :class="open ? 'rotate-180' : ''"
        viewBox="0 0 20 20"
        fill="none"
        aria-hidden="true"
      >
        <path d="M5 7.5 10 12.5 15 7.5" stroke="currentColor" stroke-linecap="round" stroke-linejoin="round" stroke-width="1.75" />
      </svg>
    </div>

    <Transition name="app-menu">
      <div
        v-if="desktopOpen"
        ref="desktopPanelRef"
        class="app-multicombobox-panel absolute left-0 right-0 top-[calc(100%+0.5rem)] z-30"
      >
        <div v-if="searchable" class="border-b border-slate-100 px-3 py-3">
          <label class="relative block">
            <svg class="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" viewBox="0 0 20 20" fill="none" aria-hidden="true">
              <path d="m14.5 14.5-3.2-3.2m1.7-4.05a5.75 5.75 0 1 1-11.5 0 5.75 5.75 0 0 1 11.5 0Z" stroke="currentColor" stroke-linecap="round" stroke-linejoin="round" stroke-width="1.75" />
            </svg>
            <input
              ref="desktopSearchRef"
              v-model="search"
              class="app-combobox-search"
              :placeholder="searchPlaceholder"
              type="text"
              @keydown="handleSearchKeydown"
            />
          </label>
        </div>

        <div class="max-h-80 overflow-y-auto px-2 py-2">
          <div v-if="filteredOptions.length === 0" class="px-3 py-6 text-sm text-slate-500">{{ emptyLabel }}</div>
          <div v-else class="space-y-1">
            <template v-for="group in renderGroups" :key="group.key">
              <div v-if="group.label" class="app-combobox-section-label" role="presentation">{{ group.label }}</div>
              <button
                v-for="entry in group.options"
                :key="optionKey(entry.option.value)"
                class="app-multicombobox-option"
                :class="{
                  'app-multicombobox-option-active': highlightedIndex === entry.index,
                  'app-multicombobox-option-selected': selectedValueKeySet.has(optionKey(entry.option.value)),
                  'app-multicombobox-option-compact': compact,
                }"
                type="button"
                role="option"
                :aria-selected="selectedValueKeySet.has(optionKey(entry.option.value))"
                :data-option-index="entry.index"
                @mouseenter="highlightedIndex = entry.index"
                @click="toggleOption(entry.option.value)"
              >
                <span class="min-w-0 flex-1 text-left">
                  <span class="block truncate text-sm font-medium text-slate-900">{{ entry.option.label }}</span>
                  <span v-if="entry.option.description" class="mt-0.5 block truncate text-xs text-slate-500">{{ entry.option.description }}</span>
                </span>
                <svg
                  v-if="selectedValueKeySet.has(optionKey(entry.option.value))"
                  class="h-4 w-4 shrink-0 text-slate-900"
                  viewBox="0 0 20 20"
                  fill="none"
                  aria-hidden="true"
                >
                  <path d="m5 10 3.25 3.25L15 6.5" stroke="currentColor" stroke-linecap="round" stroke-linejoin="round" stroke-width="1.75" />
                </svg>
              </button>
            </template>
          </div>
        </div>
      </div>
    </Transition>

    <AppDrawerShell
      :open="mobileOpen"
      desktop-breakpoint="md"
      desktop-width="md"
      labelled-by="app-multicombobox-mobile-title"
      @close="closePanel"
    >
      <template #default="{ panelClass, labelledBy }">
        <section ref="mobilePanelRef" :class="[panelClass, 'overflow-hidden']" role="dialog" aria-modal="true" :aria-labelledby="labelledBy">
          <div class="border-b border-slate-200 px-4 py-4 sm:px-6">
            <div class="flex items-start justify-between gap-4">
              <div>
                <div class="text-xs font-semibold uppercase tracking-[0.2em] text-slate-500">{{ ariaLabel }}</div>
                <h2 :id="labelledBy" class="mt-1 text-lg font-semibold text-slate-900">{{ panelTitle }}</h2>
              </div>
              <button
                class="rounded-full border border-slate-200 px-3 py-2 text-xs font-medium text-slate-700 transition hover:border-slate-300 hover:bg-slate-50"
                type="button"
                @click="closePanel"
              >
                {{ t('common.close') }}
              </button>
            </div>
          </div>

          <div class="min-h-0 flex-1 overflow-y-auto px-4 py-4 sm:px-6">
            <label v-if="searchable" class="relative block">
              <svg class="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" viewBox="0 0 20 20" fill="none" aria-hidden="true">
                <path d="m14.5 14.5-3.2-3.2m1.7-4.05a5.75 5.75 0 1 1-11.5 0 5.75 5.75 0 0 1 11.5 0Z" stroke="currentColor" stroke-linecap="round" stroke-linejoin="round" stroke-width="1.75" />
              </svg>
              <input
                ref="mobileSearchRef"
                v-model="search"
                class="app-combobox-search"
                :placeholder="searchPlaceholder"
                type="text"
                @keydown="handleSearchKeydown"
              />
            </label>

            <div class="mt-4 space-y-4">
              <div v-if="selectedOptions.length > 0" class="flex flex-wrap gap-2">
                <span
                  v-for="option in selectedOptions"
                  :key="optionKey(option.value)"
                  class="app-multicombobox-chip"
                >
                  <span class="truncate">{{ option.label }}</span>
                </span>
              </div>

              <div
                v-if="filteredOptions.length === 0"
                class="rounded-2xl border border-dashed border-slate-200 bg-slate-50 px-4 py-6 text-sm text-slate-500"
              >
                {{ emptyLabel }}
              </div>
              <div v-else class="space-y-2">
                <template v-for="group in renderGroups" :key="group.key">
                  <div v-if="group.label" class="app-combobox-section-label" role="presentation">{{ group.label }}</div>
                  <button
                    v-for="entry in group.options"
                    :key="optionKey(entry.option.value)"
                    class="app-multicombobox-option"
                    :class="{
                      'app-multicombobox-option-active': highlightedIndex === entry.index,
                      'app-multicombobox-option-selected': selectedValueKeySet.has(optionKey(entry.option.value)),
                      'app-multicombobox-option-compact': compact,
                    }"
                    type="button"
                    role="option"
                    :aria-selected="selectedValueKeySet.has(optionKey(entry.option.value))"
                    :data-option-index="entry.index"
                    @click="toggleOption(entry.option.value)"
                  >
                    <span class="min-w-0 flex-1 text-left">
                      <span class="block truncate text-sm font-medium text-slate-900">{{ entry.option.label }}</span>
                      <span v-if="entry.option.description" class="mt-0.5 block truncate text-xs text-slate-500">{{ entry.option.description }}</span>
                    </span>
                    <svg
                      v-if="selectedValueKeySet.has(optionKey(entry.option.value))"
                      class="h-4 w-4 shrink-0 text-slate-900"
                      viewBox="0 0 20 20"
                      fill="none"
                      aria-hidden="true"
                    >
                      <path d="m5 10 3.25 3.25L15 6.5" stroke="currentColor" stroke-linecap="round" stroke-linejoin="round" stroke-width="1.75" />
                    </svg>
                  </button>
                </template>
              </div>
            </div>
          </div>
        </section>
      </template>
    </AppDrawerShell>
  </div>
</template>
