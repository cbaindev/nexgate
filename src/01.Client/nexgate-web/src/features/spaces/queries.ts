import { useQuery } from '@tanstack/react-query'
import { useMemo } from 'react'
import { api } from '@/api/client'
import { unwrap } from '@/api/errors'
import { useSession } from '@/session/session'
import { accessPointsKey, SpaceTree, spacesKey } from './model'

/**
 * Espacios y puertas de un sitio, con las mismas claves que la pantalla de espacios: otras secciones (accesos) los
 * comparten desde la caché.
 */
export function useSiteStructure(siteId: string, enabled = true) {
  const session = useSession()
  const organizationId = session.status === 'active' ? session.organizationId : null
  const spaces = useQuery({
    queryKey: spacesKey(organizationId, siteId),
    queryFn: async () => unwrap(await api.GET('/api/v1/sites/{siteId}/spaces', { params: { path: { siteId } } })),
    enabled,
  })
  const points = useQuery({
    queryKey: accessPointsKey(organizationId, siteId),
    queryFn: async () => unwrap(await api.GET('/api/v1/sites/{siteId}/access-points', { params: { path: { siteId } } })),
    enabled,
  })
  const tree = useMemo(() => new SpaceTree(spaces.data ?? []), [spaces.data])
  return { spaces, points, tree }
}
