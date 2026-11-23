import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, useLocation } from 'react-router'
import { App, createQueryClient } from '@/app/App'
import { fake } from './setup'

/** Dirección actual de la aplicación montada (ruta y consulta), para verificar lo que queda en la dirección. */
export const location = { current: '' }

function LocationProbe() {
  const { pathname, search } = useLocation()
  location.current = pathname + search
  return null
}

/** Monta la aplicación en una ruta, como al abrir esa dirección (restaura la sesión al arrancar). */
export function renderApp(path = '/') {
  const queryClient = createQueryClient()
  const user = userEvent.setup()
  const result = render(
    <MemoryRouter initialEntries={[path]}>
      <App queryClient={queryClient} />
      <LocationProbe />
    </MemoryRouter>,
  )
  return { ...result, user, queryClient }
}

/** Abre la aplicación e inicia sesión con las credenciales de la API simulada. */
export async function signInAt(path = '/') {
  const rendered = renderApp(path)
  await screen.findByRole('button', { name: 'Ingresar' })
  await rendered.user.type(screen.getByLabelText('Correo electrónico'), fake.email)
  await rendered.user.type(screen.getByLabelText('Contraseña'), fake.password)
  await rendered.user.click(screen.getByRole('button', { name: 'Ingresar' }))
  return rendered
}

/** Nombres de las secciones de la navegación lateral. */
export async function navigationNames() {
  await waitFor(() => expect(screen.getAllByRole('navigation', { name: 'Navegación principal' })[0]).toBeInTheDocument())
  const nav = screen.getAllByRole('navigation', { name: 'Navegación principal' })[0]!
  return Array.from(nav.querySelectorAll('a')).map((a) => a.textContent?.trim())
}
