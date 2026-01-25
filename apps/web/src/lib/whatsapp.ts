export const buildWhatsAppUrl = (phoneE164: string, message: string) => {
  const digits = phoneE164.replace(/\D/g, '')
  if (!digits) {
    return ''
  }

  const encodedMessage = encodeURIComponent(message)
  return `https://wa.me/${digits}?text=${encodedMessage}`
}
