import { useQuery } from '@tanstack/react-query'
import { LogIn, Pencil, Plus, Route, Trash2 } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { api } from '@/api/client'
import { unwrap } from '@/api/errors'
import { ErrorNotice } from '@/app/ErrorNotice'
import type { Site } from '@/components/site-picker/SitePicker'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { useSession } from '@/session/session'
import { type AccessPoint, accessPathKey, accessPointIcons, canManageSpace, MaxDepth, type Space, spaceIcons, type SpaceTree } from './model'

/**
 * Detalle del espacio elegido: datos, acciones según el permiso, puertas que entran y camino de acceso desde afuera
 * (design de add-spaces-screens, decisión 4).
 */
export function SpaceDetail({
  site,
  space,
  tree,
  points,
  onSelect,
  onAddChild,
  onEdit,
  onDelete,
}: {
  site: Site
  space: Space
  tree: SpaceTree
  points: AccessPoint[]
  onSelect: (id: string) => void
  onAddChild: () => void
  onEdit: () => void
  onDelete: () => void
}) {
  const { t } = useTranslation()
  const session = useSession()
  const Icon = spaceIcons[space.type] ?? spaceIcons.zone!
  const parent = space.parentId ? tree.byId.get(space.parentId) : undefined
  const entering = points.filter((p) => p.toSpaceId === space.id).sort((a, b) => a.name.localeCompare(b.name))
  const canManage = canManageSpace(session, space)

  return (
    <Card className="divide-y" aria-labelledby="space-detail-title">
      <div className="flex flex-wrap items-start gap-4 p-5">
        <span className="flex size-11 shrink-0 items-center justify-center rounded-lg bg-accent text-accent-foreground">
          <Icon className="size-5" aria-hidden="true" />
        </span>
        <div className="min-w-0 flex-1 space-y-1.5">
          <h2 id="space-detail-title" className="text-lg font-semibold tracking-tight break-words">
            {space.name}
          </h2>
          <div className="flex flex-wrap gap-1.5">
            <Badge variant="secondary">{t(`spaces.types.${space.type}`, { defaultValue: space.type })}</Badge>
            {space.isCommon && <Badge variant="info">{t('spaces.common')}</Badge>}
          </div>
        </div>
        {canManage && (
          <div className="flex w-full flex-wrap gap-2 sm:w-auto">
            {space.depth < MaxDepth && (
              <Button size="sm" variant="outline" onClick={onAddChild}>
                <Plus />
                {t('spaces.addChild')}
              </Button>
            )}
            <Button size="sm" variant="ghost" onClick={onEdit} aria-label={t('spaces.editNamed', { name: space.name })}>
              <Pencil />
              {t('spaces.edit')}
            </Button>
            <Button
              size="icon-sm"
              variant="ghost"
              className="text-destructive hover:bg-destructive/10 hover:text-destructive"
              onClick={onDelete}
              aria-label={t('spaces.deleteNamed', { name: space.name })}
            >
              <Trash2 />
            </Button>
          </div>
        )}
      </div>

      <dl className="grid gap-x-6 gap-y-3 p-5 text-sm sm:grid-cols-2">
        <div>
          <dt className="text-muted-foreground">{t('spaces.parent')}</dt>
          <dd className="mt-0.5 font-medium">
            {parent ? (
              <button type="button" className="text-primary underline-offset-4 hover:underline" onClick={() => onSelect(parent.id)}>
                {tree.path(parent.id)}
              </button>
            ) : (
              t('spaces.parentNone', { site: site.name })
            )}
          </dd>
        </div>
        <div>
          <dt className="text-muted-foreground">{t('spaces.childrenCount')}</dt>
          <dd className="mt-0.5 font-medium tabular-nums">{tree.childrenOf(space.id).length}</dd>
        </div>
      </dl>

      <section className="space-y-3 p-5" aria-labelledby="space-entering-title">
        <h3 id="space-entering-title" className="flex items-center gap-2 text-sm font-semibold">
          <LogIn className="size-4 text-muted-foreground" aria-hidden="true" />
          {t('spaces.entering')}
        </h3>
        {entering.length === 0 ? (
          <p className="text-sm text-muted-foreground">{t('spaces.enteringNone')}</p>
        ) : (
          <ul className="space-y-2">
            {entering.map((point) => (
              <li key={point.id} className="flex items-center gap-3 rounded-lg border px-3 py-2 text-sm">
                <PointIcon type={point.type} />
                <span className="min-w-0 flex-1">
                  <span className="block truncate font-medium">{point.name}</span>
                  <span className="block truncate text-xs text-muted-foreground">
                    {t('spaces.points.from')}: {point.fromSpaceId ? tree.path(point.fromSpaceId) : t('spaces.points.outside')}
                  </span>
                </span>
              </li>
            ))}
          </ul>
        )}
      </section>

      <AccessPath site={site} space={space} tree={tree} />
    </Card>
  )
}

