import { useMutation } from '@tanstack/react-query'
import { CircleCheck, CircleX, FlaskConical, Info } from 'lucide-react'
import { type FormEvent, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { api } from '@/api/client'
import { describeError, unwrap } from '@/api/errors'
import type { Site } from '@/components/site-picker/SitePicker'
import { Alert } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { FieldError, FieldHint, Input, Label, Select } from '@/components/ui/form'
import type { AccessPoint } from '@/features/spaces/model'
import { useSiteStructure } from '@/features/spaces/queries'
import { cn } from '@/lib/utils'
import { formatSiteDateTime, toSiteInstant } from '@/lib/zoned-time'
import { type CredentialType, credentialTypes, type SimulationResult } from './model'

type Tested = SimulationResult & { point: string; at: string | null }

/**
 * Prueba de acceso: ¿pasaría esta credencial por esta puerta en este momento? (design de add-access-screens,
 * decisión 7). El valor probado no se guarda: se limpia apenas responde la API.
 */
export function SimulationForm({ site, point: fixed }: { site: Site; point?: AccessPoint }) {
  const { t } = useTranslation()
  const { points, tree } = useSiteStructure(site.id, !fixed)
  const [pointId, setPointId] = useState(fixed?.id ?? '')
  const [type, setType] = useState<CredentialType>('card')
  const [value, setValue] = useState('')
  const [moment, setMoment] = useState<'now' | 'at'>('now')
  const [at, setAt] = useState('')
  const [checked, setChecked] = useState(false)
  const [result, setResult] = useState<Tested | null>(null)

  const test = useMutation({
    mutationFn: async (secret: string) =>
      unwrap(
        await api.POST('/api/v1/access-points/{id}/simulations', {
          params: { path: { id: pointId } },
          body: { credentialType: type, credentialValue: secret, at: moment === 'at' ? toSiteInstant(at, site.timeZone) : null },
        }),
      ),
    onSuccess: (data) => {
      setValue('')
      const name = fixed?.name ?? points.data?.find((p) => p.id === pointId)?.name ?? ''
      setResult({ ...data, point: name, at: moment === 'at' ? toSiteInstant(at, site.timeZone) : null })
    },
  })

  const pointError = checked && !pointId ? t('access.test.pointRequired') : undefined
  const valueError = checked && !value && !result ? t('access.credentials.valueRequired') : undefined
  const atError = checked && moment === 'at' && !at ? t('access.test.atRequired') : undefined
  const error = test.error ? describeError(test.error, t) : null

  function submit(event: FormEvent) {
    event.preventDefault()
    setChecked(true)
    if (!pointId || !value || (moment === 'at' && !at)) return
    setResult(null)
    test.mutate(value)
  }

  function again() {
    setResult(null)
    setChecked(false)
    test.reset()
  }

  return (
    <div className="space-y-5">
      <form aria-label={t('access.test.formLabel')} className="space-y-5" onSubmit={submit} noValidate autoComplete="off">
        {/* Desde una puerta, el panel ya lleva el título: no se repite. */}
        <div className={cn('flex items-start gap-3', fixed && 'sr-only')}>
          <span className="flex size-10 shrink-0 items-center justify-center rounded-lg bg-accent text-accent-foreground">
            <FlaskConical className="size-5" aria-hidden="true" />
          </span>
          <div>
            <h2 className="font-semibold">{t('access.test.title')}</h2>
            <p className="text-sm text-muted-foreground">{t('access.test.description')}</p>
          </div>
        </div>
        {error && <Alert variant="destructive">{error}</Alert>}
        {fixed ? (
          <p className="rounded-lg bg-muted/60 px-3 py-2 text-sm">
            {t('access.test.fixedPoint')} <span className="font-medium">{fixed.name}</span>
          </p>
        ) : (
          <div className="space-y-2">
            <Label htmlFor="test-point">{t('access.test.point')}</Label>
            <Select
              id="test-point"
              value={pointId}
              aria-invalid={!!pointError}
              aria-describedby={pointError ? 'test-point-error' : undefined}
              onChange={(e) => setPointId(e.target.value)}
            >
              <option value="" disabled>
                {t('access.test.choosePoint')}
              </option>
              {[...(points.data ?? [])]
                .sort((a, b) => a.name.localeCompare(b.name, 'es', { numeric: true }))
                .map((point) => (
                  <option key={point.id} value={point.id}>
                    {t('access.grants.targetPoint', { name: point.name, to: tree.path(point.toSpaceId) })}
                  </option>
                ))}
            </Select>
            <FieldError id="test-point-error" message={pointError} />
          </div>
        )}
        <div className="grid gap-4 sm:grid-cols-[10rem_1fr]">
          <div className="space-y-2">
            <Label htmlFor="test-type">{t('access.credentials.type')}</Label>
            <Select id="test-type" value={type} onChange={(e) => setType(e.target.value as CredentialType)}>
              {credentialTypes.map((value) => (
                <option key={value} value={value}>
                  {t(`access.credentials.types.${value}`)}
                </option>
              ))}
            </Select>
          </div>
          <div className="space-y-2">
            <Label htmlFor="test-value">{t(type === 'pin' ? 'access.credentials.pin' : 'access.credentials.value')}</Label>
            <Input
              id="test-value"
              type={type === 'pin' ? 'password' : 'text'}
              value={value}
              maxLength={64}
              autoComplete="off"
              spellCheck={false}
              aria-invalid={!!valueError}
              aria-describedby={valueError ? 'test-value-error' : undefined}
              onChange={(e) => setValue(e.target.value)}
            />
            <FieldError id="test-value-error" message={valueError} />
          </div>
        </div>
        <fieldset className="space-y-2">
          <legend className="mb-2 text-sm font-medium">{t('access.test.moment')}</legend>
          <div className="flex flex-wrap gap-4">
            {(['now', 'at'] as const).map((option) => (
              <label key={option} className="flex items-center gap-2 text-sm">
                <input type="radio" name="test-moment" checked={moment === option} onChange={() => setMoment(option)} className="size-4 accent-primary" />
                {t(`access.test.moments.${option}`)}
              </label>
            ))}
          </div>
          {moment === 'at' && (
            <div className="space-y-2 pt-1">
              <Label htmlFor="test-at">{t('access.test.at')}</Label>
              <Input
                id="test-at"
                type="datetime-local"
                className="w-60"
                value={at}
                aria-invalid={!!atError}
                aria-describedby={atError ? 'test-at-error test-at-hint' : 'test-at-hint'}
                onChange={(e) => setAt(e.target.value)}
              />
              <FieldHint id="test-at-hint">{t('access.test.atHint', { timeZone: site.timeZone })}</FieldHint>
              <FieldError id="test-at-error" message={atError} />
            </div>
          )}
        </fieldset>
        <div className="flex gap-2">
          <Button type="submit" loading={test.isPending}>
            {t('access.test.submit')}
          </Button>
          {result && (
            <Button type="button" variant="outline" onClick={again}>
              {t('access.test.again')}
            </Button>
          )}
        </div>
      </form>

      {result && <SimulationOutcome result={result} timeZone={site.timeZone} />}
      <p className="flex items-start gap-2 text-xs text-muted-foreground">
        <Info className="mt-0.5 size-3.5 shrink-0" aria-hidden="true" />
        {t('access.test.notAnEvent')}
      </p>
    </div>
  )
}

function SimulationOutcome({ result, timeZone }: { result: Tested; timeZone: string }) {
  const { t } = useTranslation()
  const allowed = result.decision === 'allow'
  const Icon = allowed ? CircleCheck : CircleX
  return (
    <div
      role="status"
      className={cn(
        'flex animate-rise items-start gap-4 rounded-xl border p-5',
        allowed ? 'border-success/40 bg-success/8' : 'border-destructive/40 bg-destructive/8',
      )}
    >
      <Icon className={cn('size-8 shrink-0', allowed ? 'text-success' : 'text-destructive')} aria-hidden="true" />
      <div className="space-y-1">
        <p className={cn('text-lg font-semibold', allowed ? 'text-success' : 'text-destructive')}>
          {allowed ? t('access.test.allowed') : t('access.test.denied')}
        </p>
        <p className="text-sm">{t(`access.test.reasons.${result.reason}`, { defaultValue: result.reason })}</p>
        <p className="text-xs text-muted-foreground">
          {result.at
            ? t('access.test.contextAt', { point: result.point, at: formatSiteDateTime(result.at, timeZone) })
            : t('access.test.contextNow', { point: result.point })}
        </p>
      </div>
    </div>
  )
}
