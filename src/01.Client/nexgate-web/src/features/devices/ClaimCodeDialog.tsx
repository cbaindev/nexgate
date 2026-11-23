import * as DialogPrimitive from '@radix-ui/react-dialog'
import { Check, Copy, KeyRound, TriangleAlert } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button } from '@/components/ui/button'
import { QrCode } from '@/components/ui/qr-code'
import { formatSiteDateTime } from '@/lib/zoned-time'

export interface ShownClaim {
  deviceName: string
  claimCode: string
  claimExpiresAt: string
}

/**
 * El código de reclamo, que la API devuelve una sola vez: texto, QR, vencimiento y copiar (design de
 * add-device-and-event-screens, decisión 4). Vive solo en el estado de quien lo abre y se descarta al cerrar.
 */
export function ClaimCodeDialog({ claim, timeZone, onClose }: { claim: ShownClaim | null; timeZone: string; onClose: () => void }) {
  const { t } = useTranslation()
  const [copied, setCopied] = useState(false)

  async function copy() {
    if (!claim) return
    try {
      await navigator.clipboard.writeText(claim.claimCode)
      setCopied(true)
      setTimeout(() => setCopied(false), 2500)
    } catch {
      // Sin permiso para el portapapeles: el código sigue a la vista para copiarlo a mano.
    }
  }

  return (
    <DialogPrimitive.Root
      open={claim !== null}
      onOpenChange={(open) => {
        if (!open) {
          setCopied(false)
          onClose()
        }
      }}
    >
      <DialogPrimitive.Portal>
        <DialogPrimitive.Overlay className="fixed inset-0 z-50 bg-foreground/25 backdrop-blur-[2px] data-[state=closed]:animate-fade-out data-[state=open]:animate-fade-in dark:bg-black/60" />
        <DialogPrimitive.Content className="fixed top-1/2 left-1/2 z-50 max-h-[calc(100%-2rem)] w-[calc(100%-2rem)] max-w-md -translate-x-1/2 -translate-y-1/2 overflow-y-auto rounded-xl border bg-card p-6 text-card-foreground shadow-lg outline-none data-[state=closed]:animate-exit data-[state=open]:animate-enter">
          {claim && (
            <div className="space-y-5">
              <div className="flex items-start gap-3">
                <span className="flex size-10 shrink-0 items-center justify-center rounded-lg bg-accent text-accent-foreground">
                  <KeyRound className="size-5" aria-hidden="true" />
                </span>
                <div>
                  <DialogPrimitive.Title className="text-lg font-semibold tracking-tight">
                    {t('devices.claim.title', { name: claim.deviceName })}
                  </DialogPrimitive.Title>
                  <DialogPrimitive.Description className="text-sm text-muted-foreground">{t('devices.claim.description')}</DialogPrimitive.Description>
                </div>
              </div>
              <div className="flex flex-col items-center gap-4 rounded-lg bg-muted/50 p-4">
                <QrCode value={claim.claimCode} label={t('devices.claim.qrLabel')} className="size-44" />
                <p className="font-mono text-xl font-semibold tracking-[0.15em] break-all select-all" aria-label={t('devices.claim.codeLabel')}>
                  {claim.claimCode}
                </p>
                <p className="text-sm text-muted-foreground">
                  {t('devices.claim.expires', { at: formatSiteDateTime(claim.claimExpiresAt, timeZone) })}
                </p>
                <Button variant="outline" onClick={() => void copy()}>
                  {copied ? <Check /> : <Copy />}
                  {copied ? t('devices.claim.copied') : t('devices.claim.copy')}
                </Button>
                <span role="status" className="sr-only">
                  {copied ? t('devices.claim.copied') : ''}
                </span>
              </div>
              <p className="flex items-start gap-2 text-sm text-warning">
                <TriangleAlert className="mt-0.5 size-4 shrink-0" aria-hidden="true" />
                {t('devices.claim.once')}
              </p>
              <div className="flex justify-end">
                <DialogPrimitive.Close asChild>
                  <Button>{t('devices.claim.done')}</Button>
                </DialogPrimitive.Close>
              </div>
            </div>
          )}
        </DialogPrimitive.Content>
      </DialogPrimitive.Portal>
    </DialogPrimitive.Root>
  )
}
