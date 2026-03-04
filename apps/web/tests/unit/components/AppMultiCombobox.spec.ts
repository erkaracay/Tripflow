import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import AppMultiCombobox from '../../../src/components/ui/AppMultiCombobox.vue'
import { i18n, setLocale } from '../../../src/i18n'
import type { AppComboboxOption } from '../../../src/types'

const options: AppComboboxOption[] = [
  {
    value: 'guide-1',
    label: 'Ayse Demir',
    description: 'ayse@example.com',
    keywords: ['ayse', 'demir'],
  },
  {
    value: 'guide-2',
    label: 'Mert Kaya',
    description: 'mert@example.com',
    keywords: ['mert', 'kaya'],
  },
  {
    value: 'guide-3',
    label: 'Ceren Yildiz',
    description: 'ceren@example.com',
    keywords: ['ceren', 'yildiz'],
  },
]

const groupedOptions: AppComboboxOption[] = [
  {
    value: 'guide-1',
    label: 'Ayse Demir',
    groupLabel: 'Day 1',
    keywords: ['ayse'],
  },
  {
    value: 'guide-2',
    label: 'Mert Kaya',
    groupLabel: 'Day 1',
    keywords: ['mert'],
  },
  {
    value: 'guide-3',
    label: 'Ceren Yildiz',
    groupLabel: 'Day 2',
    keywords: ['ceren'],
  },
]

const originalMatchMedia = window.matchMedia

const mockMatchMedia = (matches: boolean) => {
  window.matchMedia = vi.fn().mockImplementation(() => ({
    matches,
    media: '(max-width: 767px)',
    addEventListener: vi.fn(),
    removeEventListener: vi.fn(),
    addListener: vi.fn(),
    removeListener: vi.fn(),
    onchange: null,
    dispatchEvent: vi.fn(),
  })) as typeof window.matchMedia
}

const createPointerEvent = (type: string, clientY: number, pointerId = 1, pointerType = 'touch') => {
  const event = new Event(type, { bubbles: true, cancelable: true }) as PointerEvent
  Object.defineProperties(event, {
    clientY: { value: clientY },
    pointerId: { value: pointerId },
    pointerType: { value: pointerType },
  })
  return event
}

