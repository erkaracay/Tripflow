import { describe, it, expect } from 'vitest'
import { parsePasteTable, digitsOnly } from '../../src/lib/parsePasteTable'

describe('parsePasteTable', () => {
  it('returns empty array for empty input', () => {
    expect(parsePasteTable('')).toEqual([])
  })

  it('splits tab-separated rows', () => {
    const input = '12345678901\tPOL-001\n98765432109\tPOL-002'
    expect(parsePasteTable(input)).toEqual([
      ['12345678901', 'POL-001'],
      ['98765432109', 'POL-002'],
    ])
  })

  it('normalizes \\r\\n line endings', () => {
    const input = '12345678901\tPOL-001\r\n98765432109\tPOL-002\r\n'
    expect(parsePasteTable(input)).toEqual([
      ['12345678901', 'POL-001'],
      ['98765432109', 'POL-002'],
    ])
  })

  it('skips empty and whitespace-only lines', () => {
    const input = '\n12345678901\tPOL-001\n\n  \n98765432109\tPOL-002\n'
    expect(parsePasteTable(input)).toEqual([
      ['12345678901', 'POL-001'],
      ['98765432109', 'POL-002'],
    ])
  })

  it('trims surrounding whitespace from cells', () => {
    const input = '  12345678901 \t POL-001  '
    expect(parsePasteTable(input)).toEqual([
      ['12345678901', 'POL-001'],
    ])
  })

  it('falls back to 2+ spaces when no tab is present', () => {
    const input = '12345678901   POL-001'
    expect(parsePasteTable(input)).toEqual([
      ['12345678901', 'POL-001'],
    ])
  })

  it('collapses consecutive tabs from skipped columns', () => {
    const input = '12345678901\t\tPOL-001\n98765432109\t\t\tPOL-002'
    expect(parsePasteTable(input)).toEqual([
      ['12345678901', 'POL-001'],
      ['98765432109', 'POL-002'],
    ])
  })

  it('handles tabs mixed with spaces between cells', () => {
    const input = '12345678901\t   \tPOL-001'
    expect(parsePasteTable(input)).toEqual([
      ['12345678901', 'POL-001'],
    ])
  })

  it('ignores trailing tabs after the last cell', () => {
    const input = '12345678901\tPOL-001\t\t\n'
    expect(parsePasteTable(input)).toEqual([
      ['12345678901', 'POL-001'],
    ])
  })

  it('drops the first row when looksLikeHeader returns true', () => {
    const input = 'TC No\tPoliçe\n12345678901\tPOL-001'
    const result = parsePasteTable(input, {
      looksLikeHeader: row => !/^\d{11}$/.test(row[0] ?? ''),
    })
    expect(result).toEqual([['12345678901', 'POL-001']])
  })

  it('keeps the first row when looksLikeHeader returns false', () => {
    const input = '12345678901\tPOL-001\n98765432109\tPOL-002'
    const result = parsePasteTable(input, {
      looksLikeHeader: row => !/^\d{11}$/.test(row[0] ?? ''),
    })
    expect(result).toEqual([
      ['12345678901', 'POL-001'],
      ['98765432109', 'POL-002'],
    ])
  })
})

describe('digitsOnly', () => {
  it('keeps only digits', () => {
    expect(digitsOnly('123-456 789')).toBe('123456789')
  })
  it('returns empty for non-digit input', () => {
    expect(digitsOnly('abc')).toBe('')
  })
})
