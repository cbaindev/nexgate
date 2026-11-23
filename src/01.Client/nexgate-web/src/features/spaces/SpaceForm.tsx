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
import { childSpaceTypes, type Space, spacesKey, type SpaceTree, type SpaceType } from './model'

/** Tipo sugerido para un hijo según el padre (edificio → piso → unidad); se puede cambiar en el formulario. */
const childTypeByParent: Record<string, SpaceType> = { site: 'building', building: 'floor', floor: 'unit', parking: 'zone', lot: 'zone' }

function suggestedChildType(parentType: string): SpaceType {
  return childTypeByParent[parentType] ?? 'zone'
}

export type SpaceEditing ={ mode: 'root' } | { mode: 'child'; parent: Space } | { mode: 'edit'; space: Space }

/**
 * Alta y edición de un espacio (design de add-spaces-screens, decisión 6). El padre no se cambia; la raíz es siempre
 * de tipo sitio. Los errores del servidor quedan en el formulario sin perder lo cargado.
 */
export function SpaceForm({
  site,
  tree,
  editing,
  onDone,
}: {
  site: Site
  tree: SpaceTree
  editing: SpaceEditing
  onDone: (saved: Space | null) => void
}) {
  const { t } = useTranslation()
  const session = useSession()
  const organizationId = session.status === 'active' ? session.organizationId : null
  const queryClient = useQueryClient()
  const existing = editing.mode === 'edit' ? editing.space : null
  const isRoot = editing.mode === 'root' || existing?.parentId === null
  const [name, setName] = useState(existing?.name ?? (editing.mode === 'root' ? site.name : ''))
  const [type, setType] = useState<SpaceType>(
    (existing?.type as SpaceType | undefined) ?? (isRoot ? 'site' : suggestedChildType(editing.mode === 'child' ? editing.parent.type : 'site')),
  )
  const [isCommon, setIsCommon] = useState(existing?.isCommon ?? false)
  const [nameRequired, setNameRequired] = useState(false)

  const save = useMutation({
    mutationFn: async () =>
      existing
        ? unwrap(await api.PUT('/api/v1/spaces/{id}', { params: { path: { id: existing.id } }, body: { name: name.trim(), type, isCommon } }))
        : unwrap(
            await api.POST('/api/v1/sites/{siteId}/spaces', {
              params: { path: { siteId: site.id } },
              body: { name: name.trim(), type, isCommon, parentId: editing.mode === 'child' ? editing.parent.id : null },
            }),
          ),
    onSuccess: async (saved) => {
      await queryClient.invalidateQueries({ queryKey: spacesKey(organizationId, site.id) })
      onDone(saved)
    },
  })

  const error = save.error instanceof ApiError ? save.error : null
  const nameError = nameRequired ? t('spaces.nameRequired') : error?.fieldError('name')
  const typeError = error?.fieldError('type')
  const general = save.error && !nameError && !typeError ? describeError(save.error, t) : null

  function submit(event: FormEvent) {
    event.preventDefault()
    if (!name.trim()) {
      setNameRequired(true)
      return
    }

    setNameRequired(false)
    save.mutate()
  }

  const title = existing ? t('spaces.editTitle') : isRoot ? t('spaces.createRootTitle') : t('spaces.createChildTitle')
  const location = editing.mode === 'child' ? tree.path(editing.parent.id) : existing?.parentId ? tree.path(existing.parentId) : null
  return (
    <form aria-label={title} className="flex min-h-0 flex-1 flex-col" onSubmit={submit} noValidate>
      <SheetHeader>
        <SheetTitle>{title}</SheetTitle>
        <SheetDescription>
          {location ? t('spaces.formInside', { parent: location }) : t('spaces.formRootDescription', { site: site.name })}
        </SheetDescription>
      </SheetHeader>
      <div className="flex-1 space-y-5 overflow-y-auto px-6 py-5">
        {general && <Alert variant="destructive">{general}</Alert>}
        <div className="space-y-2">
          <Label htmlFor="space-name">{t('spaces.name')}</Label>
          <Input
            id="space-name"
            value={name}
            maxLength={200}
            placeholder={t('spaces.namePlaceholder')}
            aria-invalid={!!nameError}
            aria-describedby={nameError ? 'space-name-error' : undefined}
            onChange={(e) => setName(e.target.value)}
          />
          <FieldError id="space-name-error" message={nameError} />
        </div>
        <div className="space-y-2">
          <Label htmlFor="space-type">{t('spaces.type')}</Label>
          {isRoot ? (
            <>
              <Input id="space-type" value={t('spaces.types.site')} readOnly aria-describedby="space-type-hint" />
              <FieldHint id="space-type-hint">{t('spaces.rootTypeHint')}</FieldHint>
            </>
          ) : (
            <Select id="space-type" value={type} aria-invalid={!!typeError} onChange={(e) => setType(e.target.value as SpaceType)}>
              {childSpaceTypes.map((value) => (
                <option key={value} value={value}>
                  {t(`spaces.types.${value}`)}
                </option>
              ))}
            </Select>
          )}
          <FieldError id="space-type-error" message={typeError} />
        </div>
        <div className="flex items-start gap-3 rounded-lg border p-3">
          <input
            id="space-common"
            type="checkbox"
            checked={isCommon}
            onChange={(e) => setIsCommon(e.target.checked)}
            aria-describedby="space-common-hint"
            className="mt-0.5 size-4 shrink-0 accent-primary"
          />
          <div className="space-y-0.5">
            <Label htmlFor="space-common">{t('spaces.isCommon')}</Label>
            <FieldHint id="space-common-hint">{t('spaces.isCommonHint')}</FieldHint>
          </div>
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
