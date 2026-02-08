import { describe, it, expect } from 'vitest'
import {
  normalizeName,
  normalizeEmail,
  normalizeCheckInCode,
  sanitizePhoneInput,
  normalizePhone,
  formatPhoneDisplay,
} from '../../src/lib/normalize'

describe('normalizeName', () => {
  it('trims whitespace', () => {
    expect(normalizeName('  foo  ')).toBe('foo')
  })
  it('collapses multiple spaces to one', () => {
    expect(normalizeName('a   b   c')).toBe('a b c')
  })
})

describe('normalizeEmail', () => {
  it('trims and lowercases', () => {
    expect(normalizeEmail('  FOO@BAR.COM  ')).toBe('foo@bar.com')
  })
})

describe('normalizeCheckInCode', () => {
  it('removes spaces and uppercases', () => {
    expect(normalizeCheckInCode(' abc 123 ')).toBe('ABC123')
  })
  it('strips non-alphanumeric', () => {
    expect(normalizeCheckInCode('a-b.c!')).toBe('ABC')
  })
})

describe('sanitizePhoneInput', () => {
  it('keeps only digits', () => {
    expect(sanitizePhoneInput('5551234567')).toBe('5551234567')
  })
  it('allows leading + only', () => {
    expect(sanitizePhoneInput('+905551234567')).toBe('+905551234567')
  })
  it('ignores + after digits', () => {
    expect(sanitizePhoneInput('555+123')).toBe('555123')
  })
  it('returns empty for non-digit non-plus', () => {
    expect(sanitizePhoneInput('abc')).toBe('')
  })
})

describe('normalizePhone', () => {
  it('returns empty normalized for empty input', () => {
    expect(normalizePhone('')).toEqual({ normalized: '' })
    expect(normalizePhone('   ')).toEqual({ normalized: '' })
  })
  it('returns invalid for non-phone input', () => {
    expect(normalizePhone('abc')).toEqual({
      normalized: '',
      errorKey: 'validation.phone.invalid',
    })
  })
  it('normalizes Turkish 10-digit starting with 5 to +90', () => {
    expect(normalizePhone('5551234567')).toEqual({ normalized: '+905551234567' })
  })
  it('normalizes Turkish 11-digit 0 5xx to +90', () => {
    expect(normalizePhone('05551234567')).toEqual({ normalized: '+905551234567' })
  })
  it('keeps international format with +', () => {
    expect(normalizePhone('+491234567890')).toEqual({ normalized: '+491234567890' })
  })
  it('returns requireCountry for digits without country', () => {
    expect(normalizePhone('1234567890')).toEqual({
      normalized: '1234567890',
      errorKey: 'validation.phone.requireCountry',
    })
  })
  it('returns tooLong for international over 15 digits after +', () => {
    expect(normalizePhone('+1234567890123456')).toEqual({
      normalized: '',
      errorKey: 'validation.phone.tooLong',
    })
  })
  it('strips spaces and dashes before sanitizing', () => {
    expect(normalizePhone('0555 123 45 67')).toEqual({ normalized: '+905551234567' })
  })
})

describe('formatPhoneDisplay', () => {
  it('returns empty for empty or null', () => {
    expect(formatPhoneDisplay()).toBe('')
    expect(formatPhoneDisplay('')).toBe('')
    expect(formatPhoneDisplay(null)).toBe('')
  })
  it('formats Turkish +90 10-digit with spaces', () => {
    expect(formatPhoneDisplay('+905551234567')).toBe(
      '+90 555 123 45 67'
    )
  })
  it('returns trimmed input when no special format', () => {
    expect(formatPhoneDisplay('  short  ')).toBe('short')
  })
})
