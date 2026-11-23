import { useMutation, useQueryClient } from '@tanstack/react-query'
import { type FormEvent, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { api } from '@/api/client'
import { ApiError, describeError, unwrap } from '@/api/errors'
import type { Site } from '@/components/site-picker/SitePicker'
import { Alert } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { FieldError, FieldHint, Input, Label, Select } from '@/components/ui/form'
import { SheetDescription, SheetFooter, SheetHeader, SheetTitle } from '@/components/ui/sheet'
import { useSiteStructure } from '@/features/spaces/queries'
import { cn } from '@/lib/utils'
import { useSession } from '@/session/session'
import { grantsKey, type Schedule, type Subject } from './model'
import { ValidityFields, validityError, validityToApi, type ValidityValue } from './ValidityFields'

type Effect = 'allow' | 'deny'

/**
 * Alta de un permiso (otorgamiento): efecto, destino (espacio o puerta), horario, vigencia y máximo de usos si permite.
 * La API no los modifica: se elimina y se crea otro (design de add-access-screens, decisión 5).
 */
export function GrantForm({ site, subject, schedules, onDone }: { site: Site; subject: Subject; schedules: Schedule[]; onDone: () => void }) {
  const { t } = useTranslation()
  const session = useSession()
  const organizationId = session.status === 'active' ? session.organizationId : null
  const queryClient = useQueryClient()
  const { points, tree } = useSiteStructure(site.id)
  const [effect, setEffect] = useState<Effect>('allow')
  const [target, setTarget] = useState('')
  const [scheduleId, setScheduleId] = useState('')
  const [validity, setValidity] = useState<ValidityValue>({ from: '', until: '' })
  const [maxUses, setMaxUses] = useState('')
  const [checked, setChecked] = useState(false)

  const save = useMutation({
    mutationFn: async () => {
      const [targetType, targetId] = target.split(':') as ['space' | 'access_point', string]
      return unwrap(
        await api.POST('/api/v1/sites/{siteId}/grants', {
          params: { path: { siteId: site.id } },
          body: {
            subjectId: subject.id,
            targetType,
            targetId,
            scheduleId: scheduleId || null,
            effect,
            maxUses: effect === 'allow' && maxUses ? Number(maxUses) : null,
            ...validityToApi(validity, site.timeZone),
          },
        }),
      )
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: grantsKey(organizationId, site.id, subject.id) })
      onDone()
    },
  })

  const error = save.error instanceof ApiError ? save.error : null
  const targetError = checked && !target ? t('access.grants.targetRequired') : (error?.fieldError('targetId') ?? undefined)
  const usesInvalid = effect === 'allow' && maxUses !== '' && !(Number.isInteger(Number(maxUses)) && Number(maxUses) >= 1)
  const usesError = checked && usesInvalid ? t('access.grants.usesInvalid') : (error?.fieldError('maxUses') ?? undefined)
  const untilKey = checked ? validityError(validity) : null
  const untilError = untilKey ? t(untilKey) : (error?.fieldError('validUntil') ?? undefined)
  const general = save.error && !targetError && !usesError && !untilError ? describeError(save.error, t) : null

  function submit(event: FormEvent) {
    event.preventDefault()
    setChecked(true)
    if (!target || usesInvalid || validityError(validity)) return
    save.mutate()
  }

  const title = t('access.grants.createTitle')
  return (
    <form aria-label={title} className="flex min-h-0 flex-1 flex-col" onSubmit={submit} noValidate>
      <SheetHeader>
        <SheetTitle>{title}</SheetTitle>
        <SheetDescription>{t('access.grants.formDescription', { name: subject.alias })}</SheetDescription>
      </SheetHeader>
      <div className="flex-1 space-y-5 overflow-y-auto px-6 py-5">
        {general && <Alert variant="destructive">{general}</Alert>}
        <fieldset className="space-y-2">
          <legend className="mb-2 text-sm font-medium">{t('access.grants.effect')}</legend>
          {(['allow', 'deny'] as const).map((value) => (
            <label
              key={value}
              className={cn(
                'flex cursor-pointer items-start gap-3 rounded-lg border p-3 transition-colors',
                value === 'allow' ? 'has-[:checked]:border-success has-[:checked]:bg-success/5' : 'has-[:checked]:border-destructive has-[:checked]:bg-destructive/5',
              )}
            >
              <input
                type="radio"
                name="grant-effect"
                value={value}
                checked={effect === value}
                onChange={() => setEffect(value)}
                className="mt-0.5 size-4 shrink-0 accent-primary"
              />
              <span className="space-y-0.5">
                <span className="block text-sm font-medium">{t(`access.grants.effectOptions.${value}`)}</span>
                <span className="block text-xs text-muted-foreground">{t(`access.grants.effectHints.${value}`)}</span>
              </span>
            </label>
          ))}
        </fieldset>
        <div className="space-y-2">
          <Label htmlFor="grant-target">{t('access.grants.target')}</Label>
          <Select
            id="grant-target"
            value={target}
            aria-invalid={!!targetError}
            aria-describedby={targetError ? 'grant-target-error grant-target-hint' : 'grant-target-hint'}
            onChange={(e) => setTarget(e.target.value)}
          >
            <option value="" disabled>
              {t('access.grants.chooseTarget')}
            </option>
            <optgroup label={t('access.grants.spaces')}>
              {tree.ordered().map((space) => (
                <option key={space.id} value={`space:${space.id}`}>
                  {tree.path(space.id)}
                </option>
              ))}
            </optgroup>
            <optgroup label={t('access.grants.points')}>
              {[...(points.data ?? [])]
                .sort((a, b) => a.name.localeCompare(b.name, 'es', { numeric: true }))
                .map((point) => (
                  <option key={point.id} value={`access_point:${point.id}`}>
                    {t('access.grants.targetPoint', { name: point.name, to: tree.path(point.toSpaceId) })}
                  </option>
                ))}
            </optgroup>
          </Select>
          <FieldHint id="grant-target-hint">{t('access.grants.targetHint')}</FieldHint>
          <FieldError id="grant-target-error" message={targetError} />
        </div>
        <div className="space-y-2">
          <Label htmlFor="grant-schedule">{t('access.grants.schedule')}</Label>
          <Select id="grant-schedule" value={scheduleId} onChange={(e) => setScheduleId(e.target.value)}>
            <option value="">{t('access.grants.always')}</option>
            {schedules.map((schedule) => (
              <option key={schedule.id} value={schedule.id}>
                {schedule.name}
              </option>
            ))}
          </Select>
        </div>
        <ValidityFields idPrefix="grant" value={validity} onChange={setValidity} timeZone={site.timeZone} untilError={untilError} />
        {effect === 'allow' && (
          <div className="space-y-2">
            <Label htmlFor="grant-uses">{t('access.grants.maxUses')}</Label>
            <Input
              id="grant-uses"
              type="number"
              min={1}
              inputMode="numeric"
              value={maxUses}
              className="w-32"
              aria-invalid={!!usesError}
              aria-describedby={usesError ? 'grant-uses-error grant-uses-hint' : 'grant-uses-hint'}
              onChange={(e) => setMaxUses(e.target.value)}
            />
            <FieldHint id="grant-uses-hint">{t('access.grants.maxUsesHint')}</FieldHint>
            <FieldError id="grant-uses-error" message={usesError} />
          </div>
        )}
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
