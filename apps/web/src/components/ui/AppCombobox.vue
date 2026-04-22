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
    modelValue: AppComboboxValue | null
    options: AppComboboxOption[]
    placeholder?: string
    searchPlaceholder?: string
    emptyLabel?: string
    browseAllLabel?: string
    recommendedLabel?: string
    allLabel?: string
    invalid?: boolean
    disabled?: boolean
    ariaLabel?: string
    mobileBreakpoint?: 'md' | 'lg'
    recommendedValues?: AppComboboxValue[]
    optionCountForInlineList?: number
    searchable?: boolean
    compact?: boolean
    teleportPanel?: boolean
  }>(),
  {
    placeholder: '',
    searchPlaceholder: '',
    emptyLabel: '',
    browseAllLabel: '',
    recommendedLabel: '',
    allLabel: '',
    invalid: false,
    disabled: false,
    ariaLabel: '',
    mobileBreakpoint: 'md',
    recommendedValues: () => [],
    optionCountForInlineList: 8,
    searchable: true,
    compact: false,
    teleportPanel: false,
  }
)

const emit = defineEmits<{
  (event: 'update:modelValue', value: AppComboboxValue): void
  (event: 'open'): void
  (event: 'close'): void
}>()

const { t } = useI18n()

const rootRef = ref<HTMLElement | null>(null)
const triggerRef = ref<HTMLButtonElement | null>(null)
const desktopPanelRef = ref<HTMLElement | null>(null)
const mobilePanelRef = ref<HTMLElement | null>(null)
const desktopSearchRef = ref<HTMLInputElement | null>(null)
const mobileSearchRef = ref<HTMLInputElement | null>(null)
const open = ref(false)
const search = ref('')
const showAll = ref(false)
const highlightedIndex = ref(-1)
const isMobile = ref(false)

let mediaQueryList: MediaQueryList | null = null
let mediaQueryListener:
  | ((event: MediaQueryListEvent) => void)
  | null = null

const instanceId = `app-combobox-${Math.random().toString(36).slice(2, 10)}`
const desktopListId = `${instanceId}-desktop-listbox`
const mobileListId = `${instanceId}-mobile-listbox`

const breakpointQuery = computed(() =>
  props.mobileBreakpoint === 'lg' ? '(max-width: 1023px)' : '(max-width: 767px)'
)

const normalizedSearch = computed(() => search.value.trim().toLowerCase())
const recommendedValueSet = computed(() => new Set(props.recommendedValues))

const hasValue = (value: AppComboboxValue | null | undefined) => value !== null && value !== undefined && value !== ''
const optionKey = (value: AppComboboxValue) => `${typeof value}:${String(value)}`

const selectedOption = computed<AppComboboxOption | null>(() => {
  if (props.modelValue === null || props.modelValue === undefined || props.modelValue === '') {
    return null
  }

  const selectedValue = props.modelValue

  return (
    props.options.find((option) => option.value === selectedValue) ?? {
      value: selectedValue,
      label: String(selectedValue),
      description: null,
    }
  )
})

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

const recommendedAvailableOptions = computed(() =>
  filteredOptions.value.filter((option) => recommendedValueSet.value.has(option.value))
)

const inlineOptions = computed(() => {
  const preferredOptions =
    recommendedAvailableOptions.value.length > 0 ? recommendedAvailableOptions.value : filteredOptions.value

  return preferredOptions.slice(0, props.optionCountForInlineList)
})

const remainingOptions = computed(() => {
  const inlineValues = new Set(inlineOptions.value.map((option) => option.value))
  return filteredOptions.value.filter((option) => !inlineValues.has(option.value))
})

const visibleOptions = computed(() => {
  if (normalizedSearch.value) {
    return filteredOptions.value
  }

  if (showAll.value) {
    return [...inlineOptions.value, ...remainingOptions.value]
  }

  return inlineOptions.value
})

