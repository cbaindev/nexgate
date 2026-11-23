import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Briefcase, Building2, Globe, House, type LucideIcon, Pencil, Plus, Trees } from 'lucide-react'
import { type FormEvent, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { api } from '@/api/client'
import { ApiError, describeError, unwrap } from '@/api/errors'
import type { components } from '@/api/schema'
import { ErrorNotice } from '@/app/ErrorNotice'
import { Alert } from '@/components/ui/alert'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { EmptyState } from '@/components/ui/empty-state'
import { FieldError, FieldHint, Input, Label, Select } from '@/components/ui/form'
import { PageHeader } from '@/components/ui/page-header'
import { Sheet, SheetContent, SheetDescription, SheetFooter, SheetHeader, SheetTitle } from '@/components/ui/sheet'
import { Skeleton } from '@/components/ui/skeleton'
import { hasPermissionOnSite, type Session, useSession } from '@/session/session'

type Site = components['schemas']['SiteDto']

export const siteTypes = ['building', 'gated_community', 'company', 'house'] as const

const siteIcons: Record<string, LucideIcon> = { building: Building2, gated_community: Trees, company: Briefcase, house: House }

/** Zonas horarias IANA, las de Argentina primero (design de add-admin-panel-shell, decisión 9). */
export const timeZones = [
  'America/Argentina/Cordoba',
  'America/Argentina/Buenos_Aires',
  'America/Argentina/Catamarca',
  'America/Argentina/Jujuy',
  'America/Argentina/La_Rioja',
  'America/Argentina/Mendoza',
  'America/Argentina/Rio_Gallegos',
  'America/Argentina/Salta',
  'America/Argentina/San_Juan',
  'America/Argentina/San_Luis',
  'America/Argentina/Tucuman',
  'America/Argentina/Ushuaia',
  'America/Montevideo',
  'America/Asuncion',
  'America/Santiago',
  'America/Sao_Paulo',
  'UTC',
]

/** Las claves de las consultas empiezan por la organización activa: nunca se mezclan datos de dos organizaciones. */
export const sitesKey = (organizationId: string | null) => ['org', organizationId, 'sites'] as const

type Editing = { mode: 'create' } | { mode: 'edit'; site: Site }

export function SitesPage() {
  const { t } = useTranslation()
  const session = useSession()
  const organizationId = session.status === 'active' ? session.organizationId : null
  // El formulario vive en un panel lateral; `editing` se conserva al cerrar para que el panel salga con su contenido.
  const [editing, setEditing] = useState<Editing>({ mode: 'create' })
  const [open, setOpen] = useState(false)
  const sites = useQuery({
    queryKey: sitesKey(organizationId),
    queryFn: async () => unwrap(await api.GET('/api/v1/sites')),
  })
  const canCreate = hasPermissionOnSite(session, 'sites.manage')

  function edit(next: Editing) {
    setEditing(next)
    setOpen(true)
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title={t('sites.title')}
        description={t('sites.description')}
        actions={
          canCreate && (
            <Button onClick={() => edit({ mode: 'create' })}>
              <Plus />
              {t('sites.create')}
            </Button>
          )
        }
      />

      {sites.isPending && <SitesSkeleton />}
      {sites.isError && <ErrorNotice error={sites.error} onRetry={() => void sites.refetch()} />}
      {sites.data && sites.data.length === 0 && (
        <div className="rounded-2xl border border-dashed bg-card/50">
          <EmptyState
            icon={Building2}
            title={t('sites.empty')}
            action={
              canCreate && (
                <Button variant="outline" onClick={() => edit({ mode: 'create' })}>
                  <Plus />
                  {t('sites.createFirst')}
                </Button>
              )
            }
          >
            {canCreate ? t('sites.emptyDescription') : t('sites.emptyDescriptionReadOnly')}
          </EmptyState>
        </div>
      )}
      {sites.data && sites.data.length > 0 && (
        <ul className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
          {sites.data.map((site) => (
            <li key={site.id}>
              <SiteCard site={site} session={session} onEdit={() => edit({ mode: 'edit', site })} />
            </li>
          ))}
        </ul>
      )}

      <Sheet open={open} onOpenChange={setOpen}>
        <SheetContent closeLabel={t('sites.close')}>
          <SiteForm
            key={editing.mode === 'edit' ? editing.site.id : 'new'}
            editing={editing}
            organizationId={organizationId}
            onDone={() => setOpen(false)}
          />
        </SheetContent>
      </Sheet>
    </div>
  )
}

/** Esqueletos con la forma de las tarjetas mientras llega el listado. */
function SitesSkeleton() {
  const { t } = useTranslation()
  return (
    <div aria-busy="true">
      <span role="status" className="sr-only">
        {t('app.loading')}
      </span>
      <ul className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3" aria-hidden="true">
        {[0, 1, 2].map((index) => (
          <li key={index} className="flex items-start gap-4 rounded-xl border bg-card p-5" data-testid="site-skeleton">
            <Skeleton className="size-11 rounded-lg" />
            <div className="flex-1 space-y-2.5 pt-1">
              <Skeleton className="h-4 w-2/3" />
              <Skeleton className="h-3 w-1/2" />
              <Skeleton className="h-5 w-20 rounded-full" />
            </div>
          </li>
        ))}
      </ul>
    </div>
  )
}

function SiteCard({ site, session, onEdit }: { site: Site; session: Session; onEdit: () => void }) {
  const { t } = useTranslation()
  const Icon = siteIcons[site.type] ?? Building2
  return (
    <Card className="relative flex h-full items-start gap-4 p-5 transition-[border-color,box-shadow] hover:border-primary/30 hover:shadow-md">
      <span className="flex size-11 shrink-0 items-center justify-center rounded-lg bg-accent text-accent-foreground">
        <Icon className="size-5" aria-hidden="true" />
      </span>
      <div className="min-w-0 flex-1 space-y-2">
        <h2 className="truncate pr-8 font-semibold leading-snug">{site.name}</h2>
        <p className="flex items-center gap-1.5 text-sm text-muted-foreground">
          <Globe className="size-3.5 shrink-0" aria-hidden="true" />
          <span className="truncate tabular-nums">{site.timeZone}</span>
        </p>
        <Badge variant="secondary">{t(`sites.types.${site.type}`, { defaultValue: site.type })}</Badge>
      </div>
      {hasPermissionOnSite(session, 'sites.manage', site.id) && (
        <Button
          variant="ghost"
          size="icon-sm"
          className="absolute top-3 right-3 text-muted-foreground"
          aria-label={`${t('sites.edit')} ${site.name}`}
          onClick={onEdit}
        >
          <Pencil />
        </Button>
      )}
    </Card>
  )
}

function SiteForm({ editing, organizationId, onDone }: { editing: Editing; organizationId: string | null; onDone: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const existing = editing.mode === 'edit' ? editing.site : null
  const [name, setName] = useState(existing?.name ?? '')
  const [type, setType] = useState<string>(existing?.type ?? 'building')
  const [timeZone, setTimeZone] = useState(existing?.timeZone ?? timeZones[0]!)
  const [nameRequired, setNameRequired] = useState(false)

  const save = useMutation({
    mutationFn: async () =>
      existing
        ? unwrap(await api.PUT('/api/v1/sites/{id}', { params: { path: { id: existing.id } }, body: { name, timeZone, configuration: null } }))
        : unwrap(
            await api.POST('/api/v1/sites', {
              body: { name, type: type as (typeof siteTypes)[number], timeZone, configuration: null },
            }),
          ),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: sitesKey(organizationId) })
      onDone()
    },
  })

  const error = save.error instanceof ApiError ? save.error : null
  const nameError = nameRequired ? t('sites.nameRequired') : error?.fieldError('name')
  const timeZoneError = error?.fieldError('timeZone')
  const typeError = error?.fieldError('type')
  const general = save.error && !nameError && !timeZoneError && !typeError ? describeError(save.error, t) : null

  function submit(event: FormEvent) {
    event.preventDefault()
    if (!name.trim()) {
      setNameRequired(true)
      return
    }

    setNameRequired(false)
    save.mutate()
  }

  const title = existing ? t('sites.editTitle') : t('sites.createTitle')
  return (
    <form aria-label={title} className="flex min-h-0 flex-1 flex-col" onSubmit={submit} noValidate>
      <SheetHeader>
        <SheetTitle>{title}</SheetTitle>
        <SheetDescription>{t('sites.formDescription')}</SheetDescription>
      </SheetHeader>
      <div className="flex-1 space-y-5 overflow-y-auto px-6 py-5">
        {general && <Alert variant="destructive">{general}</Alert>}
        <div className="space-y-2">
          <Label htmlFor="site-name">{t('sites.name')}</Label>
          <Input
            id="site-name"
            value={name}
            maxLength={200}
            placeholder={t('sites.namePlaceholder')}
            aria-invalid={!!nameError}
            aria-describedby={nameError ? 'site-name-error' : undefined}
            onChange={(e) => setName(e.target.value)}
          />
          <FieldError id="site-name-error" message={nameError} />
        </div>
        {!existing && (
          <div className="space-y-2">
            <Label htmlFor="site-type">{t('sites.type')}</Label>
            <Select id="site-type" value={type} aria-invalid={!!typeError} aria-describedby="site-type-hint" onChange={(e) => setType(e.target.value)}>
              {siteTypes.map((value) => (
                <option key={value} value={value}>
                  {t(`sites.types.${value}`)}
                </option>
              ))}
            </Select>
            <FieldHint id="site-type-hint">{t('sites.typeHint')}</FieldHint>
            <FieldError id="site-type-error" message={typeError} />
          </div>
        )}
        <div className="space-y-2">
          <Label htmlFor="site-time-zone">{t('sites.timeZone')}</Label>
          <Select id="site-time-zone" value={timeZone} aria-invalid={!!timeZoneError} onChange={(e) => setTimeZone(e.target.value)}>
            {(timeZones.includes(timeZone) ? timeZones : [timeZone, ...timeZones]).map((zone) => (
              <option key={zone} value={zone}>
                {zone}
              </option>
            ))}
          </Select>
          <FieldError id="site-time-zone-error" message={timeZoneError} />
        </div>
      </div>
      <SheetFooter>
        <Button type="button" variant="outline" onClick={onDone}>
          {t('sites.cancel')}
        </Button>
        <Button type="submit" loading={save.isPending}>
          {save.isPending ? t('sites.saving') : t('sites.save')}
        </Button>
      </SheetFooter>
    </form>
  )
}

