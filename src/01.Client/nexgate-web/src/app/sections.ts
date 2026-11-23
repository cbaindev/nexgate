import { Building2, CalendarClock, DoorOpen, KeyRound, LayoutDashboard, type LucideIcon, Router, ShieldCheck, Users } from 'lucide-react'
import type { Session } from '@/session/session'

/** Qué hace falta para ver una sección: un permiso (con cualquier alcance) o ser operador de plataforma. */
export type Requirement = { permission: string } | { platformOperator: true } | null

export type SectionGroup = 'main' | 'operation' | 'administration'

export const sectionGroups: { id: SectionGroup; labelKey: string | null }[] = [
  { id: 'main', labelKey: null },
  { id: 'operation', labelKey: 'sections.groups.operation' },
  { id: 'administration', labelKey: 'sections.groups.administration' },
]

export interface Section {
  id: string
  path: string
  labelKey: string
  /** Texto de la tarjeta de acceso rápido del inicio. */
  descriptionKey: string
  icon: LucideIcon
  /** Grupo de la barra lateral (design de refine-admin-panel-design, decisión 4). */
  group: SectionGroup
  requirement: Requirement
  /** La sección ya tiene pantallas; si no, muestra que llega en un próximo cambio. */
  built: boolean
}

/**
 * Tabla única de secciones (design de add-admin-panel-shell, decisión 6): de acá salen la navegación y las rutas. Es
 * solo presentación: el servidor verifica cada operación.
 */
export const sections: Section[] = [
  { id: 'home', group: 'main', descriptionKey: 'sections.descriptions.home', path: '/', labelKey: 'sections.home', icon: LayoutDashboard, requirement: null, built: true },
  { id: 'sites', group: 'operation', descriptionKey: 'sections.descriptions.sites', path: '/sitios', labelKey: 'sections.sites', icon: Building2, requirement: { permission: 'sites.read' }, built: true },
  { id: 'spaces', group: 'operation', descriptionKey: 'sections.descriptions.spaces', path: '/espacios', labelKey: 'sections.spaces', icon: DoorOpen, requirement: { permission: 'spaces.read' }, built: false },
  { id: 'access', group: 'operation', descriptionKey: 'sections.descriptions.access', path: '/accesos', labelKey: 'sections.access', icon: KeyRound, requirement: { permission: 'access.read' }, built: false },
  { id: 'devices', group: 'operation', descriptionKey: 'sections.descriptions.devices', path: '/dispositivos', labelKey: 'sections.devices', icon: Router, requirement: { permission: 'devices.read' }, built: false },
  { id: 'events', group: 'operation', descriptionKey: 'sections.descriptions.events', path: '/eventos', labelKey: 'sections.events', icon: CalendarClock, requirement: { permission: 'events.read' }, built: false },
  { id: 'members', group: 'administration', descriptionKey: 'sections.descriptions.members', path: '/miembros', labelKey: 'sections.members', icon: Users, requirement: { permission: 'members.read' }, built: false },
  { id: 'platform', group: 'administration', descriptionKey: 'sections.descriptions.platform', path: '/plataforma', labelKey: 'sections.platform', icon: ShieldCheck, requirement: { platformOperator: true }, built: false },
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
