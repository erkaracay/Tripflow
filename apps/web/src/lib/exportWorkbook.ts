export type ExportWorkbookCell = string | number | boolean | null | undefined

export type ExportWorkbookSheet = {
  name: string
  rows: ExportWorkbookCell[][]
}

export type ExportWorkbookInput = {
  fileName: string
  sheets: ExportWorkbookSheet[]
}

const XLSX_MIME_TYPE =
  'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'

const buildWorkbookBlob = async (sheets: ExportWorkbookSheet[]) => {
  const ExcelJS = await import('exceljs')
  const workbook = new ExcelJS.Workbook()

  sheets.forEach(({ name, rows }) => {
    const worksheet = workbook.addWorksheet(name)
    rows.forEach((row) => {
      worksheet.addRow(row)
    })
  })

  const buffer = await workbook.xlsx.writeBuffer()
  return new Blob([buffer], { type: XLSX_MIME_TYPE })
}

const triggerDownload = (blob: Blob, fileName: string) => {
  const objectUrl = globalThis.URL.createObjectURL(blob)
  const anchor = document.createElement('a')

  anchor.href = objectUrl
  anchor.download = fileName
  anchor.style.display = 'none'

  document.body.appendChild(anchor)
  anchor.click()
  anchor.remove()
  globalThis.URL.revokeObjectURL(objectUrl)
}

export const exportWorkbook = async ({ fileName, sheets }: ExportWorkbookInput) => {
  const blob = await buildWorkbookBlob(sheets)
  triggerDownload(blob, fileName)
  return blob
}
