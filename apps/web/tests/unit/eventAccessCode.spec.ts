import { describe, it, expect } from 'vitest'
import {
  sanitizeEventAccessCode,
  isValidEventCodeLength,
} from '../../src/lib/eventAccessCode'

describe('sanitizeEventAccessCode', () => {
  it('uppercases and strips non-alphanumeric', () => {
    expect(sanitizeEventAccessCode('abc-123')).toBe('ABC123')
  })
  it('limits to 10 characters', () => {
    expect(sanitizeEventAccessCode('ABCDEFGHIJK')).toBe('ABCDEFGHIJ')
  })
  it('allows A–Z and 0–9 only', () => {
    expect(sanitizeEventAccessCode('  aB3 xYz  ')).toBe('AB3XYZ')
  })
  it('returns empty for empty input', () => {
    expect(sanitizeEventAccessCode('')).toBe('')
  })
})

describe('isValidEventCodeLength', () => {
  it('returns true for 6–10 characters', () => {
    expect(isValidEventCodeLength('ABC123')).toBe(true)
    expect(isValidEventCodeLength('ABCDEFGH')).toBe(true)
    expect(isValidEventCodeLength('ABCDEFGHIJ')).toBe(true)
  })
  it('returns false for fewer than 6', () => {
    expect(isValidEventCodeLength('')).toBe(false)
    expect(isValidEventCodeLength('ABC12')).toBe(false)
  })
  it('returns false for more than 10', () => {
    expect(isValidEventCodeLength('ABCDEFGHIJK')).toBe(false)
  })
})
