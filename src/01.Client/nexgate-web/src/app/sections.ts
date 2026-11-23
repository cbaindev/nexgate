import { Building2, CalendarClock, DoorOpen, KeyRound, LayoutDashboard, type LucideIcon, Router, ShieldCheck, Users } from 'lucide-react'
import type { Session } from '@/session/session'

/** Qué hace falta para ver una sección: un permiso (con cualquier alcance) o ser operador de plataforma. */
export type Requirement = { permission: string } | { platformOperator: true } | null

export interface Section {
  id: string
  path: string
  labelKey: string
  icon: LucideIcon
  requirement: Requirement
  /** La sección ya tiene pantallas; si no, muestra que llega en un próximo cambio. */
  built: boolean
}

/**
 * Tabla única de secciones (design de add-admin-panel-shell, decisión 6): de acá salen la navegación y las rutas. Es
 * solo presentación: el servidor verifica cada operación.
 */
export const sections: Section[] = [
  { id: 'home', path: '/', labelKey: 'sections.home', icon: LayoutDashboard, requirement: null, built: true },
  { id: 'sites', path: '/sitios', labelKey: 'sections.sites', icon: Building2, requirement: { permission: 'sites.read' }, built: true },
  { id: 'spaces', path: '/espacios', labelKey: 'sections.spaces', icon: DoorOpen, requirement: { permission: 'spaces.read' }, built: false },
  { id: 'access', path: '/accesos', labelKey: 'sections.access', icon: KeyRound, requirement: { permission: 'access.read' }, built: false },
  { id: 'devices', path: '/dispositivos', labelKey: 'sections.devices', icon: Router, requirement: { permission: 'devices.read' }, built: false },
  { id: 'events', path: '/eventos', labelKey: 'sections.events', icon: CalendarClock, requirement: { permission: 'events.read' }, built: false },
  { id: 'members', path: '/miembros', labelKey: 'sections.members', icon: Users, requirement: { permission: 'members.read' }, built: false },
  { id: 'platform', path: '/plataforma', labelKey: 'sections.platform', icon: ShieldCheck, requirement: { platformOperator: true }, built: false },
]

export function allows(session: Session, requirement: Requirement): boolean {
  if (session.status !== 'active') {
    return false
  }

  if (requirement === null) {
    return true
  }

  return 'permission' in requirement
    ? session.permissions.some((p) => p.name === requirement.permission)
    : session.user.platformOperator
}
