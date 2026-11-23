import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { DoorClosed, Network, Plus } from 'lucide-react'
import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useSearchParams } from 'react-router'
import { api } from '@/api/client'
import { describeError, unwrap } from '@/api/errors'
import { ErrorNotice } from '@/app/ErrorNotice'
import { type Site, SiteGate, SitePicker, useSelectedSite } from '@/components/site-picker/SitePicker'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { ConfirmDialog } from '@/components/ui/confirm-dialog'
import { EmptyState } from '@/components/ui/empty-state'
import { PageHeader } from '@/components/ui/page-header'
import { Sheet, SheetContent } from '@/components/ui/sheet'
import { Skeleton } from '@/components/ui/skeleton'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { useSession } from '@/session/session'
import { AccessPointsTab } from './AccessPointsTab'
import { type AccessPoint, accessPointsKey, canCreateRoot, type Space, SpaceTree, spacesKey } from './model'
import { SpaceDetail } from './SpaceDetail'
import { type SpaceEditing, SpaceForm } from './SpaceForm'
import { SpaceTreeView } from './SpaceTreeView'

/** Parámetros de la dirección: pestaña y espacio elegido (design de add-spaces-screens, decisiones 2 y 3). */
const viewParam = 'vista'
const spaceParam = 'espacio'

export function SpacesPage() {
  const { t } = useTranslation()
  const selected = useSelectedSite()
  return (
    <div className="space-y-6">
      <PageHeader title={t('spaces.title')} description={t('spaces.description')} actions={<SitePicker selected={selected} />} />
      <SiteGate selected={selected}>{(site) => <SiteSpaces key={site.id} site={site} />}</SiteGate>
    </div>
  )
}

function SiteSpaces({ site }: { site: Site }) {
  const { t } = useTranslation()
  const session = useSession()
  const organizationId = session.status === 'active' ? session.organizationId : null
  const [params, setParams] = useSearchParams()
  const view = params.get(viewParam) === 'puertas' ? 'points' : 'spaces'
  const spaces = useQuery({
    queryKey: spacesKey(organizationId, site.id),
    queryFn: async () => unwrap(await api.GET('/api/v1/sites/{siteId}/spaces', { params: { path: { siteId: site.id } } })),
  })
  const points = useQuery({
    queryKey: accessPointsKey(organizationId, site.id),
    queryFn: async () => unwrap(await api.GET('/api/v1/sites/{siteId}/access-points', { params: { path: { siteId: site.id } } })),
  })
  const tree = useMemo(() => new SpaceTree(spaces.data ?? []), [spaces.data])

  function setView(next: string) {
    setParams((current) => {
      const updated = new URLSearchParams(current)
      if (next === 'points') updated.set(viewParam, 'puertas')
      else updated.delete(viewParam)
      return updated
    })
  }

  if (spaces.isPending || points.isPending) {
    return <TreeSkeleton />
  }

  if (spaces.isError || points.isError) {
    const retry = () => {
      void spaces.refetch()
      void points.refetch()
    }
    return <ErrorNotice error={spaces.error ?? points.error} onRetry={retry} />
  }

  return (
    <Tabs value={view} onValueChange={setView} className="space-y-5">
      <TabsList aria-label={t('spaces.tabs.label')}>
        <TabsTrigger value="spaces">
          <Network aria-hidden="true" />
          {t('spaces.tabs.spaces')}
          <Count value={spaces.data.length} />
        </TabsTrigger>
        <TabsTrigger value="points">
          <DoorClosed aria-hidden="true" />
          {t('spaces.tabs.points')}
          <Count value={points.data.length} />
        </TabsTrigger>
      </TabsList>
      <TabsContent value="spaces">
        <SpacesTab site={site} tree={tree} points={points.data} />
      </TabsContent>
      <TabsContent value="points">
        <AccessPointsTab site={site} tree={tree} points={points.data} />
      </TabsContent>
    </Tabs>
  )
}

function Count({ value }: { value: number }) {
  return <span className="rounded-full bg-muted-foreground/10 px-1.5 text-xs tabular-nums text-muted-foreground">{value}</span>
}

function TreeSkeleton() {
  const { t } = useTranslation()
  return (
    <div aria-busy="true" className="space-y-5">
      <span role="status" className="sr-only">
        {t('app.loading')}
      </span>
      <Skeleton className="h-10 w-64" />
      <div className="grid gap-6 md:grid-cols-[minmax(0,2fr)_minmax(0,3fr)]" aria-hidden="true">
        <div className="space-y-3 rounded-xl border bg-card p-4">
          {[0, 1, 2, 3, 4].map((index) => (
            <Skeleton key={index} className="h-6" style={{ marginLeft: `${Math.min(index, 3) * 1.25}rem` }} />
          ))}
        </div>
        <Skeleton className="h-48 rounded-xl" />
      </div>
    </div>
  )
}

