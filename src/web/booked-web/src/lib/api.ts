const configuredBaseUrl = import.meta.env.VITE_API_BASE_URL as string | undefined

export const API_BASE_URL = (configuredBaseUrl ?? 'http://localhost:5154').replace(/\/$/, '')

async function parseResponse(response: Response): Promise<unknown> {
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

export async function getJson<T>(path: string): Promise<T> {
  const response = await fetch(`${API_BASE_URL}${path}`)
  const payload = await parseResponse(response)

  if (!response.ok) {
    const message =
      typeof payload === 'object' && payload && 'message' in payload
        ? String((payload as { message?: unknown }).message)
        : `${response.status} ${response.statusText}`
    throw new Error(message)
  }

  return payload as T
}

export async function postJson<TRequest, TResponse>(path: string, body: TRequest): Promise<TResponse> {
  const response = await fetch(`${API_BASE_URL}${path}`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(body),
  })

  const payload = await parseResponse(response)

  if (!response.ok) {
    const message =
      typeof payload === 'object' && payload && 'message' in payload
        ? String((payload as { message?: unknown }).message)
        : `${response.status} ${response.statusText}`
    throw new Error(message)
  }

  return payload as TResponse
}