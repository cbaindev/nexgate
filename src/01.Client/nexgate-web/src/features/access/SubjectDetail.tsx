import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Ban, CalendarClock, CheckCircle2, KeyRound, Pencil, Plus, ShieldCheck, Trash2 } from 'lucide-react'
import { type ReactNode, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { api } from '@/api/client'
import { describeError, unwrap } from '@/api/errors'
import { ErrorNotice } from '@/app/ErrorNotice'
import type { Site } from '@/components/site-picker/SitePicker'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { ConfirmDialog } from '@/components/ui/confirm-dialog'
import { Sheet, SheetContent } from '@/components/ui/sheet'
import { Skeleton } from '@/components/ui/skeleton'
import { useSiteStructure } from '@/features/spaces/queries'
import { formatSiteDateTime } from '@/lib/zoned-time'
import { useSession } from '@/session/session'
import { CredentialForm } from './CredentialForm'
import { GrantForm } from './GrantForm'
import {
  type Credential,
  credentialIcons,
  credentialsKey,
  type Grant,
  grantsKey,
  schedulesKey,
  type Subject,
  subjectIcons,
  validityState,
} from './model'
import { KindBadge } from './SubjectsTab'

/** Detalle de un sujeto: credenciales y permisos (design de add-access-screens, decisión 5). */
export function SubjectDetail({
  site,
  subject,
  canManage,
  actions,
}: {
  site: Site
  subject: Subject
  canManage: boolean
  actions: ReactNode
}) {
  const Icon = subjectIcons[subject.kind] ?? subjectIcons.person!
  return (
    <Card className="divide-y" aria-labelledby="subject-detail-title">
      <div className="flex flex-wrap items-start gap-4 p-5">
        <span className="flex size-11 shrink-0 items-center justify-center rounded-lg bg-accent text-accent-foreground">
          <Icon className="size-5" aria-hidden="true" />
        </span>
        <div className="min-w-0 flex-1 space-y-1.5">
          <h2 id="subject-detail-title" className="text-lg font-semibold tracking-tight break-words">
            {subject.alias}
          </h2>
          <KindBadge kind={subject.kind} />
        </div>
        {actions && <div className="flex gap-2">{actions}</div>}
      </div>
      <Credentials site={site} subject={subject} canManage={canManage} />
      <Grants site={site} subject={subject} canManage={canManage} />
    </Card>
  )
}

function Section({
  id,
  icon: Icon,
  title,
  action,
  children,
}: {
  id: string
  icon: typeof KeyRound
  title: string
  action?: ReactNode
  children: ReactNode
}) {
  return (
    <section className="space-y-3 p-5" aria-labelledby={id}>
      <div className="flex items-center justify-between gap-2">
        <h3 id={id} className="flex items-center gap-2 text-sm font-semibold">
          <Icon className="size-4 text-muted-foreground" aria-hidden="true" />
          {title}
        </h3>
        {action}
      </div>
      {children}
    </section>
  )
}

function Loading() {
  const { t } = useTranslation()
  return (
    <div aria-busy="true" className="space-y-2">
      <span role="status" className="sr-only">
        {t('app.loading')}
      </span>
      <Skeleton className="h-12" />
    </div>
  )
}

function StateBadge({ item }: { item: { validFrom: string | null; validUntil: string | null } }) {
  const { t } = useTranslation()
  const state = validityState(item)
  const variant = state === 'valid' ? 'success' : state === 'notYet' ? 'info' : 'destructive'
  return <Badge variant={variant}>{t(`access.validity.${state}`)}</Badge>
}

function Validity({ item, timeZone }: { item: { validFrom: string | null; validUntil: string | null }; timeZone: string }) {
  const { t } = useTranslation()
  if (!item.validFrom && !item.validUntil) return <>{t('access.validity.forever')}</>
  if (item.validFrom && item.validUntil) {
    return <>{t('access.validity.between', { from: formatSiteDateTime(item.validFrom, timeZone), until: formatSiteDateTime(item.validUntil, timeZone) })}</>
  }
  return item.validFrom ? (
    <>{t('access.validity.since', { from: formatSiteDateTime(item.validFrom, timeZone) })}</>
  ) : (
    <>{t('access.validity.until', { until: formatSiteDateTime(item.validUntil!, timeZone) })}</>
  )
}

function Credentials({ site, subject, canManage }: { site: Site; subject: Subject; canManage: boolean }) {
  const { t } = useTranslation()
  const session = useSession()
  const organizationId = session.status === 'active' ? session.organizationId : null
  const queryClient = useQueryClient()
  const [editing, setEditing] = useState<Credential | null>(null)
  const [open, setOpen] = useState(false)
  const [deleting, setDeleting] = useState<Credential | null>(null)
  const credentials = useQuery({
    queryKey: credentialsKey(organizationId, site.id, subject.id),
    queryFn: async () =>
      unwrap(await api.GET('/api/v1/sites/{siteId}/credentials', { params: { path: { siteId: site.id }, query: { subjectId: subject.id } } })),
  })
  const remove = useMutation({
    mutationFn: async (credential: Credential) =>
      unwrap(await api.DELETE('/api/v1/credentials/{id}', { params: { path: { id: credential.id } } })),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: credentialsKey(organizationId, site.id, subject.id) })
      setDeleting(null)
    },
  })

  function edit(credential: Credential | null) {
    setEditing(credential)
    setOpen(true)
  }

  return (
    <Section
      id="subject-credentials-title"
      icon={KeyRound}
      title={t('access.credentials.title')}
      action={
        canManage && (
          <Button size="sm" variant="outline" onClick={() => edit(null)}>
            <Plus />
            {t('access.credentials.create')}
          </Button>
        )
      }
    >
      {credentials.isPending && <Loading />}
      {credentials.isError && <ErrorNotice error={credentials.error} onRetry={() => void credentials.refetch()} />}
      {credentials.data && credentials.data.length === 0 && <p className="text-sm text-muted-foreground">{t('access.credentials.empty')}</p>}
      {credentials.data && credentials.data.length > 0 && (
        <ul className="space-y-2" aria-label={t('access.credentials.listLabel', { name: subject.alias })}>
          {credentials.data.map((credential) => {
            const Icon = credentialIcons[credential.type] ?? credentialIcons.card!
            const label = t(`access.credentials.types.${credential.type}`, { defaultValue: credential.type })
            return (
              <li key={credential.id} className="flex items-center gap-3 rounded-lg border px-3 py-2 text-sm">
                <span className="flex size-8 shrink-0 items-center justify-center rounded-md bg-muted text-muted-foreground">
                  <Icon className="size-4" aria-hidden="true" />
                </span>
                <span className="min-w-0 flex-1">
                  <span className="flex flex-wrap items-center gap-2 font-medium">
                    {label}
                    <StateBadge item={credential} />
                  </span>
                  <span className="block truncate text-xs text-muted-foreground">
                    <Validity item={credential} timeZone={site.timeZone} />
                  </span>
                </span>
                {canManage && (
                  <span className="flex gap-1">
                    <Button size="icon-sm" variant="ghost" aria-label={t('access.credentials.editNamed', { type: label })} onClick={() => edit(credential)}>
                      <Pencil />
                    </Button>
                    <Button
                      size="icon-sm"
                      variant="ghost"
                      className="text-destructive hover:bg-destructive/10 hover:text-destructive"
                      aria-label={t('access.credentials.deleteNamed', { type: label })}
                      onClick={() => {
                        remove.reset()
                        setDeleting(credential)
                      }}
                    >
                      <Trash2 />
                    </Button>
                  </span>
                )}
              </li>
            )
          })}
        </ul>
      )}
      <Sheet open={open} onOpenChange={setOpen}>
        <SheetContent closeLabel={t('spaces.close')}>
          <CredentialForm key={editing?.id ?? 'new'} site={site} subject={subject} existing={editing} onDone={() => setOpen(false)} />
        </SheetContent>
      </Sheet>
      <ConfirmDialog
        open={deleting !== null}
        onOpenChange={(value) => !value && setDeleting(null)}
        title={t('access.credentials.deleteTitle')}
        description={t('access.credentials.deleteDescription')}
        confirmLabel={t('spaces.delete')}
        cancelLabel={t('spaces.cancel')}
        pending={remove.isPending}
        error={remove.error ? describeError(remove.error, t) : null}
        onConfirm={() => deleting && remove.mutate(deleting)}
      />
    </Section>
  )
}

