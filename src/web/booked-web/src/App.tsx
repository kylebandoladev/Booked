function App() {
  return (
    <main className="min-h-screen bg-slate-950 text-slate-100">
      <section className="mx-auto flex max-w-5xl flex-col gap-8 px-6 py-16">
        <p className="inline-flex w-fit rounded-full border border-cyan-400/30 bg-cyan-400/10 px-3 py-1 text-sm font-medium text-cyan-300">
          Booked Starter
        </p>
        <div className="space-y-4">
          <h1 className="text-4xl font-bold tracking-tight sm:text-5xl">
            Booking platform foundation is ready
          </h1>
          <p className="max-w-2xl text-slate-300">
            React + TypeScript + Tailwind is now scaffolded. Next we can build
            the authentication flows for Customer, Organization, and hidden
            Admin login.
          </p>
        </div>
        <div className="grid gap-4 sm:grid-cols-3">
          <div className="rounded-2xl border border-slate-800 bg-slate-900 p-4">
            <p className="text-sm text-slate-400">Portal</p>
            <p className="mt-1 font-semibold">Customer</p>
          </div>
          <div className="rounded-2xl border border-slate-800 bg-slate-900 p-4">
            <p className="text-sm text-slate-400">Portal</p>
            <p className="mt-1 font-semibold">Organization</p>
          </div>
          <div className="rounded-2xl border border-slate-800 bg-slate-900 p-4">
            <p className="text-sm text-slate-400">Portal</p>
            <p className="mt-1 font-semibold">Admin (hidden entry)</p>
          </div>
        </div>
      </section>
    </main>
  )
}

export default App
