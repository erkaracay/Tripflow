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

  if (!response.ok) {
    const message =
      data && typeof data === 'object' && 'message' in data
        ? String((data as { message?: string }).message ?? 'Request failed')
        : response.statusText

    throw new Error(message)
  }

  return data as T
}

export const apiGet = async <T>(path: string): Promise<T> => {
  const response = await fetch(buildUrl(path), {
    headers: {
      Accept: 'application/json',
    },
  })

  return handleResponse<T>(response)
}

export const apiPost = async <T>(path: string, body: unknown): Promise<T> => {
  const response = await fetch(buildUrl(path), {
    method: 'POST',
    headers: {
      Accept: 'application/json',
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(body),
  })

  return handleResponse<T>(response)
}

export const apiPut = async <T>(path: string, body: unknown): Promise<T> => {
  const response = await fetch(buildUrl(path), {
    method: 'PUT',
    headers: {
      Accept: 'application/json',
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(body),
  })

  return handleResponse<T>(response)
}
