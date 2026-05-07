const configuredBaseUrl = import.meta.env.VITE_API_BASE_URL as string | undefined
import { getAccessToken } from './authSession'

export const API_BASE_URL = (configuredBaseUrl ?? 'http://localhost:5154').replace(/\/$/, '')

function buildHeaders(): HeadersInit {
  const headers: Record<string, string> = {
    'Content-Type': 'application/json',
  }

  const token = getAccessToken()
  if (token) {
    headers.Authorization = `Bearer ${token}`
  }

  return headers
}

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
  if (import.meta.env.DEV) console.debug('[API] GET', `${API_BASE_URL}${path}`)
  const response = await fetch(`${API_BASE_URL}${path}`, {
    headers: buildHeaders(),
  })
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
  const url = `${API_BASE_URL}${path}`
  if (import.meta.env.DEV) console.debug('[API] POST', url, body)

  const response = await fetch(url, {
    method: 'POST',
    headers: buildHeaders(),
    body: JSON.stringify(body),
  })

  const payload = await parseResponse(response)

  if (!response.ok) {
    const serverMessage =
      typeof payload === 'object' && payload && 'message' in payload
        ? String((payload as { message?: unknown }).message)
        : typeof payload === 'string'
        ? payload
        : `${response.status} ${response.statusText}`

    const errMsg = `HTTP ${response.status}: ${serverMessage}`
    if (import.meta.env.DEV) console.debug('[API] ERROR', errMsg, payload)
    throw new Error(errMsg)
  }

  if (import.meta.env.DEV) console.debug('[API] RESPONSE', response.status, payload)

  return payload as TResponse
}