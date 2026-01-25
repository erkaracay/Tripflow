import { ref } from 'vue'

const TOKEN_KEY = 'tripflow_token'
const ORG_KEY = 'tripflow_org'

const readStorage = (key: string) => globalThis.localStorage?.getItem(key) ?? ''

const tokenRef = ref(readStorage(TOKEN_KEY))
const orgRef = ref(readStorage(ORG_KEY))

const decodeBase64Url = (value: string) => {
  const base64 = value.replace(/-/g, '+').replace(/_/g, '/')
  const padded = base64.padEnd(base64.length + ((4 - (base64.length % 4)) % 4), '=')
  try {
    return atob(padded)
  } catch {
    return ''
  }
}

export type JwtPayload = Record<string, unknown>

export const tokenState = tokenRef
export const orgState = orgRef

export const getToken = () => tokenRef.value

export const setToken = (token: string) => {
  tokenRef.value = token
  globalThis.localStorage?.setItem(TOKEN_KEY, token)
}

export const clearToken = () => {
  tokenRef.value = ''
  clearSelectedOrgId()
  globalThis.localStorage?.removeItem(TOKEN_KEY)
}

export const getTokenPayload = (token: string): JwtPayload | null => {
  const parts = token.split('.')
  const payloadPart = parts[1]
  if (!payloadPart) {
    return null
  }

  const decoded = decodeBase64Url(payloadPart)
  if (!decoded) {
    return null
  }

  try {
    return JSON.parse(decoded) as JwtPayload
  } catch {
    return null
  }
}

export const getTokenRole = (token: string): string | null => {
  const payload = getTokenPayload(token)
  if (!payload) {
    return null
  }

  const role =
    payload.role ??
    payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] ??
    payload.roles

  if (Array.isArray(role)) {
    return typeof role[0] === 'string' ? role[0] : null
  }

  return typeof role === 'string' ? role : null
}

export const getTokenOrgId = (token: string): string | null => {
  const payload = getTokenPayload(token)
  if (!payload) {
    return null
  }

  const orgId = payload.orgId
  return typeof orgId === 'string' ? orgId : null
}

export const isTokenExpired = (token: string): boolean => {
  const payload = getTokenPayload(token)
  const exp = payload?.exp
  if (typeof exp !== 'number') {
    return false
  }

  const now = Math.floor(Date.now() / 1000)
  return exp <= now
}

export const getSelectedOrgId = () => orgRef.value

export const setSelectedOrgId = (orgId: string) => {
  orgRef.value = orgId
  globalThis.localStorage?.setItem(ORG_KEY, orgId)
}

export const clearSelectedOrgId = () => {
  orgRef.value = ''
  globalThis.localStorage?.removeItem(ORG_KEY)
}
