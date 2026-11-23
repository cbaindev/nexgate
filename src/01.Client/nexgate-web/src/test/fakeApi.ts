import { http, HttpResponse } from 'msw'
import type { components } from '@/api/schema'
import { siteDayAndMinute } from '@/lib/zoned-time'

type Site = components['schemas']['SiteDto']
type Permission = components['schemas']['PermissionResponse']
type Space = components['schemas']['SpaceDto']
type AccessPoint = components['schemas']['AccessPointDto']
type AccessPointBody = Omit<AccessPoint, 'id' | 'siteId'>
type Subject = components['schemas']['SubjectDto']
type Schedule = components['schemas']['ScheduleDto']
type Credential = components['schemas']['CredentialDto']
type Grant = components['schemas']['GrantDto']
/** La API guarda solo un hash; la simulada guarda el valor para detectar duplicados y simular, y nunca lo devuelve. */
type StoredCredential = Credential & { value: string }

const publicCredential = (stored: StoredCredential): Credential => {
  const credential: Partial<StoredCredential> = { ...stored }
  delete credential.value
  return credential as Credential
}

const minutes = (time: string) => {
  const [h, m] = time.split(':').map(Number)
  return h! * 60 + m!
}

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
  /** Si está, el listado de sitios espera a que se resuelva (para ver el estado de carga). */
  sitesGate: Promise<void> | null = null
  spaces: Space[] = []
  accessPoints: AccessPoint[] = []
  /** Puertas que controla algún dispositivo: no se pueden eliminar. */
  deviceAccessPoints = new Set<string>()
  subjects: Subject[] = []
  schedules: Schedule[] = []
  credentials: StoredCredential[] = []
  grants: Grant[] = []
  /** Cuerpos de las pruebas de acceso recibidas (para verificar qué envía la SPA). */
  simulationBodies: unknown[] = []
  /** Si está, reemplaza los permisos del rol (para probar alcances por espacio). */
  permissionsOverride: Permission[] | null = null

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

    if (this.permissionsOverride) {
      return this.permissionsOverride
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
      http.get('*/api/v1/sites', async ({ request }) => {
        this.requests.push('GET /sites')
        await this.sitesGate
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
      http.put('*/api/v1/sites/:id', async ({ request, params }) => {
        if (!this.authorized(request)) {
          return problem(401, 'Se requiere autenticación', 'authentication_required')
        }

        const site = this.sites.find((s) => s.id === params.id && s.organizationId === this.activeOrganization)
        if (!site) {
          return problem(404, 'No encontrado', 'not_found')
        }

        const body = (await request.json()) as { name: string; timeZone: string }
        Object.assign(site, { name: body.name, timeZone: body.timeZone, updatedAt: new Date().toISOString() })
        return HttpResponse.json(site)
      }),
      http.get('*/api/v1/sites/:siteId/spaces', ({ request, params }) => {
        this.requests.push(`GET /sites/${params.siteId as string}/spaces`)
        return this.guard(request, params.siteId as string) ?? HttpResponse.json(this.spaces.filter((s) => s.siteId === params.siteId))
      }),
      http.post('*/api/v1/sites/:siteId/spaces', async ({ request, params }) => {
        const siteId = params.siteId as string
        const denied = this.guard(request, siteId)
        if (denied) {
          return denied
        }

        const body = (await request.json()) as { name: string; type: string; parentId: string | null; isCommon: boolean }
        if (!body.name?.trim()) {
          return problem(400, 'Datos inválidos', 'validation', { errors: { Name: ['El nombre es obligatorio.'] } })
        }

        const parent = body.parentId ? this.spaces.find((s) => s.id === body.parentId && s.siteId === siteId) : null
        if (body.parentId && !parent) {
          return problem(400, 'Datos inválidos', 'validation', { errors: { ParentId: ['El espacio padre no existe.'] } })
        }

        if (!parent && this.spaces.some((s) => s.siteId === siteId && s.parentId === null)) {
          return problem(409, 'El sitio ya tiene un espacio raíz.', 'space_root_exists')
        }

        if (this.siblingNamed(siteId, body.parentId, body.name)) {
          return problem(409, 'Ya hay un espacio con ese nombre en el mismo lugar.', 'space_name_taken')
        }

        const space = this.addSpace(siteId, body.name, body.type, parent ?? null, body.isCommon)
        return HttpResponse.json(space, { status: 201 })
      }),
      http.put('*/api/v1/spaces/:id', async ({ request, params }) => {
        const space = this.spaces.find((s) => s.id === params.id)
        const denied = this.guard(request, space?.siteId)
        if (denied || !space) {
          return denied ?? problem(404, 'No encontrado', 'not_found')
        }

        const body = (await request.json()) as { name: string; type: string; isCommon: boolean }
        if (!body.name?.trim()) {
          return problem(400, 'Datos inválidos', 'validation', { errors: { Name: ['El nombre es obligatorio.'] } })
        }

        if (this.siblingNamed(space.siteId, space.parentId, body.name, space.id)) {
          return problem(409, 'Ya hay un espacio con ese nombre en el mismo lugar.', 'space_name_taken')
        }

        Object.assign(space, { name: body.name, type: body.type, isCommon: body.isCommon })
        return HttpResponse.json(space)
      }),
      http.delete('*/api/v1/spaces/:id', ({ request, params }) => {
        const space = this.spaces.find((s) => s.id === params.id)
        const denied = this.guard(request, space?.siteId)
        if (denied || !space) {
          return denied ?? problem(404, 'No encontrado', 'not_found')
        }

        const used =
          this.spaces.some((s) => s.parentId === space.id) ||
          this.accessPoints.some((a) => a.toSpaceId === space.id || a.fromSpaceId === space.id)
        if (used) {
          return problem(409, 'El espacio tiene espacios o puertas adentro.', 'space_in_use')
        }

        this.spaces = this.spaces.filter((s) => s.id !== space.id)
        return new HttpResponse(null, { status: 204 })
      }),
      http.get('*/api/v1/spaces/:id/access-path', ({ request, params }) => {
        this.requests.push(`GET /spaces/${params.id as string}/access-path`)
        const space = this.spaces.find((s) => s.id === params.id)
        const denied = this.guard(request, space?.siteId)
        if (denied || !space) {
          return denied ?? problem(404, 'No encontrado', 'not_found')
        }

        const path = [...space.ancestorIds, space.id]
        const depth = (id: string) => this.spaces.find((s) => s.id === id)!.depth
        return HttpResponse.json(
          this.accessPoints
            .filter((a) => path.includes(a.toSpaceId))
            .map((a) => ({ ...a, toSpaceDepth: depth(a.toSpaceId) }))
            .sort((a, b) => a.toSpaceDepth - b.toSpaceDepth || a.name.localeCompare(b.name)),
        )
      }),
      http.get('*/api/v1/sites/:siteId/access-points', ({ request, params }) => {
        return (
          this.guard(request, params.siteId as string) ?? HttpResponse.json(this.accessPoints.filter((a) => a.siteId === params.siteId))
        )
      }),
      http.post('*/api/v1/sites/:siteId/access-points', async ({ request, params }) => {
        const siteId = params.siteId as string
        const denied = this.guard(request, siteId)
        if (denied) {
          return denied
        }

        const body = (await request.json()) as AccessPointBody
        const invalid = this.invalidAccessPoint(siteId, body)
        if (invalid) {
          return invalid
        }

        const point: AccessPoint = { id: crypto.randomUUID(), siteId, ...body }
        this.accessPoints.push(point)
        return HttpResponse.json(point, { status: 201 })
      }),
      http.put('*/api/v1/access-points/:id', async ({ request, params }) => {
        const point = this.accessPoints.find((a) => a.id === params.id)
        const denied = this.guard(request, point?.siteId)
        if (denied || !point) {
          return denied ?? problem(404, 'No encontrado', 'not_found')
        }

        const body = (await request.json()) as AccessPointBody
        const invalid = this.invalidAccessPoint(point.siteId, body, point.id)
        if (invalid) {
          return invalid
        }

        Object.assign(point, body)
        return HttpResponse.json(point)
      }),
      http.delete('*/api/v1/access-points/:id', ({ request, params }) => {
        const point = this.accessPoints.find((a) => a.id === params.id)
        const denied = this.guard(request, point?.siteId)
        if (denied || !point) {
          return denied ?? problem(404, 'No encontrado', 'not_found')
        }

        if (this.deviceAccessPoints.has(point.id)) {
          return problem(409, 'La puerta la controla un dispositivo.', 'access_point_in_use')
        }

        this.accessPoints = this.accessPoints.filter((a) => a.id !== point.id)
        return new HttpResponse(null, { status: 204 })
      }),
      ...this.accessHandlers(),
    ]
  }

  /** Sujetos, horarios, credenciales, permisos y prueba de acceso (cambio add-access-screens). */
  private accessHandlers() {
    const notFound = () => problem(404, 'No encontrado', 'not_found')
    const invalid = (errors: Record<string, string[]>) => problem(400, 'Datos inválidos', 'validation', { errors })
    const siteOf = (siteId: string) => this.sites.find((s) => s.id === siteId)!

    return [
      http.get('*/api/v1/sites/:siteId/subjects', ({ request, params }) => {
        this.requests.push(`GET /sites/${params.siteId as string}/subjects`)
        return (
          this.guard(request, params.siteId as string) ??
          HttpResponse.json(this.subjects.filter((s) => s.siteId === params.siteId).sort((a, b) => a.alias.localeCompare(b.alias)))
        )
      }),
      http.post('*/api/v1/sites/:siteId/subjects', async ({ request, params }) => {
        const denied = this.guard(request, params.siteId as string)
        if (denied) return denied
        const body = (await request.json()) as { kind: string; alias: string }
        if (!body.alias?.trim()) return invalid({ Alias: ['El alias es obligatorio.'] })
        return HttpResponse.json(this.addSubject(params.siteId as string, body.alias, body.kind), { status: 201 })
      }),
      http.put('*/api/v1/subjects/:id', async ({ request, params }) => {
        const subject = this.subjects.find((s) => s.id === params.id)
        const denied = this.guard(request, subject?.siteId)
        if (denied || !subject) return denied ?? notFound()
        const body = (await request.json()) as { kind: string; alias: string }
        if (!body.alias?.trim()) return invalid({ Alias: ['El alias es obligatorio.'] })
        Object.assign(subject, { kind: body.kind, alias: body.alias })
        return HttpResponse.json(subject)
      }),
      http.delete('*/api/v1/subjects/:id', ({ request, params }) => {
        const subject = this.subjects.find((s) => s.id === params.id)
        const denied = this.guard(request, subject?.siteId)
        if (denied || !subject) return denied ?? notFound()
        if (this.credentials.some((c) => c.subjectId === subject.id) || this.grants.some((g) => g.subjectId === subject.id)) {
          return problem(409, 'El sujeto tiene credenciales o permisos.', 'subject_in_use')
        }
        this.subjects = this.subjects.filter((s) => s.id !== subject.id)
        return new HttpResponse(null, { status: 204 })
      }),

      http.get('*/api/v1/sites/:siteId/schedules', ({ request, params }) => {
        return this.guard(request, params.siteId as string) ?? HttpResponse.json(this.schedules.filter((s) => s.siteId === params.siteId))
      }),
      http.post('*/api/v1/sites/:siteId/schedules', async ({ request, params }) => {
        const siteId = params.siteId as string
        const denied = this.guard(request, siteId)
        if (denied) return denied
        const body = (await request.json()) as { name: string; windows: Schedule['windows'] }
        const error = this.invalidSchedule(siteId, body)
        if (error) return error
        return HttpResponse.json(this.addSchedule(siteId, body.name, body.windows), { status: 201 })
      }),
      http.put('*/api/v1/schedules/:id', async ({ request, params }) => {
        const schedule = this.schedules.find((s) => s.id === params.id)
        const denied = this.guard(request, schedule?.siteId)
        if (denied || !schedule) return denied ?? notFound()
        const body = (await request.json()) as { name: string; windows: Schedule['windows'] }
        const error = this.invalidSchedule(schedule.siteId, body, schedule.id)
        if (error) return error
        Object.assign(schedule, { name: body.name, windows: body.windows })
        return HttpResponse.json(schedule)
      }),
      http.delete('*/api/v1/schedules/:id', ({ request, params }) => {
        const schedule = this.schedules.find((s) => s.id === params.id)
        const denied = this.guard(request, schedule?.siteId)
        if (denied || !schedule) return denied ?? notFound()
        if (this.grants.some((g) => g.scheduleId === schedule.id)) {
          return problem(409, 'Un permiso usa este horario.', 'schedule_in_use')
        }
        this.schedules = this.schedules.filter((s) => s.id !== schedule.id)
        return new HttpResponse(null, { status: 204 })
      }),

      http.get('*/api/v1/sites/:siteId/credentials', ({ request, params }) => {
        const denied = this.guard(request, params.siteId as string)
        if (denied) return denied
        const subjectId = new URL(request.url).searchParams.get('subjectId')
        return HttpResponse.json(
          this.credentials.filter((c) => c.siteId === params.siteId && (!subjectId || c.subjectId === subjectId)).map(publicCredential),
        )
      }),
      http.post('*/api/v1/sites/:siteId/credentials', async ({ request, params }) => {
        const siteId = params.siteId as string
        const denied = this.guard(request, siteId)
        if (denied) return denied
        const body = (await request.json()) as { subjectId: string; type: string; value: string; validFrom: string | null; validUntil: string | null }
        if (!body.value) return invalid({ Value: ['El valor es obligatorio.'] })
        if (!this.subjects.some((s) => s.id === body.subjectId && s.siteId === siteId)) return invalid({ SubjectId: ['Sujeto inválido.'] })
        if (this.credentials.some((c) => c.siteId === siteId && c.type === body.type && c.value === body.value)) {
          return problem(409, 'Ya hay una credencial con ese valor en el sitio.', 'credential_taken')
        }
        const credential = this.addCredential(body.subjectId, body.type, body.value, body.validFrom, body.validUntil)
        return HttpResponse.json(publicCredential(credential), { status: 201 })
      }),
      http.put('*/api/v1/credentials/:id', async ({ request, params }) => {
        const credential = this.credentials.find((c) => c.id === params.id)
        const denied = this.guard(request, credential?.siteId)
        if (denied || !credential) return denied ?? notFound()
        const body = (await request.json()) as { validFrom: string | null; validUntil: string | null }
        if (body.validFrom && body.validUntil && body.validUntil <= body.validFrom) {
          return invalid({ ValidUntil: ['La vigencia termina antes de empezar.'] })
        }
        Object.assign(credential, body)
        return HttpResponse.json(publicCredential(credential))
      }),
      http.delete('*/api/v1/credentials/:id', ({ request, params }) => {
        const credential = this.credentials.find((c) => c.id === params.id)
        const denied = this.guard(request, credential?.siteId)
        if (denied || !credential) return denied ?? notFound()
        this.credentials = this.credentials.filter((c) => c.id !== credential.id)
        return new HttpResponse(null, { status: 204 })
      }),

      http.get('*/api/v1/sites/:siteId/grants', ({ request, params }) => {
        const denied = this.guard(request, params.siteId as string)
        if (denied) return denied
        const subjectId = new URL(request.url).searchParams.get('subjectId')
        return HttpResponse.json(this.grants.filter((g) => g.siteId === params.siteId && (!subjectId || g.subjectId === subjectId)))
      }),
      http.post('*/api/v1/sites/:siteId/grants', async ({ request, params }) => {
        const siteId = params.siteId as string
        const denied = this.guard(request, siteId)
        if (denied) return denied
        const body = (await request.json()) as {
          subjectId: string
          targetType: string
          targetId: string
          scheduleId: string | null
          validFrom: string | null
          validUntil: string | null
          effect: string
          maxUses: number | null
        }
        if (body.effect === 'deny' && body.maxUses !== null) return invalid({ MaxUses: ['Una denegación no tiene usos.'] })
        const grant: Grant = {
          id: crypto.randomUUID(),
          siteId,
          subjectId: body.subjectId,
          targetType: body.targetType,
          targetId: body.targetId,
          scheduleId: body.scheduleId,
          validFrom: body.validFrom,
          validUntil: body.validUntil,
          effect: body.effect,
          origin: 'manual',
          remainingUses: body.maxUses,
        }
        this.grants.push(grant)
        return HttpResponse.json(grant, { status: 201 })
      }),
      http.delete('*/api/v1/grants/:id', ({ request, params }) => {
        const grant = this.grants.find((g) => g.id === params.id)
        const denied = this.guard(request, grant?.siteId)
        if (denied || !grant) return denied ?? notFound()
        this.grants = this.grants.filter((g) => g.id !== grant.id)
        return new HttpResponse(null, { status: 204 })
      }),

      http.post('*/api/v1/access-points/:id/simulations', async ({ request, params }) => {
        const point = this.accessPoints.find((a) => a.id === params.id)
        const denied = this.guard(request, point?.siteId)
        if (denied || !point) return denied ?? notFound()
        const body = (await request.json()) as { credentialType: string; credentialValue: string; at: string | null }
        this.simulationBodies.push(body)
        const at = body.at ?? new Date().toISOString()
        const result = (decision: string, reason: string) => HttpResponse.json({ decision, reason, policyHash: 'hash-fake' })
        const within = (from: string | null, until: string | null) => (!from || from <= at) && (!until || at < until)
        const credential = this.credentials.find(
          (c) => c.siteId === point.siteId && c.type === body.credentialType && c.value === body.credentialValue,
        )
        if (!credential || !within(credential.validFrom, credential.validUntil)) return result('deny', 'credential_invalid')

        const { day, minute } = siteDayAndMinute(at, siteOf(point.siteId).timeZone)
        const applying = this.grants.filter((g) => {
          if (g.subjectId !== credential.subjectId || !within(g.validFrom, g.validUntil)) return false
          const space = g.targetType === 'space' ? this.spaces.find((s) => s.id === g.targetId) : null
          // Un permiso a un espacio cubre las puertas de su camino de acceso: las que dan a él o a uno de sus ancestros.
          const covers = g.targetType === 'access_point' ? g.targetId === point.id : !!space && [...space.ancestorIds, space.id].includes(point.toSpaceId)
          if (!covers) return false
          const schedule = this.schedules.find((s) => s.id === g.scheduleId)
          return !schedule || schedule.windows.some((w) => w.days.includes(day) && minutes(w.from) <= minute && minute < minutes(w.to))
        })
        if (applying.some((g) => g.effect === 'deny')) return result('deny', 'explicit_deny')
        if (applying.some((g) => g.effect === 'allow' && (g.remainingUses === null || g.remainingUses > 0))) return result('allow', 'allowed')
        return result('deny', 'no_permission')
      }),
    ]
  }

  private invalidSchedule(siteId: string, body: { name: string; windows: Schedule['windows'] }, except?: string) {
    if (!body.name?.trim()) return problem(400, 'Datos inválidos', 'validation', { errors: { Name: ['El nombre es obligatorio.'] } })
    if (!body.windows?.length || body.windows.some((w) => w.days.length === 0 || minutes(w.to) <= minutes(w.from))) {
      return problem(400, 'Datos inválidos', 'validation', { errors: { Windows: ['Revisá las ventanas.'] } })
    }
    const wanted = body.name.trim().toLowerCase()
    if (this.schedules.some((s) => s.siteId === siteId && s.id !== except && s.name.toLowerCase() === wanted)) {
      return problem(409, 'Ya hay un horario con ese nombre en el sitio.', 'schedule_name_taken')
    }
    return null
  }

  addSubject(siteId: string, alias: string, kind = 'person'): Subject {
    const subject: Subject = { id: crypto.randomUUID(), siteId, kind, alias }
    this.subjects.push(subject)
    return subject
  }

  addSchedule(siteId: string, name: string, windows: Schedule['windows']): Schedule {
    const schedule: Schedule = { id: crypto.randomUUID(), siteId, name, windows }
    this.schedules.push(schedule)
    return schedule
  }

  addCredential(subjectId: string, type: string, value: string, validFrom: string | null = null, validUntil: string | null = null) {
    const subject = this.subjects.find((s) => s.id === subjectId)!
    const credential: StoredCredential = { id: crypto.randomUUID(), siteId: subject.siteId, subjectId, type, value, validFrom, validUntil }
    this.credentials.push(credential)
    return credential
  }

  addGrant(subject: Subject, target: { id: string }, options: Partial<Grant> = {}): Grant {
    const grant: Grant = {
      id: crypto.randomUUID(),
      siteId: subject.siteId,
      subjectId: subject.id,
      targetType: this.accessPoints.some((a) => a.id === target.id) ? 'access_point' : 'space',
      targetId: target.id,
      scheduleId: null,
      validFrom: null,
      validUntil: null,
      effect: 'allow',
      origin: 'manual',
      remainingUses: null,
      ...options,
    }
    this.grants.push(grant)
    return grant
  }

  /** 401 sin sesión y 404 si el sitio no es de la organización activa o no existe. */
  private guard(request: Request, siteId: string | undefined) {
    if (!this.authorized(request)) {
      return problem(401, 'Se requiere autenticación', 'authentication_required')
    }

    const site = this.sites.find((s) => s.id === siteId && s.organizationId === this.activeOrganization)
    return site ? null : problem(404, 'No encontrado', 'not_found')
  }

  private siblingNamed(siteId: string, parentId: string | null, name: string, except?: string) {
    const wanted = name.trim().toLowerCase()
    return this.spaces.some((s) => s.siteId === siteId && s.parentId === parentId && s.id !== except && s.name.toLowerCase() === wanted)
  }

  private invalidAccessPoint(siteId: string, body: AccessPointBody, except?: string) {
    const errors: Record<string, string[]> = {}
    if (!body.name?.trim()) errors.Name = ['El nombre es obligatorio.']
    if (!body.failMode) errors.FailMode = ['Elegí qué hace la puerta sin energía.']
    if (!this.spaces.some((s) => s.id === body.toSpaceId && s.siteId === siteId)) errors.ToSpaceId = ['Elegí a qué espacio da.']
    if (Object.keys(errors).length > 0) {
      return problem(400, 'Datos inválidos', 'validation', { errors })
    }

    const wanted = body.name.trim().toLowerCase()
    if (this.accessPoints.some((a) => a.siteId === siteId && a.id !== except && a.name.toLowerCase() === wanted)) {
      return problem(409, 'Ya hay una puerta con ese nombre en el sitio.', 'access_point_name_taken')
    }

    return null
  }

  addSpace(siteId: string, name: string, type: string, parent: Space | null = null, isCommon = false): Space {
    const space: Space = {
      id: crypto.randomUUID(),
      siteId,
      parentId: parent?.id ?? null,
      type,
      name,
      isCommon,
      depth: parent ? parent.depth + 1 : 0,
      ancestorIds: parent ? [...parent.ancestorIds, parent.id] : [],
    }
    this.spaces.push(space)
    return space
  }

  addAccessPoint(name: string, to: Space, from: Space | null = null, type = 'door', failMode = 'fail_secure'): AccessPoint {
    const point: AccessPoint = { id: crypto.randomUUID(), siteId: to.siteId, name, type, toSpaceId: to.id, fromSpaceId: from?.id ?? null, failMode }
    this.accessPoints.push(point)
    return point
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
