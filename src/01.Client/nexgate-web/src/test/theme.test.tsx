import { act, screen } from '@testing-library/react'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { initTheme, resolvedTheme, setThemePreference, themeStorageKey } from '@/theme/theme'
import { orgA } from './fakeApi'
import { renderApp, signInAt } from './render'
import { fake } from './setup'

// Tema claro, oscuro o del sistema (spec admin-shell, requirement "Color theme").

/** Simula `prefers-color-scheme` del sistema operativo, con aviso de cambios. */
function emulateSystem(initial: 'light' | 'dark') {
  let dark = initial === 'dark'
  const listeners = new Set<() => void>()
  window.matchMedia = ((query: string) => ({
    get matches() {
      return query.includes('dark') ? dark : !dark
    },
    media: query,
    addEventListener: (_: string, listener: () => void) => listeners.add(listener),
    removeEventListener: (_: string, listener: () => void) => listeners.delete(listener),
  })) as unknown as typeof window.matchMedia
  return (next: 'light' | 'dark') => {
    dark = next === 'dark'
    listeners.forEach((listener) => listener())
  }
}

const isDark = () => document.documentElement.classList.contains('dark')
let stop: () => void = () => {}

beforeEach(() => {
  localStorage.clear()
  document.documentElement.className = ''
})

afterEach(() => {
  stop()
  localStorage.clear()
})

describe('Tema', () => {
  it('sin elección sigue al sistema, también cuando el sistema cambia', () => {
    const setSystem = emulateSystem('dark')
    stop = initTheme()
    expect(isDark()).toBe(true)

    act(() => setSystem('light'))
    expect(isDark()).toBe(false)
  })

  it('el usuario elige claro desde el menú aunque el sistema esté en oscuro, y se mantiene al recargar', async () => {
    emulateSystem('dark')
    stop = initTheme()
    fake.roles[orgA.id] = 'administrator'
    fake.sessionValid = true
    fake.activeOrganization = orgA.id
    const { user, unmount } = renderApp()
    await screen.findByText(`Hola, ${fake.email}`)

    await user.click(screen.getByRole('button', { name: 'Menú de la cuenta' }))
    await user.click(await screen.findByRole('menuitemradio', { name: 'Claro' }))
    expect(isDark()).toBe(false)

    // Recargar: la preferencia guardada se vuelve a aplicar.
    unmount()
    stop()
    document.documentElement.className = 'dark'
    stop = initTheme()
    expect(isDark()).toBe(false)
    expect(resolvedTheme()).toBe('light')
  })

  it('la elección sobrevive al cierre de sesión', async () => {
    emulateSystem('light')
    stop = initTheme()
    const { user } = await signInAt()
    await screen.findByText(`Hola, ${fake.email}`)
    await user.click(screen.getByRole('button', { name: 'Menú de la cuenta' }))
    await user.click(await screen.findByRole('menuitemradio', { name: 'Oscuro' }))
    expect(isDark()).toBe(true)

    await user.click(screen.getByRole('button', { name: 'Menú de la cuenta' }))
    await user.click(await screen.findByRole('menuitem', { name: 'Cerrar sesión' }))
    await screen.findByRole('button', { name: 'Ingresar' })

    expect(isDark()).toBe(true)
    // La pantalla de inicio de sesión también tiene el selector.
    expect(screen.getByRole('button', { name: 'Tema' })).toBeInTheDocument()
  })

  it('lo único guardado en el navegador es la preferencia de tema', async () => {
    emulateSystem('light')
    stop = initTheme()
    await signInAt()
    await screen.findByText(`Hola, ${fake.email}`)
    act(() => setThemePreference('dark'))

    expect(Object.keys(localStorage)).toEqual([themeStorageKey])
    expect(localStorage.getItem(themeStorageKey)).toBe('dark')
    expect(sessionStorage.length).toBe(0)

    act(() => setThemePreference('system'))
    expect(localStorage.length).toBe(0)
  })

  it('sin destello: el script del encabezado aplica el tema guardado antes de cargar la aplicación', () => {
    emulateSystem('light')
    localStorage.setItem(themeStorageKey, 'dark')
    const script = readFileSync(join(__dirname, '..', '..', 'public', 'theme-init.js'), 'utf8')

    new Function(script)()

    expect(isDark()).toBe(true)
    expect(readFileSync(join(__dirname, '..', '..', 'index.html'), 'utf8')).toMatch(
      /<head>[\s\S]*<script src="\/theme-init.js"><\/script>[\s\S]*<\/head>/,
    )
  })

  it('el script ignora valores desconocidos y sigue al sistema', () => {
    emulateSystem('light')
    localStorage.setItem(themeStorageKey, '<script>')
    const script = readFileSync(join(__dirname, '..', '..', 'public', 'theme-init.js'), 'utf8')

    new Function(script)()

    expect(isDark()).toBe(false)
  })
})
