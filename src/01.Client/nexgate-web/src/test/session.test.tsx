import { act, screen, waitFor } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { api } from '@/api/client'
import { renew, RenewBeforeSeconds, restore, sessionStore, signIn } from '@/session/session'
import { orgA, orgB } from './fakeApi'
import { renderApp, signInAt } from './render'
import { fake } from './setup'

describe('Inicio de sesión y sesión', () => {
  it('inicia sesión con la cookie de renovación y muestra la organización', async () => {
    await signInAt()

    expect(await screen.findByText(`Hola, ${fake.email}`)).toBeInTheDocument()
    expect(screen.getByText(`Estás trabajando en ${orgA.name}.`)).toBeInTheDocument()
    expect(fake.signInBodies[0]).toMatchObject({ refreshTokenInCookie: true })
  })

  it('con credenciales inválidas muestra un único mensaje', async () => {
    const { user } = renderApp()
    await screen.findByRole('button', { name: 'Ingresar' })
    await user.type(screen.getByLabelText('Correo electrónico'), 'otro@nexgate.test')
    await user.type(screen.getByLabelText('Contraseña'), 'mala')
    await user.click(screen.getByRole('button', { name: 'Ingresar' }))

    expect(await screen.findByText('El correo o la contraseña no son válidos.')).toBeInTheDocument()
  })

  it('con demasiados intentos pide esperar un minuto', async () => {
    fake.signInStatus = 429
    await signInAt()

    expect(await screen.findByText('Demasiados intentos. Probá de nuevo en un minuto.')).toBeInTheDocument()
  })

  it('al recargar restaura la sesión sin pedir credenciales', async () => {
    fake.sessionValid = true
    fake.activeOrganization = orgA.id
    renderApp('/sitios')

    expect(await screen.findByRole('heading', { name: 'Sitios' })).toBeInTheDocument()
    expect(fake.signInBodies).toHaveLength(0)
    expect(fake.refreshBodies[0]).toEqual({ refreshToken: null, organizationId: null })
  })

  it('no guarda tokens en el almacenamiento del navegador', async () => {
    await signInAt()
    await screen.findByText(`Hola, ${fake.email}`)

    const stored = [...Object.values(window.localStorage), ...Object.values(window.sessionStorage), document.cookie].join('|')
    expect(stored).not.toContain('access-')
    expect(window.localStorage.length + window.sessionStorage.length).toBe(0)
  })

  it('cuando la sesión ya no se renueva vuelve al inicio de sesión', async () => {
    await signInAt()
    await screen.findByText(`Hola, ${fake.email}`)
    fake.sessionValid = false

    await act(() => renew())

    expect(await screen.findByText('Tu sesión terminó. Volvé a iniciar sesión.')).toBeInTheDocument()
  })

  it('al cerrar sesión revoca la sesión y recargar no la restaura', async () => {
    const { user } = await signInAt()
    await screen.findByText(`Hola, ${fake.email}`)

    await user.click(screen.getByRole('button', { name: 'Menú de la cuenta' }))
    await user.click(await screen.findByRole('menuitem', { name: 'Cerrar sesión' }))

    expect(await screen.findByRole('button', { name: 'Ingresar' })).toBeInTheDocument()
    expect(fake.logoutCount).toBe(1)
    await act(() => restore())
    expect(sessionStore.get()).toEqual({ status: 'signedOut', expired: false })
  })

  it('sin conexión al abrir muestra el aviso y permite reintentar', async () => {
    fake.sessionValid = true
    fake.activeOrganization = orgA.id
    const realFetch = globalThis.fetch
    const offline = vi.spyOn(globalThis, 'fetch').mockRejectedValue(new TypeError('Failed to fetch'))
    const { user } = renderApp()

    expect(await screen.findByText('Sin conexión')).toBeInTheDocument()
    offline.mockImplementation(realFetch)
    await user.click(screen.getByRole('button', { name: 'Reintentar' }))
    expect(await screen.findByText(`Hola, ${fake.email}`)).toBeInTheDocument()
  })
})

