import { screen, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { orgA } from './fakeApi'
import { renderApp } from './render'
import { fake } from './setup'

function signedInAs(role: 'administrator' | 'resident') {
  fake.roles[orgA.id] = role
  fake.sessionValid = true
  fake.activeOrganization = orgA.id
}

/** Tarjeta de un sitio del listado. */
async function siteCard(name: string) {
  return (await screen.findByRole('heading', { name })).closest('li')!
}

describe('Pantalla de sitios', () => {
  it('lista los sitios con su tipo y zona horaria', async () => {
    signedInAs('administrator')
    fake.addSite('Torre Alvear')
    renderApp('/sitios')

    const card = await siteCard('Torre Alvear')
    expect(within(card).getByText('Edificio')).toBeInTheDocument()
    expect(within(card).getByText('America/Argentina/Cordoba')).toBeInTheDocument()
  })

  it('el administrador crea un sitio y aparece sin recargar', async () => {
    signedInAs('administrator')
    const { user } = renderApp('/sitios')
    await screen.findByText('Todavía no hay sitios.')

    await user.click(screen.getByRole('button', { name: 'Nuevo sitio' }))
    const form = screen.getByRole('form', { name: 'Nuevo sitio' })
    await user.type(within(form).getByLabelText('Nombre'), 'Edificio Mitre')
    await user.selectOptions(within(form).getByLabelText('Tipo'), 'gated_community')
    await user.click(within(form).getByRole('button', { name: 'Guardar' }))

    const card = await siteCard('Edificio Mitre')
    expect(within(card).getByText('Barrio cerrado')).toBeInTheDocument()
    expect(within(card).getByText('America/Argentina/Cordoba')).toBeInTheDocument()
    expect(screen.queryByRole('form', { name: 'Nuevo sitio' })).not.toBeInTheDocument()
  })

  it('sin permiso de gestión no muestra acciones', async () => {
    signedInAs('resident')
    fake.addSite('Torre Alvear')
    renderApp('/sitios')

    expect(await screen.findByText('Torre Alvear')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Nuevo sitio' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /Editar/ })).not.toBeInTheDocument()
  })

  it('un nombre vacío se marca junto al campo, y el mensaje del servidor también', async () => {
    signedInAs('administrator')
    const { user } = renderApp('/sitios')
    await screen.findByText('Todavía no hay sitios.')
    await user.click(screen.getByRole('button', { name: 'Nuevo sitio' }))
    const form = screen.getByRole('form', { name: 'Nuevo sitio' })

    await user.click(within(form).getByRole('button', { name: 'Guardar' }))
    expect(within(form).getByText('El nombre es obligatorio.')).toBeInTheDocument()

    fake.failNext = { path: '/sites:post', status: 400, body: { errors: { Name: ['Ya existe un sitio con ese nombre.'] } } }
    await user.type(within(form).getByLabelText('Nombre'), 'Torre')
    await user.click(within(form).getByRole('button', { name: 'Guardar' }))
    expect(await within(form).findByText('Ya existe un sitio con ese nombre.')).toBeInTheDocument()
    expect(within(form).getByLabelText('Nombre')).toHaveAttribute('aria-invalid', 'true')
  })

  it('un error inesperado muestra el código de referencia', async () => {
    signedInAs('administrator')
    fake.failNext = { path: '/sites', status: 500 }
    renderApp('/sitios')

    expect(await screen.findByText('Ocurrió un error inesperado.')).toBeInTheDocument()
    expect(screen.getByText('Código de referencia: corr-500')).toBeInTheDocument()
  })

  it('un 403 dice que no hay permiso', async () => {
    signedInAs('administrator')
    const { user } = renderApp('/sitios')
    await screen.findByText('Todavía no hay sitios.')
    await user.click(screen.getByRole('button', { name: 'Nuevo sitio' }))
    const form = screen.getByRole('form', { name: 'Nuevo sitio' })
    fake.failNext = { path: '/sites:post', status: 403 }

    await user.type(within(form).getByLabelText('Nombre'), 'Torre')
    await user.click(within(form).getByRole('button', { name: 'Guardar' }))

    expect(await within(form).findByText('No tenés permiso para esta operación.')).toBeInTheDocument()
  })
})

describe('Estados de carga y vacíos', () => {
  it('mientras llega el listado muestra esqueletos y anuncia la carga', async () => {
    signedInAs('administrator')
    fake.addSite('Torre Alvear')
    let release!: () => void
    fake.sitesGate = new Promise((resolve) => (release = resolve))
    renderApp('/sitios')

    expect(await screen.findAllByTestId('site-skeleton')).toHaveLength(3)
    expect(screen.getByRole('status')).toHaveTextContent('Cargando…')
    expect(screen.getByRole('status').closest('[aria-busy="true"]')).not.toBeNull()

    release()
    expect(await screen.findByText('Torre Alvear')).toBeInTheDocument()
    expect(screen.queryByTestId('site-skeleton')).not.toBeInTheDocument()
  })

  it('sin sitios, el administrador ve el estado vacío con la acción de crear el primero', async () => {
    signedInAs('administrator')
    const { user } = renderApp('/sitios')

    expect(await screen.findByRole('heading', { name: 'Todavía no hay sitios.' })).toBeInTheDocument()
    expect(screen.getByText(/Un sitio es un edificio/)).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Crear el primer sitio' }))
    expect(screen.getByRole('form', { name: 'Nuevo sitio' })).toBeInTheDocument()
  })

  it('sin permiso de gestión, el estado vacío no ofrece crear', async () => {
    signedInAs('resident')
    renderApp('/sitios')

    expect(await screen.findByRole('heading', { name: 'Todavía no hay sitios.' })).toBeInTheDocument()
    expect(screen.getByText('Cuando tengas acceso a un sitio, lo vas a ver acá.')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Crear el primer sitio' })).not.toBeInTheDocument()
  })
})

describe('Formulario en el panel lateral', () => {
  it('se cierra con Escape sin guardar y edita un sitio existente', async () => {
    signedInAs('administrator')
    fake.addSite('Torre Alvear')
    const { user } = renderApp('/sitios')
    await siteCard('Torre Alvear')

    await user.click(screen.getByRole('button', { name: 'Nuevo sitio' }))
    expect(screen.getByRole('dialog', { name: 'Nuevo sitio' })).toBeInTheDocument()
    await user.keyboard('{Escape}')
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Editar Torre Alvear' }))
    const form = screen.getByRole('form', { name: 'Editar sitio' })
    expect(within(form).getByLabelText('Nombre')).toHaveValue('Torre Alvear')
    await user.clear(within(form).getByLabelText('Nombre'))
    await user.type(within(form).getByLabelText('Nombre'), 'Torre Alvear II')
    await user.click(within(form).getByRole('button', { name: 'Guardar' }))

    expect(await screen.findByRole('heading', { name: 'Torre Alvear II' })).toBeInTheDocument()
  })
})
