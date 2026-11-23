import { screen, waitFor, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { hasPermissionOnSpace, type Session } from '@/session/session'
import { orgA, type Role } from './fakeApi'
import { location, renderApp } from './render'
import { fake } from './setup'

function signedInAs(role: Role) {
  fake.roles[orgA.id] = role
  fake.sessionValid = true
  fake.activeOrganization = orgA.id
}

/** Torre Norte: raíz, torres A y B, pisos 5 y 6 en A con sus unidades, una pileta común y sus puertas. */
function towers() {
  const site = fake.addSite('Torre Norte')
  const root = fake.addSpace(site.id, 'Torre Norte', 'site')
  const a = fake.addSpace(site.id, 'Torre A', 'building', root)
  const b = fake.addSpace(site.id, 'Torre B', 'building', root)
  const floor5 = fake.addSpace(site.id, 'Piso 5', 'floor', a)
  const unit5a = fake.addSpace(site.id, '5A', 'unit', floor5)
  const unit5b = fake.addSpace(site.id, '5B', 'unit', floor5)
  const floor6 = fake.addSpace(site.id, 'Piso 6', 'floor', a)
  const unit6a = fake.addSpace(site.id, '6A', 'unit', floor6)
  const pool = fake.addSpace(site.id, 'Pileta', 'common_area', root, true)
  const gate = fake.addAccessPoint('Portón principal', root, null, 'gate')
  const hall = fake.addAccessPoint('Hall Torre A', a, root)
  const hallB = fake.addAccessPoint('Hall Torre B', b, root)
  fake.addAccessPoint('Puerta piso 5', floor5, a)
  fake.addAccessPoint('Puerta 5B', unit5b, floor5, 'door', 'fail_safe')
  return { site, root, a, b, floor5, unit5a, unit5b, floor6, unit6a, pool, gate, hall, hallB }
}

const tree = () => screen.findByRole('list', { name: 'Árbol de espacios' })
const node = async (name: string) => within(await tree()).findByRole('button', { name: new RegExp(`^${name}( Área común)?$`) })
const detail = (name: string) => screen.findByRole('heading', { level: 2, name })

describe('Selector de sitio', () => {
  it('con un solo sitio visible muestra sus espacios sin preguntar', async () => {
    signedInAs('administrator')
    const { site } = towers()
    renderApp('/espacios')

    expect(await node('Edificio Torre A')).toBeInTheDocument()
    await waitFor(() => expect(location.current).toContain(`sitio=${site.id}`))
  })

  it('con varios sitios pide elegir uno y no pide espacios hasta elegirlo', async () => {
    signedInAs('administrator')
    const { site } = towers()
    fake.addSite('Barrio Las Lomas')
    const { user } = renderApp('/espacios')

    expect(await screen.findByRole('heading', { name: 'Elegí un sitio' })).toBeInTheDocument()
    expect(fake.requests.some((r) => r.includes('/spaces'))).toBe(false)

    await user.selectOptions(screen.getByRole('combobox', { name: 'Sitio' }), site.id)

    expect(await node('Edificio Torre A')).toBeInTheDocument()
    await waitFor(() => expect(location.current).toContain(`sitio=${site.id}`))
  })

  it('el sitio elegido sigue después de recargar', async () => {
    signedInAs('administrator')
    fake.addSite('Barrio Las Lomas')
    const { site } = towers()
    renderApp(`/espacios?sitio=${site.id}`)

    expect(await node('Edificio Torre A')).toBeInTheDocument()
    expect(screen.getByRole('combobox', { name: 'Sitio' })).toHaveValue(site.id)
  })

  it('un sitio que no está disponible lo dice y deja elegir otro', async () => {
    signedInAs('administrator')
    towers()
    fake.addSite('Barrio Las Lomas')
    renderApp('/espacios?sitio=99999999-9999-4999-8999-000000000000')

    expect(await screen.findByText('Este sitio no está disponible. Elegí otro arriba.')).toBeInTheDocument()
    expect(screen.getByRole('combobox', { name: 'Sitio' })).toBeInTheDocument()
  })

  it('sin sitios explica que los espacios son de un sitio y ofrece ir a sitios a quien puede crearlos', async () => {
    signedInAs('administrator')
    renderApp('/espacios')

    expect(await screen.findByText('Todavía no hay sitios.')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Ir a sitios' })).toHaveAttribute('href', '/sitios')
  })

  it('sin sitios, a quien no puede crearlos no le ofrece ir a sitios', async () => {
    signedInAs('guard')
    renderApp('/espacios')

    expect(await screen.findByText('Todavía no hay sitios.')).toBeInTheDocument()
    expect(screen.queryByRole('link', { name: 'Ir a sitios' })).not.toBeInTheDocument()
  })
})

describe('Árbol de espacios', () => {
  it('muestra el edificio, sus pisos y las unidades de cada piso con el nombre de su tipo', async () => {
    signedInAs('administrator')
    towers()
    const { user } = renderApp('/espacios')

    expect(await node('Edificio Torre A')).toBeInTheDocument()
    expect(await node('Piso Piso 5')).toBeInTheDocument()
    expect(within(await tree()).queryByRole('button', { name: 'Unidad 5B' })).not.toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Desplegar Piso 5' }))
    await user.click(screen.getByRole('button', { name: 'Desplegar Piso 6' }))

    expect(await node('Unidad 5A')).toBeInTheDocument()
    expect(await node('Unidad 5B')).toBeInTheDocument()
    expect(await node('Unidad 6A')).toBeInTheDocument()
    expect(await node('Área común Pileta')).toHaveTextContent('Área común')
  })

  it('abre los ancestros del espacio elegido y no los pliega al elegir uno de ellos', async () => {
    signedInAs('administrator')
    const { site, unit5b } = towers()
    const { user } = renderApp(`/espacios?sitio=${site.id}&espacio=${unit5b.id}`)

    expect(await node('Unidad 5B')).toHaveAttribute('aria-current', 'true')
    await user.click(await node('Piso Piso 5'))

    expect(await node('Piso Piso 5')).toHaveAttribute('aria-current', 'true')
    expect(await node('Unidad 5B')).toBeInTheDocument()
  })

  it('el filtro muestra la coincidencia y los espacios que la contienen, sin distinguir mayúsculas', async () => {
    signedInAs('administrator')
    towers()
    const { user } = renderApp('/espacios')
    await node('Edificio Torre A')

    await user.type(screen.getByLabelText('Filtrar espacios por nombre'), '5b')

    const list = await tree()
    expect(within(list).getByRole('button', { name: 'Unidad 5B' })).toBeInTheDocument()
    expect(within(list).getByRole('button', { name: 'Piso Piso 5' })).toBeInTheDocument()
    expect(within(list).getByRole('button', { name: 'Edificio Torre A' })).toBeInTheDocument()
    expect(within(list).queryByRole('button', { name: 'Unidad 5A' })).not.toBeInTheDocument()
    expect(within(list).queryByRole('button', { name: 'Edificio Torre B' })).not.toBeInTheDocument()
    expect(within(list).queryByRole('button', { name: /Pileta/ })).not.toBeInTheDocument()
  })

  it('el filtro no distingue tildes', async () => {
    signedInAs('administrator')
    towers()
    const { user } = renderApp('/espacios')
    await node('Edificio Torre A')

    await user.type(screen.getByLabelText('Filtrar espacios por nombre'), 'porton')
    expect(await screen.findByText('Ningún espacio coincide con la búsqueda.')).toBeInTheDocument()

    await user.clear(screen.getByLabelText('Filtrar espacios por nombre'))
    await user.type(screen.getByLabelText('Filtrar espacios por nombre'), 'PILETA')
    expect(await node('Área común Pileta')).toBeInTheDocument()
  })

  it('el detalle muestra tipo, área común, puertas que entran y el camino de acceso en orden', async () => {
    signedInAs('administrator')
    const { site, unit5b } = towers()
    renderApp(`/espacios?sitio=${site.id}&espacio=${unit5b.id}`)

    expect(await detail('5B')).toBeInTheDocument()
    const card = (await detail('5B')).closest('[aria-labelledby="space-detail-title"]') as HTMLElement
    expect(within(card).getByText('Unidad')).toBeInTheDocument()
    expect(within(card).getByText('Torre A › Piso 5')).toBeInTheDocument()
    const entering = within(card).getByRole('heading', { name: 'Puertas que entran' }).parentElement!
    expect(within(entering).getByText('Puerta 5B')).toBeInTheDocument()

    const path = await screen.findByRole('list', { name: 'Camino de acceso a 5B' })
    const steps = within(path)
      .getAllByRole('listitem')
      .map((li) => li.querySelector('.font-medium')?.textContent ?? li.textContent)
    expect(steps).toEqual(['Afuera', 'Portón principal', 'Hall Torre A', 'Puerta piso 5', 'Puerta 5B'])
  })

  it('un espacio común lo dice en el detalle', async () => {
    signedInAs('administrator')
    const { site, pool } = towers()
    renderApp(`/espacios?sitio=${site.id}&espacio=${pool.id}`)

    const card = (await detail('Pileta')).closest('[aria-labelledby="space-detail-title"]') as HTMLElement
    expect(within(card).getAllByText('Área común').length).toBeGreaterThan(0)
    expect(await within(card).findByText('Portón principal')).toBeInTheDocument()
  })

  it('un sitio sin espacios ofrece crear el espacio raíz', async () => {
    signedInAs('administrator')
    fake.addSite('Torre Sur')
    const { user } = renderApp('/espacios')

    expect(await screen.findByText('Este sitio todavía no tiene espacios.')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Crear el espacio raíz' }))
    const form = screen.getByRole('form', { name: 'Espacio raíz' })
    expect(within(form).getByLabelText('Nombre')).toHaveValue('Torre Sur')
    await user.click(within(form).getByRole('button', { name: 'Guardar' }))

    expect(await node('Sitio Torre Sur')).toBeInTheDocument()
    expect(fake.spaces).toHaveLength(1)
    expect(fake.spaces[0]).toMatchObject({ type: 'site', parentId: null })
  })

  it('agregar un piso dentro de un edificio lo muestra sin recargar', async () => {
    signedInAs('administrator')
    const { site, b } = towers()
    const { user } = renderApp(`/espacios?sitio=${site.id}&espacio=${b.id}`)

    await user.click(await screen.findByRole('button', { name: 'Agregar adentro' }))
    const form = screen.getByRole('form', { name: 'Nuevo espacio' })
    expect(within(form).getByText('Dentro de Torre B.')).toBeInTheDocument()
    // Dentro de un edificio se sugiere un piso.
    expect(within(form).getByLabelText('Tipo')).toHaveValue('floor')
    await user.type(within(form).getByLabelText('Nombre'), 'Piso 1')
    await user.click(within(form).getByRole('button', { name: 'Guardar' }))

    expect(await node('Piso Piso 1')).toBeInTheDocument()
    expect(await detail('Piso 1')).toBeInTheDocument()
    expect(screen.queryByRole('form', { name: 'Nuevo espacio' })).not.toBeInTheDocument()
  })

  it('un nombre repetido muestra el mensaje del servidor y conserva lo cargado', async () => {
    signedInAs('administrator')
    const { site, a } = towers()
    const { user } = renderApp(`/espacios?sitio=${site.id}&espacio=${a.id}`)

    await user.click(await screen.findByRole('button', { name: 'Agregar adentro' }))
    const form = screen.getByRole('form', { name: 'Nuevo espacio' })
    await user.type(within(form).getByLabelText('Nombre'), 'piso 5')
    await user.selectOptions(within(form).getByLabelText('Tipo'), 'floor')
    await user.click(within(form).getByRole('button', { name: 'Guardar' }))

    expect(await within(form).findByText('Ya hay un espacio con ese nombre en el mismo lugar.')).toBeInTheDocument()
    expect(within(form).getByLabelText('Nombre')).toHaveValue('piso 5')
    expect(within(form).getByLabelText('Tipo')).toHaveValue('floor')
  })

  it('un nombre vacío se marca junto al campo', async () => {
    signedInAs('administrator')
    const { site, a } = towers()
    const { user } = renderApp(`/espacios?sitio=${site.id}&espacio=${a.id}`)

    await user.click(await screen.findByRole('button', { name: 'Agregar adentro' }))
    const form = screen.getByRole('form', { name: 'Nuevo espacio' })
    await user.click(within(form).getByRole('button', { name: 'Guardar' }))

    expect(within(form).getByLabelText('Nombre')).toHaveAccessibleDescription('El nombre es obligatorio.')
  })

  it('editar renombra el espacio y lo marca como área común', async () => {
    signedInAs('administrator')
    const { site, unit6a } = towers()
    const { user } = renderApp(`/espacios?sitio=${site.id}&espacio=${unit6a.id}`)

    await user.click(await screen.findByRole('button', { name: 'Editar 6A' }))
    const form = screen.getByRole('form', { name: 'Editar espacio' })
    await user.clear(within(form).getByLabelText('Nombre'))
    await user.type(within(form).getByLabelText('Nombre'), 'SUM')
    await user.selectOptions(within(form).getByLabelText('Tipo'), 'common_area')
    await user.click(within(form).getByLabelText('Es un área común'))
    await user.click(within(form).getByRole('button', { name: 'Guardar' }))

    expect(await node('Área común SUM')).toHaveTextContent('Área común')
    expect(fake.spaces.find((s) => s.id === unit6a.id)).toMatchObject({ name: 'SUM', type: 'common_area', isCommon: true })
  })

  it('eliminar pide confirmación: cancelar no elimina y confirmar lo saca del árbol', async () => {
    signedInAs('administrator')
    const { site, unit6a } = towers()
    const { user } = renderApp(`/espacios?sitio=${site.id}&espacio=${unit6a.id}`)

    await user.click(await screen.findByRole('button', { name: 'Eliminar 6A' }))
    let dialog = screen.getByRole('alertdialog', { name: '¿Eliminar 6A?' })
    expect(within(dialog).getByRole('button', { name: 'Cancelar' })).toHaveFocus()
    await user.click(within(dialog).getByRole('button', { name: 'Cancelar' }))
    expect(fake.spaces.some((s) => s.id === unit6a.id)).toBe(true)

    await user.click(screen.getByRole('button', { name: 'Eliminar 6A' }))
    dialog = screen.getByRole('alertdialog', { name: '¿Eliminar 6A?' })
    await user.click(within(dialog).getByRole('button', { name: 'Eliminar' }))

    await waitFor(() => expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument())
    expect(within(await tree()).queryByRole('button', { name: 'Unidad 6A' })).not.toBeInTheDocument()
    expect(await detail('Piso 6')).toBeInTheDocument()
  })

  it('un espacio con hijos no se elimina y el mensaje del servidor queda en el diálogo', async () => {
    signedInAs('administrator')
    const { site, floor5 } = towers()
    const { user } = renderApp(`/espacios?sitio=${site.id}&espacio=${floor5.id}`)

    await user.click(await screen.findByRole('button', { name: 'Eliminar Piso 5' }))
    const dialog = screen.getByRole('alertdialog')
    await user.click(within(dialog).getByRole('button', { name: 'Eliminar' }))

    expect(await within(dialog).findByText('El espacio tiene espacios o puertas adentro.')).toBeInTheDocument()
    expect(fake.spaces.some((s) => s.id === floor5.id)).toBe(true)
  })

  it('el guardia ve el árbol, el detalle y el camino sin acciones', async () => {
    signedInAs('guard')
    const { site, unit5b } = towers()
    renderApp(`/espacios?sitio=${site.id}&espacio=${unit5b.id}`)

    expect(await screen.findByRole('list', { name: 'Camino de acceso a 5B' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Agregar adentro' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /^Editar/ })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /^Eliminar/ })).not.toBeInTheDocument()
  })

  it('quien maneja solo la torre A ve todo el árbol pero solo modifica dentro de la torre A', async () => {
    signedInAs('administrator')
    const { site, a, b, root, floor5 } = towers()
    fake.permissionsOverride = [
      { name: 'organization.read', organization: true, siteIds: [], spaces: [] },
      { name: 'sites.read', organization: false, siteIds: [], spaces: [{ siteId: site.id, spaceId: a.id }] },
      { name: 'spaces.read', organization: false, siteIds: [], spaces: [{ siteId: site.id, spaceId: a.id }] },
      { name: 'spaces.manage', organization: false, siteIds: [], spaces: [{ siteId: site.id, spaceId: a.id }] },
    ]
    const { user } = renderApp(`/espacios?sitio=${site.id}&espacio=${floor5.id}`)

    expect(await screen.findByRole('button', { name: 'Agregar adentro' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Editar Piso 5' })).toBeInTheDocument()

    await user.click(await node('Edificio Torre B'))
    expect(await detail('Torre B')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Agregar adentro' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Editar Torre B' })).not.toBeInTheDocument()

    await user.click(await node('Sitio Torre Norte'))
    expect(screen.queryByRole('button', { name: 'Editar Torre Norte' })).not.toBeInTheDocument()
    expect(b.id).not.toBe(root.id)
  })
})

describe('Puertas', () => {
  it('lista cada puerta con su tipo, a dónde da, desde dónde se entra y qué hace sin energía', async () => {
    signedInAs('administrator')
    const { site } = towers()
    renderApp(`/espacios?sitio=${site.id}&vista=puertas`)

    const table = await screen.findByRole('table', { name: 'Puertas de Torre Norte' })
    const gate = within(table).getByText('Portón principal').closest('tr')!
    expect(within(gate).getByText('Portón')).toBeInTheDocument()
    expect(within(gate).getByText('Torre Norte')).toBeInTheDocument()
    expect(within(gate).getByText('Afuera')).toBeInTheDocument()
    expect(within(gate).getByText('Queda cerrada')).toBeInTheDocument()
    const unit = within(table).getByText('Puerta 5B').closest('tr')!
    expect(within(unit).getByText('Torre A › Piso 5 › 5B')).toBeInTheDocument()
    expect(within(unit).getByText('Torre A › Piso 5')).toBeInTheDocument()
    expect(within(unit).getByText('Se abre')).toBeInTheDocument()
  })

  it('las pestañas cambian con el teclado y quedan en la dirección', async () => {
    signedInAs('administrator')
    towers()
    const { user } = renderApp('/espacios')
    await node('Edificio Torre A')

    await user.click(screen.getByRole('tab', { name: /Puertas/ }))

    expect(await screen.findByRole('table', { name: 'Puertas de Torre Norte' })).toBeInTheDocument()
    expect(screen.getByRole('tab', { name: /Puertas/ })).toHaveAttribute('aria-selected', 'true')
    expect(location.current).toContain('vista=puertas')
  })

  it('el formulario no trae un modo de falla elegido y explica cada uno', async () => {
    signedInAs('administrator')
    const { site } = towers()
    const { user } = renderApp(`/espacios?sitio=${site.id}&vista=puertas`)

    await user.click(await screen.findByRole('button', { name: 'Nueva puerta' }))
    const form = screen.getByRole('form', { name: 'Nueva puerta' })
    const safe = within(form).getByRole('radio', { name: /Se abre sin energía/ })
    const secure = within(form).getByRole('radio', { name: /Queda cerrada sin energía/ })
    expect(safe).not.toBeChecked()
    expect(secure).not.toBeChecked()
    expect(within(form).getByText(/salidas de emergencia/)).toBeInTheDocument()
    expect(within(form).getByText(/sin energía no se puede entrar/)).toBeInTheDocument()

    await user.type(within(form).getByLabelText('Nombre'), 'Cochera')
    await user.click(within(form).getByRole('button', { name: 'Guardar' }))
    expect(within(form).getByText('Elegí qué hace la puerta sin energía.')).toBeInTheDocument()
    expect(within(form).getByText('Elegí a qué espacio da la puerta.')).toBeInTheDocument()
  })

  it('crear una puerta la muestra sin recargar', async () => {
    signedInAs('administrator')
    const { site, b, root } = towers()
    const { user } = renderApp(`/espacios?sitio=${site.id}&vista=puertas`)

    await user.click(await screen.findByRole('button', { name: 'Nueva puerta' }))
    const form = screen.getByRole('form', { name: 'Nueva puerta' })
    await user.type(within(form).getByLabelText('Nombre'), 'Cochera Torre B')
    await user.selectOptions(within(form).getByLabelText('Tipo'), 'barrier')
    await user.selectOptions(within(form).getByLabelText('Da a'), b.id)
    await user.selectOptions(within(form).getByLabelText('Se entra desde'), root.id)
    await user.click(within(form).getByRole('radio', { name: /Queda cerrada sin energía/ }))
    await user.click(within(form).getByRole('button', { name: 'Guardar' }))

    const table = await screen.findByRole('table')
    const row = (await within(table).findByText('Cochera Torre B')).closest('tr')!
    expect(within(row).getByText('Barrera')).toBeInTheDocument()
    expect(fake.accessPoints.find((p) => p.name === 'Cochera Torre B')).toMatchObject({
      toSpaceId: b.id,
      fromSpaceId: root.id,
      failMode: 'fail_secure',
      type: 'barrier',
    })
  })

  it('una puerta controlada por un dispositivo no se elimina y el mensaje queda a la vista', async () => {
    signedInAs('administrator')
    const { site, gate } = towers()
    fake.deviceAccessPoints.add(gate.id)
    const { user } = renderApp(`/espacios?sitio=${site.id}&vista=puertas`)

    await user.click(await screen.findByRole('button', { name: 'Eliminar Portón principal' }))
    const dialog = screen.getByRole('alertdialog', { name: '¿Eliminar Portón principal?' })
    await user.click(within(dialog).getByRole('button', { name: 'Eliminar' }))

    expect(await within(dialog).findByText('La puerta la controla un dispositivo.')).toBeInTheDocument()
    expect(fake.accessPoints.some((p) => p.id === gate.id)).toBe(true)
  })

  it('eliminar una puerta libre la saca de la lista', async () => {
    signedInAs('administrator')
    const { site, hallB } = towers()
    const { user } = renderApp(`/espacios?sitio=${site.id}&vista=puertas`)

    await user.click(await screen.findByRole('button', { name: 'Eliminar Hall Torre B' }))
    await user.click(within(screen.getByRole('alertdialog')).getByRole('button', { name: 'Eliminar' }))

    await waitFor(() => expect(screen.queryByText('Hall Torre B')).not.toBeInTheDocument())
    expect(fake.accessPoints.some((p) => p.id === hallB.id)).toBe(false)
  })

  it('el guardia ve las puertas sin acciones', async () => {
    signedInAs('guard')
    const { site } = towers()
    renderApp(`/espacios?sitio=${site.id}&vista=puertas`)

    expect(await screen.findByRole('table')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Nueva puerta' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /^Editar/ })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /^Eliminar/ })).not.toBeInTheDocument()
  })

  it('quien maneja solo la torre A modifica solo las puertas que dan a la torre A y elige destinos dentro de ella', async () => {
    signedInAs('administrator')
    const { site, a } = towers()
    fake.permissionsOverride = [
      { name: 'spaces.read', organization: false, siteIds: [], spaces: [{ siteId: site.id, spaceId: a.id }] },
      { name: 'spaces.manage', organization: false, siteIds: [], spaces: [{ siteId: site.id, spaceId: a.id }] },
    ]
    const { user } = renderApp(`/espacios?sitio=${site.id}&vista=puertas`)

    expect(await screen.findByRole('button', { name: 'Editar Hall Torre A' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Editar Hall Torre B' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Editar Portón principal' })).not.toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Nueva puerta' }))
    const targets = within(screen.getByLabelText('Da a'))
      .getAllByRole('option')
      .map((o) => o.textContent)
    expect(targets).toEqual(['Elegí un espacio', 'Torre A', 'Torre A › Piso 5', 'Torre A › Piso 5 › 5A', 'Torre A › Piso 5 › 5B', 'Torre A › Piso 6', 'Torre A › Piso 6 › 6A'])
    const origins = within(screen.getByLabelText('Se entra desde'))
      .getAllByRole('option')
      .map((o) => o.textContent)
    expect(origins).toContain('Torre Norte')
    expect(origins).toContain('Afuera')
  })
})

describe('Permiso por espacio', () => {
  const site = 's1'
  const session = (grant: Partial<{ organization: boolean; siteIds: string[]; spaces: { siteId: string; spaceId: string }[] }>): Session => ({
    status: 'active',
    user: { id: 'u', email: 'x@nexgate.test', platformOperator: false, activeOrganizationId: 'o', organizations: [] },
    organizationId: 'o',
    permissions: [{ name: 'spaces.manage', organization: false, siteIds: [], spaces: [], ...grant }],
  })
  const unit = { id: 'u5b', siteId: site, ancestorIds: ['root', 'a', 'p5'] }

  it.each([
    ['toda la organización', { organization: true }, true],
    ['el sitio del espacio', { siteIds: [site] }, true],
    ['otro sitio', { siteIds: ['s2'] }, false],
    ['el espacio mismo', { spaces: [{ siteId: site, spaceId: 'u5b' }] }, true],
    ['un ancestro', { spaces: [{ siteId: site, spaceId: 'a' }] }, true],
    ['otra rama', { spaces: [{ siteId: site, spaceId: 'b' }] }, false],
    ['el mismo id en otro sitio', { spaces: [{ siteId: 's2', spaceId: 'a' }] }, false],
  ])('con alcance de %s', (_, grant, expected) => {
    expect(hasPermissionOnSpace(session(grant), 'spaces.manage', unit)).toBe(expected)
  })

  it('sin el permiso no alcanza', () => {
    expect(hasPermissionOnSpace(session({ organization: true }), 'spaces.read', unit)).toBe(false)
  })
})
