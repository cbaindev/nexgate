import { act, fireEvent, screen, waitFor, within } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { orgA, type Role } from './fakeApi'
import { location, renderApp } from './render'
import { fake } from './setup'

function signedInAs(role: Role) {
  fake.roles[orgA.id] = role
  fake.sessionValid = true
  fake.activeOrganization = orgA.id
}

/** Torre Norte con dos puertas, un dispositivo, la titular de 5B y algunos eventos. */
function site() {
  const site = fake.addSite('Torre Norte')
  const root = fake.addSpace(site.id, 'Torre Norte', 'site')
  const a = fake.addSpace(site.id, 'Torre A', 'building', root)
  const gate = fake.addAccessPoint('Portón principal', root, null, 'gate')
  const hall = fake.addAccessPoint('Hall Torre A', a, root)
  const device = fake.addDevice(site.id, 'Controlador central', { accessPointIds: [gate.id, hall.id] })
  const owner = fake.addSubject(site.id, 'Titular 5B')
  return { site, gate, hall, device, owner }
}

const table = () => screen.findByRole('table', { name: 'Eventos de Torre Norte' })
const rows = async () => within(await table()).getAllByRole('row').slice(1)
const setValue = (element: HTMLElement, value: string) => fireEvent.change(element, { target: { value } })

afterEach(() => {
  vi.useRealTimers()
})

