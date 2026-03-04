import { afterEach, describe, expect, it, vi } from 'vitest'
import {
  formatTimeZoneOffsetPreview,
  getAllTimeZoneOptions,
  getBrowserTimeZone,
  getRecommendedTimeZoneValues,
  getTimeZoneDescription,
  getTimeZoneDisplayLabel,
  getTimeZoneKeywords,
} from '../../src/lib/timezones'

const originalDateTimeFormat = Intl.DateTimeFormat
const originalSupportedValuesOf = (Intl as unknown as { supportedValuesOf?: (key: string) => string[] }).supportedValuesOf

afterEach(() => {
  Intl.DateTimeFormat = originalDateTimeFormat

  if (originalSupportedValuesOf) {
    ;(Intl as unknown as { supportedValuesOf?: (key: string) => string[] }).supportedValuesOf = originalSupportedValuesOf
  } else {
    delete (Intl as unknown as { supportedValuesOf?: (key: string) => string[] }).supportedValuesOf
  }
})

describe('getBrowserTimeZone', () => {
  it('returns the resolved browser time zone when present', () => {
    Intl.DateTimeFormat = vi.fn(() => ({
      resolvedOptions: () => ({ timeZone: 'Europe/Paris' }),
    })) as unknown as typeof Intl.DateTimeFormat

    expect(getBrowserTimeZone()).toBe('Europe/Paris')
  })

  it('returns null when the browser does not expose a time zone', () => {
    Intl.DateTimeFormat = vi.fn(() => ({
      resolvedOptions: () => ({ timeZone: '' }),
    })) as unknown as typeof Intl.DateTimeFormat

    expect(getBrowserTimeZone()).toBeNull()
  })
})

describe('getAllTimeZoneOptions', () => {
  it('uses Intl.supportedValuesOf when available', () => {
    ;(Intl as unknown as { supportedValuesOf?: (key: string) => string[] }).supportedValuesOf = vi
      .fn()
      .mockReturnValue(['Europe/Istanbul', 'UTC'])

    expect(getAllTimeZoneOptions()).toEqual([
      expect.objectContaining({
        value: 'Europe/Istanbul',
        label: 'Istanbul (UTC+03:00)',
        description: 'Europe/Istanbul',
      }),
      expect.objectContaining({
        value: 'UTC',
        label: 'UTC (UTC+00:00)',
        description: 'UTC',
      }),
    ])
  })

  it('falls back to a built-in option list when supportedValuesOf is unavailable', () => {
    delete (Intl as unknown as { supportedValuesOf?: (key: string) => string[] }).supportedValuesOf

    const values = getAllTimeZoneOptions().map((option) => option.value)
    expect(values).toContain('Europe/Istanbul')
    expect(values).toContain('UTC')
  })
})

describe('getRecommendedTimeZoneValues', () => {
  it('prepends the browser time zone and removes duplicates', () => {
    expect(getRecommendedTimeZoneValues('Europe/Paris').slice(0, 3)).toEqual([
      'Europe/Paris',
      'Europe/Istanbul',
      'Europe/London',
    ])
  })

  it('does not duplicate browser time zone when already curated', () => {
    const values = getRecommendedTimeZoneValues('Europe/Istanbul')
    expect(values.filter((value) => value === 'Europe/Istanbul')).toHaveLength(1)
  })

  it('filters repeated offsets when browser time zone already covers one', () => {
    const values = getRecommendedTimeZoneValues('Europe/Paris')

    expect(values).not.toContain('Europe/Berlin')
    expect(values).toContain('Europe/Paris')
    expect(values).toContain('UTC')
  })
})

describe('time zone labels', () => {
  it('formats humanized display labels', () => {
    expect(getTimeZoneDisplayLabel('America/Argentina/Buenos_Aires')).toBe('Buenos Aires (UTC-03:00)')
  })

  it('returns the raw IANA id as description', () => {
    expect(getTimeZoneDescription('Europe/Istanbul')).toBe('Europe/Istanbul')
  })

  it('generates searchable keywords', () => {
    expect(getTimeZoneKeywords('Europe/Istanbul')).toContain('istanbul')
    expect(getTimeZoneKeywords('Europe/Istanbul')).toContain('utc+03:00')
  })
})

describe('formatTimeZoneOffsetPreview', () => {
  it('formats UTC as a normalized offset', () => {
    expect(formatTimeZoneOffsetPreview('UTC')).toBe('UTC+00:00')
  })

  it('formats a fixed-offset IANA zone', () => {
    expect(formatTimeZoneOffsetPreview('Asia/Dubai')).toBe('UTC+04:00')
  })
})
