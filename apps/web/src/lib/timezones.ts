import type { AppComboboxOption } from '../types'

const FALLBACK_TIME_ZONES = [
  'Europe/Istanbul',
  'Europe/London',
  'Europe/Berlin',
  'Europe/Athens',
  'Asia/Dubai',
  'Asia/Almaty',
  'America/New_York',
  'Asia/Tokyo',
  'UTC',
]

const CURATED_TIME_ZONES = [
  'Europe/Istanbul',
  'Europe/London',
  'Europe/Berlin',
  'Europe/Athens',
  'Asia/Dubai',
  'Asia/Almaty',
  'America/New_York',
  'Asia/Tokyo',
  'UTC',
]

const getSupportedTimeZoneIds = (): string[] => {
  const supportedValuesOf = (Intl as unknown as {
    supportedValuesOf?: (key: string) => string[]
  }).supportedValuesOf

  if (typeof supportedValuesOf === 'function') {
    const values = supportedValuesOf('timeZone')
      .map((value) => value.trim())
      .filter(Boolean)

    if (values.length > 0) {
      return values
    }
  }

  return [...FALLBACK_TIME_ZONES]
}

const uniqueValues = (values: string[]) => [...new Set(values.map((value) => value.trim()).filter(Boolean))]

const humanizeTimeZoneSegment = (timeZoneId: string) => {
  const segments = timeZoneId.split('/').filter(Boolean)
  const value = segments[segments.length - 1] ?? timeZoneId
  return value.replace(/_/g, ' ')
}

export const getBrowserTimeZone = (): string | null => {
  const timeZone = Intl.DateTimeFormat().resolvedOptions().timeZone?.trim()
  return timeZone || null
}

export const formatTimeZoneOffsetPreview = (timeZoneId: string): string => {
  const normalized = timeZoneId.trim()
  if (!normalized) {
    return ''
  }

  try {
    const formatter = new Intl.DateTimeFormat('en-US', {
      timeZone: normalized,
      timeZoneName: 'longOffset',
      hour: '2-digit',
      minute: '2-digit',
      hour12: false,
    })

    const parts = formatter.formatToParts(new Date())
    const offset = parts.find((part) => part.type === 'timeZoneName')?.value ?? ''

    if (offset === 'GMT' || offset === 'UTC') {
      return 'UTC+00:00'
    }

    const match = offset.match(/^(?:GMT|UTC)([+-])(\d{1,2})(?::?(\d{2}))?$/)
    if (!match) {
      return ''
    }

    const [, sign, hours = '0', minutes = '00'] = match
    return `UTC${sign}${hours.padStart(2, '0')}:${minutes}`
  } catch {
    return ''
  }
}

export const getTimeZoneDisplayLabel = (timeZoneId: string): string => {
  const cityLabel = humanizeTimeZoneSegment(timeZoneId.trim())
  const offset = formatTimeZoneOffsetPreview(timeZoneId)

  if (!cityLabel) {
    return timeZoneId.trim()
  }

  return offset ? `${cityLabel} (${offset})` : cityLabel
}

export const getTimeZoneDescription = (timeZoneId: string): string => timeZoneId.trim()

export const getTimeZoneKeywords = (timeZoneId: string): string[] => {
  const normalized = timeZoneId.trim()
  if (!normalized) {
    return []
  }

  const offset = formatTimeZoneOffsetPreview(normalized)
  const segments = normalized.split('/').flatMap((segment) => segment.split('_'))

  return uniqueValues([
    normalized.toLowerCase(),
    getTimeZoneDisplayLabel(normalized).toLowerCase(),
    ...segments.map((segment) => segment.toLowerCase()),
    offset.toLowerCase(),
  ])
}

export const getAllTimeZoneOptions = (): AppComboboxOption[] =>
  getSupportedTimeZoneIds().map((timeZoneId) => ({
    value: timeZoneId,
    label: getTimeZoneDisplayLabel(timeZoneId),
    description: getTimeZoneDescription(timeZoneId),
    keywords: getTimeZoneKeywords(timeZoneId),
  }))

export const getRecommendedTimeZoneValues = (browserTimeZone?: string | null): string[] => {
  const values = browserTimeZone ? [browserTimeZone, ...CURATED_TIME_ZONES] : [...CURATED_TIME_ZONES]
  const uniqueTimeZones = uniqueValues(values)
  const seenOffsets = new Set<string>()

  return uniqueTimeZones.filter((timeZoneId) => {
    if (timeZoneId === 'UTC') {
      return true
    }

    const offset = formatTimeZoneOffsetPreview(timeZoneId)
    if (!offset) {
      return true
    }

    if (seenOffsets.has(offset)) {
      return false
    }

    seenOffsets.add(offset)
    return true
  })
}
