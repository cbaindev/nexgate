import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Ban, DoorClosed, KeyRound, Pencil, Plus, Router, Trash2 } from 'lucide-react'
import { type FormEvent, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useSearchParams } from 'react-router'
import { api } from '@/api/client'
import { ApiError, describeError, unwrap } from '@/api/errors'
import { ErrorNotice } from '@/app/ErrorNotice'
import { type Site, SiteGate, SitePicker, useSelectedSite } from '@/components/site-picker/SitePicker'
import { Alert } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { ConfirmDialog } from '@/components/ui/confirm-dialog'
import { EmptyState } from '@/components/ui/empty-state'
import { FieldError, FieldHint, Input, Label } from '@/components/ui/form'
import { PageHeader } from '@/components/ui/page-header'
import { Sheet, SheetContent, SheetDescription, SheetFooter, SheetHeader, SheetTitle } from '@/components/ui/sheet'
import { Skeleton } from '@/components/ui/skeleton'
import { useSiteStructure } from '@/features/spaces/queries'
import { cn } from '@/lib/utils'
import { hasPermissionOnSite, useSession } from '@/session/session'
import { ClaimCodeDialog, type ShownClaim } from './ClaimCodeDialog'
import { DeviceDetail, StatusBadge } from './DeviceDetail'
import { type Device, devicesKey, DevicesRefreshMs } from './model'

const deviceParam = 'dispositivo'

type Confirming = { kind: 'revoke' | 'claim' | 'delete'; device: Device }

export function DevicesPage() {
  const { t } = useTranslation()
  // Los dispositivos exigen el permiso sobre el sitio entero: un alcance de espacio no alcanza (spec device-provisioning).
  const selected = useSelectedSite({ permission: 'devices.read' })
  return (
    <div className="space-y-6">
      <PageHeader title={t('devices.title')} description={t('devices.description')} actions={<SitePicker selected={selected} />} />
      <SiteGate selected={selected} noSites={{ title: t('access.noSites'), description: t('devices.noSitesDescription') }}>
        {(site) => <SiteDevices key={site.id} site={site} />}
      </SiteGate>
    </div>
  )
}

