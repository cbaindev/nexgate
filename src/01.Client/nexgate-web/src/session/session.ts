import { useSyncExternalStore } from 'react'
import { api, authApi, connectSession } from '@/api/client'
import { ApiError, unwrap } from '@/api/errors'
import type { components } from '@/api/schema'

export type CurrentUser = components['schemas']['CurrentUserInfo']
export type Permission = components['schemas']['PermissionResponse']
type TokenResponse = components['schemas']['TokenResponse']

/**
 * Estado de la sesión (design de add-admin-panel-shell, decisión 5). El token de acceso no forma parte del estado: vive
 * solo en una variable de este módulo y nunca se guarda en el navegador.
 */
export type Session =
  | { status: 'restoring' }
  | { status: 'signedOut'; expired: boolean }
  | { status: 'offline' }
  | { status: 'choosingOrganization'; user: CurrentUser; unavailable: boolean }
  | { status: 'active'; user: CurrentUser; organizationId: string | null; permissions: Permission[] }

/** Segundos antes del vencimiento del token de acceso en que se renueva. */
export const RenewBeforeSeconds = 60

let state: Session = { status: 'restoring' }
let accessToken: string | null = null
let renewTimer: ReturnType<typeof setTimeout> | undefined
let renewing: Promise<boolean> | null = null
const listeners = new Set<() => void>()
const resetListeners = new Set<() => void>()

function set(next: Session) {
  state = next
  listeners.forEach((listener) => listener())
}

export const sessionStore = {
  get: () => state,
  subscribe(listener: () => void) {
    listeners.add(listener)
    return () => {
      listeners.delete(listener)
    }
  },
}

export function useSession(): Session {
  return useSyncExternalStore(sessionStore.subscribe, sessionStore.get, sessionStore.get)
}

/** Se llama cada vez que cambian la organización o la sesión: ahí se vacía la caché de datos. */
export function onSessionReset(listener: () => void) {
  resetListeners.add(listener)
  return () => {
    resetListeners.delete(listener)
  }
}

function notifyReset() {
  resetListeners.forEach((listener) => listener())
}

function clearTokens() {
  accessToken = null
  clearTimeout(renewTimer)
  renewTimer = undefined
}

function schedule(expiresIn: number) {
  clearTimeout(renewTimer)
  const seconds = Math.max(5, expiresIn - RenewBeforeSeconds)
  renewTimer = setTimeout(() => void renew(), seconds * 1000)
}

async function loadContext(tokens: TokenResponse, unavailable = false) {
  accessToken = tokens.accessToken
  schedule(tokens.expiresIn)
  const user = unwrap(await api.GET('/api/v1/me'))
  if (tokens.organizationId === null && user.organizations.length > 1) {
    set({ status: 'choosingOrganization', user, unavailable })
    return
  }

  if (tokens.organizationId === null && user.organizations.length === 1) {
    // Una sola membresía (por ejemplo, después de perder otra): se elige sola.
    await chooseOrganization(user.organizations[0]!.id)
    return
  }

  const permissions = tokens.organizationId === null ? [] : unwrap(await api.GET('/api/v1/me/permissions')).permissions
  set({ status: 'active', user, organizationId: tokens.organizationId, permissions })
}

function signedOut(expired: boolean) {
  clearTokens()
  notifyReset()
  set({ status: 'signedOut', expired })
}

/**
 * Cada renovación rota la cookie y el servidor revoca la sesión si ve dos veces el mismo token. Por eso las renovaciones
 * van de a una: en esta pestaña con una cola, y entre pestañas con Web Locks (si el navegador lo soporta). Así cada
 * renovación sale con la cookie vigente.
 */
let refreshQueue: Promise<unknown> = Promise.resolve()

function refreshTokens(organizationId: string | null) {
  const call = () => authApi.POST('/api/v1/auth/refresh', { body: { refreshToken: null, organizationId } })
  const locked = (): ReturnType<typeof call> =>
    typeof navigator !== 'undefined' && navigator.locks
      ? (navigator.locks.request('nexgate-session-refresh', call) as ReturnType<typeof call>)
      : call()
  const next = refreshQueue.then(locked, locked)
  refreshQueue = next.catch(() => undefined)
  return next
}

let restoring: Promise<void> | null = null

/**
 * Al arrancar: renueva con la cookie, si la hay, para no pedir credenciales al recargar. Una sola restauración a la vez
 * (React monta dos veces en desarrollo).
 */
export function restore(): Promise<void> {
  restoring ??= restoreOnce().finally(() => {
    restoring = null
  })
  return restoring
}

