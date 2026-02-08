import { describe, it, expect } from 'vitest'
import {
  getTokenPayload,
  getTokenRole,
  isTokenExpired,
} from '../../src/lib/auth'

// Build a minimal JWT payload part (base64url) for testing
function payloadPart(obj: Record<string, unknown>): string {
  const json = JSON.stringify(obj)
  const base64 = typeof btoa !== 'undefined'
    ? btoa(json)
    : Buffer.from(json, 'utf-8').toString('base64')
  return base64.replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '')
}

function makeToken(payload: Record<string, unknown>): string {
  return `header.${payloadPart(payload)}.signature`
}

describe('getTokenPayload', () => {
  it('returns payload for valid JWT', () => {
    const token = makeToken({ role: 'Admin', sub: 'user-1' })
    expect(getTokenPayload(token)).toEqual({ role: 'Admin', sub: 'user-1' })
  })
  it('returns null when payload part missing', () => {
    expect(getTokenPayload('onlyone')).toBe(null)
    expect(getTokenPayload('a.b')).toBe(null)
  })
  it('returns null for invalid base64 in payload', () => {
    expect(getTokenPayload('a.!!!.c')).toBe(null)
  })
  it('returns null for non-JSON payload', () => {
    const bad = btoa('not json at all').replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '')
    expect(getTokenPayload(`a.${bad}.c`)).toBe(null)
  })
})

describe('getTokenRole', () => {
  it('returns role from payload.role', () => {
    const token = makeToken({ role: 'AgencyAdmin' })
    expect(getTokenRole(token)).toBe('AgencyAdmin')
  })
  it('returns null when payload missing', () => {
    expect(getTokenRole('a.b.c')).toBe(null)
  })
  it('returns null when role missing', () => {
    const token = makeToken({ sub: 'x' })
    expect(getTokenRole(token)).toBe(null)
  })
  it('returns first element when role is array', () => {
    const token = makeToken({ role: ['Guide', 'Admin'] })
    expect(getTokenRole(token)).toBe('Guide')
  })
})

describe('isTokenExpired', () => {
  it('returns false when exp is in the future', () => {
    const token = makeToken({ role: 'Admin', exp: 9999999999 })
    expect(isTokenExpired(token)).toBe(false)
  })
  it('returns true when exp is in the past', () => {
    const token = makeToken({ role: 'User', exp: 1 })
    expect(isTokenExpired(token)).toBe(true)
  })
  it('returns false when exp is missing', () => {
    const token = makeToken({ role: 'Admin' })
    expect(isTokenExpired(token)).toBe(false)
  })
  it('returns false for invalid token', () => {
    expect(isTokenExpired('invalid')).toBe(false)
  })
})