describe('Eventos', () => {
  it('lista los eventos del más reciente, con hora del sitio, puerta, persona, resultado con su razón y dispositivo', async () => {
    signedInAs('guard')
    const { site: s, gate, hall, device, owner } = site()
    fake.addEvent(device, gate, '2026-12-02T13:00:05Z', { subjectId: owner.id })
    fake.addEvent(device, hall, '2026-12-02T13:10:00Z', { decision: 'deny', reason: 'credential_invalid' })
    renderApp(`/eventos?sitio=${s.id}`)

    const [first, second] = await rows()
    await waitFor(() => expect(first).toHaveTextContent('Controlador central'))
    expect(first).toHaveTextContent('02/12/2026 10:10:00')
    expect(first).toHaveTextContent('Hall Torre A')
    expect(first).toHaveTextContent('Sin identificar')
    expect(first).toHaveTextContent('Denegado')
    expect(first).toHaveTextContent('Credencial desconocida, vencida o todavía no vigente.')
    await waitFor(() => expect(second).toHaveTextContent('Titular 5B'))
    expect(second).toHaveTextContent('02/12/2026 10:00:05')
    expect(second).toHaveTextContent('Portón principal')
    expect(second).toHaveTextContent('Permitido')
    expect(second).toHaveTextContent('Un permiso lo habilitaba.')
  })

  it('ofrece solo los sitios que el permiso de leer eventos cubre entero', async () => {
    signedInAs('guard')
    const { site: a } = site()
    const b = fake.addSite('Barrio Las Lomas')
    const lot = fake.addSpace(b.id, 'Barrio Las Lomas', 'site')
    fake.permissionsOverride = [{ name: 'events.read', organization: false, siteIds: [a.id], spaces: [{ siteId: b.id, spaceId: lot.id }] }]
    renderApp('/eventos')

    expect(await screen.findByText('Todavía no hay eventos en este sitio.')).toBeInTheDocument()
    expect(within(screen.getByRole('combobox', { name: 'Sitio' })).getAllByRole('option').map((o) => o.textContent)).toEqual(['Torre Norte'])
    expect(fake.requests.some((r) => r.includes(b.id))).toBe(false)
  })

  it('sin permiso de leer accesos no muestra personas ni las pide', async () => {
    signedInAs('guard')
    const { site: s, gate, device, owner } = site()
    fake.addEvent(device, gate, '2026-12-02T13:00:05Z', { subjectId: owner.id })
    fake.permissionsOverride = [
      { name: 'events.read', organization: true, siteIds: [], spaces: [] },
      { name: 'spaces.read', organization: true, siteIds: [], spaces: [] },
    ]
    renderApp(`/eventos?sitio=${s.id}`)

    const [first] = await rows()
    await waitFor(() => expect(first).toHaveTextContent('Portón principal'))
    expect(screen.queryByRole('columnheader', { name: 'Persona' })).not.toBeInTheDocument()
    expect(screen.queryByLabelText('Persona')).not.toBeInTheDocument()
    expect(first).not.toHaveTextContent('Titular 5B')
    expect(fake.requests.some((r) => r.includes('/subjects'))).toBe(false)
  })

  it('marca los eventos registrados sin conexión', async () => {
    signedInAs('guard')
    const { site: s, gate, device } = site()
    fake.addEvent(device, gate, '2026-12-02T13:00:00Z', { receivedAt: '2026-12-02T13:10:00Z' })
    fake.addEvent(device, gate, '2026-12-02T13:20:00Z', { receivedAt: '2026-12-02T13:20:01Z' })
    renderApp(`/eventos?sitio=${s.id}`)

    const [recent, late] = await rows()
    expect(late).toHaveTextContent('Registrado sin conexión')
    expect(recent).not.toHaveTextContent('Registrado sin conexión')
  })

  it('filtra por puerta y fechas, y los filtros quedan después de recargar', async () => {
    signedInAs('guard')
    const { site: s, gate, hall, device } = site()
    fake.addEvent(device, gate, '2026-12-01T13:00:00Z')
    fake.addEvent(device, gate, '2026-12-02T13:00:00Z')
    fake.addEvent(device, hall, '2026-12-02T13:05:00Z')
    fake.addEvent(device, gate, '2026-12-03T13:00:00Z')
    const { user, unmount } = renderApp(`/eventos?sitio=${s.id}`)
    expect(await rows()).toHaveLength(4)

    await user.selectOptions(await screen.findByLabelText('Puerta'), gate.id)
    setValue(screen.getByLabelText('Desde'), '2026-12-02T00:00')
    setValue(screen.getByLabelText('Hasta'), '2026-12-03T00:00')

    await waitFor(async () => expect(await rows()).toHaveLength(1))
    expect((await rows())[0]).toHaveTextContent('02/12/2026 10:00:00')
    expect(fake.requests.at(-1)).toContain('from=2026-12-02T03%3A00%3A00Z')

    const address = location.current
    expect(address).toContain(`puerta=${gate.id}`)
    unmount()
    renderApp(address)
    await waitFor(async () => expect(await rows()).toHaveLength(1))
    expect(screen.getByLabelText('Puerta')).toHaveValue(gate.id)
    expect(screen.getByText('Sin actualización automática: el rango tiene fin.')).toBeInTheDocument()
  })

  it('carga más eventos sin repetir', async () => {
    signedInAs('guard')
    const { site: s, gate, device } = site()
    for (let i = 0; i < 60; i++) {
      fake.addEvent(device, gate, new Date(Date.UTC(2026, 11, 2, 13, i)).toISOString())
    }
    const { user } = renderApp(`/eventos?sitio=${s.id}`)

    expect(await rows()).toHaveLength(50)
    await user.click(screen.getByRole('button', { name: 'Cargar más eventos' }))

    await waitFor(async () => expect(await rows()).toHaveLength(60))
    const times = (await rows()).map((r) => r.querySelector('.tabular-nums')?.textContent)
    expect(new Set(times).size).toBe(60)
    expect(screen.getByText('No hay más eventos.')).toBeInTheDocument()
  })

  it('cada 15 segundos suma arriba los eventos nuevos, resaltados y anunciados', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true })
    signedInAs('guard')
    const { site: s, gate, hall, device } = site()
    fake.addEvent(device, gate, '2026-12-02T13:00:00Z')
    renderApp(`/eventos?sitio=${s.id}`)
    expect(await rows()).toHaveLength(1)
    expect(screen.getByText('Se actualiza sola cada 15 segundos.')).toBeInTheDocument()

    fake.addEvent(device, hall, '2026-12-02T13:30:00Z', { decision: 'deny', reason: 'no_permission' })
    await act(() => vi.advanceTimersByTimeAsync(15_000))

    await waitFor(async () => expect(await rows()).toHaveLength(2))
    const [first] = await rows()
    expect(first).toHaveTextContent('Hall Torre A')
    expect(first).toHaveClass('animate-rise')
    expect(screen.getByText('1 evento nuevo')).toBeInTheDocument()
  })

  it('en pausa no pide eventos nuevos hasta reanudar', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true })
    signedInAs('guard')
    const { site: s, gate, device } = site()
    fake.addEvent(device, gate, '2026-12-02T13:00:00Z')
    const { user } = renderApp(`/eventos?sitio=${s.id}`)
    expect(await rows()).toHaveLength(1)

    await user.click(screen.getByRole('button', { name: 'Pausar' }))
    expect(screen.getByText('Actualización en pausa.')).toBeInTheDocument()
    const before = fake.requests.filter((r) => r.includes('/access-events')).length
    fake.addEvent(device, gate, '2026-12-02T13:30:00Z')
    await act(() => vi.advanceTimersByTimeAsync(45_000))

    expect(fake.requests.filter((r) => r.includes('/access-events')).length).toBe(before)
    expect(await rows()).toHaveLength(1)

    await user.click(screen.getByRole('button', { name: 'Reanudar' }))
    await act(() => vi.advanceTimersByTimeAsync(15_000))
    await waitFor(async () => expect(await rows()).toHaveLength(2))
  })
})
