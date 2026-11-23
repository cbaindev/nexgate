import { screen, waitFor, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { orgA, type Role } from './fakeApi'
import { location, renderApp } from './render'
import { fake } from './setup'

function signedInAs(role: Role) {
  fake.roles[orgA.id] = role
  fake.sessionValid = true
  fake.activeOrganization = orgA.id
}

const minutesAgo = (m: number) => new Date(Date.now() - m * 60_000).toISOString()
const daysFromNow = (d: number) => new Date(Date.now() + d * 24 * 3600_000).toISOString()

/** Torre Norte con su portón y el hall de la torre A. */
function site() {
  const site = fake.addSite('Torre Norte')
  const root = fake.addSpace(site.id, 'Torre Norte', 'site')
  const a = fake.addSpace(site.id, 'Torre A', 'building', root)
  const gate = fake.addAccessPoint('Portón principal', root, null, 'gate')
  const hall = fake.addAccessPoint('Hall Torre A', a, root)
  return { site, gate, hall }
}

const deviceButton = async (name: string) =>
  within(await screen.findByRole('list', { name: 'Dispositivos del sitio' })).findByRole('button', { name: new RegExp(`^${name}`) })
const detail = (name: string) => screen.findByRole('heading', { level: 2, name })
const detailCard = async (name: string) => (await detail(name)).closest('[aria-labelledby="device-detail-title"]') as HTMLElement

describe('Dispositivos', () => {
  it('ofrece solo los sitios que el permiso de leer dispositivos cubre entero', async () => {
    signedInAs('guard')
    const { site: a } = site()
    const b = fake.addSite('Barrio Las Lomas')
    const lot = fake.addSpace(b.id, 'Barrio Las Lomas', 'site')
    fake.addDevice(a.id, 'Controlador portón')
    fake.permissionsOverride = [
      { name: 'devices.read', organization: false, siteIds: [a.id], spaces: [{ siteId: b.id, spaceId: lot.id }] },
      { name: 'events.read', organization: false, siteIds: [a.id], spaces: [{ siteId: b.id, spaceId: lot.id }] },
    ]
    renderApp('/dispositivos')

    expect(await deviceButton('Controlador portón')).toBeInTheDocument()
    expect(within(screen.getByRole('combobox', { name: 'Sitio' })).getAllByRole('option').map((o) => o.textContent)).toEqual(['Torre Norte'])
    expect(fake.requests.some((r) => r.includes(b.id))).toBe(false)
  })

  it('lista los dispositivos del sitio por nombre, con su estado', async () => {
    signedInAs('administrator')
    const { site: s } = site()
    fake.addDevice(s.id, 'Portería', { status: 'revoked' })
    fake.addDevice(s.id, 'Hall', { status: 'pending' })
    fake.addDevice(s.id, 'Cochera')
    renderApp('/dispositivos')

    const list = await screen.findByRole('list', { name: 'Dispositivos del sitio' })
    await within(list).findByRole('button', { name: /Cochera/ })
    expect(within(list).getAllByRole('button').map((b) => b.textContent)).toEqual(['CocheraActivo', 'HallPendiente', 'PorteríaRevocado'])
  })

  it('el detalle muestra conexión, último contacto, política pendiente, firmware, certificado y puertas', async () => {
    signedInAs('administrator')
    const { site: s, gate } = site()
    const device = fake.addDevice(s.id, 'Controlador portón', {
      lastSeenAt: minutesAgo(2),
      reportedPolicyVersion: 3,
      policyVersion: 4,
      firmwareVersion: '1.4.2',
      certificateExpiresAt: '2027-11-23T15:00:00Z',
      accessPointIds: [gate.id],
    })
    renderApp(`/dispositivos?sitio=${s.id}&dispositivo=${device.id}`)

    const card = await detailCard('Controlador portón')
    expect(within(card).getByText('En línea')).toBeInTheDocument()
    expect(within(card).getByText('hace 2 minutos')).toBeInTheDocument()
    expect(within(card).getByText('Actualización pendiente (v3 de v4)')).toBeInTheDocument()
    expect(within(card).getByText('1.4.2')).toBeInTheDocument()
    expect(within(card).getByText('23/11/2027 12:00')).toBeInTheDocument()
    expect(await within(card).findByText('Portón principal')).toBeInTheDocument()
    expect(within(card).queryByText(/El certificado vence/)).not.toBeInTheDocument()
  })

  it('un dispositivo sin contacto lo dice y desde cuándo', async () => {
    signedInAs('administrator')
    const { site: s } = site()
    const device = fake.addDevice(s.id, 'Hall', { lastSeenAt: minutesAgo(60), reportedPolicyVersion: 2, policyVersion: 2 })
    renderApp(`/dispositivos?sitio=${s.id}&dispositivo=${device.id}`)

    const card = await detailCard('Hall')
    expect(within(card).getByText('Sin contacto')).toBeInTheDocument()
    expect(within(card).getByText('hace 1 hora')).toBeInTheDocument()
    expect(within(card).getByText('Al día')).toBeInTheDocument()
  })

  it('avisa si el certificado vence en menos de 30 días', async () => {
    signedInAs('administrator')
    const { site: s } = site()
    const device = fake.addDevice(s.id, 'Hall', { certificateExpiresAt: daysFromNow(10) })
    renderApp(`/dispositivos?sitio=${s.id}&dispositivo=${device.id}`)

    const card = await detailCard('Hall')
    expect(within(card).getByText(/El certificado vence el/)).toBeInTheDocument()
  })

  it('asignar el portón y el hall los muestra en el dispositivo sin recargar', async () => {
    signedInAs('administrator')
    const { site: s, gate, hall } = site()
    const device = fake.addDevice(s.id, 'Controlador central')
    const { user } = renderApp(`/dispositivos?sitio=${s.id}&dispositivo=${device.id}`)

    await user.click(await screen.findByRole('button', { name: 'Puertas' }))
    const form = screen.getByRole('form', { name: 'Puertas de Controlador central' })
    await user.click(await within(form).findByRole('checkbox', { name: /Portón principal/ }))
    await user.click(within(form).getByRole('checkbox', { name: /Hall Torre A/ }))
    await user.click(within(form).getByRole('button', { name: 'Guardar' }))

    const list = await screen.findByRole('list', { name: 'Puertas de Controlador central' })
    expect(within(list).getAllByRole('listitem').map((li) => li.querySelector('.font-medium')?.textContent)).toEqual(['Portón principal', 'Hall Torre A'])
    expect(fake.devices.find((d) => d.id === device.id)?.accessPointIds).toEqual([gate.id, hall.id])
  })

  it('la puerta que controla otro dispositivo no se puede elegir y dice cuál la controla', async () => {
    signedInAs('administrator')
    const { site: s, gate } = site()
    fake.addDevice(s.id, 'Controlador portón', { accessPointIds: [gate.id] })
    const device = fake.addDevice(s.id, 'Controlador hall')
    const { user } = renderApp(`/dispositivos?sitio=${s.id}&dispositivo=${device.id}`)

    await user.click(await screen.findByRole('button', { name: 'Puertas' }))
    const form = screen.getByRole('form', { name: 'Puertas de Controlador hall' })
    const box = await within(form).findByRole('checkbox', { name: /Portón principal/ })
    expect(box).toBeDisabled()
    expect(box).toHaveAccessibleDescription('La controla Controlador portón')
    expect(within(form).getByRole('checkbox', { name: /Hall Torre A/ })).toBeEnabled()
  })

  it('revocar pide confirmación explicando qué pasa y deja el dispositivo revocado', async () => {
    signedInAs('administrator')
    const { site: s } = site()
    const device = fake.addDevice(s.id, 'Hall', { lastSeenAt: minutesAgo(1) })
    const { user } = renderApp(`/dispositivos?sitio=${s.id}&dispositivo=${device.id}`)

    await user.click(await screen.findByRole('button', { name: 'Revocar' }))
    const dialog = screen.getByRole('alertdialog', { name: '¿Revocar Hall?' })
    expect(dialog).toHaveTextContent('Deja de funcionar enseguida y conserva sus puertas')
    await user.click(within(dialog).getByRole('button', { name: 'Revocar' }))

    const card = await detailCard('Hall')
    await waitFor(() => expect(within(card).getAllByText('Revocado').length).toBeGreaterThan(0))
    expect(within(card).getByText(/Revocado: no funciona/)).toBeInTheDocument()
    expect(within(card).queryByRole('button', { name: 'Revocar' })).not.toBeInTheDocument()
  })

  it('el guardia ve los dispositivos y su detalle sin acciones', async () => {
    signedInAs('guard')
    const { site: s } = site()
    const device = fake.addDevice(s.id, 'Hall')
    renderApp(`/dispositivos?sitio=${s.id}&dispositivo=${device.id}`)

    expect(await detail('Hall')).toBeInTheDocument()
    for (const name of ['Nuevo dispositivo', 'Puertas', 'Renombrar', 'Código nuevo', 'Revocar', 'Eliminar Hall']) {
      expect(screen.queryByRole('button', { name })).not.toBeInTheDocument()
    }
  })
})

describe('Código de reclamo', () => {
  it('al dar de alta muestra el código, su QR, el vencimiento y copiar, y avisa que no se vuelve a mostrar', async () => {
    signedInAs('administrator')
    site()
    const { user } = renderApp('/dispositivos')

    await user.click(await screen.findByRole('button', { name: 'Dar de alta el primer dispositivo' }))
    await user.type(screen.getByLabelText('Nombre'), 'Controlador portón')
    await user.click(screen.getByRole('button', { name: 'Guardar' }))

    const dialog = await screen.findByRole('dialog', { name: 'Código de reclamo de Controlador portón' })
    expect(within(dialog).getByText('CLAIM-0001-XK7P')).toBeInTheDocument()
    expect(within(dialog).getByRole('img', { name: 'Código QR del código de reclamo' })).toBeInTheDocument()
    expect(within(dialog).getByText('Vence el 30/11/2026 14:00.')).toBeInTheDocument()
    expect(within(dialog).getByText('No se vuelve a mostrar. Si lo perdés, generá uno nuevo.')).toBeInTheDocument()

    await user.click(within(dialog).getByRole('button', { name: 'Copiar' }))
    expect(await within(dialog).findByRole('button', { name: 'Copiado' })).toBeInTheDocument()
    expect(await navigator.clipboard.readText()).toBe('CLAIM-0001-XK7P')
  })

  it('al cerrarlo el código no queda en la página, la dirección ni el navegador, y el detalle dice que espera el reclamo', async () => {
    signedInAs('administrator')
    site()
    const { user } = renderApp('/dispositivos')

    await user.click(await screen.findByRole('button', { name: 'Dar de alta el primer dispositivo' }))
    await user.type(screen.getByLabelText('Nombre'), 'Hall')
    await user.click(screen.getByRole('button', { name: 'Guardar' }))
    await user.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Listo' }))

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
    const card = await detailCard('Hall')
    expect(within(card).getByText('Esperando que se reclame desde el equipo. El código vence el 30/11/2026 14:00.')).toBeInTheDocument()
    expect(document.body.innerHTML).not.toContain('CLAIM-0001')
    expect(location.current).not.toContain('CLAIM')
    expect([...Object.values(localStorage), ...Object.values(sessionStorage)].join(' ')).not.toContain('CLAIM')
  })

  it('un código nuevo pide confirmación y muestra el código nuevo', async () => {
    signedInAs('administrator')
    const { site: s } = site()
    const device = fake.addDevice(s.id, 'Hall')
    const { user } = renderApp(`/dispositivos?sitio=${s.id}&dispositivo=${device.id}`)

    await user.click(await screen.findByRole('button', { name: 'Código nuevo' }))
    const confirm = screen.getByRole('alertdialog', { name: '¿Generar un código nuevo para Hall?' })
    expect(confirm).toHaveTextContent('deja de funcionar hasta que se reclame con el código nuevo')
    await user.click(within(confirm).getByRole('button', { name: 'Generar código' }))

    const dialog = await screen.findByRole('dialog', { name: 'Código de reclamo de Hall' })
    expect(within(dialog).getByText('CLAIM-0001-XK7P')).toBeInTheDocument()
    expect(fake.devices[0]!.status).toBe('pending')
  })
})
