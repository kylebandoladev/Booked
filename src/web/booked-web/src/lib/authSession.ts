import type { AuthResponse, AuthToken, UserInfo } from '../types/auth'

const SESSION_KEY = 'booked.auth.session'

export interface StoredAuthSession {
  token: AuthToken
  user?: UserInfo | null
}

export function getStoredSession(): StoredAuthSession | null {
  if (typeof window === 'undefined') {
    return null
  }

  const raw = window.localStorage.getItem(SESSION_KEY)
  if (!raw) {
    return null
  }

  try {
    return JSON.parse(raw) as StoredAuthSession
  } catch {
    window.localStorage.removeItem(SESSION_KEY)
    return null
  }
}

export function saveSessionFromAuthResponse(response: AuthResponse): void {
  if (typeof window === 'undefined') {
    return
  }

  if (!response.token?.accessToken) {
    return
  }

  const session: StoredAuthSession = {
    token: response.token,
    user: response.user ?? null,
  }

  window.localStorage.setItem(SESSION_KEY, JSON.stringify(session))
}

export function clearSession(): void {
  if (typeof window === 'undefined') {
    return
  }

  window.localStorage.removeItem(SESSION_KEY)
}

export function getAccessToken(): string | null {
  return getStoredSession()?.token?.accessToken ?? null
}
