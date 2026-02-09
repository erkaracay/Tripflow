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

export const formatDate = (value?: string | null) => {
  if (!value) {
    return '—'
  }
  const trimmed = value.trim()
  if (!trimmed) {
    return '—'
  }
  const datePart = trimmed.includes('T')
    ? trimmed.split('T')[0]
    : trimmed.split(' ')[0]
  return datePart || trimmed
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