describe('Renovación', () => {
  it('se programa antes del vencimiento del token de acceso', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true })
    try {
      fake.expiresIn = 120
      expect(await signIn(fake.email, fake.password)).toBeNull()
      const issued = fake.tokenCount

      await vi.advanceTimersByTimeAsync((120 - RenewBeforeSeconds) * 1000 + 50)

      await waitFor(() => expect(fake.tokenCount).toBe(issued + 1))
    } finally {
      vi.useRealTimers()
    }
  })

  it('nunca hace dos renovaciones a la vez (el servidor revocaría la sesión)', async () => {
    fake.sessionValid = true
    fake.activeOrganization = orgA.id
    let inFlight = 0
    let maxInFlight = 0
    const realFetch = globalThis.fetch
    vi.spyOn(globalThis, 'fetch').mockImplementation(async (input, init) => {
      const refresh = input instanceof Request && input.url.endsWith('/api/v1/auth/refresh')
      inFlight += refresh ? 1 : 0
      maxInFlight = Math.max(maxInFlight, inFlight)
      try {
        return await realFetch(input, init)
      } finally {
        inFlight -= refresh ? 1 : 0
      }
    })

    await act(() => Promise.all([restore(), restore()]))
    await act(() => Promise.all([renew(), renew(), restore()]))

    expect(maxInFlight).toBe(1)
    expect(fake.refreshBodies).toHaveLength(3)
    expect(sessionStore.get().status).toBe('active')
  })

  it('ante un 401 renueva una sola vez y reintenta la operación', async () => {
    expect(await signIn(fake.email, fake.password)).toBeNull()
    fake.accessToken = 'otro-token'
    const before = fake.refreshBodies.length

    const result = await api.GET('/api/v1/me')

    expect(result.response.status).toBe(200)
    expect(fake.refreshBodies.length).toBe(before + 1)
  })
})

describe('Organización activa', () => {
  it('con varias membresías pide elegir y entra en la elegida', async () => {
    fake.organizations = [orgA, orgB]
    const { user } = await signInAt()

    await user.click(await screen.findByRole('button', { name: orgB.name }))

    expect(await screen.findByText(`Estás trabajando en ${orgB.name}.`)).toBeInTheDocument()
    expect(fake.refreshBodies.at(-1)).toEqual({ refreshToken: null, organizationId: orgB.id })
  })

  it('al cambiar de organización vacía los datos de la anterior', async () => {
    fake.organizations = [orgA, orgB]
    fake.roles[orgB.id] = 'resident'
    fake.addSite('Torre Alvear', orgA.id)
    fake.addSite('Lote 14', orgB.id)
    fake.sessionValid = true
    fake.activeOrganization = orgA.id
    const { user, queryClient } = renderApp('/sitios')
    expect(await screen.findByText('Torre Alvear')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Menú de la cuenta' }))
    await user.click(await screen.findByRole('menuitem', { name: orgB.name }))

    expect(await screen.findByText('Lote 14')).toBeInTheDocument()
    expect(screen.queryByText('Torre Alvear')).not.toBeInTheDocument()
    expect(queryClient.getQueryData(['org', orgA.id, 'sites'])).toBeUndefined()
  })

  it('si la membresía terminó pide elegir otra', async () => {
    fake.organizations = [orgA, orgB]
    fake.sessionValid = true
    fake.activeOrganization = orgA.id
    renderApp()
    await screen.findByText(`Estás trabajando en ${orgA.name}.`)
    fake.organizations = [orgB, { id: '33333333-3333-4333-8333-333333333333', name: 'Otra' }]

    await act(() => renew())

    expect(await screen.findByText('Ya no tenés acceso a esa organización. Elegí otra.')).toBeInTheDocument()
  })
})