describe('AppMultiCombobox', () => {
  beforeEach(() => {
    setLocale('tr')
    mockMatchMedia(false)
  })

  afterEach(() => {
    window.matchMedia = originalMatchMedia
  })

  it('shows the placeholder while no value is selected', () => {
    const wrapper = mount(AppMultiCombobox, {
      props: {
        modelValue: [],
        options,
        placeholder: 'Rehber seç',
      },
      global: {
        plugins: [i18n],
      },
    })

    expect(wrapper.get('.app-multicombobox-trigger').text()).toContain('Rehber seç')
  })

  it('adds selections in order and toggles them off on repeated click', async () => {
    const wrapper = mount(AppMultiCombobox, {
      props: {
        modelValue: [],
        options,
        placeholder: 'Rehber seç',
        searchPlaceholder: 'Ara',
      },
      global: {
        plugins: [i18n],
      },
    })

    await wrapper.get('.app-multicombobox-trigger').trigger('click')
    await wrapper.findAll('.app-multicombobox-option')[0]!.trigger('click')
    expect(wrapper.emitted('update:modelValue')?.[0]).toEqual([['guide-1']])

    await wrapper.setProps({ modelValue: ['guide-1'] })
    await wrapper.findAll('.app-multicombobox-option')[1]!.trigger('click')
    expect(wrapper.emitted('update:modelValue')?.[1]).toEqual([['guide-1', 'guide-2']])

    await wrapper.setProps({ modelValue: ['guide-1', 'guide-2'] })
    await wrapper.findAll('.app-multicombobox-option')[0]!.trigger('click')
    expect(wrapper.emitted('update:modelValue')?.[2]).toEqual([['guide-2']])
  })

  it('renders selected chips and shows overflow count after maxVisibleChips', () => {
    const wrapper = mount(AppMultiCombobox, {
      props: {
        modelValue: ['guide-1', 'guide-2', 'guide-3'],
        options,
        placeholder: 'Rehber seç',
        maxVisibleChips: 2,
      },
      global: {
        plugins: [i18n],
      },
    })

    const chips = wrapper.findAll('.app-multicombobox-chip')
    expect(chips).toHaveLength(3)
    expect(chips[0]?.text()).toContain('Ayse Demir')
    expect(chips[1]?.text()).toContain('Mert Kaya')
    expect(chips[2]?.text()).toContain('+1')
  })

  it('removes a selected chip without opening the panel', async () => {
    const wrapper = mount(AppMultiCombobox, {
      props: {
        modelValue: ['guide-1', 'guide-2'],
        options,
        placeholder: 'Rehber seç',
        removeChipLabel: 'Seçimi kaldır',
      },
      global: {
        plugins: [i18n],
      },
    })

    await wrapper.findAll('.app-multicombobox-chip-remove')[0]!.trigger('click')

    expect(wrapper.emitted('update:modelValue')?.[0]).toEqual([['guide-2']])
    expect(wrapper.find('.app-multicombobox-panel').exists()).toBe(false)
  })

  it('renders grouped options and keeps matching group labels while searching', async () => {
    const wrapper = mount(AppMultiCombobox, {
      props: {
        modelValue: [],
        options: groupedOptions,
        placeholder: 'Rehber seç',
        searchPlaceholder: 'Ara',
        emptyLabel: 'Boş',
      },
      global: {
        plugins: [i18n],
      },
    })

    await wrapper.get('.app-multicombobox-trigger').trigger('click')
    expect(wrapper.text()).toContain('Day 1')
    expect(wrapper.text()).toContain('Day 2')

    await wrapper.get('.app-combobox-search').setValue('ceren')

    const labels = wrapper.findAll('.app-combobox-section-label').map((node) => node.text())
    expect(labels).toContain('Day 2')
    expect(labels).not.toContain('Day 1')
  })

  it('opens inside the mobile drawer when the viewport is mobile', async () => {
    mockMatchMedia(true)

    const wrapper = mount(AppMultiCombobox, {
      props: {
        modelValue: [],
        options,
        placeholder: 'Rehber seç',
        ariaLabel: 'Rehber seç',
      },
      attachTo: document.body,
      global: {
        plugins: [i18n],
      },
    })

    await wrapper.get('.app-multicombobox-trigger').trigger('click')

    expect(document.body.textContent).toContain('Rehber seç')
    expect(document.body.textContent).toContain('Ayse Demir')
  })

  it('supports Home and End key navigation while open', async () => {
    const wrapper = mount(AppMultiCombobox, {
      props: {
        modelValue: [],
        options,
        placeholder: 'Rehber seç',
        searchPlaceholder: 'Ara',
      },
      global: {
        plugins: [i18n],
      },
    })

    const trigger = wrapper.get('.app-multicombobox-trigger')
    await trigger.trigger('click')
    const searchInput = wrapper.get('.app-combobox-search')
    await searchInput.trigger('keydown', { key: 'End' })
    await searchInput.trigger('keydown', { key: 'Enter' })
    expect(wrapper.emitted('update:modelValue')?.[0]).toEqual([['guide-3']])

    await wrapper.setProps({ modelValue: [] })
    await searchInput.trigger('keydown', { key: 'Home' })
    await searchInput.trigger('keydown', { key: 'Enter' })
    expect(wrapper.emitted('update:modelValue')?.[1]).toEqual([['guide-1']])
  })

  it('restores focus to trigger when closed with Escape', async () => {
    const wrapper = mount(AppMultiCombobox, {
      props: {
        modelValue: [],
        options,
        placeholder: 'Rehber seç',
        searchPlaceholder: 'Ara',
      },
      attachTo: document.body,
      global: {
        plugins: [i18n],
      },
    })

    const trigger = wrapper.get('.app-multicombobox-trigger')
    await trigger.trigger('click')
    await wrapper.get('.app-combobox-search').trigger('keydown', { key: 'Escape' })

    expect((document.activeElement as HTMLElement | null)?.classList.contains('app-multicombobox-trigger')).toBe(true)
    expect(wrapper.emitted('close')).toBeTruthy()
  })

  it('closes mobile drawer after a swipe from the handle without mutating selection', async () => {
    mockMatchMedia(true)

    const wrapper = mount(AppMultiCombobox, {
      props: {
        modelValue: ['guide-1'],
        options,
        placeholder: 'Rehber seç',
      },
      attachTo: document.body,
      global: {
        plugins: [i18n],
      },
    })

    await wrapper.get('.app-multicombobox-trigger').trigger('click')
    const handle = document.querySelector('[data-drawer-swipe-handle]')
    expect(handle).toBeTruthy()

    handle!.dispatchEvent(createPointerEvent('pointerdown', 0))
    window.dispatchEvent(createPointerEvent('pointermove', 120))
    window.dispatchEvent(createPointerEvent('pointerup', 120))

    await wrapper.vm.$nextTick()

    expect(wrapper.emitted('close')).toBeTruthy()
    expect(wrapper.emitted('update:modelValue')).toBeUndefined()
  })

  it('does not open or remove selections while disabled', async () => {
    const wrapper = mount(AppMultiCombobox, {
      props: {
        modelValue: ['guide-1'],
        options,
        placeholder: 'Rehber seç',
        disabled: true,
        removeChipLabel: 'Seçimi kaldır',
      },
      global: {
        plugins: [i18n],
      },
    })

    await wrapper.get('.app-multicombobox-trigger').trigger('click')
    await wrapper.findAll('.app-multicombobox-chip-remove')[0]!.trigger('click')

    expect(wrapper.find('.app-multicombobox-panel').exists()).toBe(false)
    expect(wrapper.emitted('update:modelValue')).toBeUndefined()
  })
})
