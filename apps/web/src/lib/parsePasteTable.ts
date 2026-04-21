/**
 * Parse tab- or whitespace-separated rows from clipboard text (Excel / Numbers / Google Sheets).
 *
 * - Splits lines on `\r\n` or `\n`.
 * - Splits each line on `\t` first; falls back to 2+ spaces when no tab is present.
 * - Drops fully empty rows.
 * - Optional header skip: if the first row's first cell doesn't look like data,
 *   the caller can drop it using `looksLikeHeader`.
 */

export type ParsedRow = string[]

export type ParsePasteTableOptions = {
  /**
   * If provided and returns true for the first row, that row is treated as a header and removed.
   */
  looksLikeHeader?: (firstRow: ParsedRow) => boolean
}

const splitRow = (line: string): ParsedRow => {
  // Tabs win if present — Excel, Numbers, and Google Sheets all use \t by default.
  // Fallback: split on 2+ whitespace (so single spaces inside values don't break cells).
  const raw = line.includes('\t') ? line.split('\t') : line.split(/\s{2,}/)

  // Empty cells can appear when Excel emits consecutive tabs (e.g. skipped columns, `TC\t\tPolicy`
  // or trailing blanks). Filter them out so row[1] is always the next non-empty value.
  return raw.map(cell => cell.trim()).filter(cell => cell.length > 0)
}

export const parsePasteTable = (raw: string, options?: ParsePasteTableOptions): ParsedRow[] => {
  if (!raw) {
    return []
  }

  const normalized = raw.replace(/\r\n/g, '\n').replace(/\r/g, '\n')
  const lines = normalized.split('\n')

  const rows: ParsedRow[] = []
  for (const line of lines) {
    if (line.trim().length === 0) {
      continue
    }
    const cells = splitRow(line)
    if (cells.every(cell => cell.length === 0)) {
      continue
    }
    rows.push(cells)
  }

  const firstRow = rows[0]
  if (firstRow !== undefined && options?.looksLikeHeader?.(firstRow)) {
    rows.shift()
  }

  return rows
}

/**
 * Keep only digits from a value.
 */
export const digitsOnly = (value: string): string => value.replace(/\D/g, '')
