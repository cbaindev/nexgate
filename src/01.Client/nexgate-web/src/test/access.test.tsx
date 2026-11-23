import { fireEvent, screen, waitFor, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { describeDays, describeWindow } from '@/features/access/model'
import { orgA, type Role } from './fakeApi'
import { location, renderApp } from './render'
import { fake } from './setup'

function signedInAs(role: Role) {
  fake.roles[orgA.id] = role
  fake.sessionValid = true
  fake.activeOrganization = orgA.id
}

const weekdays = ['monday', 'tuesday', 'wednesday', 'thursday', 'friday']

/** Torre Norte con la torre A, el piso 5 y la unidad 5B, sus puertas, un horario y la titular de 5B con tarjeta y permiso. */
function building() {
  const site = fake.addSite('Torre Norte')
  const root = fake.addSpace(site.id, 'Torre Norte', 'site')
  const a = fake.addSpace(site.id, 'Torre A', 'building', root)
  const floor5 = fake.addSpace(site.id, 'Piso 5', 'floor', a)
  const unit5b = fake.addSpace(site.id, '5B', 'unit', floor5)
  const b = fake.addSpace(site.id, 'Torre B', 'building', root)
  const gate = fake.addAccessPoint('Portón principal', root, null, 'gate')
  const hall = fake.addAccessPoint('Hall Torre A', a, root)
  const hallB = fake.addAccessPoint('Hall Torre B', b, root)
  const door5b = fake.addAccessPoint('Puerta 5B', unit5b, floor5)
  const office = fake.addSchedule(site.id, 'Lunes a viernes de 8 a 17', [{ days: weekdays, from: '08:00', to: '17:00' }])
  const owner = fake.addSubject(site.id, 'Titular 5B')
  const card = fake.addCredential(owner.id, 'card', '1234')
  const grant = fake.addGrant(owner, unit5b, { scheduleId: office.id })
  return { site, root, a, floor5, unit5b, b, gate, hall, hallB, door5b, office, owner, card, grant }
}

const subjectButton = async (alias: string) =>
  within(await screen.findByRole('list', { name: 'Personas del sitio' })).findByRole('button', { name: new RegExp(`${alias}$`) })
const form = (name: string) => screen.getByRole('form', { name })
const setValue = (element: HTMLElement, value: string) => fireEvent.change(element, { target: { value } })

describe('Selector de sitio en accesos', () => {
  it('ofrece solo los sitios que el permiso de leer accesos cubre entero y no pide datos del otro', async () => {
    signedInAs('guard')
    const { site } = building()
    const other = fake.addSite('Barrio Las Lomas')
    const lot = fake.addSpace(other.id, 'Barrio Las Lomas', 'site')
    fake.permissionsOverride = [
      { name: 'sites.read', organization: true, siteIds: [], spaces: [] },
      { name: 'access.read', organization: false, siteIds: [site.id], spaces: [{ siteId: other.id, spaceId: lot.id }] },
    ]
    renderApp('/accesos')

    expect(await subjectButton('Titular 5B')).toBeInTheDocument()
    const options = within(screen.getByRole('combobox', { name: 'Sitio' })).getAllByRole('option').map((o) => o.textContent)
    expect(options).toEqual(['Torre Norte'])
    expect(fake.requests.some((r) => r.includes(other.id))).toBe(false)
  })
})

describe('Personas', () => {
  it('lista las personas del sitio por alias, con su tipo', async () => {
    signedInAs('administrator')
    const { site } = building()
    fake.addSubject(site.id, 'Proveedor de limpieza', 'anonymous')
    fake.addSubject(site.id, 'Ana 3C')
    renderApp('/accesos')

    const list = await screen.findByRole('list', { name: 'Personas del sitio' })
    await within(list).findByRole('button', { name: /Titular 5B/ })
    expect(within(list).getAllByRole('button').map((b) => b.textContent)).toEqual(['Ana 3C', 'Proveedor de limpieza', 'Titular 5B'])
    expect(within(list).getByRole('button', { name: 'Anónimo Proveedor de limpieza' })).toBeInTheDocument()
    expect(within(list).getByRole('button', { name: 'Persona Ana 3C' })).toBeInTheDocument()
  })

  it('el detalle muestra credenciales con su estado y permisos con destino, horario, vigencia y usos', async () => {
    signedInAs('administrator')
    const { site, owner, hall } = building()
    fake.addCredential(owner.id, 'pin', '9999', '2020-01-01T00:00:00Z', '2021-01-01T00:00:00Z')
    fake.addCredential(owner.id, 'rf', 'AB12', '2099-01-01T03:00:00Z', null)
    fake.addGrant(owner, hall, { remainingUses: 3, validUntil: '2099-12-31T03:00:00Z' })
    renderApp(`/accesos?sitio=${site.id}&sujeto=${owner.id}`)

    const credentials = await screen.findByRole('list', { name: 'Credenciales de Titular 5B' })
    const items = within(credentials).getAllByRole('listitem')
    expect(items.map((li) => li.querySelector('.font-medium')?.textContent)).toEqual(
      expect.arrayContaining(['TarjetaVigente', 'PINVencida', 'Llavero o tagPor empezar']),
    )
    expect(within(credentials).getByText('Desde el 01/01/2099 00:00')).toBeInTheDocument()
    expect(credentials).not.toHaveTextContent('1234')

    const grants = screen.getByRole('list', { name: 'Permisos de Titular 5B' })
    const toUnit = within(grants).getByText('Torre A › Piso 5 › 5B').closest('li')!
    expect(within(toUnit).getByText('Permite')).toBeInTheDocument()
    expect(within(toUnit).getByText('Lunes a viernes de 8 a 17')).toBeInTheDocument()
    expect(within(toUnit).getByText('Sin vencimiento')).toBeInTheDocument()
    const toHall = within(grants).getByText('Hall Torre A (da a Torre A)').closest('li')!
    expect(within(toHall).getByText('Siempre')).toBeInTheDocument()
    expect(within(toHall).getByText('Quedan 3 usos')).toBeInTheDocument()
    expect(within(toHall).getByText('Hasta el 31/12/2099 00:00')).toBeInTheDocument()
  })

  it('el formulario pide solo tipo y alias y advierte que no se carguen datos personales', async () => {
    signedInAs('administrator')
    building()
    const { user } = renderApp('/accesos')

    await user.click(await screen.findByRole('button', { name: 'Nueva persona' }))
    const subject = form('Nueva persona')
    expect(within(subject).getAllByRole('textbox')).toHaveLength(1)
    expect(within(subject).getByRole('radio', { name: 'Persona' })).toBeChecked()
    expect(within(subject).getByText(/No cargues documento, teléfono ni correo/)).toBeInTheDocument()

    await user.type(within(subject).getByLabelText('Alias'), 'Encargado')
    await user.click(within(subject).getByRole('radio', { name: 'Anónimo' }))
    await user.click(within(subject).getByRole('button', { name: 'Guardar' }))

    expect(await screen.findByRole('heading', { name: 'Encargado' })).toBeInTheDocument()
    expect(fake.subjects.find((s) => s.alias === 'Encargado')).toMatchObject({ kind: 'anonymous' })
  })

  it('una persona con credenciales o permisos no se elimina y el mensaje del servidor queda en el diálogo', async () => {
    signedInAs('administrator')
    const { site, owner } = building()
    const { user } = renderApp(`/accesos?sitio=${site.id}&sujeto=${owner.id}`)

    await user.click(await screen.findByRole('button', { name: 'Eliminar Titular 5B' }))
    const dialog = screen.getByRole('alertdialog')
    await user.click(within(dialog).getByRole('button', { name: 'Eliminar' }))

    expect(await within(dialog).findByText('El sujeto tiene credenciales o permisos.')).toBeInTheDocument()
    expect(fake.subjects.some((s) => s.id === owner.id)).toBe(true)
  })

  it('el guardia ve personas, credenciales, permisos y horarios sin acciones', async () => {
    signedInAs('guard')
    const { site, owner } = building()
    const { user } = renderApp(`/accesos?sitio=${site.id}&sujeto=${owner.id}`)

    expect(await screen.findByRole('list', { name: 'Permisos de Titular 5B' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Nueva persona' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Agregar' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /^(Editar|Eliminar|Cambiar)/ })).not.toBeInTheDocument()

    await user.click(screen.getByRole('tab', { name: 'Horarios' }))
    expect(await screen.findByText('Lunes a viernes, 08:00 a 17:00')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Nuevo horario' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /^(Editar|Eliminar)/ })).not.toBeInTheDocument()
  })
})

describe('Credenciales', () => {
  it('al registrar una tarjeta el valor no queda en la pantalla, la dirección ni el navegador', async () => {
    signedInAs('administrator')
    const { site, owner } = building()
    const { user } = renderApp(`/accesos?sitio=${site.id}&sujeto=${owner.id}`)
    const credentials = await screen.findByRole('heading', { name: 'Credenciales' })

    await user.click(within(credentials.closest('section')!).getByRole('button', { name: 'Agregar' }))
    const credential = form('Nueva credencial')
    expect(within(credential).getByLabelText('Número')).toHaveAttribute('autocomplete', 'off')
    await user.type(within(credential).getByLabelText('Número'), 'TARJETA-77881')
    await user.click(within(credential).getByRole('button', { name: 'Guardar' }))

    await waitFor(() => expect(screen.queryByRole('form', { name: 'Nueva credencial' })).not.toBeInTheDocument())
    expect(await screen.findAllByText('Tarjeta')).toHaveLength(2)
    expect(fake.credentials.some((c) => c.value === 'TARJETA-77881' && c.subjectId === owner.id)).toBe(true)
    expect(document.body.innerHTML).not.toContain('TARJETA-77881')
    expect(location.current).not.toContain('TARJETA-77881')
    const stored = [...Object.values(localStorage), ...Object.values(sessionStorage)].join(' ')
    expect(stored).not.toContain('TARJETA-77881')
  })

  it('el PIN se oculta mientras se escribe', async () => {
    signedInAs('administrator')
    const { site, owner } = building()
    const { user } = renderApp(`/accesos?sitio=${site.id}&sujeto=${owner.id}`)
    const credentials = await screen.findByRole('heading', { name: 'Credenciales' })

    await user.click(within(credentials.closest('section')!).getByRole('button', { name: 'Agregar' }))
    const credential = form('Nueva credencial')
    expect(within(credential).getByLabelText('Número')).toHaveAttribute('type', 'text')
    await user.selectOptions(within(credential).getByLabelText('Tipo'), 'pin')

    expect(within(credential).getByLabelText('PIN')).toHaveAttribute('type', 'password')
  })

  it('una credencial repetida en el sitio muestra el mensaje del servidor y no se crea', async () => {
    signedInAs('administrator')
    const { site, owner } = building()
    const { user } = renderApp(`/accesos?sitio=${site.id}&sujeto=${owner.id}`)
    const credentials = await screen.findByRole('heading', { name: 'Credenciales' })

    await user.click(within(credentials.closest('section')!).getByRole('button', { name: 'Agregar' }))
    const credential = form('Nueva credencial')
    await user.type(within(credential).getByLabelText('Número'), '1234')
    await user.click(within(credential).getByRole('button', { name: 'Guardar' }))

    expect(await within(credential).findByText('Ya hay una credencial con ese valor en el sitio.')).toBeInTheDocument()
    expect(fake.credentials).toHaveLength(1)
  })

  it('cambiar la vigencia la guarda en la hora del sitio', async () => {
    signedInAs('administrator')
    const { site, owner, card } = building()
    const { user } = renderApp(`/accesos?sitio=${site.id}&sujeto=${owner.id}`)

    await user.click(await screen.findByRole('button', { name: 'Cambiar la vigencia de Tarjeta' }))
    const credential = form('Cambiar vigencia')
    expect(within(credential).queryByLabelText('Número')).not.toBeInTheDocument()
    setValue(within(credential).getByLabelText('Hasta'), '2027-03-31T23:59')
    await user.click(within(credential).getByRole('button', { name: 'Guardar' }))

    expect(await screen.findByText('Hasta el 31/03/2027 23:59')).toBeInTheDocument()
    expect(fake.credentials.find((c) => c.id === card.id)?.validUntil).toBe('2027-04-01T02:59:00Z')
  })
})

describe('Permisos', () => {
  async function openGrantForm(user: ReturnType<typeof renderApp>['user']) {
    const grants = await screen.findByRole('heading', { name: 'Permisos' })
    await user.click(within(grants.closest('section')!).getByRole('button', { name: 'Agregar' }))
    return form('Nuevo permiso')
  }

  it('dar acceso a la unidad 5B con un horario lo muestra con su ruta y su horario', async () => {
    signedInAs('administrator')
    const { site, unit5b, office } = building()
    const tenant = fake.addSubject(site.id, 'Inquilina 5B')
    const { user } = renderApp(`/accesos?sitio=${site.id}&sujeto=${tenant.id}`)
    expect(await screen.findByText('No tiene permisos.')).toBeInTheDocument()

    const grant = await openGrantForm(user)
    await waitFor(() => expect(within(grant).getAllByRole('option').length).toBeGreaterThan(3))
    await user.selectOptions(within(grant).getByLabelText('A dónde'), `space:${unit5b.id}`)
    await user.selectOptions(within(grant).getByLabelText('Horario'), office.id)
    await user.click(within(grant).getByRole('button', { name: 'Guardar' }))

    const list = await screen.findByRole('list', { name: 'Permisos de Inquilina 5B' })
    const item = within(list).getByText('Torre A › Piso 5 › 5B').closest('li')!
    expect(within(item).getByText('Lunes a viernes de 8 a 17')).toBeInTheDocument()
    expect(fake.grants.find((g) => g.subjectId === tenant.id)).toMatchObject({ targetType: 'space', targetId: unit5b.id, scheduleId: office.id, effect: 'allow' })
  })

  it('la vigencia se carga y se muestra en la hora del sitio, sin importar la del navegador', async () => {
    signedInAs('administrator')
    const { site, owner, hall } = building()
    const { user } = renderApp(`/accesos?sitio=${site.id}&sujeto=${owner.id}`)

    const grant = await openGrantForm(user)
    await waitFor(() => expect(within(grant).getAllByRole('option').length).toBeGreaterThan(3))
    await user.selectOptions(within(grant).getByLabelText('A dónde'), `access_point:${hall.id}`)
    expect(within(grant).getByText('Opcional. Hora de America/Argentina/Cordoba.')).toBeInTheDocument()
    setValue(within(grant).getByLabelText('Desde'), '2026-12-01T08:00')
    await user.click(within(grant).getByRole('button', { name: 'Guardar' }))

    expect(await screen.findByText('Desde el 01/12/2026 08:00')).toBeInTheDocument()
    expect(fake.grants.find((g) => g.targetId === hall.id)?.validFrom).toBe('2026-12-01T11:00:00Z')
  })

  it('una denegación no ofrece máximo de usos y se marca como que gana sobre cualquier permiso', async () => {
    signedInAs('administrator')
    const { site, owner, gate } = building()
    const { user } = renderApp(`/accesos?sitio=${site.id}&sujeto=${owner.id}`)

    const grant = await openGrantForm(user)
    expect(within(grant).getByLabelText('Máximo de usos')).toBeInTheDocument()
    await user.click(within(grant).getByRole('radio', { name: /Denegar/ }))
    expect(within(grant).queryByLabelText('Máximo de usos')).not.toBeInTheDocument()
    await waitFor(() => expect(within(grant).getAllByRole('option').length).toBeGreaterThan(3))
    await user.selectOptions(within(grant).getByLabelText('A dónde'), `access_point:${gate.id}`)
    await user.click(within(grant).getByRole('button', { name: 'Guardar' }))

    const list = await screen.findByRole('list', { name: 'Permisos de Titular 5B' })
    const item = (await within(list).findByText('Portón principal (da a Torre Norte)')).closest('li')!
    expect(within(item).getByText('Deniega')).toBeInTheDocument()
    expect(within(item).getByText('Gana sobre cualquier permiso.')).toBeInTheDocument()
    expect(fake.grants.find((g) => g.targetId === gate.id)).toMatchObject({ effect: 'deny', remainingUses: null })
  })

  it('sin destino no se envía', async () => {
    signedInAs('administrator')
    const { site, owner } = building()
    const { user } = renderApp(`/accesos?sitio=${site.id}&sujeto=${owner.id}`)

    const grant = await openGrantForm(user)
    await user.click(within(grant).getByRole('button', { name: 'Guardar' }))

    expect(within(grant).getByText('Elegí un espacio o una puerta.')).toBeInTheDocument()
    expect(fake.grants).toHaveLength(1)
  })
})

describe('Horarios', () => {
  it('describe los días en palabras', () => {
    expect(describeDays(weekdays)).toBe('lunes a viernes')
    expect(describeDays(['monday', 'wednesday', 'friday'])).toBe('lunes, miércoles y viernes')
    expect(describeDays(['saturday', 'sunday'])).toBe('sábado y domingo')
    expect(describeDays(['monday', 'tuesday', 'wednesday', 'thursday', 'friday', 'saturday', 'sunday'])).toBe('todos los días')
    expect(describeWindow({ days: ['sunday'], from: '00:00', to: '24:00' })).toBe('Domingo, 00:00 a 24:00')
  })

  it('crea un horario con dos ventanas y lo muestra en palabras', async () => {
    signedInAs('administrator')
    const { site } = building()
    const { user } = renderApp(`/accesos?sitio=${site.id}&vista=horarios`)

    await user.click(await screen.findByRole('button', { name: 'Nuevo horario' }))
    const schedule = form('Nuevo horario')
    await user.type(within(schedule).getByLabelText('Nombre'), 'Mantenimiento')
    await user.click(within(schedule).getByRole('button', { name: 'Agregar ventana' }))
    const second = within(schedule).getByRole('group', { name: 'Ventana 2' })
    for (const day of ['lunes', 'martes', 'miércoles', 'jueves', 'viernes', 'sábado']) {
      await user.click(within(second).getByRole('checkbox', { name: day }))
    }
    setValue(within(second).getByLabelText('Desde'), '09:00')
    setValue(within(second).getByLabelText('Hasta'), '13:00')
    await user.click(within(schedule).getByRole('button', { name: 'Guardar' }))

    const card = (await screen.findByRole('heading', { name: 'Mantenimiento' })).closest('li')!
    expect(within(card).getByText('Lunes a viernes, 08:00 a 17:00')).toBeInTheDocument()
    expect(within(card).getByText('Sábado, 09:00 a 13:00')).toBeInTheDocument()
  })

  it('"hasta el final del día" envía 24:00', async () => {
    signedInAs('administrator')
    const { site } = building()
    const { user } = renderApp(`/accesos?sitio=${site.id}&vista=horarios`)

    await user.click(await screen.findByRole('button', { name: 'Nuevo horario' }))
    const schedule = form('Nuevo horario')
    await user.type(within(schedule).getByLabelText('Nombre'), 'Noche')
    setValue(within(schedule).getByLabelText('Desde'), '20:00')
    await user.click(within(schedule).getByRole('checkbox', { name: 'Hasta el final del día' }))
    await user.click(within(schedule).getByRole('button', { name: 'Guardar' }))

    expect(await screen.findByText('Lunes a viernes, 20:00 a 24:00')).toBeInTheDocument()
    expect(fake.schedules.find((s) => s.name === 'Noche')?.windows[0]).toMatchObject({ from: '20:00', to: '24:00' })
  })

  it('una ventana sin días o que termina antes de empezar se marca y no se envía', async () => {
    signedInAs('administrator')
    const { site } = building()
    const { user } = renderApp(`/accesos?sitio=${site.id}&vista=horarios`)

    await user.click(await screen.findByRole('button', { name: 'Nuevo horario' }))
    const schedule = form('Nuevo horario')
    await user.type(within(schedule).getByLabelText('Nombre'), 'Roto')
    for (const day of ['lunes', 'martes', 'miércoles', 'jueves', 'viernes']) {
      await user.click(within(schedule).getByRole('checkbox', { name: day }))
    }
    await user.click(within(schedule).getByRole('button', { name: 'Guardar' }))
    expect(within(schedule).getByText('Elegí al menos un día.')).toBeInTheDocument()

    await user.click(within(schedule).getByRole('checkbox', { name: 'lunes' }))
    setValue(within(schedule).getByLabelText('Hasta'), '07:00')
    await user.click(within(schedule).getByRole('button', { name: 'Guardar' }))
    expect(within(schedule).getByText('El fin tiene que ser después del inicio.')).toBeInTheDocument()
    expect(fake.schedules).toHaveLength(1)
  })

  it('un horario que usa un permiso no se elimina y el mensaje queda en el diálogo', async () => {
    signedInAs('administrator')
    const { site, office } = building()
    const { user } = renderApp(`/accesos?sitio=${site.id}&vista=horarios`)

    await user.click(await screen.findByRole('button', { name: 'Eliminar Lunes a viernes de 8 a 17' }))
    const dialog = screen.getByRole('alertdialog')
    await user.click(within(dialog).getByRole('button', { name: 'Eliminar' }))

    expect(await within(dialog).findByText('Un permiso usa este horario.')).toBeInTheDocument()
    expect(fake.schedules.some((s) => s.id === office.id)).toBe(true)
  })
})

describe('Probar acceso', () => {
  async function test(user: ReturnType<typeof renderApp>['user'], point: string, value: string, at: string) {
    const simulation = await screen.findByRole('form', { name: 'Probar acceso' })
    await waitFor(() => expect(within(simulation).getAllByRole('option').length).toBeGreaterThan(4))
    await user.selectOptions(within(simulation).getByLabelText('Puerta'), point)
    await user.type(within(simulation).getByLabelText('Número'), value)
    await user.click(within(simulation).getByRole('radio', { name: 'Otra fecha y hora' }))
    setValue(within(simulation).getByLabelText('Fecha y hora'), at)
    await user.click(within(simulation).getByRole('button', { name: 'Probar' }))
    return simulation
  }

  it('la tarjeta de la titular pasaría por la puerta de 5B dentro del horario, y el valor no queda', async () => {
    signedInAs('administrator')
    const { site, door5b } = building()
    const { user } = renderApp(`/accesos?sitio=${site.id}&vista=probar`)

    const simulation = await test(user, door5b.id, '1234', '2026-12-02T10:00')

    const result = await screen.findByRole('status')
    expect(result).toHaveTextContent('Pasaría')
    expect(result).toHaveTextContent('Un permiso lo habilita en ese momento.')
    expect(result).toHaveTextContent('En Puerta 5B, el 02/12/2026 10:00.')
    expect(within(simulation).getByLabelText('Número')).toHaveValue('')
    expect(fake.simulationBodies[0]).toMatchObject({ credentialType: 'card', credentialValue: '1234', at: '2026-12-02T13:00:00Z' })
  })

  it('fuera del horario no pasaría, y un valor desconocido tampoco, cada uno con su razón', async () => {
    signedInAs('administrator')
    const { site, door5b } = building()
    const { user } = renderApp(`/accesos?sitio=${site.id}&vista=probar`)

    await test(user, door5b.id, '1234', '2026-12-05T10:00')
    expect(await screen.findByRole('status')).toHaveTextContent('Ningún permiso vigente lo habilita a esa hora en esta puerta.')

    await user.click(screen.getByRole('button', { name: 'Probar otra' }))
    const simulation = screen.getByRole('form', { name: 'Probar acceso' })
    await user.type(within(simulation).getByLabelText('Número'), 'no-existe')
    await user.click(within(simulation).getByRole('button', { name: 'Probar' }))
    const result = await screen.findByRole('status')
    await waitFor(() => expect(result).toHaveTextContent('No pasaría'))
    expect(result).toHaveTextContent('La credencial no existe en el sitio, venció o todavía no empieza.')
  })

  it('una denegación gana sobre el permiso', async () => {
    signedInAs('administrator')
    const { site, door5b, owner, unit5b } = building()
    fake.addGrant(owner, unit5b, { effect: 'deny' })
    const { user } = renderApp(`/accesos?sitio=${site.id}&vista=probar`)

    await test(user, door5b.id, '1234', '2026-12-02T10:00')

    const result = await screen.findByRole('status')
    expect(result).toHaveTextContent('No pasaría')
    expect(result).toHaveTextContent('Una denegación gana sobre cualquier permiso.')
  })

  it('avisa que la prueba no es un evento', async () => {
    signedInAs('guard')
    const { site } = building()
    renderApp(`/accesos?sitio=${site.id}&vista=probar`)

    expect(await screen.findByText('La prueba no abre la puerta, no queda registrada y no consume usos.')).toBeInTheDocument()
  })

  it('desde una puerta de "Espacios y puertas" abre la prueba con esa puerta elegida', async () => {
    signedInAs('guard')
    const { site, door5b } = building()
    const { user } = renderApp(`/espacios?sitio=${site.id}&vista=puertas`)

    await user.click(await screen.findByRole('button', { name: 'Probar acceso en Puerta 5B' }))
    const simulation = await screen.findByRole('form', { name: 'Probar acceso' })
    expect(within(simulation).getByText('Puerta 5B')).toBeInTheDocument()
    expect(within(simulation).queryByLabelText('Puerta')).not.toBeInTheDocument()
    await user.type(within(simulation).getByLabelText('Número'), '1234')
    await user.click(within(simulation).getByRole('radio', { name: 'Otra fecha y hora' }))
    setValue(within(simulation).getByLabelText('Fecha y hora'), '2026-12-02T10:00')
    await user.click(within(simulation).getByRole('button', { name: 'Probar' }))

    expect(await screen.findByRole('status')).toHaveTextContent('Pasaría')
    expect(fake.simulationBodies).toHaveLength(1)
    expect(door5b.id).toBeTruthy()
  })

  it('sin permiso de leer accesos las puertas no ofrecen probar acceso', async () => {
    signedInAs('administrator')
    const { site } = building()
    fake.permissionsOverride = [{ name: 'spaces.read', organization: true, siteIds: [], spaces: [] }]
    renderApp(`/espacios?sitio=${site.id}&vista=puertas`)

    expect(await screen.findByRole('table')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /^Probar acceso en/ })).not.toBeInTheDocument()
  })
})
