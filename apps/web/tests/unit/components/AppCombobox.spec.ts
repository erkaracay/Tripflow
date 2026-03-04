import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import AppCombobox from '../../../src/components/ui/AppCombobox.vue'
import { i18n, setLocale } from '../../../src/i18n'
import type { AppComboboxOption } from '../../../src/types'

const options: AppComboboxOption[] = [
  {
    value: 'Europe/Istanbul',
    label: 'Istanbul (UTC+03:00)',
    description: 'Europe/Istanbul',
    keywords: ['istanbul', 'utc+03:00'],
  },
  {
    value: 'Europe/London',
    label: 'London (UTC+00:00)',
    description: 'Europe/London',
    keywords: ['london', 'utc+00:00'],
  },
  {
    value: 'Asia/Dubai',
    label: 'Dubai (UTC+04:00)',
    description: 'Asia/Dubai',
    keywords: ['dubai', 'utc+04:00'],
  },
]

const groupedOptions: AppComboboxOption[] = [
  {
    value: 'activity-1',
    label: '09:00 Welcome',
    description: 'Day 1',
    keywords: ['welcome'],
    groupLabel: 'Day 1',
  },
  {
    value: 'activity-2',
    label: '11:00 Transfer',
    description: 'Day 1',
    keywords: ['transfer'],
    groupLabel: 'Day 1',
  },
  {
    value: 'activity-3',
    label: '10:00 Feedback',
    description: 'Day 2',
    keywords: ['feedback'],
    groupLabel: 'Day 2',
  },
]

const numericOptions: AppComboboxOption[] = [
  { value: 25, label: '25' },
  { value: 50, label: '50' },
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

describe('AppCombobox', () => {
  beforeEach(() => {
    setLocale('tr')
    mockMatchMedia(false)
  })

  afterEach(() => {
    window.matchMedia = originalMatchMedia
  })

  it('shows the placeholder while no value is selected', () => {
    const wrapper = mount(AppCombobox, {
      props: {
        modelValue: null,
        options,
        placeholder: 'Saat dilimi seçin',
      },
      global: {
        plugins: [i18n],
      },
    })

    expect(wrapper.get('.app-combobox-trigger').text()).toContain('Saat dilimi seçin')
  })

  it('opens and renders the recommended section', async () => {
    const wrapper = mount(AppCombobox, {
      props: {
        modelValue: null,
        options,
        placeholder: 'Saat dilimi seçin',
        recommendedValues: ['Europe/Istanbul', 'Europe/London'],
        recommendedLabel: 'Önerilen',
        allLabel: 'Tüm saat dilimleri',
        browseAllLabel: 'Tüm saat dilimlerinde ara',
        emptyLabel: 'Boş',
        searchPlaceholder: 'Ara',
      },
      global: {
        plugins: [i18n],
      },
    })

    await wrapper.get('.app-combobox-trigger').trigger('click')

    expect(wrapper.text()).toContain('Önerilen')
    expect(wrapper.text()).toContain('Tüm saat dilimlerinde ara')
  })

  it('hides the search input and applies compact styles when configured', async () => {
    const wrapper = mount(AppCombobox, {
      props: {
        modelValue: 'Europe/Istanbul',
        options,
        placeholder: 'Saat dilimi seçin',
        searchable: false,
        compact: true,
      },
      global: {
        plugins: [i18n],
      },
    })

    expect(wrapper.get('.app-combobox-trigger').classes()).toContain('app-combobox-trigger-compact')

    await wrapper.get('.app-combobox-trigger').trigger('click')

    expect(wrapper.find('.app-combobox-search').exists()).toBe(false)
    expect(wrapper.find('.app-combobox-option').classes()).toContain('app-combobox-option-compact')
  })

  it('renders grouped options and preserves matching group labels while searching', async () => {
    const wrapper = mount(AppCombobox, {
      props: {
        modelValue: null,
        options: groupedOptions,
        placeholder: 'Aktivite seçin',
        searchPlaceholder: 'Ara',
        emptyLabel: 'Boş',
      },
      global: {
        plugins: [i18n],
      },
    })

    await wrapper.get('.app-combobox-trigger').trigger('click')
    expect(wrapper.text()).toContain('Day 1')
    expect(wrapper.text()).toContain('Day 2')

    await wrapper.get('.app-combobox-search').setValue('feedback')

    const labels = wrapper.findAll('.app-combobox-section-label').map((node) => node.text())
    expect(labels).toContain('Day 2')
    expect(labels).not.toContain('Day 1')
    expect(wrapper.text()).toContain('10:00 Feedback')
  })

  it('emits numeric values without coercing them to strings', async () => {
    const wrapper = mount(AppCombobox, {
      props: {
        modelValue: null,
        options: numericOptions,
        placeholder: 'Sayfa boyutu',
        searchable: false,
      },
      global: {
        plugins: [i18n],
      },
    })

    await wrapper.get('.app-combobox-trigger').trigger('click')
    await wrapper.findAll('.app-combobox-option')[1]?.trigger('click')

    expect(wrapper.emitted('update:modelValue')).toEqual([[50]])
  })

  it('supports keyboard navigation and selection', async () => {
    const wrapper = mount(AppCombobox, {
      props: {
        modelValue: null,
        options,
        placeholder: 'Saat dilimi seçin',
        recommendedValues: ['Europe/Istanbul', 'Europe/London'],
        recommendedLabel: 'Önerilen',
        allLabel: 'Tüm saat dilimleri',
        browseAllLabel: 'Tüm saat dilimlerinde ara',
        emptyLabel: 'Boş',
        searchPlaceholder: 'Ara',
      },
      global: {
        plugins: [i18n],
      },
    })

    await wrapper.get('.app-combobox-trigger').trigger('click')
    await wrapper.get('.app-combobox-search').trigger('keydown', { key: 'ArrowDown' })
    await wrapper.get('.app-combobox-search').trigger('keydown', { key: 'Enter' })

    expect(wrapper.emitted('update:modelValue')).toEqual([['Europe/London']])
  })

  it('marks the trigger as invalid when requested', () => {
    const wrapper = mount(AppCombobox, {
      props: {
        modelValue: null,
        options,
        placeholder: 'Saat dilimi seçin',
        invalid: true,
      },
      global: {
        plugins: [i18n],
      },
    })

    expect(wrapper.get('.app-combobox-trigger').classes()).toContain('app-combobox-trigger-invalid')
  })
})
