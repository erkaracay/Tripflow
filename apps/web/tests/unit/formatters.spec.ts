import { describe, it, expect } from 'vitest'
import { formatBaggage, formatDate, formatTime } from '../../src/lib/formatters'

describe('formatBaggage', () => {
  it('returns "—" when both args empty', () => {
    expect(formatBaggage()).toBe('—')
    expect(formatBaggage(null, null)).toBe('—')
  })
  it('formats pieces and kg', () => {
    expect(formatBaggage(2, 15)).toBe('2 pc · 15 kg')
    expect(formatBaggage(1, 10)).toBe('1 pc · 10 kg')
  })
})

describe('formatDate', () => {
  it('returns "—" for empty', () => {
    expect(formatDate()).toBe('—')
    expect(formatDate('')).toBe('—')
  })
  it('extracts date part from YYYY-MM-DD', () => {
    expect(formatDate('2026-02-08')).toBe('2026-02-08')
  })
})

describe('formatTime', () => {
  it('returns "—" for empty', () => {
    expect(formatTime()).toBe('—')
  })
  it('extracts HH:mm from ISO string', () => {
    expect(formatTime('2026-02-08T14:30:00')).toBe('14:30')
  })
})
