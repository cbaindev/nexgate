import { screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { orgA } from './fakeApi'
import { navigationNames, renderApp } from './render'
import { fake } from './setup'

function signedInAs(role: 'administrator' | 'guard' | 'resident', platformOperator = false) {
  fake.roles[orgA.id] = role
  fake.platformOperator = platformOperator
  fake.sessionValid = true
  fake.activeOrganization = orgA.id
}

describe('Navegación por permisos', () => {
  it('el administrador ve todas las secciones de la organización y no la de plataforma', async () => {
    signedInAs('administrator')
    renderApp()

    expect(await navigationNames()).toEqual(['Inicio', 'Sitios', 'Espacios y puertas', 'Accesos', 'Dispositivos', 'Eventos', 'Miembros'])
  })

  it('el guardia no ve miembros', async () => {
    signedInAs('guard')
    renderApp()

    expect(await navigationNames()).toEqual(['Inicio', 'Sitios', 'Espacios y puertas', 'Accesos', 'Dispositivos', 'Eventos'])
  })

  it('el residente ve solo el inicio y los sitios', async () => {
    signedInAs('resident')
    renderApp()

    expect(await navigationNames()).toEqual(['Inicio', 'Sitios'])
  })

  it('el operador de plataforma ve plataforma', async () => {
    signedInAs('resident', true)
    renderApp()

    expect(await navigationNames()).toContain('Plataforma')
  })

  it('una sección sin permiso por dirección muestra "sin permiso" y no pide datos', async () => {
    signedInAs('resident')
    renderApp('/dispositivos')

    expect(await screen.findByText('No tenés permiso para ver esta sección.')).toBeInTheDocument()
    expect(fake.requests).toEqual([])
  })

  it('una sección sin pantallas todavía dice que llega en un próximo cambio', async () => {
    signedInAs('administrator')
    renderApp('/miembros')

    expect(await screen.findByText('Esta sección llega en un próximo cambio.')).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Miembros' })).toBeInTheDocument()
  })

  it('una dirección inexistente muestra "página no encontrada"', async () => {
    signedInAs('administrator')
    renderApp('/no-existe')

    expect(await screen.findByText('No existe la página que buscás.')).toBeInTheDocument()
  })
})
