import { http, HttpResponse } from 'msw'
import type { components } from '@/api/schema'

type Site = components['schemas']['SiteDto']
type Permission = components['schemas']['PermissionResponse']

export type Role = 'administrator' | 'guard' | 'resident'

/** Permisos por rol, como el catálogo de Identity. */
const permissionsByRole: Record<Role, string[]> = {
  administrator: [
    'access.manage', 'access.read', 'devices.manage', 'devices.read', 'events.read', 'members.manage', 'members.read',
    'organization.read', 'sites.manage', 'sites.read', 'spaces.manage', 'spaces.read',
  ],
  guard: ['access.read', 'devices.read', 'events.read', 'organization.read', 'sites.read', 'spaces.read'],
  resident: ['organization.read', 'sites.read'],
}

export const orgA = { id: '11111111-1111-4111-8111-111111111111', name: 'Consorcio Alvear' }
export const orgB = { id: '22222222-2222-4222-8222-222222222222', name: 'Barrio Las Lomas' }

const problem = (status: number, title: string, code: string, extra: Record<string, unknown> = {}) =>
  HttpResponse.json({ type: 'about:blank', title, status, code, correlationId: `corr-${status}`, ...extra }, { status })

/**
 * API simulada para las pruebas de la SPA. La "cookie" de renovación es <c>sessionValid</c>: jsdom no maneja cookies
 * httpOnly, y lo que importa es que la SPA nunca toca el token de renovación.
 */
export class FakeApi {
  email = 'admin@nexgate.test'
  password = 'contraseña-segura-123'
  organizations = [orgA]
  roles: Record<string, Role> = { [orgA.id]: 'administrator', [orgB.id]: 'administrator' }
  platformOperator = false
  sessionValid = false
  activeOrganization: string | null = null
  tokenCount = 0
  accessToken: string | null = null
  expiresIn = 900
  signInStatus: number | null = null
  sites: Site[] = []
  signInBodies: unknown[] = []
  refreshBodies: unknown[] = []
  logoutCount = 0
  requests: string[] = []
  failNext: { path: string; status: number; body?: Record<string, unknown> } | null = null

  private issue() {
    this.tokenCount += 1
    this.accessToken = `access-${this.tokenCount}`
    return { accessToken: this.accessToken, tokenType: 'Bearer', expiresIn: this.expiresIn, refreshToken: null, organizationId: this.activeOrganization }
  }

  private authorized(request: Request) {
    return request.headers.get('Authorization') === `Bearer ${this.accessToken}`
  }

  private permissions(): Permission[] {
    if (this.activeOrganization === null) {
      return []
    }

    return (permissionsByRole[this.roles[this.activeOrganization] ?? 'resident'] ?? []).map((name) => ({
      name,
      organization: true,
      siteIds: [],
      spaces: [],
    }))
  }

  handlers() {
    const fail = (path: string) => {
      if (this.failNext?.path === path) {
        const { status, body } = this.failNext
        this.failNext = null
        return problem(status, 'Error', status === 500 ? 'internal_error' : 'error', body)
      }

      return null
    }

    return [
      http.post('*/api/v1/auth/token', async ({ request }) => {
        const body = (await request.json()) as { email: string; password: string; refreshTokenInCookie?: boolean }
        this.signInBodies.push(body)
        if (this.signInStatus === 429) {
          return problem(429, 'Demasiados intentos. Probá de nuevo en un minuto.', 'too_many_requests')
        }

        if (body.email !== this.email || body.password !== this.password) {
          return problem(401, 'Credenciales inválidas', 'invalid_credentials')
        }

        this.sessionValid = true
        this.activeOrganization = this.organizations.length === 1 ? this.organizations[0]!.id : null
        return HttpResponse.json(this.issue())
      }),
      http.post('*/api/v1/auth/refresh', async ({ request }) => {
        const body = (await request.json()) as { refreshToken: string | null; organizationId: string | null }
        this.refreshBodies.push(body)
        if (!this.sessionValid) {
          return problem(401, 'Token de renovación inválido', 'invalid_refresh_token')
        }

        if (body.organizationId) {
          if (!this.organizations.some((o) => o.id === body.organizationId)) {
            return problem(403, 'Organización no disponible', 'organization_not_available')
          }

          this.activeOrganization = body.organizationId
        } else if (this.activeOrganization && !this.organizations.some((o) => o.id === this.activeOrganization)) {
          this.activeOrganization = null
        }

        return HttpResponse.json(this.issue())
      }),
      http.post('*/api/v1/auth/logout', () => {
        this.logoutCount += 1
        this.sessionValid = false
        return new HttpResponse(null, { status: 204 })
      }),
      http.get('*/api/v1/me', ({ request }) => {
        if (!this.authorized(request)) {
          return problem(401, 'Se requiere autenticación', 'authentication_required')
        }

        return HttpResponse.json({
          id: '99999999-9999-4999-8999-999999999999',
          email: this.email,
          platformOperator: this.platformOperator,
          activeOrganizationId: this.activeOrganization,
          organizations: this.organizations,
        })
      }),
      http.get('*/api/v1/me/permissions', ({ request }) => {
        if (!this.authorized(request)) {
          return problem(401, 'Se requiere autenticación', 'authentication_required')
        }

        return HttpResponse.json({ organizationId: this.activeOrganization, permissions: this.permissions() })
      }),
      http.get('*/api/v1/sites', ({ request }) => {
        this.requests.push('GET /sites')
        if (!this.authorized(request)) {
          return problem(401, 'Se requiere autenticación', 'authentication_required')
        }

        return fail('/sites') ?? HttpResponse.json(this.sites.filter((s) => s.organizationId === this.activeOrganization))
      }),
      http.post('*/api/v1/sites', async ({ request }) => {
        if (!this.authorized(request)) {
          return problem(401, 'Se requiere autenticación', 'authentication_required')
        }

        const failure = fail('/sites:post')
        if (failure) {
          return failure
        }

        const body = (await request.json()) as { name: string; type: string; timeZone: string }
        const site: Site = {
          id: crypto.randomUUID(),
          organizationId: this.activeOrganization!,
          name: body.name,
          type: body.type,
          timeZone: body.timeZone,
          configuration: {},
          createdAt: new Date().toISOString(),
          updatedAt: new Date().toISOString(),
        }
        this.sites.push(site)
        return HttpResponse.json(site, { status: 201 })
      }),
    ]
  }

  addSite(name: string, organizationId = orgA.id): Site {
    const site: Site = {
      id: crypto.randomUUID(),
      organizationId,
      name,
      type: 'building',
      timeZone: 'America/Argentina/Cordoba',
      configuration: {},
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
    }
    this.sites.push(site)
    return site
  }
}
