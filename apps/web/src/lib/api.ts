import { clearToken, getAuthRole, getSelectedOrgId } from './auth'
import { pushToast } from './toast'
import type { AuthMeResponse, PortalLoginResponse, PortalMeResponse, PortalResolveEventResponse } from '../types'

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL as string

export const buildUrl = (path: string) => {
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
    const isPortal = path.startsWith('/e/')
    clearToken()
    fetch(buildUrl('/api/auth/logout'), { method: 'POST', credentials: 'include' }).catch(() => {})
    if (!isPortal) {
      pushToast({ key: 'toast.sessionExpired', tone: 'error' })
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

    const error = new Error(message) as Error & { status?: number; payload?: unknown }
    error.status = response.status
    error.payload = data
    throw error
  }

  return data as T
}

const handlePortalResponse = async <T>(response: Response): Promise<T> => {
  const data = await parseBody(response)

  if (!response.ok) {
    const message =
      data && typeof data === 'object' && 'message' in data
        ? String((data as { message?: string }).message ?? 'Request failed')
        : response.statusText

    const error = new Error(message) as Error & { status?: number; payload?: unknown }
    error.status = response.status
    error.payload = data
    throw error
  }

  return data as T
}

const throwApiError = (response: Response, data: unknown): never => {
  const message =
    data && typeof data === 'object' && 'message' in data
      ? String((data as { message?: string }).message ?? 'Request failed')
      : response.statusText

  const error = new Error(message) as Error & { status?: number; payload?: unknown }
  error.status = response.status
  error.payload = data
  throw error
}

