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

describe('Pantalla de sitios', () => {
  it('lista los sitios con su tipo y zona horaria', async () => {
    signedInAs('administrator')
    fake.addSite('Torre Alvear')
    renderApp('/sitios')

    expect(await screen.findByText('Torre Alvear')).toBeInTheDocument()
    expect(screen.getByText('Edificio · America/Argentina/Cordoba')).toBeInTheDocument()
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

    expect(await screen.findByText('Edificio Mitre')).toBeInTheDocument()
    expect(screen.getByText('Barrio cerrado · America/Argentina/Cordoba')).toBeInTheDocument()
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
