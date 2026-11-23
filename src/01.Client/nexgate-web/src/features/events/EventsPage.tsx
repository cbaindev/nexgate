import { useInfiniteQuery, useQuery, useQueryClient } from '@tanstack/react-query'
import { CalendarClock, CircleCheck, CircleX, CloudOff, Pause, Play, X } from 'lucide-react'
import { useEffect, useMemo, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useSearchParams } from 'react-router'
import { api } from '@/api/client'
import { unwrap } from '@/api/errors'
import type { components } from '@/api/schema'
import { ErrorNotice } from '@/app/ErrorNotice'
import { type Site, SiteGate, SitePicker, useSelectedSite } from '@/components/site-picker/SitePicker'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { EmptyState } from '@/components/ui/empty-state'
import { Input, Label, Select } from '@/components/ui/form'
import { PageHeader } from '@/components/ui/page-header'
import { Skeleton } from '@/components/ui/skeleton'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { subjectsKey } from '@/features/access/model'
import { devicesKey } from '@/features/devices/model'
import { useSiteStructure } from '@/features/spaces/queries'
import { cn } from '@/lib/utils'
import { formatSiteDateTime, toSiteInstant } from '@/lib/zoned-time'
import { hasPermissionOnSite, useSession } from '@/session/session'

type AccessEvent = components['schemas']['AccessEventDto']

/** Pedido cada 15 s mientras la pestaña está visible (decisión del usuario, design de add-device-and-event-screens). */
export const EventsRefreshMs = 15_000
const PageSize = 50
/** Un evento que llegó más tarde que esto se registró sin conexión. */
const OfflineMinutes = 2
const HighlightMs = 4_000

/** Filtros en la dirección; las fechas en la hora del sitio (`YYYY-MM-DDTHH:mm`). */
const filterParams = { accessPointId: 'puerta', subjectId: 'persona', from: 'desde', to: 'hasta' } as const
type Filters = Record<keyof typeof filterParams, string>

export function EventsPage() {
  const { t } = useTranslation()
  // Los eventos exigen el permiso sobre el sitio entero (spec event-ingestion).
  const selected = useSelectedSite({ permission: 'events.read' })
  return (
    <div className="space-y-6">
      <PageHeader title={t('events.title')} description={t('events.description')} actions={<SitePicker selected={selected} />} />
      <SiteGate selected={selected} noSites={{ title: t('access.noSites'), description: t('events.noSitesDescription') }}>
        {(site) => <SiteEvents key={site.id} site={site} />}
      </SiteGate>
    </div>
  )
}

function SiteEvents({ site }: { site: Site }) {
  const { t } = useTranslation()
  const session = useSession()
  const organizationId = session.status === 'active' ? session.organizationId : null
  const [params, setParams] = useSearchParams()
  const filters = Object.fromEntries(Object.entries(filterParams).map(([key, param]) => [key, params.get(param) ?? ''])) as Filters
  const canSubjects = hasPermissionOnSite(session, 'access.read', site.id)
  const canDevices = hasPermissionOnSite(session, 'devices.read', site.id)
  const { points } = useSiteStructure(site.id)
  // Sin permiso de accesos no se piden sujetos ni se muestran sus nombres.
  const subjects = useQuery({
    queryKey: subjectsKey(organizationId, site.id),
    queryFn: async () => unwrap(await api.GET('/api/v1/sites/{siteId}/subjects', { params: { path: { siteId: site.id } } })),
    enabled: canSubjects,
  })
  const devices = useQuery({
    queryKey: devicesKey(organizationId, site.id),
    queryFn: async () => unwrap(await api.GET('/api/v1/sites/{siteId}/devices', { params: { path: { siteId: site.id } } })),
    enabled: canDevices,
  })

  function setFilter(key: keyof Filters, value: string) {
    setParams(
      (current) => {
        const next = new URLSearchParams(current)
        if (value) next.set(filterParams[key], value)
        else next.delete(filterParams[key])
        return next
      },
      { replace: true },
    )
  }

  const hasFilters = Object.values(filters).some(Boolean)
  const names = {
    point: (id: string) => points.data?.find((p) => p.id === id)?.name ?? '…',
    subject: (id: string | null) => (id ? (subjects.data?.find((s) => s.id === id)?.alias ?? '…') : t('events.unidentified')),
    device: (id: string) => (canDevices ? (devices.data?.find((d) => d.id === id)?.name ?? '…') : '—'),
  }

  return (
    <div className="space-y-4">
      <Card className="p-4">
        <form className="grid gap-3 sm:grid-cols-2 lg:grid-cols-[1fr_1fr_auto_auto_auto] lg:items-end" aria-label={t('events.filters')} onSubmit={(e) => e.preventDefault()}>
          <div className="space-y-1.5">
            <Label htmlFor="events-point">{t('events.point')}</Label>
            <Select id="events-point" value={filters.accessPointId} onChange={(e) => setFilter('accessPointId', e.target.value)}>
              <option value="">{t('events.all')}</option>
              {[...(points.data ?? [])]
                .sort((a, b) => a.name.localeCompare(b.name, 'es', { numeric: true }))
                .map((point) => (
                  <option key={point.id} value={point.id}>
                    {point.name}
                  </option>
                ))}
            </Select>
          </div>
          {canSubjects && (
            <div className="space-y-1.5">
              <Label htmlFor="events-subject">{t('events.subject')}</Label>
              <Select id="events-subject" value={filters.subjectId} onChange={(e) => setFilter('subjectId', e.target.value)}>
                <option value="">{t('events.all')}</option>
                {(subjects.data ?? []).map((subject) => (
                  <option key={subject.id} value={subject.id}>
                    {subject.alias}
                  </option>
                ))}
              </Select>
            </div>
          )}
          <div className="space-y-1.5">
            <Label htmlFor="events-from">{t('access.validity.from')}</Label>
            <Input id="events-from" type="datetime-local" value={filters.from} onChange={(e) => setFilter('from', e.target.value)} />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="events-to">{t('access.validity.untilLabel')}</Label>
            <Input id="events-to" type="datetime-local" value={filters.to} onChange={(e) => setFilter('to', e.target.value)} />
          </div>
          <Button
            type="button"
            variant="ghost"
            disabled={!hasFilters}
            onClick={() =>
              setParams((current) => {
                const next = new URLSearchParams(current)
                Object.values(filterParams).forEach((p) => next.delete(p))
                return next
              })
            }
          >
            <X />
            {t('events.clear')}
          </Button>
        </form>
        <p className="mt-2 text-xs text-muted-foreground">{t('access.test.atHint', { timeZone: site.timeZone })}</p>
      </Card>
      <EventList key={JSON.stringify(filters)} site={site} filters={filters} showSubject={canSubjects} names={names} />
    </div>
  )
}