const handleResponseWithPayload = async <T>(response: Response): Promise<T> => {
  const data = await parseBody(response)

  if (response.status === 401) {
    const path = globalThis.location?.pathname ?? ''
    const isPortal = path.startsWith('/e/')
    clearToken()
    fetch(buildUrl('/api/auth/logout'), { method: 'POST', credentials: 'include' }).catch(() => {})
    if (!isPortal) {
      pushToast({ key: 'toast.sessionExpired', tone: 'error' })
      if (path !== '/login') {
        globalThis.location?.assign('/login')
      }
    }
  }

  if (!response.ok) {
    throwApiError(response, data)
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

  const role = getAuthRole()
  if (role === 'SuperAdmin') {
    const orgId = getSelectedOrgId()
    if (orgId) {
      headers['X-Org-Id'] = orgId
    }
  }

  return headers
}

export const checkAuth = async (): Promise<AuthMeResponse | null> => {
  const res = await fetch(buildUrl('/api/auth/me'), {
    credentials: 'include',
    headers: { Accept: 'application/json' },
  })
  if (res.status === 401) return null
  if (!res.ok) {
    const data = await res.text()
    throw new Error(data || res.statusText)
  }
  return res.json() as Promise<AuthMeResponse>
}

export const apiGet = async <T>(path: string): Promise<T> => {
  const response = await fetch(buildUrl(path), {
    credentials: 'include',
    headers: buildHeaders(),
  })

  return handleResponse<T>(response)
}

export const apiPost = async <T>(path: string, body: unknown): Promise<T> => {
  const response = await fetch(buildUrl(path), {
    method: 'POST',
    credentials: 'include',
    headers: buildHeaders('application/json'),
    body: JSON.stringify(body),
  })

  return handleResponse<T>(response)
}

export const apiPostWithPayload = async <T>(path: string, body: unknown): Promise<T> => {
  const response = await fetch(buildUrl(path), {
    method: 'POST',
    credentials: 'include',
    headers: buildHeaders('application/json'),
    body: JSON.stringify(body),
  })

  return handleResponseWithPayload<T>(response)
}

export const apiPostWithHeaders = async <T>(
  path: string,
  body: unknown
): Promise<{ data: T; headers: Headers }> => {
  const response = await fetch(buildUrl(path), {
    method: 'POST',
    credentials: 'include',
    headers: buildHeaders('application/json'),
    body: JSON.stringify(body),
  })

  const data = await handleResponse<T>(response)
  return { data, headers: response.headers }
}

export const apiPut = async <T>(path: string, body: unknown): Promise<T> => {
  const response = await fetch(buildUrl(path), {
    method: 'PUT',
    credentials: 'include',
    headers: buildHeaders('application/json'),
    body: JSON.stringify(body),
  })

  return handleResponse<T>(response)
}

export const apiPutWithHeaders = async <T>(
  path: string,
  body: unknown
): Promise<{ data: T; headers: Headers }> => {
  const response = await fetch(buildUrl(path), {
    method: 'PUT',
    credentials: 'include',
    headers: buildHeaders('application/json'),
    body: JSON.stringify(body),
  })

  const data = await handleResponse<T>(response)
  return { data, headers: response.headers }
}

export const apiPatch = async <T>(path: string, body: unknown): Promise<T> => {
  const response = await fetch(buildUrl(path), {
    method: 'PATCH',
    credentials: 'include',
    headers: buildHeaders('application/json'),
    body: JSON.stringify(body),
  })

  return handleResponse<T>(response)
}

export const apiPatchWithPayload = async <T>(path: string, body: unknown): Promise<T> => {
  const response = await fetch(buildUrl(path), {
    method: 'PATCH',
    credentials: 'include',
    headers: buildHeaders('application/json'),
    body: JSON.stringify(body),
  })

  return handleResponseWithPayload<T>(response)
}

export const apiDelete = async <T>(path: string): Promise<T> => {
  const response = await fetch(buildUrl(path), {
    method: 'DELETE',
    credentials: 'include',
    headers: buildHeaders(),
  })

  return handleResponse<T>(response)
}

export const apiDownload = async (path: string): Promise<Blob> => {
  const response = await fetch(buildUrl(path), {
    credentials: 'include',
    headers: buildHeaders(),
  })

  if (!response.ok) {
    const data = await parseBody(response)
    throwApiError(response, data)
  }

  return response.blob()
}

export const apiPostForm = async <T>(path: string, formData: FormData): Promise<T> => {
  const response = await fetch(buildUrl(path), {
    method: 'POST',
    credentials: 'include',
    headers: buildHeaders(),
    body: formData,
  })

  return handleResponseWithPayload<T>(response)
}

const portalPost = async <T>(path: string, body: unknown, headers?: Record<string, string>): Promise<T> => {
  const response = await fetch(buildUrl(path), {
    method: 'POST',
    credentials: 'include',
    headers: { Accept: 'application/json', 'Content-Type': 'application/json', ...headers },
    body: JSON.stringify(body),
  })

  return handlePortalResponse<T>(response)
}

const portalGet = async <T>(path: string, headers?: Record<string, string>): Promise<T> => {
  const response = await fetch(buildUrl(path), {
    credentials: 'include',
    headers: { Accept: 'application/json', ...headers },
  })

  return handlePortalResponse<T>(response)
}

export const portalLogin = async (
  eventAccessCode: string,
  tcNo: string
): Promise<PortalLoginResponse> => {
  return portalPost<PortalLoginResponse>('/api/portal/login', { eventAccessCode, tcNo })
}

export const portalGetMe = async (sessionToken?: string): Promise<PortalMeResponse> => {
  const headers = sessionToken ? { 'X-Portal-Session': sessionToken } : undefined
  return portalGet<PortalMeResponse>('/api/portal/me', headers)
}

export const checkPortalSession = async (): Promise<PortalMeResponse | null> => {
  try {
    const res = await fetch(buildUrl('/api/portal/me'), {
      credentials: 'include',
      headers: { Accept: 'application/json' },
    })
    if (res.status === 401) return null
    if (!res.ok) throw new Error(await res.text() || res.statusText)
    return res.json() as Promise<PortalMeResponse>
  } catch (err) {
    // Network errors or 401 are expected when not logged in - return null silently
    if (err instanceof TypeError || (err instanceof Error && /Failed to fetch|NetworkError/i.test(err.message))) {
      return null
    }
    throw err
  }
}

export const portalResolveEvent = async (eventAccessCode: string): Promise<PortalResolveEventResponse> => {
  return portalGet<PortalResolveEventResponse>(
    `/api/portal/resolve?eventAccessCode=${encodeURIComponent(eventAccessCode)}`
  )
}

export const portalLogout = async (): Promise<void> => {
  await fetch(buildUrl('/api/portal/logout'), { method: 'POST', credentials: 'include' })
}
