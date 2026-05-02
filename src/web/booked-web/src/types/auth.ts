export interface AuthToken {
  accessToken: string
  refreshToken: string
  expiresAt: string
}

export interface UserInfo {
  id: string
  email: string
  role: string
  name?: string | null
  organizationName?: string | null
}

export interface AuthResponse {
  success: boolean
  message: string
  token?: AuthToken | null
  user?: UserInfo | null
}

export interface CustomerRegisterRequest {
  email: string
  password: string
  fullName: string
}

export interface CustomerLoginRequest {
  email: string
  password: string
}

export interface OrganizationRegisterRequest {
  email: string
  password: string
  organizationName: string
  subscriptionType: 'monthly' | 'quarterly' | 'yearly'
}

export interface OrganizationLoginRequest {
  email: string
  password: string
}

export interface AdminLoginRequest {
  adminKey: string
  password: string
}

export interface HealthResponse {
  status: string
  service: string
}