function query(site: Site, filters: Filters, cursor: string | null) {
  return {
    accessPointId: filters.accessPointId || undefined,
    subjectId: filters.subjectId || undefined,
    from: filters.from ? toSiteInstant(filters.from, site.timeZone) : undefined,
    to: filters.to ? toSiteInstant(filters.to, site.timeZone) : undefined,
    cursor: cursor ?? undefined,
    limit: PageSize,
  }
}

function EventList({
  site,
  filters,
  showSubject,
  names,
}: {
  site: Site
  filters: Filters
  showSubject: boolean
  names: { point: (id: string) => string; subject: (id: string | null) => string; device: (id: string) => string }
}) {
  const { t } = useTranslation()
  const session = useSession()
  const organizationId = session.status === 'active' ? session.organizationId : null
  const [paused, setPaused] = useState(false)
  const [fresh, setFresh] = useState<AccessEvent[]>([])
  const [highlighted, setHighlighted] = useState<Set<string>>(new Set())
  const [announcement, setAnnouncement] = useState('')
  const queryClient = useQueryClient()
  const key = ['org', organizationId, 'sites', site.id, 'events', filters] as const
  const latestKey = [...key, 'latest'] as const
  const fetchPage = async (cursor: string | null) =>
    unwrap(await api.GET('/api/v1/sites/{siteId}/access-events', { params: { path: { siteId: site.id }, query: query(site, filters, cursor) } }))

  const events = useInfiniteQuery({
    queryKey: key,
    queryFn: async ({ pageParam }) => {
      const page = await fetchPage(pageParam)
      // La primera página también es la última vista: así la actualización no la vuelve a pedir enseguida.
      if (pageParam === null) queryClient.setQueryData(latestKey, page)
      return page
    },
    initialPageParam: null as string | null,
    getNextPageParam: (last) => last.nextCursor ?? undefined,
    refetchOnWindowFocus: false,
  })

  // Actualización: solo la primera página, aparte, para no volver a pedir todas las cargadas (decisión 6).
  const live = !filters.to && !paused
  const latest = useQuery({
    queryKey: latestKey,
    queryFn: () => fetchPage(null),
    enabled: live && events.isSuccess,
    refetchInterval: live ? EventsRefreshMs : false,
    staleTime: EventsRefreshMs,
  })

  const loaded = useMemo(() => events.data?.pages.flatMap((p) => p.items) ?? [], [events.data])
  const known = useRef(new Set<string>())
  useEffect(() => {
    loaded.forEach((e) => known.current.add(e.id))
  }, [loaded])

  useEffect(() => {
    const incoming = latest.data?.items.filter((e) => !known.current.has(e.id)) ?? []
    if (incoming.length === 0) return
    incoming.forEach((e) => known.current.add(e.id))
    setFresh((current) => [...incoming, ...current])
    setHighlighted(new Set(incoming.map((e) => e.id)))
    setAnnouncement(t('events.newCount', { count: incoming.length }))
    const timer = setTimeout(() => setHighlighted(new Set()), HighlightMs)
    return () => clearTimeout(timer)
  }, [latest.data, t])

  if (events.isPending) {
    return (
      <div aria-busy="true" className="space-y-2">
        <span role="status" className="sr-only">
          {t('app.loading')}
        </span>
        {[0, 1, 2, 3].map((i) => (
          <Skeleton key={i} className="h-12" />
        ))}
      </div>
    )
  }

  if (events.isError) return <ErrorNotice error={events.error} onRetry={() => void events.refetch()} />

  const seen = new Set<string>()
  const rows = [...fresh, ...loaded].filter((e) => (seen.has(e.id) ? false : (seen.add(e.id), true)))

  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-center justify-between gap-2 text-sm text-muted-foreground">
        <span className="flex items-center gap-2">
          {live && <span className="size-2 animate-pulse rounded-full bg-success" aria-hidden="true" />}
          {filters.to ? t('events.liveOffRange') : paused ? t('events.paused') : t('events.live')}
        </span>
        {!filters.to && (
          <Button size="sm" variant="outline" onClick={() => setPaused(!paused)}>
            {paused ? <Play /> : <Pause />}
            {paused ? t('events.resume') : t('events.pause')}
          </Button>
        )}
      </div>
      <div aria-live="polite" className="sr-only">
        {announcement}
      </div>

      {rows.length === 0 ? (
        <div className="rounded-2xl border border-dashed bg-card/50">
          <EmptyState icon={CalendarClock} title={Object.values(filters).some(Boolean) ? t('events.emptyFiltered') : t('events.empty')}>
            {t('events.emptyDescription')}
          </EmptyState>
        </div>
      ) : (
        <Table aria-label={t('events.tableLabel', { site: site.name })} className="block md:table">
          <TableHeader className="hidden md:table-header-group">
            <TableRow>
              <TableHead>{t('events.time')}</TableHead>
              <TableHead>{t('events.point')}</TableHead>
              {showSubject && <TableHead>{t('events.subject')}</TableHead>}
              <TableHead>{t('events.result')}</TableHead>
              <TableHead>{t('events.device')}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody className="block md:table-row-group">
            {rows.map((event) => (
              <EventRow key={event.id} event={event} site={site} showSubject={showSubject} names={names} highlighted={highlighted.has(event.id)} />
            ))}
          </TableBody>
        </Table>
      )}

      {events.hasNextPage ? (
        <div className="flex justify-center">
          <Button variant="outline" loading={events.isFetchingNextPage} onClick={() => void events.fetchNextPage()}>
            {t('events.loadMore')}
          </Button>
        </div>
      ) : (
        rows.length > 0 && <p className="text-center text-sm text-muted-foreground">{t('events.noMore')}</p>
      )}
    </div>
  )
}

