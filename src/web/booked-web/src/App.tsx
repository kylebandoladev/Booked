import { useMemo, useState } from 'react'
import {
  adminLogin,
  customerLogin,
  customerRegister,
  getHealth,
  organizationLogin,
  organizationRegister,
} from './lib/authApi'
import { API_BASE_URL } from './lib/api'
import type { AuthResponse } from './types/auth'

type Portal = 'customer' | 'organization'
type Mode = 'register' | 'login'

function App() {
  const isAdminRoute = useMemo(() => window.location.pathname.toLowerCase() === '/admin-login', [])

  const [portal, setPortal] = useState<Portal>('customer')
  const [mode, setMode] = useState<Mode>('register')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [success, setSuccess] = useState('')
  const [response, setResponse] = useState<AuthResponse | null>(null)
  const [health, setHealth] = useState('')

  const [customerRegisterForm, setCustomerRegisterForm] = useState({
    email: '',
    password: '',
    fullName: '',
  })
  const [customerLoginForm, setCustomerLoginForm] = useState({ email: '', password: '' })

  const [organizationRegisterForm, setOrganizationRegisterForm] = useState({
    email: '',
    password: '',
    organizationName: '',
    subscriptionType: 'monthly' as 'monthly' | 'quarterly' | 'yearly',
  })
  const [organizationLoginForm, setOrganizationLoginForm] = useState({ email: '', password: '' })

  const [adminForm, setAdminForm] = useState({ adminKey: '', password: '' })

  async function withSubmit(handler: () => Promise<AuthResponse>) {
    setBusy(true)
    setError('')
    setSuccess('')

    try {
      const data = await handler()
      setResponse(data)
      setSuccess(data.message)
    } catch (submissionError) {
      const message = submissionError instanceof Error ? submissionError.message : 'Request failed'
      setError(message)
      setResponse(null)
    } finally {
      setBusy(false)
    }
  }

  async function checkHealth() {
    setHealth('Checking...')
    try {
      const data = await getHealth()
      setHealth(`${data.status} (${data.service})`)
    } catch (healthError) {
      const message = healthError instanceof Error ? healthError.message : 'Health check failed'
      setHealth(`Failed: ${message}`)
    }
  }

  return (
    <main className="min-h-screen bg-slate-950 text-slate-100">
      <section className="mx-auto flex w-full max-w-3xl flex-col gap-6 px-6 py-10">
        <div className="rounded-2xl border border-slate-800 bg-slate-900/70 p-5">
          <h1 className="text-2xl font-bold tracking-tight">Booked Auth Test Client</h1>
          <p className="mt-2 text-sm text-slate-300">API Base: {API_BASE_URL}</p>
          <div className="mt-4 flex flex-wrap items-center gap-3">
            <button
              className="rounded-lg bg-cyan-500 px-3 py-2 text-sm font-semibold text-slate-950 hover:bg-cyan-400"
              type="button"
              onClick={checkHealth}
            >
              Check Health
            </button>
            <p className="text-sm text-slate-300">{health}</p>
          </div>
        </div>

        {isAdminRoute ? (
          <div className="rounded-2xl border border-slate-800 bg-slate-900 p-5">
            <h2 className="text-lg font-semibold">Admin Login</h2>
            <form
              className="mt-4 grid gap-3"
              onSubmit={(e) => {
                e.preventDefault()
                withSubmit(() => adminLogin(adminForm))
              }}
            >
              <input
                className="rounded-lg border border-slate-700 bg-slate-950 px-3 py-2"
                placeholder="Admin Key"
                value={adminForm.adminKey}
                onChange={(e) => setAdminForm((prev) => ({ ...prev, adminKey: e.target.value }))}
                required
              />
              <input
                className="rounded-lg border border-slate-700 bg-slate-950 px-3 py-2"
                type="password"
                placeholder="Password"
                value={adminForm.password}
                onChange={(e) => setAdminForm((prev) => ({ ...prev, password: e.target.value }))}
                required
              />
              <button
                className="rounded-lg bg-cyan-500 px-3 py-2 font-semibold text-slate-950 hover:bg-cyan-400 disabled:opacity-50"
                type="submit"
                disabled={busy}
              >
                {busy ? 'Submitting...' : 'Login'}
              </button>
            </form>
          </div>
        ) : (
          <div className="rounded-2xl border border-slate-800 bg-slate-900 p-5">
            <div className="flex flex-wrap gap-2">
              <button
                className={`rounded-lg px-3 py-2 text-sm font-semibold ${portal === 'customer' ? 'bg-cyan-500 text-slate-950' : 'bg-slate-800 text-slate-200'}`}
                type="button"
                onClick={() => setPortal('customer')}
              >
                Customer
              </button>
              <button
                className={`rounded-lg px-3 py-2 text-sm font-semibold ${portal === 'organization' ? 'bg-cyan-500 text-slate-950' : 'bg-slate-800 text-slate-200'}`}
                type="button"
                onClick={() => setPortal('organization')}
              >
                Organization
              </button>
              <button
                className={`rounded-lg px-3 py-2 text-sm font-semibold ${mode === 'register' ? 'bg-indigo-500 text-white' : 'bg-slate-800 text-slate-200'}`}
                type="button"
                onClick={() => setMode('register')}
              >
                Register
              </button>
              <button
                className={`rounded-lg px-3 py-2 text-sm font-semibold ${mode === 'login' ? 'bg-indigo-500 text-white' : 'bg-slate-800 text-slate-200'}`}
                type="button"
                onClick={() => setMode('login')}
              >
                Login
              </button>
            </div>

            {portal === 'customer' && mode === 'register' && (
              <form
                className="mt-4 grid gap-3"
                onSubmit={(e) => {
                  e.preventDefault()
                  withSubmit(() => customerRegister(customerRegisterForm))
                }}
              >
                <input
                  className="rounded-lg border border-slate-700 bg-slate-950 px-3 py-2"
                  placeholder="Full Name"
                  value={customerRegisterForm.fullName}
                  onChange={(e) =>
                    setCustomerRegisterForm((prev) => ({ ...prev, fullName: e.target.value }))
                  }
                  required
                />
                <input
                  className="rounded-lg border border-slate-700 bg-slate-950 px-3 py-2"
                  type="email"
                  placeholder="Email"
                  value={customerRegisterForm.email}
                  onChange={(e) => setCustomerRegisterForm((prev) => ({ ...prev, email: e.target.value }))}
                  required
                />
                <input
                  className="rounded-lg border border-slate-700 bg-slate-950 px-3 py-2"
                  type="password"
                  placeholder="Password"
                  value={customerRegisterForm.password}
                  onChange={(e) =>
                    setCustomerRegisterForm((prev) => ({ ...prev, password: e.target.value }))
                  }
                  required
                />
                <button
                  className="rounded-lg bg-cyan-500 px-3 py-2 font-semibold text-slate-950 hover:bg-cyan-400 disabled:opacity-50"
                  type="submit"
                  disabled={busy}
                >
                  {busy ? 'Submitting...' : 'Register Customer'}
                </button>
              </form>
            )}

            {portal === 'customer' && mode === 'login' && (
              <form
                className="mt-4 grid gap-3"
                onSubmit={(e) => {
                  e.preventDefault()
                  withSubmit(() => customerLogin(customerLoginForm))
                }}
              >
                <input
                  className="rounded-lg border border-slate-700 bg-slate-950 px-3 py-2"
                  type="email"
                  placeholder="Email"
                  value={customerLoginForm.email}
                  onChange={(e) => setCustomerLoginForm((prev) => ({ ...prev, email: e.target.value }))}
                  required
                />
                <input
                  className="rounded-lg border border-slate-700 bg-slate-950 px-3 py-2"
                  type="password"
                  placeholder="Password"
                  value={customerLoginForm.password}
                  onChange={(e) => setCustomerLoginForm((prev) => ({ ...prev, password: e.target.value }))}
                  required
                />
                <button
                  className="rounded-lg bg-cyan-500 px-3 py-2 font-semibold text-slate-950 hover:bg-cyan-400 disabled:opacity-50"
                  type="submit"
                  disabled={busy}
                >
                  {busy ? 'Submitting...' : 'Login Customer'}
                </button>
              </form>
            )}

            {portal === 'organization' && mode === 'register' && (
              <form
                className="mt-4 grid gap-3"
                onSubmit={(e) => {
                  e.preventDefault()
                  withSubmit(() => organizationRegister(organizationRegisterForm))
                }}
              >
                <input
                  className="rounded-lg border border-slate-700 bg-slate-950 px-3 py-2"
                  placeholder="Organization Name"
                  value={organizationRegisterForm.organizationName}
                  onChange={(e) =>
                    setOrganizationRegisterForm((prev) => ({
                      ...prev,
                      organizationName: e.target.value,
                    }))
                  }
                  required
                />
                <input
                  className="rounded-lg border border-slate-700 bg-slate-950 px-3 py-2"
                  type="email"
                  placeholder="Email"
                  value={organizationRegisterForm.email}
                  onChange={(e) =>
                    setOrganizationRegisterForm((prev) => ({ ...prev, email: e.target.value }))
                  }
                  required
                />
                <input
                  className="rounded-lg border border-slate-700 bg-slate-950 px-3 py-2"
                  type="password"
                  placeholder="Password"
                  value={organizationRegisterForm.password}
                  onChange={(e) =>
                    setOrganizationRegisterForm((prev) => ({ ...prev, password: e.target.value }))
                  }
                  required
                />
                <select
                  className="rounded-lg border border-slate-700 bg-slate-950 px-3 py-2"
                  value={organizationRegisterForm.subscriptionType}
                  onChange={(e) =>
                    setOrganizationRegisterForm((prev) => ({
                      ...prev,
                      subscriptionType: e.target.value as 'monthly' | 'quarterly' | 'yearly',
                    }))
                  }
                >
                  <option value="monthly">monthly</option>
                  <option value="quarterly">quarterly</option>
                  <option value="yearly">yearly</option>
                </select>
                <button
                  className="rounded-lg bg-cyan-500 px-3 py-2 font-semibold text-slate-950 hover:bg-cyan-400 disabled:opacity-50"
                  type="submit"
                  disabled={busy}
                >
                  {busy ? 'Submitting...' : 'Register Organization'}
                </button>
              </form>
            )}

            {portal === 'organization' && mode === 'login' && (
              <form
                className="mt-4 grid gap-3"
                onSubmit={(e) => {
                  e.preventDefault()
                  withSubmit(() => organizationLogin(organizationLoginForm))
                }}
              >
                <input
                  className="rounded-lg border border-slate-700 bg-slate-950 px-3 py-2"
                  type="email"
                  placeholder="Email"
                  value={organizationLoginForm.email}
                  onChange={(e) =>
                    setOrganizationLoginForm((prev) => ({ ...prev, email: e.target.value }))
                  }
                  required
                />
                <input
                  className="rounded-lg border border-slate-700 bg-slate-950 px-3 py-2"
                  type="password"
                  placeholder="Password"
                  value={organizationLoginForm.password}
                  onChange={(e) =>
                    setOrganizationLoginForm((prev) => ({ ...prev, password: e.target.value }))
                  }
                  required
                />
                <button
                  className="rounded-lg bg-cyan-500 px-3 py-2 font-semibold text-slate-950 hover:bg-cyan-400 disabled:opacity-50"
                  type="submit"
                  disabled={busy}
                >
                  {busy ? 'Submitting...' : 'Login Organization'}
                </button>
              </form>
            )}
          </div>
        )}

        {(error || success) && (
          <div
            className={`rounded-2xl border p-4 ${
              error ? 'border-rose-400/40 bg-rose-500/10 text-rose-200' : 'border-emerald-400/40 bg-emerald-500/10 text-emerald-200'
            }`}
          >
            {error || success}
          </div>
        )}

        {response && (
          <pre className="overflow-auto rounded-2xl border border-slate-800 bg-slate-900 p-4 text-xs text-slate-200">
            {JSON.stringify(response, null, 2)}
          </pre>
        )}
      </section>
    </main>
  )
}

export default App
