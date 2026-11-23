import { useMutation, useQueryClient } from '@tanstack/react-query'
import { DoorClosed, Pencil, Plus, Trash2, Zap, ZapOff } from 'lucide-react'
import { type FormEvent, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { api } from '@/api/client'
import { ApiError, describeError, unwrap } from '@/api/errors'
import type { Site } from '@/components/site-picker/SitePicker'
import { Alert } from '@/components/ui/alert'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { ConfirmDialog } from '@/components/ui/confirm-dialog'
import { EmptyState } from '@/components/ui/empty-state'
import { FieldError, Input, Label, Select } from '@/components/ui/form'
import { Sheet, SheetContent, SheetDescription, SheetFooter, SheetHeader, SheetTitle } from '@/components/ui/sheet'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { cn } from '@/lib/utils'
import { useSession } from '@/session/session'
import {
  type AccessPoint,
  accessPointIcons,
  type AccessPointType,
  accessPointTypes,
  canManageAccessPoint,
  canManageSpace,
  type FailMode,
  failModes,
  type SpaceTree,
} from './model'

type Editing = { mode: 'create' } | { mode: 'edit'; point: AccessPoint }

// En celular cada fila se muestra como tarjeta, con la etiqueta de cada dato delante (un solo marcado para los dos).
const cell = 'flex items-baseline justify-between gap-4 px-4 py-1.5 md:table-cell md:px-4 md:py-3 before:text-xs before:text-muted-foreground before:content-[attr(data-label)] md:before:content-none'

/** Pestaña de puertas del sitio (design de add-spaces-screens, decisiones 2, 5 y 6). */
export function AccessPointsTab({ site, tree, points }: { site: Site; tree: SpaceTree; points: AccessPoint[] }) {
  const { t } = useTranslation()
  const session = useSession()
  const organizationId = session.status === 'active' ? session.organizationId : null
  const queryClient = useQueryClient()
  const [editing, setEditing] = useState<Editing>({ mode: 'create' })
  const [open, setOpen] = useState(false)
  const [deleting, setDeleting] = useState<AccessPoint | null>(null)
  const canCreate = tree.ordered().some((space) => canManageSpace(session, space))
  const sorted = [...points].sort((a, b) => a.name.localeCompare(b.name, 'es', { numeric: true }))

  function edit(next: Editing) {
    setEditing(next)
    setOpen(true)
  }

  const remove = useMutation({
    mutationFn: async (point: AccessPoint) => unwrap(await api.DELETE('/api/v1/access-points/{id}', { params: { path: { id: point.id } } })),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['org', organizationId, 'sites', site.id] })
      setDeleting(null)
    },
  })

  return (
    <div className="space-y-4">
      {canCreate && sorted.length > 0 && (
        <div className="flex justify-end">
          <Button onClick={() => edit({ mode: 'create' })}>
            <Plus />
            {t('spaces.points.create')}
          </Button>
        </div>
      )}

      {sorted.length === 0 ? (
        <div className="rounded-2xl border border-dashed bg-card/50">
          <EmptyState
            icon={DoorClosed}
            title={t('spaces.points.empty')}
            action={
              canCreate && (
                <Button variant="outline" onClick={() => edit({ mode: 'create' })}>
                  <Plus />
                  {t('spaces.points.createFirst')}
                </Button>
              )
            }
          >
            {tree.roots.length === 0 ? t('spaces.points.emptyNoSpaces') : t('spaces.points.emptyDescription')}
          </EmptyState>
        </div>
      ) : (
        <Table aria-label={t('spaces.points.tableLabel', { site: site.name })} className="block md:table">
          <TableHeader className="hidden md:table-header-group">
            <TableRow>
              <TableHead>{t('spaces.points.name')}</TableHead>
              <TableHead>{t('spaces.points.type')}</TableHead>
              <TableHead>{t('spaces.points.to')}</TableHead>
              <TableHead>{t('spaces.points.from')}</TableHead>
              <TableHead>{t('spaces.points.withoutPower')}</TableHead>
              <TableHead>
                <span className="sr-only">{t('spaces.points.actions')}</span>
              </TableHead>
            </TableRow>
          </TableHeader>
          <TableBody className="block md:table-row-group">
            {sorted.map((point) => {
              const Icon = accessPointIcons[point.type] ?? accessPointIcons.door!
              const manageable = canManageAccessPoint(session, point, tree)
              return (
                <TableRow key={point.id} className="block py-2 md:table-row md:py-0">
                  <TableCell className="px-4 py-1.5 md:py-3">
                    <span className="flex items-center gap-2.5 font-medium">
                      <span className="flex size-8 shrink-0 items-center justify-center rounded-md bg-accent text-accent-foreground">
                        <Icon className="size-4" aria-hidden="true" />
                      </span>
                      {point.name}
                    </span>
                  </TableCell>
                  <TableCell className={cell} data-label={t('spaces.points.type')}>
                    {t(`spaces.points.types.${point.type}`, { defaultValue: point.type })}
                  </TableCell>
                  <TableCell className={cell} data-label={t('spaces.points.to')}>
                    {tree.path(point.toSpaceId)}
                  </TableCell>
                  <TableCell className={cell} data-label={t('spaces.points.from')}>
                    {point.fromSpaceId ? tree.path(point.fromSpaceId) : <span className="text-muted-foreground">{t('spaces.points.outside')}</span>}
                  </TableCell>
                  <TableCell className={cell} data-label={t('spaces.points.withoutPower')}>
                    <FailModeBadge mode={point.failMode} />
                  </TableCell>
                  <TableCell className="flex justify-end gap-1 px-4 py-1.5 md:table-cell md:py-3 md:text-right">
                    {manageable && (
                      <span className="inline-flex gap-1">
                        <Button
                          size="icon-sm"
                          variant="ghost"
                          aria-label={t('spaces.points.editNamed', { name: point.name })}
                          onClick={() => edit({ mode: 'edit', point })}
                        >
                          <Pencil />
                        </Button>
                        <Button
                          size="icon-sm"
                          variant="ghost"
                          className="text-destructive hover:bg-destructive/10 hover:text-destructive"
                          aria-label={t('spaces.points.deleteNamed', { name: point.name })}
                          onClick={() => {
                            remove.reset()
                            setDeleting(point)
                          }}
                        >
                          <Trash2 />
                        </Button>
                      </span>
                    )}
                  </TableCell>
                </TableRow>
              )
            })}
          </TableBody>
        </Table>
      )}

      <Sheet open={open} onOpenChange={setOpen}>
        <SheetContent closeLabel={t('spaces.close')}>
          <AccessPointForm
            key={editing.mode === 'edit' ? editing.point.id : 'new'}
            site={site}
            tree={tree}
            editing={editing}
            onDone={() => setOpen(false)}
          />
        </SheetContent>
      </Sheet>
      <ConfirmDialog
        open={deleting !== null}
        onOpenChange={(value) => !value && setDeleting(null)}
        title={t('spaces.points.deleteTitle', { name: deleting?.name ?? '' })}
        description={t('spaces.points.deleteDescription')}
        confirmLabel={t('spaces.delete')}
        cancelLabel={t('spaces.cancel')}
        pending={remove.isPending}
        error={remove.error ? describeError(remove.error, t) : null}
        onConfirm={() => deleting && remove.mutate(deleting)}
      />
    </div>
  )
}

