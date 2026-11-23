import {
  ArrowUpDown,
  Building,
  Car,
  DoorClosed,
  Fence,
  House,
  Landmark,
  Layers,
  LayoutGrid,
  type LucideIcon,
  MapPin,
  Construction,
  Trees,
  Columns3,
} from 'lucide-react'
import type { components } from '@/api/schema'
import { hasPermissionOnSite, hasPermissionOnSpace, type Session } from '@/session/session'

export type Space = components['schemas']['SpaceDto']
export type AccessPoint = components['schemas']['AccessPointDto']
export type AccessPathEntry = components['schemas']['AccessPathEntryDto']

/** Tipos de espacio de la API: la raíz es siempre `site`; los demás, de los hijos. */
export const childSpaceTypes = ['building', 'floor', 'unit', 'common_area', 'parking', 'lot', 'zone'] as const
export type SpaceType = 'site' | (typeof childSpaceTypes)[number]

export const accessPointTypes = ['door', 'gate', 'barrier', 'turnstile', 'elevator'] as const
export type AccessPointType = (typeof accessPointTypes)[number]

export const failModes = ['fail_safe', 'fail_secure'] as const
export type FailMode = (typeof failModes)[number]

/** Máximo de niveles debajo de la raíz que admite la API. */
export const MaxDepth = 10

export const spaceIcons: Record<string, LucideIcon> = {
  site: Landmark,
  building: Building,
  floor: Layers,
  unit: House,
  common_area: Trees,
  parking: Car,
  lot: LayoutGrid,
  zone: MapPin,
}

export const accessPointIcons: Record<string, LucideIcon> = {
  door: DoorClosed,
  gate: Fence,
  barrier: Construction,
  turnstile: Columns3,
  elevator: ArrowUpDown,
}

/** Claves de consulta por sitio: cuelgan de la de sitios, que empieza por la organización activa. */
export const spacesKey = (organizationId: string | null, siteId: string) => ['org', organizationId, 'sites', siteId, 'spaces'] as const
export const accessPointsKey = (organizationId: string | null, siteId: string) =>
  ['org', organizationId, 'sites', siteId, 'access-points'] as const
export const accessPathKey = (organizationId: string | null, siteId: string, spaceId: string) =>
  ['org', organizationId, 'sites', siteId, 'access-path', spaceId] as const

/** Árbol del sitio armado desde la lista plana (design de add-spaces-screens, decisión 3). */
export class SpaceTree {
  readonly byId = new Map<string, Space>()
  readonly children = new Map<string | null, Space[]>()
  readonly roots: Space[]

  constructor(spaces: Space[]) {
    for (const space of spaces) {
      this.byId.set(space.id, space)
      const siblings = this.children.get(space.parentId) ?? []
      siblings.push(space)
      this.children.set(space.parentId, siblings)
    }

    for (const siblings of this.children.values()) {
      siblings.sort((a, b) => a.name.localeCompare(b.name, 'es', { numeric: true }))
    }

    this.roots = this.children.get(null) ?? []
  }

  childrenOf(id: string): Space[] {
    return this.children.get(id) ?? []
  }

  /** Los espacios en el orden del árbol (padre antes que sus hijos). */
  ordered(): Space[] {
    const result: Space[] = []
    const visit = (space: Space) => {
      result.push(space)
      this.childrenOf(space.id).forEach(visit)
    }
    this.roots.forEach(visit)
    return result
  }

  /** Ruta legible, sin la raíz salvo que sea la raíz: "Torre A › Piso 5 › 5B". */
  path(id: string | null | undefined): string {
    const space = id ? this.byId.get(id) : undefined
    if (!space) {
      return ''
    }

    const names = [...space.ancestorIds.slice(1), space.id].map((a) => this.byId.get(a)?.name ?? '')
    return space.parentId === null ? space.name : names.join(' › ')
  }

  /**
   * Filtro por nombre sin distinguir mayúsculas ni tildes: las coincidencias y sus ancestros. Devuelve null sin filtro.
   */
  filter(query: string): { visible: Set<string>; matches: Set<string> } | null {
    const wanted = normalize(query.trim())
    if (!wanted) {
      return null
    }

    const matches = new Set<string>()
    const visible = new Set<string>()
    for (const space of this.byId.values()) {
      if (normalize(space.name).includes(wanted)) {
        matches.add(space.id)
        visible.add(space.id)
        space.ancestorIds.forEach((id) => visible.add(id))
      }
    }

    return { visible, matches }
  }
}

/** Minúsculas y sin tildes, carácter por carácter, así las posiciones coinciden con el texto original. */
export function normalize(text: string): string {
  return Array.from(text, (c) => c.normalize('NFD').replace(/\p{M}/gu, '').toLowerCase().charAt(0) || c).join('')
}

export function canManageSpace(session: Session, space: Space) {
  return hasPermissionOnSpace(session, 'spaces.manage', space)
}

export function canCreateRoot(session: Session, siteId: string) {
  return hasPermissionOnSite(session, 'spaces.manage', siteId)
}

/** Una puerta se maneja con permiso sobre el espacio al que da; el origen puede estar fuera del alcance (spec access-points). */
export function canManageAccessPoint(session: Session, point: AccessPoint, tree: SpaceTree) {
  const target = tree.byId.get(point.toSpaceId)
  return !!target && canManageSpace(session, target)
}
