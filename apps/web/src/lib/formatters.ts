export const formatBaggage = (pieces?: number | null, kg?: number | null, allowance?: string | null) => {
  const segments: string[] = []
  if (typeof pieces === 'number' && pieces > 0) {
    segments.push(`${pieces} pc`)
  }
  if (typeof kg === 'number' && kg > 0) {
    segments.push(`${kg} kg`)
  }
  if (segments.length > 0) {
    return segments.join(' · ')
  }
  // Fallback: eğer pieces ve kg yoksa ama allowance string'i varsa onu kullan
  if (allowance && allowance.trim()) {
    return allowance.trim()
  }
  return '—'
}

export const formatCabinBaggage = (value?: string | null) => {
  if (!value || !value.trim()) {
    return '—'
  }

  const trimmed = value.trim()

  // Try to parse patterns like "1pc 8kg", "1 pc 8kg", "1 pc 8 kg", etc.
  // Match: optional number + optional "pc"/"pcs" + optional number + optional "kg"/"kgs"
  const pattern1 = /^(\d+)\s*(?:pc|pcs)?\s*(\d+)\s*(?:kg|kgs)?$/i
  const match1 = trimmed.match(pattern1)
  if (match1) {
    const pieces = match1[1]
    const kg = match1[2]
    return `${pieces} pc · ${kg} kg`
  }

  // Try to parse patterns like "8kg", "8 kg", "8"
  const pattern2 = /^(\d+)\s*(?:kg|kgs)?$/i
  const match2 = trimmed.match(pattern2)
  if (match2) {
    const kg = match2[1]
    return `${kg} kg`
  }

  // If it doesn't match known patterns, return as-is (might already be formatted)
  return trimmed
}

export const formatDate = (value?: string | null) => {
  if (!value) {
    return '—'
  }
  const trimmed = value.trim()
  if (!trimmed) {
    return '—'
  }

  const datePart = trimmed.includes('T') ? trimmed.split('T')[0] : trimmed.split(' ')[0]
  const normalizedDatePart = datePart || trimmed

  // yyyy-MM-dd / yyyy.MM.dd / yyyy/MM/dd
  const yearFirst = normalizedDatePart.match(/^(\d{4})[./-](\d{1,2})[./-](\d{1,2})$/)
  if (yearFirst) {
    const year = yearFirst[1] ?? ''
    const month = yearFirst[2] ?? ''
    const day = yearFirst[3] ?? ''
    return `${day.padStart(2, '0')}.${month.padStart(2, '0')}.${year}`
  }

  // dd-MM-yyyy / dd.MM.yyyy / dd/MM/yyyy
  const dayFirst = normalizedDatePart.match(/^(\d{1,2})[./-](\d{1,2})[./-](\d{4})$/)
  if (dayFirst) {
    const day = dayFirst[1] ?? ''
    const month = dayFirst[2] ?? ''
    const year = dayFirst[3] ?? ''
    return `${day.padStart(2, '0')}.${month.padStart(2, '0')}.${year}`
  }

  const parsed = new Date(trimmed)
  if (!Number.isNaN(parsed.getTime())) {
    const day = parsed.getDate().toString().padStart(2, '0')
    const month = (parsed.getMonth() + 1).toString().padStart(2, '0')
    const year = parsed.getFullYear().toString()
    return `${day}.${month}.${year}`
  }

  return normalizedDatePart
}

export const formatTime = (value?: string | null) => {
  if (!value) {
    return '—'
  }
  let trimmed = value.trim()
  if (!trimmed) {
    return '—'
  }
  if (trimmed.includes('T')) {
    trimmed = trimmed.split('T')[1] ?? trimmed
  }
  if (trimmed.includes(' ')) {
    trimmed = trimmed.split(' ')[1] ?? trimmed
  }
  trimmed = trimmed.replace('Z', '')
  return trimmed.length >= 5 ? trimmed.slice(0, 5) : trimmed
}

/**
 * Parse a UTC timestamp string and format in the device's local timezone.
 * Use only for API values that are UTC (e.g. log CreatedAt, LoggedAt).
 * Handles ISO 8601 with Z and legacy "yyyy-MM-dd HH:mm:ss" (appends Z).
 */
export function formatUtcToLocal(
  value: string | null | undefined,
  options?: { dateOnly?: boolean; timeOnly?: boolean }
): string {
  if (value == null || value.trim() === '') {
    return '—'
  }
  let utcString = value.trim()
  if (!utcString.includes('T') && !utcString.endsWith('Z')) {
    utcString = utcString.replace(' ', 'T') + (utcString.includes(':') ? 'Z' : '')
  }
  const date = new Date(utcString)
  if (Number.isNaN(date.getTime())) {
    return value
  }
  if (options?.dateOnly) {
    return date.toLocaleDateString(undefined, { day: '2-digit', month: '2-digit', year: 'numeric' })
  }
  if (options?.timeOnly) {
    return date.toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit', hour12: false })
  }
  return date.toLocaleString(undefined, {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
    hour12: false,
  })
}

/** UTC timestamp to local "dd.MM.yyyy HH:mm" for log tables. */
export function formatUtcDateTimeLocal(value: string | null | undefined): string {
  if (value == null || value.trim() === '') {
    return '—'
  }
  let utcString = value.trim()
  if (!utcString.includes('T') && !utcString.endsWith('Z')) {
    utcString = utcString.replace(' ', 'T') + (utcString.includes(':') ? 'Z' : '')
  }
  const date = new Date(utcString)
  if (Number.isNaN(date.getTime())) {
    return value
  }
  const d = date.getDate().toString().padStart(2, '0')
  const m = (date.getMonth() + 1).toString().padStart(2, '0')
  const y = date.getFullYear()
  const h = date.getHours().toString().padStart(2, '0')
  const min = date.getMinutes().toString().padStart(2, '0')
  return `${d}.${m}.${y} ${h}:${min}`
}