function SiteDevices({ site }: { site: Site }) {
  const { t } = useTranslation()
  const session = useSession()
  const organizationId = session.status === 'active' ? session.organizationId : null
  const queryClient = useQueryClient()
  const [params, setParams] = useSearchParams()
  const canManage = hasPermissionOnSite(session, 'devices.manage', site.id)
  const [naming, setNaming] = useState<Device | null>(null)
  const [nameOpen, setNameOpen] = useState(false)
  const [assigning, setAssigning] = useState<Device | null>(null)
  const [confirming, setConfirming] = useState<Confirming | null>(null)
  // El código de reclamo vive solo acá, mientras el diálogo está abierto.
  const [claim, setClaim] = useState<ShownClaim | null>(null)
  const devices = useQuery({
    queryKey: devicesKey(organizationId, site.id),
    queryFn: async () => unwrap(await api.GET('/api/v1/sites/{siteId}/devices', { params: { path: { siteId: site.id } } })),
    refetchInterval: DevicesRefreshMs,
  })
  const { points, tree } = useSiteStructure(site.id)
  const selectedId = params.get(deviceParam)
  const selected = devices.data?.find((d) => d.id === selectedId) ?? null

  function select(id: string | null) {
    setParams((current) => {
      const next = new URLSearchParams(current)
      if (id) next.set(deviceParam, id)
      else next.delete(deviceParam)
      return next
    })
    if (id && typeof window !== 'undefined' && window.innerWidth < 768) {
      requestAnimationFrame(() => document.getElementById('device-detail')?.scrollIntoView?.({ behavior: 'smooth', block: 'start' }))
    }
  }

  function confirm(kind: Confirming['kind'], device: Device) {
    act.reset()
    setConfirming({ kind, device })
  }

  const refresh = () => queryClient.invalidateQueries({ queryKey: devicesKey(organizationId, site.id) })

  const act = useMutation({
    mutationFn: async ({ kind, device }: Confirming) => {
      const path = { params: { path: { id: device.id } } }
      if (kind === 'revoke') return { issued: null, device: unwrap(await api.POST('/api/v1/devices/{id}/revocation', path)) }
      if (kind === 'claim') {
        const issued = unwrap(await api.POST('/api/v1/devices/{id}/claim-code', path))
        return { issued, device: issued.device }
      }
      unwrap(await api.DELETE('/api/v1/devices/{id}', path))
      return { issued: null, device: null }
    },
    onSuccess: async ({ issued }, { kind, device }) => {
      await refresh()
      setConfirming(null)
      if (kind === 'delete') select(null)
      if (issued) setClaim({ deviceName: device.name, claimCode: issued.claimCode, claimExpiresAt: issued.claimExpiresAt })
    },
  })

  if (devices.isPending) {
    return (
      <div aria-busy="true" className="space-y-3">
        <span role="status" className="sr-only">
          {t('app.loading')}
        </span>
        {[0, 1, 2].map((i) => (
          <Skeleton key={i} className="h-14 max-w-sm" />
        ))}
      </div>
    )
  }

  if (devices.isError) return <ErrorNotice error={devices.error} onRetry={() => void devices.refetch()} />

  const pointName = (id: string) => points.data?.find((p) => p.id === id)?.name ?? '…'
  const nameSheet = (
    <Sheet open={nameOpen} onOpenChange={setNameOpen}>
      <SheetContent closeLabel={t('spaces.close')}>
        <DeviceNameForm
          key={naming?.id ?? 'new'}
          site={site}
          existing={naming}
          onDone={(result) => {
            setNameOpen(false)
            if (result?.device) select(result.device.id)
            if (result?.claim) setClaim(result.claim)
          }}
        />
      </SheetContent>
    </Sheet>
  )
  const claimDialog = <ClaimCodeDialog claim={claim} timeZone={site.timeZone} onClose={() => setClaim(null)} />

  if (devices.data.length === 0) {
    return (
      <>
        <div className="rounded-2xl border border-dashed bg-card/50">
          <EmptyState
            icon={Router}
            title={t('devices.empty')}
            action={
              canManage && (
                <Button
                  variant="outline"
                  onClick={() => {
                    setNaming(null)
                    setNameOpen(true)
                  }}
                >
                  <Plus />
                  {t('devices.createFirst')}
                </Button>
              )
            }
          >
            {t('devices.emptyDescription')}
          </EmptyState>
        </div>
        {nameSheet}
        {claimDialog}
      </>
    )
  }

  const confirmText = confirming ? t(`devices.confirm.${confirming.kind}`) : ''
  return (
    <div className="grid items-start gap-6 md:grid-cols-[minmax(0,2fr)_minmax(0,3fr)]">
      <Card className="space-y-3 p-3">
        {canManage && (
          <Button
            className="w-full"
            onClick={() => {
              setNaming(null)
              setNameOpen(true)
            }}
          >
            <Plus />
            {t('devices.create')}
          </Button>
        )}
        <ul aria-label={t('devices.listLabel')} className="space-y-0.5">
          {devices.data.map((device) => {
            const current = device.id === selected?.id
            return (
              <li key={device.id}>
                <button
                  type="button"
                  aria-current={current ? 'true' : undefined}
                  onClick={() => select(device.id)}
                  className={cn(
                    'flex w-full items-center gap-2.5 rounded-md px-2.5 py-2 text-left text-sm transition-colors outline-none',
                    'hover:bg-accent hover:text-accent-foreground focus-visible:ring-2 focus-visible:ring-ring',
                    current && 'bg-primary/10 font-medium text-primary hover:bg-primary/15 hover:text-primary',
                  )}
                >
                  <Router className="size-4 shrink-0 opacity-80" aria-hidden="true" />
                  <span className="min-w-0 flex-1 truncate">{device.name}</span>
                  <StatusBadge device={device} />
                </button>
              </li>
            )
          })}
        </ul>
      </Card>
      <div id="device-detail" className="scroll-mt-20">
        {selected ? (
          <DeviceDetail
            site={site}
            device={selected}
            pointName={pointName}
            pathOf={(id) => {
              const point = points.data?.find((p) => p.id === id)
              return point ? tree.path(point.toSpaceId) : ''
            }}
            actions={
              canManage && (
                <>
                  <Button size="sm" variant="outline" onClick={() => setAssigning(selected)}>
                    <DoorClosed />
                    {t('devices.assign')}
                  </Button>
                  <Button
                    size="sm"
                    variant="ghost"
                    aria-label={t('spaces.editNamed', { name: selected.name })}
                    onClick={() => {
                      setNaming(selected)
                      setNameOpen(true)
                    }}
                  >
                    <Pencil />
                    {t('devices.rename')}
                  </Button>
                  <Button size="sm" variant="ghost" onClick={() => confirm('claim', selected)}>
                    <KeyRound />
                    {t('devices.newCode')}
                  </Button>
                  {selected.status !== 'revoked' && (
                    <Button
                      size="sm"
                      variant="ghost"
                      className="text-destructive hover:bg-destructive/10 hover:text-destructive"
                      onClick={() => confirm('revoke', selected)}
                    >
                      <Ban />
                      {t('devices.revoke')}
                    </Button>
                  )}
                  <Button
                    size="icon-sm"
                    variant="ghost"
                    className="text-destructive hover:bg-destructive/10 hover:text-destructive"
                    aria-label={t('spaces.deleteNamed', { name: selected.name })}
                    onClick={() => confirm('delete', selected)}
                  >
                    <Trash2 />
                  </Button>
                </>
              )
            }
          />
        ) : (
          <div className="rounded-xl border border-dashed bg-card/50 px-6 py-12 text-center text-sm text-muted-foreground">{t('devices.pick')}</div>
        )}
      </div>
      {nameSheet}
      {claimDialog}
      <Sheet open={assigning !== null} onOpenChange={(open) => !open && setAssigning(null)}>
        <SheetContent closeLabel={t('spaces.close')}>
          {assigning && (
            <AssignForm
              key={assigning.id}
              device={assigning}
              devices={devices.data}
              site={site}
              onDone={() => setAssigning(null)}
            />
          )}
        </SheetContent>
      </Sheet>
      <ConfirmDialog
        open={confirming !== null}
        onOpenChange={(open) => !open && setConfirming(null)}
        title={confirming ? t(`devices.confirmTitle.${confirming.kind}`, { name: confirming.device.name }) : ''}
        description={confirmText}
        confirmLabel={confirming ? t(`devices.confirmAction.${confirming.kind}`) : ''}
        cancelLabel={t('spaces.cancel')}
        pending={act.isPending}
        error={act.error ? describeError(act.error, t) : null}
        onConfirm={() => confirming && act.mutate(confirming)}
      />
    </div>
  )
}

