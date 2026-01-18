export const normalizeName = (value: string) => value.trim().replace(/\s+/g, ' ')

export const normalizeEmail = (value: string) => value.trim().toLowerCase()

export const normalizeCheckInCode = (value: string) =>
  value.replace(/\s+/g, '').toUpperCase().replace(/[^A-Z0-9]/g, '')

export const sanitizePhoneInput = (value: string) => {
  let result = ''
  for (const char of value) {
    if (char >= '0' && char <= '9') {
      result += char
      continue
    }

    if (char === '+' && result.length === 0) {
      result += char
    }
  }

  return result
}

type PhoneNormalization = {
  normalized: string
  errorKey?: string
}

export const normalizePhone = (raw: string): PhoneNormalization => {
  const trimmed = raw.trim()
  if (!trimmed) {
    return { normalized: '' }
  }

  const cleaned = trimmed.replace(/[\s\-()]/g, '')
  const sanitized = sanitizePhoneInput(cleaned)

  if (!sanitized) {
    return { normalized: '', errorKey: 'validation.phone.invalid' }
  }

  if (sanitized.startsWith('+')) {
    const digits = sanitized.slice(1)
    if (!digits) {
      return { normalized: '', errorKey: 'validation.phone.invalid' }
    }
    if (digits.length > 15) {
      return { normalized: '', errorKey: 'validation.phone.tooLong' }
    }
    return { normalized: `+${digits}` }
  }

  const digits = sanitized

  if (digits.startsWith('0') && digits.length === 11) {
    const rest = digits.slice(1)
    if (rest.startsWith('5')) {
      return { normalized: `+90${rest}` }
    }
  }

  if (digits.startsWith('5') && digits.length === 10) {
    return { normalized: `+90${digits}` }
  }

  return { normalized: digits, errorKey: 'validation.phone.requireCountry' }
}

const chunkString = (value: string, size: number) =>
  value.match(new RegExp(`.{1,${size}}`, 'g')) ?? []

export const formatPhoneDisplay = (value?: string | null) => {
  if (!value) {
    return ''
  }

  const trimmed = value.trim()
  if (!trimmed) {
    return ''
  }

  const sanitized = sanitizePhoneInput(trimmed.replace(/[\s\-()]/g, ''))

  if (sanitized.startsWith('+90')) {
    const digits = sanitized.slice(3)
    if (digits.length === 10) {
      return `+90 ${digits.slice(0, 3)} ${digits.slice(3, 6)} ${digits.slice(6, 8)} ${digits.slice(8)}`
    }
  }

  if (sanitized.startsWith('+')) {
    const digits = sanitized.slice(1)
    if (!digits) {
      return trimmed
    }

    let countryLength = 1
    if (digits.length > 11) {
      countryLength = 3
    } else if (digits.length > 10) {
      countryLength = 2
    }

    if (digits.startsWith('1') && digits.length === 11) {
      countryLength = 1
    }

    const country = digits.slice(0, countryLength)
    const rest = digits.slice(countryLength)
    if (!rest) {
      return `+${country}`
    }

    if (rest.length === 10) {
      return `+${country} ${rest.slice(0, 4)} ${rest.slice(4, 7)} ${rest.slice(7)}`
    }

    if (rest.length === 9) {
      return `+${country} ${rest.slice(0, 3)} ${rest.slice(3, 6)} ${rest.slice(6)}`
    }

    if (rest.length === 8) {
      return `+${country} ${rest.slice(0, 4)} ${rest.slice(4)}`
    }

    return `+${country} ${chunkString(rest, 3).join(' ')}`
  }

  return trimmed
}