function PointIcon({ type }: { type: string }) {
  const Icon = accessPointIcons[type] ?? accessPointIcons.door!
  return (
    <span className="flex size-8 shrink-0 items-center justify-center rounded-md bg-muted text-muted-foreground">
      <Icon className="size-4" aria-hidden="true" />
    </span>
  )
}

/** Camino de acceso calculado por la API: de afuera hasta el espacio, en orden. */
function AccessPath({ site, space, tree }: { site: Site; space: Space; tree: SpaceTree }) {
  const { t } = useTranslation()
  const session = useSession()
  const organizationId = session.status === 'active' ? session.organizationId : null
  const path = useQuery({
    queryKey: accessPathKey(organizationId, site.id, space.id),
    queryFn: async () => unwrap(await api.GET('/api/v1/spaces/{id}/access-path', { params: { path: { id: space.id } } })),
  })

  return (
    <section className="space-y-3 p-5" aria-labelledby="space-path-title">
      <h3 id="space-path-title" className="flex items-center gap-2 text-sm font-semibold">
        <Route className="size-4 text-muted-foreground" aria-hidden="true" />
        {t('spaces.path')}
      </h3>
      {path.isPending && (
        <div aria-busy="true" className="space-y-2">
          <span role="status" className="sr-only">
            {t('app.loading')}
          </span>
          <Skeleton className="h-9" />
          <Skeleton className="h-9 w-4/5" />
        </div>
      )}
      {path.isError && <ErrorNotice error={path.error} onRetry={() => void path.refetch()} />}
      {path.data && path.data.length === 0 && <p className="text-sm text-muted-foreground">{t('spaces.pathNone')}</p>}
      {path.data && path.data.length > 0 && (
        <ol
          aria-label={t('spaces.pathLabel', { name: space.name })}
          className="relative before:absolute before:top-2 before:bottom-6 before:left-[0.55rem] before:w-px before:bg-border"
        >
          <li className="relative flex items-center gap-3 pb-3 pl-1 text-sm text-muted-foreground">
            <span className="relative z-10 size-2.5 rounded-full bg-muted-foreground/40 ring-4 ring-card" aria-hidden="true" />
            {t('spaces.points.outside')}
          </li>
          {path.data.map((entry) => (
            <li key={entry.id} className="relative flex gap-3 pb-3 pl-1 last:pb-0">
              <span className="relative z-10 mt-3.5 flex size-2.5 shrink-0 rounded-full bg-primary ring-4 ring-card" aria-hidden="true" />
              <span className="min-w-0 flex-1 rounded-lg border px-3 py-2 text-sm">
                <span className="block font-medium">{entry.name}</span>
                <span className="block text-xs text-muted-foreground">
                  {t(`spaces.points.types.${entry.type}`, { defaultValue: entry.type })} · {t('spaces.points.to')}{' '}
                  {tree.path(entry.toSpaceId)}
                </span>
              </span>
            </li>
          ))}
        </ol>
      )}
    </section>
  )
}
