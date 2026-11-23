import { CreditCard, Hash, KeyRound, type LucideIcon, User, UserRound } from 'lucide-react'
import type { components } from '@/api/schema'

export type Subject = components['schemas']['SubjectDto']
export type Schedule = components['schemas']['ScheduleDto']
export type ScheduleWindow = components['schemas']['ScheduleWindowDto']
export type Credential = components['schemas']['CredentialDto']
export type Grant = components['schemas']['GrantDto']
export type SimulationResult = components['schemas']['SimulationResultDto']

export const subjectKinds = ['person', 'anonymous'] as const
export type SubjectKind = (typeof subjectKinds)[number]

export const credentialTypes = ['card', 'rf', 'pin'] as const
export type CredentialType = (typeof credentialTypes)[number]

export const days = ['monday', 'tuesday', 'wednesday', 'thursday', 'friday', 'saturday', 'sunday'] as const
export type Day = (typeof days)[number]

/** Máximo de ventanas por horario que admite la API. */
export const MaxWindows = 20

export const subjectIcons: Record<string, LucideIcon> = { person: User, anonymous: UserRound }
export const credentialIcons: Record<string, LucideIcon> = { card: CreditCard, rf: KeyRound, pin: Hash }

/** Claves de consulta por sitio: cuelgan de la del sitio, así invalidarlo refresca todo lo suyo. */
const siteKey = (organizationId: string | null, siteId: string) => ['org', organizationId, 'sites', siteId] as const
export const subjectsKey = (o: string | null, s: string) => [...siteKey(o, s), 'subjects'] as const
export const schedulesKey = (o: string | null, s: string) => [...siteKey(o, s), 'schedules'] as const
export const credentialsKey = (o: string | null, s: string, subjectId: string) => [...siteKey(o, s), 'credentials', subjectId] as const
export const grantsKey = (o: string | null, s: string, subjectId: string) => [...siteKey(o, s), 'grants', subjectId] as const

export type CredentialState = 'valid' | 'notYet' | 'expired'

/** Estado de una credencial (o de un permiso) en un instante, según su vigencia. */
export function validityState(item: { validFrom: string | null; validUntil: string | null }, now = new Date()): CredentialState {
  const at = now.getTime()
  if (item.validFrom && Date.parse(item.validFrom) > at) return 'notYet'
  if (item.validUntil && Date.parse(item.validUntil) <= at) return 'expired'
  return 'valid'
}

const dayNames: Record<Day, string> = {
  monday: 'lunes',
  tuesday: 'martes',
  wednesday: 'miércoles',
  thursday: 'jueves',
  friday: 'viernes',
  saturday: 'sábado',
  sunday: 'domingo',
}

export const dayInitials: Record<Day, string> = {
  monday: 'L',
  tuesday: 'M',
  wednesday: 'M',
  thursday: 'J',
  friday: 'V',
  saturday: 'S',
  sunday: 'D',
}

export const dayName = (day: Day) => dayNames[day]

function joinList(items: string[]) {
  return items.length <= 1 ? (items[0] ?? '') : `${items.slice(0, -1).join(', ')} y ${items.at(-1)}`
}

/**
 * Días de una ventana en palabras (design de add-access-screens, decisión 6): tres o más días seguidos son un rango
 * ("lunes a viernes"), los demás se enumeran, y los siete son "todos los días".
 */
export function describeDays(selected: readonly string[]): string {
  const indexes = days.map((d, i) => (selected.includes(d) ? i : -1)).filter((i) => i >= 0)
  if (indexes.length === 7) return 'todos los días'
  const parts: string[] = []
  let start = 0
  while (start < indexes.length) {
    let end = start
    while (end + 1 < indexes.length && indexes[end + 1] === indexes[end]! + 1) end++
    const run = indexes.slice(start, end + 1).map((i) => dayNames[days[i]!])
    if (run.length >= 3) parts.push(`${run[0]} a ${run.at(-1)}`)
    else parts.push(...run)
    start = end + 1
  }
  return joinList(parts)
}

/** "Lunes a viernes, 08:00 a 17:00". */
export function describeWindow(window: { days: readonly string[]; from: string; to: string }): string {
  const text = `${describeDays(window.days)}, ${window.from} a ${window.to}`
  return text.charAt(0).toUpperCase() + text.slice(1)
}
