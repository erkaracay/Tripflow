/** Sanitize user input for event access code: A–Z, 0–9, max 10 chars */
export const sanitizeEventAccessCode = (value: string): string =>
  value.replace(/[^A-Za-z0-9]/g, '').toUpperCase().slice(0, 10)

/** Check if code length is valid (6–10 characters) for event access code */
export const isValidEventCodeLength = (code: string): boolean => {
  const len = code.length
  return len >= 6 && len <= 10
}
