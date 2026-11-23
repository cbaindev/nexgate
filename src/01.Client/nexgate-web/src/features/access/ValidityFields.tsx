import { useTranslation } from 'react-i18next'
import { FieldError, FieldHint, Input, Label } from '@/components/ui/form'
import { toSiteInstant, toSiteLocal } from '@/lib/zoned-time'

export interface ValidityValue {
  /** Hora del sitio (`YYYY-MM-DDTHH:mm`) o vacío. */
  from: string
  until: string
}

export const validityFrom = (item: { validFrom: string | null; validUntil: string | null } | null, timeZone: string): ValidityValue => ({
  from: item?.validFrom ? toSiteLocal(item.validFrom, timeZone) : '',
  until: item?.validUntil ? toSiteLocal(item.validUntil, timeZone) : '',
})

/** Instantes para la API, desde la hora del sitio (design de add-access-screens, decisión 3). */
export const validityToApi = (value: ValidityValue, timeZone: string) => ({
  validFrom: value.from ? toSiteInstant(value.from, timeZone) : null,
  validUntil: value.until ? toSiteInstant(value.until, timeZone) : null,
})

/** Mensaje si la vigencia termina antes de empezar (las fechas en el mismo formato se comparan como texto). */
export const validityError = (value: ValidityValue) => (value.from && value.until && value.until <= value.from ? 'access.validity.endBeforeStart' : null)

/** Vigencia opcional "desde" y "hasta", en la hora del sitio. */
export function ValidityFields({
  idPrefix,
  value,
  onChange,
  timeZone,
  untilError,
}: {
  idPrefix: string
  value: ValidityValue
  onChange: (value: ValidityValue) => void
  timeZone: string
  untilError?: string
}) {
  const { t } = useTranslation()
  return (
    <fieldset className="space-y-3">
      <legend className="text-sm font-medium">{t('access.validity.label')}</legend>
      <FieldHint id={`${idPrefix}-zone`}>{t('access.validity.hint', { timeZone })}</FieldHint>
      <div className="grid gap-3 sm:grid-cols-2">
        <div className="space-y-2">
          <Label htmlFor={`${idPrefix}-from`}>{t('access.validity.from')}</Label>
          <Input
            id={`${idPrefix}-from`}
            type="datetime-local"
            value={value.from}
            aria-describedby={`${idPrefix}-zone`}
            onChange={(e) => onChange({ ...value, from: e.target.value })}
          />
        </div>
        <div className="space-y-2">
          <Label htmlFor={`${idPrefix}-until`}>{t('access.validity.untilLabel')}</Label>
          <Input
            id={`${idPrefix}-until`}
            type="datetime-local"
            value={value.until}
            aria-invalid={!!untilError}
            aria-describedby={untilError ? `${idPrefix}-until-error ${idPrefix}-zone` : `${idPrefix}-zone`}
            onChange={(e) => onChange({ ...value, until: e.target.value })}
          />
          <FieldError id={`${idPrefix}-until-error`} message={untilError} />
        </div>
      </div>
    </fieldset>
  )
}
