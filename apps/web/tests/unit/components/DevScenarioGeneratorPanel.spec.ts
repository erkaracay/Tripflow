import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import DevScenarioGeneratorPanel from '../../../src/components/admin/DevScenarioGeneratorPanel.vue'
import { i18n, setLocale } from '../../../src/i18n'
import type { ScenarioPresetDto } from '../../../src/types'

vi.mock('../../../src/lib/api', () => ({
  apiPost: vi.fn(),
}))

const presets: ScenarioPresetDto[] = [
  {
    id: 'minimal',
    label: 'Minimal',
    defaults: {
      dayCount: 2,
      participantCount: 20,
      equipmentTypeCount: 1,
      activityDensity: 'light',
      mealMode: 'none',
      flightLegMode: 'mixed',
      includeFlights: false,
      eventCheckInCoveragePercent: 0,
      mealSelectionCoveragePercent: 0,
      participantNamingMode: 'random',
    },
  },
  {
    id: 'balanced',
    label: 'Balanced',
    defaults: {
      dayCount: 3,
      participantCount: 40,
      equipmentTypeCount: 2,
      activityDensity: 'normal',
      mealMode: 'breakfast_only',
      flightLegMode: 'mixed',
      includeFlights: true,
      eventCheckInCoveragePercent: 20,
      mealSelectionCoveragePercent: 70,
      participantNamingMode: 'random',
    },
  },
  {
    id: 'operations_heavy',
    label: 'Operations Heavy',
    defaults: {
      dayCount: 4,
      participantCount: 80,
      equipmentTypeCount: 4,
      activityDensity: 'dense',
      mealMode: 'breakfast_and_dinner',
      flightLegMode: 'mixed',
      includeFlights: true,
      eventCheckInCoveragePercent: 35,
      mealSelectionCoveragePercent: 85,
      participantNamingMode: 'random',
    },
  },
  {
    id: 'flight_heavy',
    label: 'Flight Heavy',
    defaults: {
      dayCount: 3,
      participantCount: 48,
      equipmentTypeCount: 2,
      activityDensity: 'normal',
      mealMode: 'breakfast_only',
      flightLegMode: 'layover_heavy',
      includeFlights: true,
      eventCheckInCoveragePercent: 10,
      mealSelectionCoveragePercent: 30,
      participantNamingMode: 'random',
    },
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

const openPanel = async (wrapper: ReturnType<typeof mount>) => {
  const toggleButton = wrapper
    .findAll('button')
    .find((button) => button.text() === 'Open generator')

  expect(toggleButton).toBeDefined()
  await toggleButton!.trigger('click')
}

const openAdvanced = async (wrapper: ReturnType<typeof mount>) => {
  const advancedButton = wrapper
    .findAll('button')
    .find((button) => button.text() === 'Open advanced settings')

  expect(advancedButton).toBeDefined()
  await advancedButton!.trigger('click')
}

const findFlightModeSelect = (wrapper: ReturnType<typeof mount>) =>
  wrapper
    .findAll('select')
    .find((select) => select.text().includes('Direct only') && select.text().includes('Layover heavy'))

const findNamingModeSelect = (wrapper: ReturnType<typeof mount>) =>
  wrapper
    .findAll('select')
    .find((select) => select.text().includes('Random sample names') && select.text().includes('Prefix + Guest'))

const findPresetTab = (wrapper: ReturnType<typeof mount>, label: string) =>
  wrapper
    .findAll('button[role="tab"]')
    .find((button) => button.text().trim().toLowerCase() === label.trim().toLowerCase())

describe('DevScenarioGeneratorPanel', () => {
  beforeEach(async () => {
    setLocale('en')
    mockMatchMedia(false)
    const { apiPost } = await import('../../../src/lib/api')
    vi.mocked(apiPost).mockReset()
  })

  afterEach(() => {
    window.matchMedia = originalMatchMedia
  })

  it('applies preset defaults when the preset changes', async () => {
    const wrapper = mount(DevScenarioGeneratorPanel, {
      props: { presets },
      global: {
        plugins: [i18n],
      },
    })

    await openPanel(wrapper)

    const flightHeavyTab = findPresetTab(wrapper, 'Flight heavy')

    expect(flightHeavyTab).toBeDefined()
    await flightHeavyTab!.trigger('click')
    await openAdvanced(wrapper)

    const numberInputs = wrapper.findAll('input[type="number"]')
    expect((numberInputs[0]!.element as HTMLInputElement).value).toBe('3')
    expect((numberInputs[1]!.element as HTMLInputElement).value).toBe('48')

    const flightModeSelect = findFlightModeSelect(wrapper)
    expect(flightModeSelect).toBeDefined()
    expect((flightModeSelect!.element as HTMLSelectElement).value).toBe('layover_heavy')
  })

  it('submits and emits the generated event payload', async () => {
    const response = {
      eventId: 'ev-1',
      name: '[DEV] Balanced',
      startDate: '2026-03-10',
      endDate: '2026-03-12',
      timeZoneId: 'Europe/Istanbul',
      eventAccessCode: 'ABC12345',
      created: {
        days: 3,
        activities: 10,
        mealActivities: 3,
        participants: 40,
        equipmentTypes: 2,
        mealGroups: 3,
        mealOptions: 9,
        mealSelections: 28,
        flightSegments: 60,
        eventCheckIns: 8,
      },
    }

    const { apiPost } = await import('../../../src/lib/api')
    vi.mocked(apiPost).mockResolvedValue(response)

    const wrapper = mount(DevScenarioGeneratorPanel, {
      props: { presets },
      global: {
        plugins: [i18n],
      },
    })

    await openPanel(wrapper)
    await wrapper.get('form').trigger('submit.prevent')
    await flushPromises()

    expect(apiPost).toHaveBeenCalledWith(
      '/api/dev/scenario-events',
      expect.objectContaining({
        preset: 'balanced',
        timeZoneId: expect.any(String),
        flightLegMode: 'mixed',
      })
    )
    expect(wrapper.emitted('generated')).toEqual([[response]])
  })

  it('requires a prefix when prefix naming mode is selected', async () => {
    const wrapper = mount(DevScenarioGeneratorPanel, {
      props: { presets },
      global: {
        plugins: [i18n],
      },
    })

    await openPanel(wrapper)
    await openAdvanced(wrapper)

    const namingModeSelect = findNamingModeSelect(wrapper)
    expect(namingModeSelect).toBeDefined()
    await namingModeSelect!.setValue('prefix')

    await wrapper.get('form').trigger('submit.prevent')
    await flushPromises()

    expect(wrapper.text()).toContain('A prefix is required when prefix naming mode is selected.')
  })

  it('disables flight structure selection when flight generation is turned off', async () => {
    const wrapper = mount(DevScenarioGeneratorPanel, {
      props: { presets },
      global: {
        plugins: [i18n],
      },
    })

    await openPanel(wrapper)
    await openAdvanced(wrapper)

    await wrapper.get('input[type="checkbox"]').setValue(false)

    const flightModeSelect = findFlightModeSelect(wrapper)
    expect(flightModeSelect).toBeDefined()
    expect(flightModeSelect!.attributes('disabled')).toBeDefined()
  })

  it('renders exact summary counts for the flight-heavy preset', async () => {
    const wrapper = mount(DevScenarioGeneratorPanel, {
      props: { presets },
      global: {
        plugins: [i18n],
      },
    })

    await openPanel(wrapper)

    const flightHeavyTab = findPresetTab(wrapper, 'Flight heavy')

    expect(flightHeavyTab).toBeDefined()
    await flightHeavyTab!.trigger('click')

    expect(wrapper.text()).toContain('3 days')
    expect(wrapper.text()).toContain('12 activities')
    expect(wrapper.text()).toContain('3 meal activities')
    expect(wrapper.text()).toContain('48 participants')
    expect(wrapper.text()).toContain('2 equipment types')
    expect(wrapper.text()).toContain('42 meal selections')
    expect(wrapper.text()).toContain('5 event check-ins')
    expect(wrapper.text()).toContain('144 flight segments')
    expect(wrapper.text()).toContain('24 direct passengers · 24 layover passengers')
  })
})
