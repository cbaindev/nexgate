import { useSyncExternalStore } from 'react'

/**
 * Tema claro, oscuro o del sistema (design de refine-admin-panel-design, decisión 3).
 *
 * La preferencia es de presentación, no de sesión: es lo único que la aplicación guarda en el navegador, con la clave
 * `nexgate.theme`, y por eso este es el único archivo con excepción a la regla de almacenamiento del lint.
 * `public/theme-init.js` aplica la misma preferencia antes del primer pintado; los dos deben leer la misma clave.
 */
export type ThemePreference = 'light' | 'dark' | 'system'

export const themeStorageKey = 'nexgate.theme'
export const themePreferences: ThemePreference[] = ['light', 'dark', 'system']

/** Color de la barra del navegador y de la ventana instalada en cada tema: índigo de la marca, más oscuro en el tema oscuro. */
const themeColors = { light: '#3f35c8', dark: '#161a33' } as const

const listeners = new Set<() => void>()
let preference: ThemePreference = read()

function read(): ThemePreference {
  try {
    const stored = localStorage.getItem(themeStorageKey)
    return themePreferences.includes(stored as ThemePreference) ? (stored as ThemePreference) : 'system'
  } catch {
    // Almacenamiento bloqueado: se sigue al sistema.
    return 'system'
  }
}

function systemQuery() {
  return typeof window.matchMedia === 'function' ? window.matchMedia('(prefers-color-scheme: dark)') : null
}

export function resolvedTheme(value: ThemePreference = preference): 'light' | 'dark' {
  if (value !== 'system') {
    return value
  }

  return systemQuery()?.matches ? 'dark' : 'light'
}

function apply() {
  const theme = resolvedTheme()
  document.documentElement.classList.toggle('dark', theme === 'dark')
  document.documentElement.style.colorScheme = theme
  document.querySelector('meta[name="theme-color"]')?.setAttribute('content', themeColors[theme])
  listeners.forEach((listener) => listener())
}

export function setThemePreference(value: ThemePreference) {
  preference = value
  try {
    if (value === 'system') {
      localStorage.removeItem(themeStorageKey)
    } else {
      localStorage.setItem(themeStorageKey, value)
    }
  } catch {
    // Sin almacenamiento, la elección dura hasta recargar.
  }

  apply()
}

export function getThemePreference() {
  return preference
}

/** Aplica la preferencia guardada y sigue los cambios del sistema mientras la preferencia sea "sistema". */
export function initTheme(): () => void {
  preference = read()
  apply()
  const query = systemQuery()
  const onChange = () => {
    if (preference === 'system') {
      apply()
    }
  }

  query?.addEventListener('change', onChange)
  return () => query?.removeEventListener('change', onChange)
}

function subscribe(listener: () => void) {
  listeners.add(listener)
  return () => {
    listeners.delete(listener)
  }
}

/** Preferencia actual y tema resuelto, para el selector. */
export function useTheme() {
  const value = useSyncExternalStore(subscribe, () => preference)
  const resolved = useSyncExternalStore(subscribe, () => resolvedTheme())
  return { preference: value, resolved }
}
