import { DoorClosed, RefreshCw, Router, ShieldCheck, Wifi, WifiOff } from 'lucide-react'
import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import type { Site } from '@/components/site-picker/SitePicker'
import { Alert } from '@/components/ui/alert'
import { Badge } from '@/components/ui/badge'
import { Card } from '@/components/ui/card'
import { relativeTime } from '@/lib/relative-time'
import { formatSiteDateTime } from '@/lib/zoned-time'
import { certificateExpiresSoon, connection, type Device, policyState } from './model'

export function StatusBadge({ device }: { device: Device }) {
  const { t } = useTranslation()
  const variant = device.status === 'active' ? 'success' : device.status === 'pending' ? 'info' : 'destructive'
  return (
    <Badge variant={variant} className="shrink-0">
      {t(`devices.status.${device.status}`, { defaultValue: device.status })}
    </Badge>
  )
}

/** Detalle de un dispositivo: estado, conexión, política, firmware, certificado y puertas (decisión 2). */
export function DeviceDetail({
  site,
  device,
  pointName,
  pathOf,
  actions,
}: {
  site: Site
  device: Device
  pointName: (id: string) => string
  pathOf: (id: string) => string
  actions: ReactNode
}) {
  const { t } = useTranslation()
  const link = connection(device)
  const policy = policyState(device)
  const at = (iso: string) => formatSiteDateTime(iso, site.timeZone)

  return (
    <Card className="divide-y" aria-labelledby="device-detail-title">
      <div className="space-y-4 p-5">
        <div className="flex items-start gap-4">
          <span className="flex size-11 shrink-0 items-center justify-center rounded-lg bg-accent text-accent-foreground">
            <Router className="size-5" aria-hidden="true" />
          </span>
          <div className="min-w-0 flex-1 space-y-1.5">
            <h2 id="device-detail-title" className="text-lg font-semibold tracking-tight break-words">
              {device.name}
            </h2>
            <div className="flex flex-wrap gap-1.5">
              <StatusBadge device={device} />
              {link === 'online' && (
                <Badge variant="success">
                  <Wifi className="size-3" aria-hidden="true" />
                  {t('devices.connection.online')}
                </Badge>
              )}
              {link === 'offline' && (
                <Badge variant="warning">
                  <WifiOff className="size-3" aria-hidden="true" />
                  {t('devices.connection.offline')}
                </Badge>
              )}
              {link === 'never' && <Badge variant="secondary">{t('devices.connection.never')}</Badge>}
            </div>
          </div>
        </div>
        {actions && <div className="flex flex-wrap gap-2">{actions}</div>}
      </div>

      {device.status === 'pending' && device.claimExpiresAt && (
        <div className="p-5">
          <Alert variant="info">{t('devices.pendingClaim', { at: at(device.claimExpiresAt) })}</Alert>
        </div>
      )}
      {device.status === 'revoked' && (
        <div className="p-5">
          <Alert variant="destructive">{t('devices.revokedNotice')}</Alert>
        </div>
      )}

      <dl className="grid gap-x-6 gap-y-4 p-5 text-sm sm:grid-cols-2">
        <div>
          <dt className="text-muted-foreground">{t('devices.lastSeen')}</dt>
          <dd className="mt-0.5 font-medium" title={device.lastSeenAt ? at(device.lastSeenAt) : undefined}>
            {device.lastSeenAt ? relativeTime(device.lastSeenAt) : t('devices.never')}
          </dd>
        </div>
        <div>
          <dt className="text-muted-foreground">{t('devices.policy')}</dt>
          <dd className="mt-0.5 flex items-center gap-1.5 font-medium">
            {policy.kind === 'current' && (
              <>
                <ShieldCheck className="size-4 text-success" aria-hidden="true" />
                {t('devices.policyCurrent')}
              </>
            )}
            {policy.kind === 'pending' && (
              <>
                <RefreshCw className="size-4 text-warning" aria-hidden="true" />
                {t('devices.policyPending', { reported: policy.reported ?? '—', current: policy.current })}
              </>
            )}
            {policy.kind === 'none' && t('devices.policyNone')}
          </dd>
        </div>
        <div>
          <dt className="text-muted-foreground">{t('devices.firmware')}</dt>
          <dd className="mt-0.5 font-medium tabular-nums">{device.firmwareVersion ?? '—'}</dd>
        </div>
        <div>
          <dt className="text-muted-foreground">{t('devices.certificate')}</dt>
          <dd className="mt-0.5 font-medium tabular-nums">{device.certificateExpiresAt ? at(device.certificateExpiresAt) : '—'}</dd>
        </div>
      </dl>
      {certificateExpiresSoon(device) && (
        <div className="p-5">
          <Alert variant="warning">
            {t('devices.certificateSoon', { at: at(device.certificateExpiresAt!) })}
          </Alert>
        </div>
      )}

      <section className="space-y-3 p-5" aria-labelledby="device-points-title">
        <h3 id="device-points-title" className="flex items-center gap-2 text-sm font-semibold">
          <DoorClosed className="size-4 text-muted-foreground" aria-hidden="true" />
          {t('devices.points')}
        </h3>
        {device.accessPointIds.length === 0 ? (
          <p className="text-sm text-muted-foreground">{t('devices.pointsNone')}</p>
        ) : (
          <ul className="space-y-2" aria-label={t('devices.pointsLabel', { name: device.name })}>
            {device.accessPointIds.map((id) => (
              <li key={id} className="rounded-lg border px-3 py-2 text-sm">
                <span className="block font-medium">{pointName(id)}</span>
                <span className="block text-xs text-muted-foreground">
                  {t('spaces.points.to')} {pathOf(id)}
                </span>
              </li>
            ))}
          </ul>
        )}
      </section>
    </Card>
  )
}