async function restoreOnce(): Promise<void> {
  set({ status: 'restoring' })
  try {
    const result = await refreshTokens(null)
    if (!result.response.ok) {
      signedOut(false)
      return
    }

    await loadContext(result.data!)
  } catch (error) {
    if (error instanceof ApiError && error.isNetwork) {
      clearTokens()
      set({ status: 'offline' })
      return
    }

    signedOut(false)
  }
}

/** Inicia sesión con el token de renovación en la cookie. Devuelve el error para mostrarlo, o null. */
export async function signIn(email: string, password: string): Promise<ApiError | null> {
  try {
    const tokens = unwrap(
      await authApi.POST('/api/v1/auth/token', { body: { email, password, organizationId: null, refreshTokenInCookie: true } }),
    )
    notifyReset()
    await loadContext(tokens)
    return null
  } catch (error) {
    return error instanceof ApiError ? error : new ApiError(500)
  }
}

/** Cambia la organización activa renovando con ella: el servidor valida la membresía. */
export async function chooseOrganization(organizationId: string): Promise<ApiError | null> {
  try {
    const result = await refreshTokens(organizationId)
    if (result.response.status === 401) {
      signedOut(true)
      return null
    }

    const tokens = unwrap(result)
    notifyReset()
    await loadContext(tokens)
    return null
  } catch (error) {
    if (error instanceof ApiError && error.status === 403 && state.status !== 'signedOut') {
      // La membresía terminó: se vuelve a elegir con la organización de la sesión, si queda alguna.
      return renewWithoutOrganization()
    }

    return error instanceof ApiError ? error : new ApiError(500)
  }
}

async function renewWithoutOrganization(): Promise<ApiError | null> {
  const result = await refreshTokens(null)
  if (!result.response.ok) {
    signedOut(true)
    return null
  }

  notifyReset()
  await loadContext(result.data!, true)
  return state.status === 'choosingOrganization' ? null : new ApiError(403)
}

/**
 * Renueva el token de acceso con la cookie (programado y ante un 401). Una sola renovación a la vez. Si la sesión ya no
 * se puede renovar, vuelve al inicio de sesión. Si la membresía de la organización activa terminó, pide elegir otra.
 */
export function renew(): Promise<boolean> {
  renewing ??= (async () => {
    try {
      const result = await refreshTokens(null)
      if (!result.response.ok) {
        signedOut(true)
        return false
      }

      const previous = state.status === 'active' ? state.organizationId : null
      const tokens = result.data!
      if (previous !== null && tokens.organizationId !== previous) {
        notifyReset()
        await loadContext(tokens, true)
        return false
      }

      accessToken = tokens.accessToken
      schedule(tokens.expiresIn)
      return true
    } catch {
      return false
    } finally {
      renewing = null
    }
  })()
  return renewing
}

export async function signOut(): Promise<void> {
  try {
    await authApi.POST('/api/v1/auth/logout', { body: { refreshToken: null } })
  } catch {
    // Sin conexión: igual se descarta todo en este dispositivo.
  }

  signedOut(false)
}

connectSession({ accessToken: () => accessToken, renew })

/** Solo para las pruebas: vuelve al estado inicial. */
export function resetSessionForTests() {
  clearTokens()
  renewing = null
  restoring = null
  refreshQueue = Promise.resolve()
  state = { status: 'restoring' }
}

/** El permiso, con cualquier alcance, en la organización activa. */
export function hasPermission(session: Session, permission: string) {
  return session.status === 'active' && session.permissions.some((p) => p.name === permission)
}

/** El permiso con alcance de organización o sobre ese sitio entero. */
export function hasPermissionOnSite(session: Session, permission: string, siteId?: string) {
  if (session.status !== 'active') {
    return false
  }

  const grant = session.permissions.find((p) => p.name === permission)
  return !!grant && (grant.organization || (siteId !== undefined && grant.siteIds.includes(siteId)))
}

/**
 * El permiso sobre un espacio: con alcance de organización, del sitio del espacio, del espacio mismo o de uno de sus
 * ancestros (design de add-spaces-screens, decisión 5). Es solo presentación: el servidor verifica cada operación.
 */
export function hasPermissionOnSpace(
  session: Session,
  permission: string,
  space: { id: string; siteId: string; ancestorIds: string[] },
) {
  if (session.status !== 'active') {
    return false
  }

  const grant = session.permissions.find((p) => p.name === permission)
  if (!grant) {
    return false
  }

  if (grant.organization || grant.siteIds.includes(space.siteId)) {
    return true
  }

  const scoped = new Set(grant.spaces.filter((s) => s.siteId === space.siteId).map((s) => s.spaceId))
  return scoped.has(space.id) || space.ancestorIds.some((id) => scoped.has(id))
}