type NameResult = { device: Device; claim: ShownClaim | null } | null

/** Alta (devuelve el código de reclamo) o cambio de nombre de un dispositivo. */
function DeviceNameForm({ site, existing, onDone }: { site: Site; existing: Device | null; onDone: (result: NameResult) => void }) {
  const { t } = useTranslation()
  const session = useSession()
  const organizationId = session.status === 'active' ? session.organizationId : null
  const queryClient = useQueryClient()
  const [name, setName] = useState(existing?.name ?? '')
  const [required, setRequired] = useState(false)
  const save = useMutation({
    mutationFn: async (): Promise<NonNullable<NameResult>> => {
      if (existing) {
        const device = unwrap(await api.PUT('/api/v1/devices/{id}', { params: { path: { id: existing.id } }, body: { name: name.trim() } }))
        return { device, claim: null }
      }
      const issued = unwrap(await api.POST('/api/v1/sites/{siteId}/devices', { params: { path: { siteId: site.id } }, body: { name: name.trim() } }))
      return { device: issued.device, claim: { deviceName: issued.device.name, claimCode: issued.claimCode, claimExpiresAt: issued.claimExpiresAt } }
    },
    onSuccess: async (result) => {
      await queryClient.invalidateQueries({ queryKey: devicesKey(organizationId, site.id) })
      onDone(result)
    },
  })
  const error = save.error instanceof ApiError ? save.error : null
  const nameError = required ? t('spaces.nameRequired') : error?.fieldError('name')
  const general = save.error && !nameError ? describeError(save.error, t) : null

  function submit(event: FormEvent) {
    event.preventDefault()
    setRequired(!name.trim())
    if (name.trim()) save.mutate()
  }

  const title = existing ? t('devices.renameTitle') : t('devices.createTitle')
  return (
    <form aria-label={title} className="flex min-h-0 flex-1 flex-col" onSubmit={submit} noValidate>
      <SheetHeader>
        <SheetTitle>{title}</SheetTitle>
        <SheetDescription>{existing ? t('devices.renameDescription') : t('devices.createDescription')}</SheetDescription>
      </SheetHeader>
      <div className="flex-1 space-y-5 overflow-y-auto px-6 py-5">
        {general && <Alert variant="destructive">{general}</Alert>}
        <div className="space-y-2">
          <Label htmlFor="device-name">{t('spaces.name')}</Label>
          <Input
            id="device-name"
            value={name}
            maxLength={200}
            placeholder={t('devices.namePlaceholder')}
            aria-invalid={!!nameError}
            aria-describedby={nameError ? 'device-name-error' : undefined}
            onChange={(e) => setName(e.target.value)}
          />
          <FieldError id="device-name-error" message={nameError} />
        </div>
      </div>
      <SheetFooter>
        <Button type="button" variant="outline" onClick={() => onDone(null)}>
          {t('spaces.cancel')}
        </Button>
        <Button type="submit" loading={save.isPending}>
          {save.isPending ? t('spaces.saving') : t('spaces.save')}
        </Button>
      </SheetFooter>
    </form>
  )
}

