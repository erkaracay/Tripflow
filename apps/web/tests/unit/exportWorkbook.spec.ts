import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { exportWorkbook } from '../../src/lib/exportWorkbook'

describe('exportWorkbook', () => {
  const originalCreateObjectURL = globalThis.URL.createObjectURL
  const originalRevokeObjectURL = globalThis.URL.revokeObjectURL
  let clickSpy: ReturnType<typeof vi.spyOn>

  beforeEach(() => {
    globalThis.URL.createObjectURL = vi.fn(() => 'blob:tripflow-export')
    globalThis.URL.revokeObjectURL = vi.fn()
    clickSpy = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {})
  })

  afterEach(() => {
    clickSpy.mockRestore()
    globalThis.URL.createObjectURL = originalCreateObjectURL
    globalThis.URL.revokeObjectURL = originalRevokeObjectURL
    document.body.innerHTML = ''
  })

  it('creates a workbook with multiple sheets and downloads it', async () => {
    const blob = await exportWorkbook({
      fileName: 'participants.xlsx',
      sheets: [
        {
          name: 'participants',
          rows: [
            ['tc_no', 'participant_name'],
            ['11111111111', 'Ada Lovelace'],
            ['22222222222', 'Alan Turing'],
          ],
        },
        {
          name: 'flight_segments',
          rows: [
            ['direction', 'segment_index'],
            ['Arrival', 1],
          ],
        },
      ],
    })

    expect(blob.size).toBeGreaterThan(0)
    expect(globalThis.URL.createObjectURL).toHaveBeenCalledWith(expect.any(Blob))
    expect(globalThis.URL.revokeObjectURL).toHaveBeenCalledWith('blob:tripflow-export')
    expect(clickSpy).toHaveBeenCalledTimes(1)
    expect(document.body.querySelector('a')).toBeNull()

    const ExcelJS = await import('exceljs')
    const workbook = new ExcelJS.Workbook()
    await workbook.xlsx.load(await blob.arrayBuffer())

    expect(workbook.worksheets.map((worksheet) => worksheet.name)).toEqual([
      'participants',
      'flight_segments',
    ])
    expect(workbook.getWorksheet('participants')?.getRow(1).getCell(1).value).toBe('tc_no')
    expect(workbook.getWorksheet('participants')?.getRow(2).getCell(2).value).toBe('Ada Lovelace')
    expect(workbook.getWorksheet('flight_segments')?.getRow(2).getCell(2).value).toBe(1)
  })
})
