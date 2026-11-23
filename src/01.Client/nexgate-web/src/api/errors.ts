import type { TFunction } from 'i18next'

/**
 * Error de una operación de la API, a partir del problem details (design de add-admin-panel-shell, decisión 8).
 * <c>status</c> 0 es sin conexión con el servidor.
 */
export class ApiError extends Error {
  readonly status: number
  readonly code?: string
  readonly title?: string
  readonly errors: Record<string, string[]>
  readonly correlationId?: string

  constructor(status: number, problem?: Partial<Problem>) {
    super(problem?.title ?? `HTTP ${status}`)
    this.name = 'ApiError'
    this.status = status
    this.code = problem?.code
    this.title = problem?.title
    this.errors = problem?.errors ?? {}
    this.correlationId = problem?.correlationId
  }

  get isNetwork() {
    return this.status === 0
  }

  /** Mensaje del servidor para un campo del formulario (las claves del servidor usan otra capitalización). */
  fieldError(field: string): string | undefined {
    const key = Object.keys(this.errors).find((k) => k.toLowerCase() === field.toLowerCase())
    return key ? this.errors[key]?.[0] : undefined
  }
}

interface Problem {
  title: string
  status: number
  code: string
  correlationId: string
  errors: Record<string, string[]>
}

export const networkError = () => new ApiError(0)

/** Convierte la respuesta de openapi-fetch en un valor o en un <see cref="ApiError"/>. */
export function unwrap<T>(result: { data?: T; error?: unknown; response: Response }): T {
  if (result.response.ok) {
    return result.data as T
  }

  throw new ApiError(result.response.status, (result.error ?? undefined) as Partial<Problem> | undefined)
}

/** Mensaje en español para mostrar un error que no es de un campo. */
export function describeError(error: unknown, t: TFunction): string {
  if (!(error instanceof ApiError)) {
    return t('errors.unexpected')
  }

  switch (true) {
    case error.isNetwork:
      return t('errors.network')
    case error.status === 400:
      return Object.keys(error.errors).length > 0 ? t('errors.validation') : (error.title ?? t('errors.unexpected'))
    case error.status === 403:
      return t('errors.forbidden')
    case error.status === 404:
      return t('errors.notFound')
    case error.status === 409:
      return error.title ?? t('errors.unexpected')
    case error.status === 429:
      return t('errors.tooManyRequests')
    default:
      return t('errors.unexpected')
  }
}