/** Puertas que controla el dispositivo: las de otro aparecen deshabilitadas con su nombre (decisión 3). */
function AssignForm({ site, device, devices, onDone }: { site: Site; device: Device; devices: Device[]; onDone: () => void }) {
  const { t } = useTranslation()
  const session = useSession()
  const organizationId = session.status === 'active' ? session.organizationId : null
  const queryClient = useQueryClient()
  const { points, tree } = useSiteStructure(site.id)
  const [chosen, setChosen] = useState<string[]>(device.accessPointIds)
  const owner = (pointId: string) => devices.find((d) => d.id !== device.id && d.accessPointIds.includes(pointId))
  const save = useMutation({
    mutationFn: async () =>
      unwrap(await api.PUT('/api/v1/devices/{id}/access-points', { params: { path: { id: device.id } }, body: { accessPointIds: chosen } })),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: devicesKey(organizationId, site.id) })
      onDone()
    },
    onError: () => void queryClient.invalidateQueries({ queryKey: devicesKey(organizationId, site.id) }),
  })

  const sorted = [...(points.data ?? [])].sort((a, b) => a.name.localeCompare(b.name, 'es', { numeric: true }))
  return (
    <form
      aria-label={t('devices.assignTitle', { name: device.name })}
      className="flex min-h-0 flex-1 flex-col"
      onSubmit={(e) => {
        e.preventDefault()
        save.mutate()
      }}
    >
      <SheetHeader>
        <SheetTitle>{t('devices.assignTitle', { name: device.name })}</SheetTitle>
        <SheetDescription>{t('devices.assignDescription')}</SheetDescription>
      </SheetHeader>
      <div className="flex-1 space-y-3 overflow-y-auto px-6 py-5">
        {save.error && <Alert variant="destructive">{describeError(save.error, t)}</Alert>}
        {sorted.length === 0 && <p className="text-sm text-muted-foreground">{t('devices.noPoints')}</p>}
        <fieldset className="space-y-2">
          <legend className="sr-only">{t('devices.assignTitle', { name: device.name })}</legend>
          {sorted.map((point) => {
            const other = owner(point.id)
            const on = chosen.includes(point.id)
            return (
              <label
                key={point.id}
                className={cn(
                  'flex items-start gap-3 rounded-lg border p-3 transition-colors has-[:checked]:border-primary has-[:checked]:bg-primary/5',
                  other ? 'cursor-not-allowed opacity-60' : 'cursor-pointer',
                )}
              >
                <input
                  type="checkbox"
                  className="mt-0.5 size-4 shrink-0 accent-primary"
                  checked={on}
                  disabled={!!other}
                  aria-describedby={`assign-${point.id}-hint`}
                  onChange={() => setChosen(on ? chosen.filter((id) => id !== point.id) : [...chosen, point.id])}
                />
                <span className="space-y-0.5">
                  <span className="block text-sm font-medium">{point.name}</span>
                  <FieldHint id={`assign-${point.id}-hint`}>
                    {other ? t('devices.controlledBy', { name: other.name }) : t('spaces.points.to') + ' ' + tree.path(point.toSpaceId)}
                  </FieldHint>
                </span>
              </label>
            )
          })}
        </fieldset>
      </div>
      <SheetFooter>
        <Button type="button" variant="outline" onClick={onDone}>
          {t('spaces.cancel')}
        </Button>
        <Button type="submit" loading={save.isPending}>
          {save.isPending ? t('spaces.saving') : t('spaces.save')}
        </Button>
      </SheetFooter>
    </form>
  )
}
