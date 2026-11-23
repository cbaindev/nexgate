import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Pencil, Plus } from 'lucide-react'
import { type FormEvent, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { api } from '@/api/client'
import { ApiError, describeError, unwrap } from '@/api/errors'
import type { components } from '@/api/schema'
import { ErrorNotice } from '@/app/ErrorNotice'
import { Button } from '@/components/ui/button'
import { Alert, Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { FieldError, Input, Label, Select } from '@/components/ui/form'
import { hasPermissionOnSite, type Session, useSession } from '@/session/session'

type Site = components['schemas']['SiteDto']

export const siteTypes = ['building', 'gated_community', 'company', 'house'] as const

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

type Editing = { mode: 'create' } | { mode: 'edit'; site: Site } | null

export function SitesPage() {
  const { t } = useTranslation()
  const session = useSession()
  const organizationId = session.status === 'active' ? session.organizationId : null
  const [editing, setEditing] = useState<Editing>(null)
  const sites = useQuery({
    queryKey: sitesKey(organizationId),
    queryFn: async () => unwrap(await api.GET('/api/v1/sites')),
  })
  const canCreate = hasPermissionOnSite(session, 'sites.manage')

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold">{t('sites.title')}</h1>
          <p className="text-muted-foreground">{t('sites.description')}</p>
        </div>
        {canCreate && editing === null && (
          <Button onClick={() => setEditing({ mode: 'create' })}>
            <Plus />
            {t('sites.create')}
          </Button>
        )}
      </div>

      {editing && <SiteForm editing={editing} organizationId={organizationId} onDone={() => setEditing(null)} />}

      {sites.isPending && <p className="text-muted-foreground">{t('app.loading')}</p>}
      {sites.isError && <ErrorNotice error={sites.error} onRetry={() => void sites.refetch()} />}
      {sites.data && sites.data.length === 0 && <p className="text-muted-foreground">{t('sites.empty')}</p>}
      {sites.data && sites.data.length > 0 && (
        <ul className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
          {sites.data.map((site) => (
            <li key={site.id}>
              <SiteCard site={site} session={session} onEdit={() => setEditing({ mode: 'edit', site })} />
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}

function SiteCard({ site, session, onEdit }: { site: Site; session: Session; onEdit: () => void }) {
  const { t } = useTranslation()
  return (
    <Card>
      <CardHeader className="flex-row items-start justify-between gap-2 space-y-0">
        <div className="min-w-0 space-y-1">
          <CardTitle className="truncate text-base">{site.name}</CardTitle>
          <p className="text-sm text-muted-foreground">
            {t(`sites.types.${site.type}`, { defaultValue: site.type })} · {site.timeZone}
          </p>
        </div>
        {hasPermissionOnSite(session, 'sites.manage', site.id) && (
          <Button variant="ghost" size="icon" aria-label={`${t('sites.edit')} ${site.name}`} onClick={onEdit}>
            <Pencil />
          </Button>
        )}
      </CardHeader>
    </Card>
  )
}

function SiteForm({ editing, organizationId, onDone }: { editing: NonNullable<Editing>; organizationId: string | null; onDone: () => void }) {
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
    <Card>
      <CardHeader>
        <CardTitle>{title}</CardTitle>
      </CardHeader>
      <CardContent>
        <form aria-label={title} className="grid gap-4 sm:grid-cols-2" onSubmit={submit} noValidate>
          {general && (
            <Alert variant="destructive" className="sm:col-span-2">
              {general}
            </Alert>
          )}
          <div className="space-y-2 sm:col-span-2">
            <Label htmlFor="site-name">{t('sites.name')}</Label>
            <Input
              id="site-name"
              value={name}
              maxLength={200}
              aria-invalid={!!nameError}
              aria-describedby={nameError ? 'site-name-error' : undefined}
              onChange={(e) => setName(e.target.value)}
            />
            <FieldError id="site-name-error" message={nameError} />
          </div>
          {!existing && (
            <div className="space-y-2">
              <Label htmlFor="site-type">{t('sites.type')}</Label>
              <Select id="site-type" value={type} aria-invalid={!!typeError} onChange={(e) => setType(e.target.value)}>
                {siteTypes.map((value) => (
                  <option key={value} value={value}>
                    {t(`sites.types.${value}`)}
                  </option>
                ))}
              </Select>
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
          <div className="flex gap-2 sm:col-span-2">
            <Button type="submit" disabled={save.isPending}>
              {save.isPending ? t('sites.saving') : t('sites.save')}
            </Button>
            <Button type="button" variant="outline" onClick={onDone}>
              {t('sites.cancel')}
            </Button>
          </div>
        </form>
      </CardContent>
    </Card>
  )
}
