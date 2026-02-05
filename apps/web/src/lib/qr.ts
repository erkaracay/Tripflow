export const normalizeQrCode = (raw: string) => {
  const trimmed = raw.trim()
  if (!trimmed) {
    return ''
  }

  let candidate = trimmed
  const queryMatch = trimmed.match(/[?&](?:code|checkInCode)=([^&#]+)/i)
  if (queryMatch?.[1]) {
    try {
      candidate = decodeURIComponent(queryMatch[1])
    } catch {
      candidate = queryMatch[1]
    }
  } else {
    try {
      const url = new URL(trimmed)
      candidate = url.searchParams.get('code') ?? url.searchParams.get('checkInCode') ?? trimmed
    } catch {
      // Not a URL; keep raw string.
    }
  }

  const normalized = candidate.toUpperCase().replace(/[^A-Z0-9]/g, '')
  return normalized.length === 8 ? normalized : ''
}
