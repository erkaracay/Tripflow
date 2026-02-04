export const formatBaggage = (pieces?: number | null, kg?: number | null) => {
  const segments: string[] = []
  if (typeof pieces === 'number' && pieces > 0) {
    segments.push(`${pieces} pc`)
  }
  if (typeof kg === 'number' && kg > 0) {
    segments.push(`${kg} kg`)
  }
  return segments.length > 0 ? segments.join(' · ') : '—'
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
