import type { components } from '@/api/schema'
import { minutesSince } from '@/lib/relative-time'

export type Device = components['schemas']['DeviceDto']
export type IssuedDevice = components['schemas']['IssuedDevice']

/** Un dispositivo activo está en línea si se comunicó en los últimos minutos (design de add-device-and-event-screens, decisión 2). */
export const OnlineWindowMinutes = 5
/** Aviso cuando al certificado le quedan menos días que estos. */
export const CertificateWarningDays = 30
/** El detalle se refresca solo, así se ve cómo se conecta y baja la política después de instalar. */
export const DevicesRefreshMs = 30_000

export const devicesKey = (organizationId: string | null, siteId: string) => ['org', organizationId, 'sites', siteId, 'devices'] as const

export type Connection = 'online' | 'offline' | 'never'

export function connection(device: Device, now = new Date()): Connection | null {
  if (device.status !== 'active') return null
  if (!device.lastSeenAt) return 'never'
  return minutesSince(device.lastSeenAt, now) < OnlineWindowMinutes ? 'online' : 'offline'
}

export type PolicyState = { kind: 'none' } | { kind: 'current' } | { kind: 'pending'; reported: number | null; current: number }

export function policyState(device: Device): PolicyState {
  if (device.policyVersion === null) return { kind: 'none' }
  if (device.reportedPolicyVersion === device.policyVersion) return { kind: 'current' }
  return { kind: 'pending', reported: device.reportedPolicyVersion, current: device.policyVersion }
}

export function certificateExpiresSoon(device: Device, now = new Date()): boolean {
  if (!device.certificateExpiresAt) return false
  return Date.parse(device.certificateExpiresAt) - now.getTime() < CertificateWarningDays * 24 * 3600 * 1000
}