const buildRenderGroups = (options: AppComboboxOption[], startIndex = 0): ComboboxRenderGroup[] => {
  const groups: ComboboxRenderGroup[] = []
  const groupIndexByKey = new Map<string, number>()

  options.forEach((option, offset) => {
    const label = option.groupLabel?.trim() || null
    const key = label ? `group:${label}` : 'group:__ungrouped__'
    const existingIndex = groupIndexByKey.get(key)
    const entry = {
      option,
      index: startIndex + offset,
    }

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

const filteredRenderGroups = computed(() => buildRenderGroups(filteredOptions.value))
const inlineRenderGroups = computed(() => buildRenderGroups(inlineOptions.value))
const remainingRenderGroups = computed(() => buildRenderGroups(remainingOptions.value, inlineOptions.value.length))

const hasBrowseAll = computed(() => !normalizedSearch.value && remainingOptions.value.length > 0)
const showRecommendedSection = computed(() => !normalizedSearch.value && recommendedAvailableOptions.value.length > 0)
const showAllSection = computed(() => !normalizedSearch.value && showAll.value && remainingOptions.value.length > 0)
const desktopOpen = computed(() => open.value && !isMobile.value)
const mobileOpen = computed(() => open.value && isMobile.value)
const panelTitle = computed(() => selectedOption.value?.label || props.ariaLabel || props.placeholder || '')
const activeListId = computed(() => (isMobile.value ? mobileListId : desktopListId))
const activeOptionId = computed(() => {
  const option = visibleOptions.value[highlightedIndex.value]
  if (!option) {
    return null
  }
  return optionDomId(option.value)
})

const activePanelRef = () => (isMobile.value ? mobilePanelRef.value : desktopPanelRef.value)
const activeSearchRef = () => (isMobile.value ? mobileSearchRef.value : desktopSearchRef.value)
const optionDomId = (value: AppComboboxValue) => `${instanceId}-option-${optionKey(value).replace(/[^a-zA-Z0-9_-]/g, '-')}`

const syncMobileMode = () => {
  if (typeof window === 'undefined' || typeof window.matchMedia !== 'function') {
    isMobile.value = false
    return
  }

  const query = breakpointQuery.value
  const nextQueryList = window.matchMedia(query)
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
  if (!visibleOptions.value.length) {
    highlightedIndex.value = -1
    return
  }

  const selectedIndex = hasValue(props.modelValue)
    ? visibleOptions.value.findIndex((option) => option.value === props.modelValue)
    : -1

  highlightedIndex.value = selectedIndex >= 0 ? selectedIndex : 0
}

const resetPanelState = () => {
  search.value = ''
  showAll.value = false
  highlightedIndex.value = -1
}

const focusTrigger = async () => {
  await nextTick()
  triggerRef.value?.focus()
}

const closePanel = (restoreFocus = false) => {
  if (!open.value) {
    return
  }

  open.value = false
  resetPanelState()
  emit('close')

  if (restoreFocus) {
    void focusTrigger()
  }
}

const closePanelAndRestoreFocus = () => closePanel(true)

const openPanel = async () => {
  if (props.disabled) {
    return
  }

  const selectedInline = hasValue(props.modelValue)
    ? inlineOptions.value.some((option) => option.value === props.modelValue)
    : false

  showAll.value = Boolean(
    hasValue(props.modelValue)
      && !selectedInline
      && filteredOptions.value.some((option) => option.value === props.modelValue)
  )
  search.value = ''
  open.value = true
  syncHighlightedIndex()
  emit('open')

  await focusSearchInput()
  await scrollHighlightedIntoView()
}

const toggleOpen = () => {
  if (open.value) {
    closePanel()
    return
  }

  void openPanel()
}

const selectOption = (option: AppComboboxOption) => {
  emit('update:modelValue', option.value)
  closePanel()
}

const moveHighlight = async (direction: 1 | -1) => {
  if (!visibleOptions.value.length) {
    return
  }

  if (highlightedIndex.value < 0) {
    highlightedIndex.value = direction > 0 ? 0 : visibleOptions.value.length - 1
  } else {
    highlightedIndex.value =
      (highlightedIndex.value + direction + visibleOptions.value.length) % visibleOptions.value.length
  }

  await scrollHighlightedIntoView()
}

const moveHighlightToBoundary = async (boundary: 'start' | 'end') => {
  if (!visibleOptions.value.length) {
    return
  }
  highlightedIndex.value = boundary === 'start' ? 0 : visibleOptions.value.length - 1
  await scrollHighlightedIntoView()
}

const selectHighlighted = () => {
  const option = visibleOptions.value[highlightedIndex.value]
  if (option) {
    selectOption(option)
  }
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

  if (event.key === 'Home' || event.key === 'End') {
    event.preventDefault()
    if (!open.value) {
      await openPanel()
    }
    await moveHighlightToBoundary(event.key === 'Home' ? 'start' : 'end')
    return
  }

  if (!props.searchable && open.value && (event.key === 'Enter' || event.key === ' ')) {
    event.preventDefault()
    selectHighlighted()
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

  if (event.key === 'Home' || event.key === 'End') {
    event.preventDefault()
    await moveHighlightToBoundary(event.key === 'Home' ? 'start' : 'end')
    return
  }

  if (event.key === 'Enter') {
    event.preventDefault()
    selectHighlighted()
    return
  }

  if (event.key === 'Escape') {
    event.preventDefault()
    closePanel(true)
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
  if (target instanceof Node) {
    if (rootRef.value?.contains(target)) {
      return
    }
    if (props.teleportPanel && desktopPanelRef.value?.contains(target)) {
      return
    }
  }

  closePanel(true)
}

const handleDocumentKeydown = (event: KeyboardEvent) => {
  if (!open.value || event.key !== 'Escape') {
    return
  }

  closePanel(true)
}

const revealAllOptions = async () => {
  showAll.value = true
  syncHighlightedIndex()
  await focusSearchInput()
  await scrollHighlightedIntoView()
}

const teleportPanelStyle = ref<Record<string, string>>({})

const updateTeleportPanelPosition = () => {
  if (!props.teleportPanel) return
  const trigger = triggerRef.value
  if (!trigger) return
  const rect = trigger.getBoundingClientRect()
  teleportPanelStyle.value = {
    position: 'fixed',
    left: `${rect.left}px`,
    top: `${rect.bottom + 8}px`,
    width: `${rect.width}px`,
  }
}

watch(
  () => desktopOpen.value,
  async (isOpen) => {
    if (!props.teleportPanel) return
    if (isOpen) {
      await nextTick()
      updateTeleportPanelPosition()
    }
  }
)

const handleViewportChange = () => {
  if (desktopOpen.value) {
    updateTeleportPanelPosition()
  }
}

watch(visibleOptions, () => {
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
  if (props.teleportPanel) {
    window.addEventListener('scroll', handleViewportChange, true)
    window.addEventListener('resize', handleViewportChange)
  }
})

onUnmounted(() => {
  document.removeEventListener('mousedown', handleOutsidePointer)
  document.removeEventListener('touchstart', handleOutsidePointer)
  document.removeEventListener('keydown', handleDocumentKeydown)
  if (props.teleportPanel) {
    window.removeEventListener('scroll', handleViewportChange, true)
    window.removeEventListener('resize', handleViewportChange)
  }

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
    <button
      ref="triggerRef"
      class="app-combobox-trigger"
      :class="{
        'app-combobox-trigger-invalid': invalid,
        'app-combobox-trigger-open': open,
        'app-combobox-trigger-compact': compact,
        'cursor-not-allowed opacity-60': disabled,
      }"
      type="button"
      role="combobox"
      aria-haspopup="listbox"
      :aria-label="ariaLabel || placeholder"
      :aria-expanded="open"
      :aria-controls="open ? activeListId : undefined"
      :aria-activedescendant="open && activeOptionId ? activeOptionId : undefined"
      :disabled="disabled"
      @click="toggleOpen"
      @keydown="handleTriggerKeydown"
    >
      <span class="min-w-0 flex-1 text-left">
        <template v-if="selectedOption">
          <span class="block truncate text-sm font-medium text-slate-900">{{ selectedOption.label }}</span>
          <span v-if="selectedOption.description" class="block truncate text-xs text-slate-500">{{ selectedOption.description }}</span>
        </template>
        <span v-else class="block truncate text-sm text-slate-400">{{ placeholder }}</span>
      </span>
      <svg
        class="h-4 w-4 shrink-0 text-slate-400 transition-transform duration-200"
        :class="open ? 'rotate-180' : ''"
        viewBox="0 0 20 20"
        fill="none"
        aria-hidden="true"
      >
        <path d="M5 7.5 10 12.5 15 7.5" stroke="currentColor" stroke-linecap="round" stroke-linejoin="round" stroke-width="1.75" />
      </svg>
    </button>

    <Teleport to="body" :disabled="!teleportPanel">
    <Transition name="app-menu">
      <div
        v-if="desktopOpen"
        ref="desktopPanelRef"
        :class="[
          'app-combobox-panel',
          teleportPanel ? 'fixed z-[60]' : 'absolute left-0 right-0 top-[calc(100%+0.5rem)] z-30',
        ]"
        :style="teleportPanel ? teleportPanelStyle : undefined"
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
              role="combobox"
              aria-haspopup="listbox"
              :aria-label="searchPlaceholder || placeholder"
              :aria-controls="activeListId"
              :aria-activedescendant="activeOptionId || undefined"
              @keydown="handleSearchKeydown"
            />
          </label>
        </div>

        <div
          :id="desktopListId"
          class="max-h-80 overflow-y-auto px-2 py-2"
          role="listbox"
          :aria-label="ariaLabel || placeholder"
        >
          <template v-if="normalizedSearch">
            <div v-if="filteredOptions.length === 0" class="px-3 py-6 text-sm text-slate-500">{{ emptyLabel }}</div>
            <div v-else class="space-y-1">
              <template v-for="group in filteredRenderGroups" :key="group.key">
                <div v-if="group.label" class="app-combobox-section-label">{{ group.label }}</div>
                <button
                  v-for="entry in group.options"
                  :key="optionKey(entry.option.value)"
                  class="app-combobox-option"
                  :class="{
                    'app-combobox-option-active': highlightedIndex === entry.index,
                    'app-combobox-option-selected': entry.option.value === modelValue,
                    'app-combobox-option-compact': compact,
                  }"
                  type="button"
                  role="option"
                  :id="optionDomId(entry.option.value)"
                  :aria-selected="entry.option.value === modelValue"
                  :data-option-index="entry.index"
                  @mouseenter="highlightedIndex = entry.index"
                  @click="selectOption(entry.option)"
                >
                  <span class="min-w-0 flex-1 text-left">
                    <span class="block truncate text-sm font-medium text-slate-900">{{ entry.option.label }}</span>
                    <span v-if="entry.option.description" class="mt-0.5 block truncate text-xs text-slate-500">{{ entry.option.description }}</span>
                  </span>
                  <svg v-if="entry.option.value === modelValue" class="h-4 w-4 shrink-0 text-slate-900" viewBox="0 0 20 20" fill="none" aria-hidden="true">
                    <path d="m5 10 3.25 3.25L15 6.5" stroke="currentColor" stroke-linecap="round" stroke-linejoin="round" stroke-width="1.75" />
                  </svg>
                </button>
              </template>
            </div>
          </template>

          <template v-else>
            <div v-if="inlineOptions.length === 0" class="px-3 py-6 text-sm text-slate-500">{{ emptyLabel }}</div>

            <template v-else>
              <div v-if="showRecommendedSection || !showAllSection" class="px-3 pb-2 pt-1 text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">
                {{ showRecommendedSection ? recommendedLabel : allLabel }}
              </div>
              <div class="space-y-1">
                <template v-for="group in inlineRenderGroups" :key="group.key">
                  <div v-if="group.label" class="app-combobox-section-label">{{ group.label }}</div>
                  <button
                    v-for="entry in group.options"
                    :key="optionKey(entry.option.value)"
                    class="app-combobox-option"
                    :class="{
                      'app-combobox-option-active': highlightedIndex === entry.index,
                      'app-combobox-option-selected': entry.option.value === modelValue,
                      'app-combobox-option-compact': compact,
                    }"
                    type="button"
                    role="option"
                    :id="optionDomId(entry.option.value)"
                    :aria-selected="entry.option.value === modelValue"
                    :data-option-index="entry.index"
                    @mouseenter="highlightedIndex = entry.index"
                    @click="selectOption(entry.option)"
                  >
                    <span class="min-w-0 flex-1 text-left">
                      <span class="block truncate text-sm font-medium text-slate-900">{{ entry.option.label }}</span>
                      <span v-if="entry.option.description" class="mt-0.5 block truncate text-xs text-slate-500">{{ entry.option.description }}</span>
                    </span>
                    <svg v-if="entry.option.value === modelValue" class="h-4 w-4 shrink-0 text-slate-900" viewBox="0 0 20 20" fill="none" aria-hidden="true">
                      <path d="m5 10 3.25 3.25L15 6.5" stroke="currentColor" stroke-linecap="round" stroke-linejoin="round" stroke-width="1.75" />
                    </svg>
                  </button>
                </template>
              </div>

              <button
                v-if="hasBrowseAll && !showAll"
                class="mt-2 w-full rounded-xl border border-dashed border-slate-200 px-3 py-2 text-sm font-medium text-slate-600 transition hover:border-slate-300 hover:bg-slate-50"
                type="button"
                @click="revealAllOptions"
              >
                {{ browseAllLabel }}
              </button>

              <template v-if="showAllSection">
                <div class="px-3 pb-2 pt-4 text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">{{ allLabel }}</div>
                <div class="space-y-1">
                  <template v-for="group in remainingRenderGroups" :key="group.key">
                    <div v-if="group.label" class="app-combobox-section-label">{{ group.label }}</div>
                    <button
                      v-for="entry in group.options"
                      :key="optionKey(entry.option.value)"
                      class="app-combobox-option"
                      :class="{
                        'app-combobox-option-active': highlightedIndex === entry.index,
                        'app-combobox-option-selected': entry.option.value === modelValue,
                        'app-combobox-option-compact': compact,
                      }"
                      type="button"
                      role="option"
                      :id="optionDomId(entry.option.value)"
                      :aria-selected="entry.option.value === modelValue"
                      :data-option-index="entry.index"
                      @mouseenter="highlightedIndex = entry.index"
                      @click="selectOption(entry.option)"
                    >
                      <span class="min-w-0 flex-1 text-left">
                        <span class="block truncate text-sm font-medium text-slate-900">{{ entry.option.label }}</span>
                        <span v-if="entry.option.description" class="mt-0.5 block truncate text-xs text-slate-500">{{ entry.option.description }}</span>
                      </span>
                      <svg v-if="entry.option.value === modelValue" class="h-4 w-4 shrink-0 text-slate-900" viewBox="0 0 20 20" fill="none" aria-hidden="true">
                        <path d="m5 10 3.25 3.25L15 6.5" stroke="currentColor" stroke-linecap="round" stroke-linejoin="round" stroke-width="1.75" />
                      </svg>
                    </button>
                  </template>
                </div>
              </template>
            </template>
          </template>
        </div>
      </div>
    </Transition>
    </Teleport>

    <AppDrawerShell
      :open="mobileOpen"
      desktop-breakpoint="md"
      desktop-width="md"
      labelled-by="app-combobox-mobile-title"
      :swipe-to-close="true"
      @close="closePanelAndRestoreFocus"
    >
      <template #default="{ panelClass, labelledBy }">
        <section ref="mobilePanelRef" :class="[panelClass, 'overflow-hidden']" role="dialog" aria-modal="true" :aria-labelledby="labelledBy">
          <div class="border-b border-slate-200 px-4 py-4 sm:px-6">
            <div class="app-drawer-swipe-handle-wrap" data-drawer-swipe-handle>
              <div class="app-drawer-swipe-handle" />
            </div>
            <div class="flex items-start justify-between gap-4">
              <div>
                <div class="text-xs font-semibold uppercase tracking-[0.2em] text-slate-500">{{ ariaLabel }}</div>
                <h2 :id="labelledBy" class="mt-1 text-lg font-semibold text-slate-900">{{ panelTitle }}</h2>
              </div>
              <button
                class="rounded-full border border-slate-200 px-3 py-2 text-xs font-medium text-slate-700 transition hover:border-slate-300 hover:bg-slate-50"
                type="button"
                @click="closePanelAndRestoreFocus"
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
                role="combobox"
                aria-haspopup="listbox"
                :aria-label="searchPlaceholder || placeholder"
                :aria-controls="activeListId"
                :aria-activedescendant="activeOptionId || undefined"
                @keydown="handleSearchKeydown"
              />
            </label>

            <div
              :id="mobileListId"
              class="mt-4 space-y-4"
              role="listbox"
              :aria-label="ariaLabel || placeholder"
            >
              <template v-if="normalizedSearch">
                <div v-if="filteredOptions.length === 0" class="rounded-2xl border border-dashed border-slate-200 bg-slate-50 px-4 py-6 text-sm text-slate-500">
                  {{ emptyLabel }}
                </div>
                <div v-else class="space-y-2">
                  <template v-for="group in filteredRenderGroups" :key="group.key">
                    <div v-if="group.label" class="app-combobox-section-label">{{ group.label }}</div>
                    <button
                      v-for="entry in group.options"
                      :key="optionKey(entry.option.value)"
                      class="app-combobox-option"
                      :class="{
                        'app-combobox-option-active': highlightedIndex === entry.index,
                        'app-combobox-option-selected': entry.option.value === modelValue,
                        'app-combobox-option-compact': compact,
                      }"
                      type="button"
                      role="option"
                      :id="optionDomId(entry.option.value)"
                      :aria-selected="entry.option.value === modelValue"
                      :data-option-index="entry.index"
                      @click="selectOption(entry.option)"
                    >
                      <span class="min-w-0 flex-1 text-left">
                        <span class="block truncate text-sm font-medium text-slate-900">{{ entry.option.label }}</span>
                        <span v-if="entry.option.description" class="mt-0.5 block truncate text-xs text-slate-500">{{ entry.option.description }}</span>
                      </span>
                      <svg v-if="entry.option.value === modelValue" class="h-4 w-4 shrink-0 text-slate-900" viewBox="0 0 20 20" fill="none" aria-hidden="true">
                        <path d="m5 10 3.25 3.25L15 6.5" stroke="currentColor" stroke-linecap="round" stroke-linejoin="round" stroke-width="1.75" />
                      </svg>
                    </button>
                  </template>
                </div>
              </template>

              <template v-else>
                <div v-if="inlineOptions.length === 0" class="rounded-2xl border border-dashed border-slate-200 bg-slate-50 px-4 py-6 text-sm text-slate-500">
                  {{ emptyLabel }}
                </div>

                <template v-else>
                  <section class="space-y-2">
                    <div class="app-combobox-section-label">{{ showRecommendedSection ? recommendedLabel : allLabel }}</div>
                    <div class="space-y-2">
                      <template v-for="group in inlineRenderGroups" :key="group.key">
                        <div v-if="group.label" class="app-combobox-section-label">{{ group.label }}</div>
                        <button
                          v-for="entry in group.options"
                          :key="optionKey(entry.option.value)"
                          class="app-combobox-option"
                          :class="{
                            'app-combobox-option-active': highlightedIndex === entry.index,
                            'app-combobox-option-selected': entry.option.value === modelValue,
                            'app-combobox-option-compact': compact,
                          }"
                          type="button"
                          role="option"
                          :id="optionDomId(entry.option.value)"
                          :aria-selected="entry.option.value === modelValue"
                          :data-option-index="entry.index"
                          @click="selectOption(entry.option)"
                        >
                          <span class="min-w-0 flex-1 text-left">
                            <span class="block truncate text-sm font-medium text-slate-900">{{ entry.option.label }}</span>
                            <span v-if="entry.option.description" class="mt-0.5 block truncate text-xs text-slate-500">{{ entry.option.description }}</span>
                          </span>
                          <svg v-if="entry.option.value === modelValue" class="h-4 w-4 shrink-0 text-slate-900" viewBox="0 0 20 20" fill="none" aria-hidden="true">
                            <path d="m5 10 3.25 3.25L15 6.5" stroke="currentColor" stroke-linecap="round" stroke-linejoin="round" stroke-width="1.75" />
                          </svg>
                        </button>
                      </template>
                    </div>
                  </section>

                  <button
                    v-if="hasBrowseAll && !showAll"
                    class="w-full rounded-2xl border border-dashed border-slate-200 bg-white px-4 py-3 text-sm font-medium text-slate-700 transition hover:border-slate-300 hover:bg-slate-50"
                    type="button"
                    @click="revealAllOptions"
                  >
                    {{ browseAllLabel }}
                  </button>

                  <section v-if="showAllSection" class="space-y-2">
                    <div class="app-combobox-section-label">{{ allLabel }}</div>
                    <div class="space-y-2">
                      <template v-for="group in remainingRenderGroups" :key="group.key">
                        <div v-if="group.label" class="app-combobox-section-label">{{ group.label }}</div>
                        <button
                          v-for="entry in group.options"
                          :key="optionKey(entry.option.value)"
                          class="app-combobox-option"
                          :class="{
                            'app-combobox-option-active': highlightedIndex === entry.index,
                            'app-combobox-option-selected': entry.option.value === modelValue,
                            'app-combobox-option-compact': compact,
                          }"
                          type="button"
                          role="option"
                          :id="optionDomId(entry.option.value)"
                          :aria-selected="entry.option.value === modelValue"
                          :data-option-index="entry.index"
                          @click="selectOption(entry.option)"
                        >
                          <span class="min-w-0 flex-1 text-left">
                            <span class="block truncate text-sm font-medium text-slate-900">{{ entry.option.label }}</span>
                            <span v-if="entry.option.description" class="mt-0.5 block truncate text-xs text-slate-500">{{ entry.option.description }}</span>
                          </span>
                          <svg v-if="entry.option.value === modelValue" class="h-4 w-4 shrink-0 text-slate-900" viewBox="0 0 20 20" fill="none" aria-hidden="true">
                            <path d="m5 10 3.25 3.25L15 6.5" stroke="currentColor" stroke-linecap="round" stroke-linejoin="round" stroke-width="1.75" />
                          </svg>
                        </button>
                      </template>
                    </div>
                  </section>
                </template>
              </template>
            </div>
          </div>
        </section>
      </template>
    </AppDrawerShell>
  </div>
</template>