function FailModeBadge({ mode }: { mode: string }) {
  const { t } = useTranslation()
  return mode === 'fail_safe' ? (
    <Badge variant="warning">
      <ZapOff className="size-3" aria-hidden="true" />
      {t('spaces.points.failModesShort.fail_safe')}
    </Badge>
  ) : (
    <Badge variant="secondary">
      <Zap className="size-3" aria-hidden="true" />
      {t('spaces.points.failModesShort.fail_secure')}
    </Badge>
  )
}

function AccessPointForm({ site, tree, editing, onDone }: { site: Site; tree: SpaceTree; editing: Editing; onDone: () => void }) {
  const { t } = useTranslation()
  const session = useSession()
  const organizationId = session.status === 'active' ? session.organizationId : null
  const queryClient = useQueryClient()
  const existing = editing.mode === 'edit' ? editing.point : null
  const ordered = tree.ordered()
  // Destino: solo espacios que el usuario puede manejar; origen: cualquiera del sitio o afuera (spec access-points).
  const targets = ordered.filter((space) => canManageSpace(session, space))
  const [name, setName] = useState(existing?.name ?? '')
  const [type, setType] = useState<AccessPointType>((existing?.type as AccessPointType | undefined) ?? 'door')
  const [toSpaceId, setToSpaceId] = useState(existing?.toSpaceId ?? '')
  const [fromSpaceId, setFromSpaceId] = useState(existing?.fromSpaceId ?? '')
  const [failMode, setFailMode] = useState<FailMode | ''>((existing?.failMode as FailMode | undefined) ?? '')
  const [missing, setMissing] = useState<Record<string, boolean>>({})

  const save = useMutation({
    mutationFn: async () => {
      const body = { name: name.trim(), type, toSpaceId, fromSpaceId: fromSpaceId || null, failMode: failMode as FailMode }
      return existing
        ? unwrap(await api.PUT('/api/v1/access-points/{id}', { params: { path: { id: existing.id } }, body }))
        : unwrap(await api.POST('/api/v1/sites/{siteId}/access-points', { params: { path: { siteId: site.id } }, body }))
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['org', organizationId, 'sites', site.id] })
      onDone()
    },
  })

  const error = save.error instanceof ApiError ? save.error : null
  const nameError = missing.name ? t('spaces.nameRequired') : error?.fieldError('name')
  const toError = missing.to ? t('spaces.points.toRequired') : error?.fieldError('toSpaceId')
  const fromError = error?.fieldError('fromSpaceId')
  const failModeError = missing.failMode ? t('spaces.points.failModeRequired') : error?.fieldError('failMode')
  const general = save.error && !nameError && !toError && !fromError && !failModeError ? describeError(save.error, t) : null

  function submit(event: FormEvent) {
    event.preventDefault()
    const next = { name: !name.trim(), to: !toSpaceId, failMode: !failMode }
    setMissing(next)
    if (!Object.values(next).some(Boolean)) {
      save.mutate()
    }
  }

  const title = existing ? t('spaces.points.editTitle') : t('spaces.points.createTitle')
  return (
    <form aria-label={title} className="flex min-h-0 flex-1 flex-col" onSubmit={submit} noValidate>
      <SheetHeader>
        <SheetTitle>{title}</SheetTitle>
        <SheetDescription>{t('spaces.points.formDescription')}</SheetDescription>
      </SheetHeader>
      <div className="flex-1 space-y-5 overflow-y-auto px-6 py-5">
        {general && <Alert variant="destructive">{general}</Alert>}
        <div className="space-y-2">
          <Label htmlFor="point-name">{t('spaces.points.name')}</Label>
          <Input
            id="point-name"
            value={name}
            maxLength={200}
            placeholder={t('spaces.points.namePlaceholder')}
            aria-invalid={!!nameError}
            aria-describedby={nameError ? 'point-name-error' : undefined}
            onChange={(e) => setName(e.target.value)}
          />
          <FieldError id="point-name-error" message={nameError} />
        </div>
        <div className="space-y-2">
          <Label htmlFor="point-type">{t('spaces.points.type')}</Label>
          <Select id="point-type" value={type} onChange={(e) => setType(e.target.value as AccessPointType)}>
            {accessPointTypes.map((value) => (
              <option key={value} value={value}>
                {t(`spaces.points.types.${value}`)}
              </option>
            ))}
          </Select>
        </div>
        <div className="space-y-2">
          <Label htmlFor="point-to">{t('spaces.points.to')}</Label>
          <Select
            id="point-to"
            value={toSpaceId}
            aria-invalid={!!toError}
            aria-describedby={toError ? 'point-to-error' : undefined}
            onChange={(e) => setToSpaceId(e.target.value)}
          >
            <option value="" disabled>
              {t('spaces.points.choose')}
            </option>
            {targets.map((space) => (
              <option key={space.id} value={space.id}>
                {tree.path(space.id)}
              </option>
            ))}
          </Select>
          <FieldError id="point-to-error" message={toError} />
        </div>
        <div className="space-y-2">
          <Label htmlFor="point-from">{t('spaces.points.from')}</Label>
          <Select
            id="point-from"
            value={fromSpaceId}
            aria-invalid={!!fromError}
            aria-describedby={fromError ? 'point-from-error' : undefined}
            onChange={(e) => setFromSpaceId(e.target.value)}
          >
            <option value="">{t('spaces.points.outside')}</option>
            {ordered
              .filter((space) => space.id !== toSpaceId)
              .map((space) => (
                <option key={space.id} value={space.id}>
                  {tree.path(space.id)}
                </option>
              ))}
          </Select>
          <FieldError id="point-from-error" message={fromError} />
        </div>
        <fieldset className="space-y-2" aria-describedby={failModeError ? 'point-fail-mode-error' : undefined}>
          <legend className="mb-2 text-sm font-medium">{t('spaces.points.failMode')}</legend>
          {failModes.map((mode) => (
            <label
              key={mode}
              className={cn(
                'flex cursor-pointer items-start gap-3 rounded-lg border p-3 transition-colors has-[:checked]:border-primary has-[:checked]:bg-primary/5',
                failModeError && 'border-destructive/50',
              )}
            >
              <input
                type="radio"
                name="point-fail-mode"
                value={mode}
                checked={failMode === mode}
                onChange={() => setFailMode(mode)}
                className="mt-0.5 size-4 shrink-0 accent-primary"
              />
              <span className="space-y-0.5">
                <span className="block text-sm font-medium">{t(`spaces.points.failModes.${mode}`)}</span>
                <span className="block text-xs text-muted-foreground">{t(`spaces.points.failModeHints.${mode}`)}</span>
              </span>
            </label>
          ))}
          <FieldError id="point-fail-mode-error" message={failModeError} />
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
