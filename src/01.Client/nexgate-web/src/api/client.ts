import createClient from 'openapi-fetch'
import { networkError } from './errors'
import type { paths } from './schema'

/** Origen de la API: el mismo de la SPA (design de add-admin-panel-shell, decisión 4). */
export const baseUrl = typeof window !== 'undefined' ? window.location.origin : ''

/** Lo que el cliente necesita de la sesión. Lo registra el módulo de sesión (sin dependencia circular). */
export interface SessionBridge {
  accessToken(): string | null
  /** Renueva la sesión; true si hay un token nuevo. */
  renew(): Promise<boolean>
}

let bridge: SessionBridge = { accessToken: () => null, renew: async () => false }

export function connectSession(session: SessionBridge) {
  bridge = session
}

async function send(request: Request): Promise<Response> {
  try {
    return await fetch(request)
  } catch {
    throw networkError()
  }
}

/**
 * Agrega el token de acceso y, ante un 401, renueva la sesión una sola vez y reintenta (decisión 2). La cookie de
 * renovación la maneja el navegador: este código nunca la ve.
 */
async function authorizedFetch(input: Request): Promise<Response> {
  const retry = input.clone()
  const token = bridge.accessToken()
  if (token) {
    input.headers.set('Authorization', `Bearer ${token}`)
  }

  const response = await send(input)
  if (response.status !== 401 || !token || !(await bridge.renew())) {
    return response
  }

  retry.headers.set('Authorization', `Bearer ${bridge.accessToken()}`)
  return send(retry)
}

/** Cliente de las operaciones de negocio, con el token de la sesión. */
export const api = createClient<paths>({ baseUrl, fetch: authorizedFetch })

/** Cliente de las operaciones de autenticación: sin token, con la cookie de renovación. */
export const authApi = createClient<paths>({ baseUrl, fetch: send, credentials: 'same-origin' })