function SpacesTab({ site, tree, points }: { site: Site; tree: SpaceTree; points: AccessPoint[] }) {
  const { t } = useTranslation()
  const session = useSession()
  const organizationId = session.status === 'active' ? session.organizationId : null
  const queryClient = useQueryClient()
  const [params, setParams] = useSearchParams()
  const selectedId = params.get(spaceParam)
  const selected = selectedId ? (tree.byId.get(selectedId) ?? null) : null
  // El formulario vive en un panel lateral; `editing` se conserva al cerrar para que el panel salga con su contenido.
  const [editing, setEditing] = useState<SpaceEditing>({ mode: 'root' })
  const [formOpen, setFormOpen] = useState(false)
  const [deleting, setDeleting] = useState<Space | null>(null)

  function select(id: string | null) {
    setParams((current) => {
      const next = new URLSearchParams(current)
      if (id) next.set(spaceParam, id)
      else next.delete(spaceParam)
      return next
    })
    if (id && typeof window !== 'undefined' && window.innerWidth < 768) {
      // En celular el detalle va debajo del árbol.
      requestAnimationFrame(() => document.getElementById('space-detail')?.scrollIntoView?.({ behavior: 'smooth', block: 'start' }))
    }
  }

  function edit(next: SpaceEditing) {
    setEditing(next)
    setFormOpen(true)
  }

  const remove = useMutation({
    mutationFn: async (space: Space) => unwrap(await api.DELETE('/api/v1/spaces/{id}', { params: { path: { id: space.id } } })),
    onSuccess: async (_, space) => {
      await queryClient.invalidateQueries({ queryKey: spacesKey(organizationId, site.id) })
      select(space.parentId)
      setDeleting(null)
    },
  })

  const canRoot = canCreateRoot(session, site.id)
  if (tree.roots.length === 0) {
    return (
      <>
        <div className="rounded-2xl border border-dashed bg-card/50">
          <EmptyState
            icon={Network}
            title={t('spaces.empty')}
            action={
              canRoot && (
                <Button variant="outline" onClick={() => edit({ mode: 'root' })}>
                  <Plus />
                  {t('spaces.createRoot')}
                </Button>
              )
            }
          >
            {canRoot ? t('spaces.emptyDescription') : t('spaces.emptyDescriptionReadOnly')}
          </EmptyState>
        </div>
        <FormSheet site={site} tree={tree} editing={editing} open={formOpen} onOpenChange={setFormOpen} onSaved={select} />
      </>
    )
  }

  return (
    <div className="grid items-start gap-6 md:grid-cols-[minmax(0,2fr)_minmax(0,3fr)]">
      <Card className="p-3">
        <SpaceTreeView tree={tree} selectedId={selected?.id ?? null} onSelect={select} />
      </Card>
      <div id="space-detail" className="scroll-mt-20">
        {selected ? (
          <SpaceDetail
            key={selected.id}
            site={site}
            space={selected}
            tree={tree}
            points={points}
            onSelect={select}
            onAddChild={() => edit({ mode: 'child', parent: selected })}
            onEdit={() => edit({ mode: 'edit', space: selected })}
            onDelete={() => {
              remove.reset()
              setDeleting(selected)
            }}
          />
        ) : (
          <div className="rounded-xl border border-dashed bg-card/50 px-6 py-12 text-center text-sm text-muted-foreground">
            {t('spaces.pick')}
          </div>
        )}
      </div>
      <FormSheet site={site} tree={tree} editing={editing} open={formOpen} onOpenChange={setFormOpen} onSaved={select} />
      <ConfirmDialog
        open={deleting !== null}
        onOpenChange={(open) => !open && setDeleting(null)}
        title={t('spaces.deleteTitle', { name: deleting?.name ?? '' })}
        description={t('spaces.deleteDescription')}
        confirmLabel={t('spaces.delete')}
        cancelLabel={t('spaces.cancel')}
        pending={remove.isPending}
        error={remove.error ? describeError(remove.error, t) : null}
        onConfirm={() => deleting && remove.mutate(deleting)}
      />
    </div>
  )
}

function FormSheet({
  site,
  tree,
  editing,
  open,
  onOpenChange,
  onSaved,
}: {
  site: Site
  tree: SpaceTree
  editing: SpaceEditing
  open: boolean
  onOpenChange: (open: boolean) => void
  onSaved: (id: string) => void
}) {
  const { t } = useTranslation()
  const key = editing.mode === 'edit' ? editing.space.id : editing.mode === 'child' ? `child-${editing.parent.id}` : 'root'
  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent closeLabel={t('spaces.close')}>
        <SpaceForm
          key={key}
          site={site}
          tree={tree}
          editing={editing}
          onDone={(saved) => {
            onOpenChange(false)
            if (saved) onSaved(saved.id)
          }}
        />
      </SheetContent>
    </Sheet>
  )
}
