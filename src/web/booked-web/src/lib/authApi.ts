import { getJson, postJson } from './api'
import type {
  AdminLoginRequest,
  AuthResponse,
  CustomerLoginRequest,
  CustomerRegisterRequest,
  HealthResponse,
  OrganizationLoginRequest,
  OrganizationRegisterRequest,
  RefreshTokenRequest,
} from '../types/auth'

export function getHealth() {
  return getJson<HealthResponse>('/api/auth/health')
}

export function customerRegister(body: CustomerRegisterRequest) {
  return postJson<CustomerRegisterRequest, AuthResponse>('/api/auth/customer/register', body)
}

export function customerLogin(body: CustomerLoginRequest) {
  return postJson<CustomerLoginRequest, AuthResponse>('/api/auth/customer/login', body)
}

export function organizationRegister(body: OrganizationRegisterRequest) {
  return postJson<OrganizationRegisterRequest, AuthResponse>('/api/auth/organization/register', body)
}

export function organizationLogin(body: OrganizationLoginRequest) {
  return postJson<OrganizationLoginRequest, AuthResponse>('/api/auth/organization/login', body)
}

export function adminLogin(body: AdminLoginRequest) {
  return postJson<AdminLoginRequest, AuthResponse>('/api/auth/admin/login', body)
}

export function refreshToken(body: RefreshTokenRequest) {
  return postJson<RefreshTokenRequest, AuthResponse>('/api/auth/refresh', body)
}