const cell =
  'flex items-baseline justify-between gap-4 px-4 py-1.5 md:table-cell md:px-4 md:py-3 before:text-xs before:text-muted-foreground before:content-[attr(data-label)] md:before:content-none'

function EventRow({
  event,
  site,
  showSubject,
  names,
  highlighted,
}: {
  event: AccessEvent
  site: Site
  showSubject: boolean
  names: { point: (id: string) => string; subject: (id: string | null) => string; device: (id: string) => string }
  highlighted: boolean
}) {
  const { t } = useTranslation()
  const allowed = event.decision === 'allow'
  const offline = Date.parse(event.receivedAt) - Date.parse(event.occurredAt) > OfflineMinutes * 60_000
  return (
    <TableRow className={cn('block py-2 transition-colors duration-700 md:table-row md:py-0', highlighted && 'animate-rise bg-primary/8')}>
      <TableCell className={cell} data-label={t('events.time')}>
        <span className="text-right md:text-left">
          <span className="block font-medium tabular-nums" title={formatSiteDateTime(event.occurredAt, site.timeZone, { seconds: true })}>
            {formatSiteDateTime(event.occurredAt, site.timeZone, { seconds: true })}
          </span>
          {offline && (
            <span className="flex items-center gap-1 text-xs text-muted-foreground">
              <CloudOff className="size-3" aria-hidden="true" />
              {t('events.offline')}
            </span>
          )}
        </span>
      </TableCell>
      <TableCell className={cell} data-label={t('events.point')}>
        {names.point(event.accessPointId)}
      </TableCell>
      {showSubject && (
        <TableCell className={cell} data-label={t('events.subject')}>
          <span className={cn(!event.subjectId && 'text-muted-foreground')}>{names.subject(event.subjectId)}</span>
        </TableCell>
      )}
      <TableCell className={cell} data-label={t('events.result')}>
        <span className="text-right md:text-left">
          <Badge variant={allowed ? 'success' : 'destructive'}>
            {allowed ? <CircleCheck className="size-3" aria-hidden="true" /> : <CircleX className="size-3" aria-hidden="true" />}
            {t(`events.decisions.${event.decision}`, { defaultValue: event.decision })}
          </Badge>
          <span className="mt-0.5 block text-xs text-muted-foreground">{t(`events.reasons.${event.reason}`, { defaultValue: event.reason })}</span>
        </span>
      </TableCell>
      <TableCell className={cell} data-label={t('events.device')}>
        {names.device(event.deviceId)}
      </TableCell>
    </TableRow>
  )
}
