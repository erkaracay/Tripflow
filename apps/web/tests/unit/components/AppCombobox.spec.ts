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

  it('emits update:modelValue when an option is selected', async () => {
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
    await wrapper.findAll('.app-combobox-option')[0]?.trigger('click')

    expect(wrapper.emitted('update:modelValue')).toEqual([['Europe/Istanbul']])
  })

  it('filters options by search value', async () => {
    const wrapper = mount(AppCombobox, {
      props: {
        modelValue: null,
        options,
        placeholder: 'Saat dilimi seçin',
        recommendedValues: ['Europe/Istanbul'],
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
    await wrapper.get('.app-combobox-search').setValue('dubai')

    const optionTexts = wrapper.findAll('.app-combobox-option').map((option) => option.text())
    expect(optionTexts).toHaveLength(1)
    expect(optionTexts[0]).toContain('Dubai')
  })

  it('reveals the full list after browse all is clicked', async () => {
    const wrapper = mount(AppCombobox, {
      props: {
        modelValue: null,
        options,
        placeholder: 'Saat dilimi seçin',
        recommendedValues: ['Europe/Istanbul'],
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
    await wrapper.get('button[type="button"][class*="border-dashed"]').trigger('click')

    expect(wrapper.text()).toContain('Tüm saat dilimleri')
    expect(wrapper.text()).toContain('Dubai')
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