function Grants({ site, subject, canManage }: { site: Site; subject: Subject; canManage: boolean }) {
  const { t } = useTranslation()
  const session = useSession()
  const organizationId = session.status === 'active' ? session.organizationId : null
  const queryClient = useQueryClient()
  const [open, setOpen] = useState(false)
  const [formKey, setFormKey] = useState(0)
  const [deleting, setDeleting] = useState<Grant | null>(null)
  const { points, tree } = useSiteStructure(site.id)
  const grants = useQuery({
    queryKey: grantsKey(organizationId, site.id, subject.id),
    queryFn: async () =>
      unwrap(await api.GET('/api/v1/sites/{siteId}/grants', { params: { path: { siteId: site.id }, query: { subjectId: subject.id } } })),
  })
  const schedules = useQuery({
    queryKey: schedulesKey(organizationId, site.id),
    queryFn: async () => unwrap(await api.GET('/api/v1/sites/{siteId}/schedules', { params: { path: { siteId: site.id } } })),
  })
  const remove = useMutation({
    mutationFn: async (grant: Grant) => unwrap(await api.DELETE('/api/v1/grants/{id}', { params: { path: { id: grant.id } } })),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: grantsKey(organizationId, site.id, subject.id) })
      setDeleting(null)
    },
  })

  function target(grant: Grant) {
    if (grant.targetType === 'access_point') {
      const point = points.data?.find((p) => p.id === grant.targetId)
      return point ? t('access.grants.targetPoint', { name: point.name, to: tree.path(point.toSpaceId) }) : '…'
    }
    return tree.path(grant.targetId) || '…'
  }

  return (
    <Section
      id="subject-grants-title"
      icon={ShieldCheck}
      title={t('access.grants.title')}
      action={
        canManage && (
          <Button
            size="sm"
            variant="outline"
            onClick={() => {
              setFormKey((k) => k + 1)
              setOpen(true)
            }}
          >
            <Plus />
            {t('access.grants.create')}
          </Button>
        )
      }
    >
      {grants.isPending && <Loading />}
      {grants.isError && <ErrorNotice error={grants.error} onRetry={() => void grants.refetch()} />}
      {grants.data && grants.data.length === 0 && <p className="text-sm text-muted-foreground">{t('access.grants.empty')}</p>}
      {grants.data && grants.data.length > 0 && (
        <ul className="space-y-2" aria-label={t('access.grants.listLabel', { name: subject.alias })}>
          {grants.data.map((grant) => {
            const deny = grant.effect === 'deny'
            const schedule = schedules.data?.find((s) => s.id === grant.scheduleId)
            const where = target(grant)
            return (
              <li key={grant.id} className="flex items-start gap-3 rounded-lg border px-3 py-2.5 text-sm">
                <span
                  className={
                    deny
                      ? 'flex size-8 shrink-0 items-center justify-center rounded-md bg-destructive/10 text-destructive'
                      : 'flex size-8 shrink-0 items-center justify-center rounded-md bg-success/10 text-success'
                  }
                >
                  {deny ? <Ban className="size-4" aria-hidden="true" /> : <CheckCircle2 className="size-4" aria-hidden="true" />}
                </span>
                <span className="min-w-0 flex-1 space-y-1">
                  <span className="flex flex-wrap items-center gap-2">
                    <Badge variant={deny ? 'destructive' : 'success'}>{t(`access.grants.effects.${grant.effect}`)}</Badge>
                    <span className="font-medium">{where}</span>
                  </span>
                  <span className="flex flex-wrap items-center gap-x-3 gap-y-0.5 text-xs text-muted-foreground">
                    <span className="inline-flex items-center gap-1">
                      <CalendarClock className="size-3" aria-hidden="true" />
                      {schedule ? schedule.name : t('access.grants.always')}
                    </span>
                    <span>
                      <Validity item={grant} timeZone={site.timeZone} />
                    </span>
                    {grant.remainingUses !== null && <span>{t('access.grants.remainingUses', { count: grant.remainingUses })}</span>}
                    {grant.origin !== 'manual' && <span>{t('access.grants.origin', { origin: grant.origin })}</span>}
                  </span>
                  {deny && <span className="block text-xs text-destructive">{t('access.grants.denyWins')}</span>}
                </span>
                {canManage && (
                  <Button
                    size="icon-sm"
                    variant="ghost"
                    className="text-destructive hover:bg-destructive/10 hover:text-destructive"
                    aria-label={t('access.grants.deleteNamed', { target: where })}
                    onClick={() => {
                      remove.reset()
                      setDeleting(grant)
                    }}
                  >
                    <Trash2 />
                  </Button>
                )}
              </li>
            )
          })}
        </ul>
      )}
      <Sheet open={open} onOpenChange={setOpen}>
        <SheetContent closeLabel={t('spaces.close')}>
          <GrantForm key={formKey} site={site} subject={subject} schedules={schedules.data ?? []} onDone={() => setOpen(false)} />
        </SheetContent>
      </Sheet>
      <ConfirmDialog
        open={deleting !== null}
        onOpenChange={(value) => !value && setDeleting(null)}
        title={t('access.grants.deleteTitle')}
        description={t('access.grants.deleteDescription')}
        confirmLabel={t('spaces.delete')}
        cancelLabel={t('spaces.cancel')}
        pending={remove.isPending}
        error={remove.error ? describeError(remove.error, t) : null}
        onConfirm={() => deleting && remove.mutate(deleting)}
      />
    </Section>
  )
}
