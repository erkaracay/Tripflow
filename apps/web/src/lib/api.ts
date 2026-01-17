import { clearToken, getSelectedOrgId, getToken, getTokenRole, isTokenExpired } from './auth'
import { pushToast } from './toast'

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL as string

const buildUrl = (path: string) => {
  if (path.startsWith('http')) {
    return path
  }

  const base = API_BASE_URL?.replace(/\/$/, '') ?? ''
  const suffix = path.replace(/^\//, '')
  return `${base}/${suffix}`
}

const parseBody = async (response: Response) => {
  const text = await response.text()
  if (!text) {
    return null
  }

  try {
    return JSON.parse(text)
  } catch {
    return text
  }
}

const handleResponse = async <T>(response: Response): Promise<T> => {
  const data = await parseBody(response)

  if (response.status === 401) {
    const path = globalThis.location?.pathname ?? ''
    const isPortal = path.startsWith('/t/')
    clearToken()
    if (!isPortal) {
      pushToast('Oturum süren doldu, tekrar giriş yap', 'error')
      if (path !== '/login') {
        globalThis.location?.assign('/login')
      }
    }
  }

  if (!response.ok) {
    const message =
      data && typeof data === 'object' && 'message' in data
        ? String((data as { message?: string }).message ?? 'Request failed')
        : response.statusText

    throw new Error(message)
  }

  return data as T
}

const buildHeaders = (contentType?: string) => {
  const headers: Record<string, string> = {
    Accept: 'application/json',
  }

  if (contentType) {
    headers['Content-Type'] = contentType
  }

  const token = getToken()
  if (token && !isTokenExpired(token)) {
    headers.Authorization = `Bearer ${token}`

    const role = getTokenRole(token)
    if (role === 'SuperAdmin') {
      const orgId = getSelectedOrgId()
      if (orgId) {
        headers['X-Org-Id'] = orgId
      }
    }
  }

  return headers
}

export const apiGet = async <T>(path: string): Promise<T> => {
  const response = await fetch(buildUrl(path), {
    headers: buildHeaders(),
  })

  return handleResponse<T>(response)
}

export const apiPost = async <T>(path: string, body: unknown): Promise<T> => {
  const response = await fetch(buildUrl(path), {
    method: 'POST',
    headers: buildHeaders('application/json'),
    body: JSON.stringify(body),
  })

  return handleResponse<T>(response)
}

export const apiPut = async <T>(path: string, body: unknown): Promise<T> => {
  const response = await fetch(buildUrl(path), {
    method: 'PUT',
    headers: buildHeaders('application/json'),
    body: JSON.stringify(body),
  })

  return handleResponse<T>(response)
}

export const apiDelete = async <T>(path: string): Promise<T> => {
  const response = await fetch(buildUrl(path), {
    method: 'DELETE',
    headers: buildHeaders(),
  })

  return handleResponse<T>(response)
}
