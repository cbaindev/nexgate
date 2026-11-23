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
import { useSession } from '@/session/session'
import { type Credential, credentialsKey, type CredentialType, credentialTypes, type Subject } from './model'
import { ValidityFields, validityError, validityFrom, validityToApi, type ValidityValue } from './ValidityFields'

/**
 * Alta de una credencial (tipo, valor y vigencia) o cambio de su vigencia. El valor se escribe una sola vez: vive solo
 * en el estado de este formulario, que se desmonta al guardar (design de add-access-screens, decisión 4).
 */
export function CredentialForm({
  site,
  subject,
  existing,
  onDone,
}: {
  site: Site
  subject: Subject
  existing: Credential | null
  onDone: () => void
}) {
  const { t } = useTranslation()
  const session = useSession()
  const organizationId = session.status === 'active' ? session.organizationId : null
  const queryClient = useQueryClient()
  const [type, setType] = useState<CredentialType>('card')
  const [value, setValue] = useState('')
  const [validity, setValidity] = useState<ValidityValue>(validityFrom(existing, site.timeZone))
  const [valueRequired, setValueRequired] = useState(false)
  const [checked, setChecked] = useState(false)

  const save = useMutation({
    // El valor llega como variable de la mutación: no forma parte de ninguna clave de consulta.
    mutationFn: async (secret: string) => {
      const dates = validityToApi(validity, site.timeZone)
      return existing
        ? unwrap(await api.PUT('/api/v1/credentials/{id}', { params: { path: { id: existing.id } }, body: dates }))
        : unwrap(
            await api.POST('/api/v1/sites/{siteId}/credentials', {
              params: { path: { siteId: site.id } },
              body: { subjectId: subject.id, type, value: secret, ...dates },
            }),
          )
    },
    onSuccess: async () => {
      setValue('')
      await queryClient.invalidateQueries({ queryKey: credentialsKey(organizationId, site.id, subject.id) })
      onDone()
    },
  })

  const error = save.error instanceof ApiError ? save.error : null
  const valueError = valueRequired ? t('access.credentials.valueRequired') : error?.fieldError('value')
  const untilKey = checked ? validityError(validity) : null
  const untilError = untilKey ? t(untilKey) : (error?.fieldError('validUntil') ?? undefined)
  const general = save.error && !valueError && !untilError ? describeError(save.error, t) : null

  function submit(event: FormEvent) {
    event.preventDefault()
    setChecked(true)
    const missing = !existing && !value
    setValueRequired(missing)
    if (missing || validityError(validity)) return
    save.mutate(value)
  }

  const title = existing ? t('access.credentials.editTitle') : t('access.credentials.createTitle')
  return (
    <form aria-label={title} className="flex min-h-0 flex-1 flex-col" onSubmit={submit} noValidate autoComplete="off">
      <SheetHeader>
        <SheetTitle>{title}</SheetTitle>
        <SheetDescription>{t('access.credentials.formDescription', { name: subject.alias })}</SheetDescription>
      </SheetHeader>
      <div className="flex-1 space-y-5 overflow-y-auto px-6 py-5">
        {general && <Alert variant="destructive">{general}</Alert>}
        {!existing && (
          <>
            <div className="space-y-2">
              <Label htmlFor="credential-type">{t('access.credentials.type')}</Label>
              <Select id="credential-type" value={type} onChange={(e) => setType(e.target.value as CredentialType)}>
                {credentialTypes.map((value) => (
                  <option key={value} value={value}>
                    {t(`access.credentials.types.${value}`)}
                  </option>
                ))}
              </Select>
            </div>
            <div className="space-y-2">
              <Label htmlFor="credential-value">{t(type === 'pin' ? 'access.credentials.pin' : 'access.credentials.value')}</Label>
              <Input
                id="credential-value"
                type={type === 'pin' ? 'password' : 'text'}
                inputMode={type === 'pin' ? 'numeric' : undefined}
                value={value}
                maxLength={64}
                autoComplete="off"
                spellCheck={false}
                aria-invalid={!!valueError}
                aria-describedby={valueError ? 'credential-value-error credential-value-hint' : 'credential-value-hint'}
                onChange={(e) => setValue(e.target.value)}
              />
              <FieldHint id="credential-value-hint">{t('access.credentials.valueHint')}</FieldHint>
              <FieldError id="credential-value-error" message={valueError} />
            </div>
          </>
        )}
        <ValidityFields idPrefix="credential" value={validity} onChange={setValidity} timeZone={site.timeZone} untilError={untilError} />
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
