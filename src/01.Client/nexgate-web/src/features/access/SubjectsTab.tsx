import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Pencil, Plus, Search, Trash2, Users } from 'lucide-react'
import { type FormEvent, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useSearchParams } from 'react-router'
import { api } from '@/api/client'
import { ApiError, describeError, unwrap } from '@/api/errors'
import { ErrorNotice } from '@/app/ErrorNotice'
import type { Site } from '@/components/site-picker/SitePicker'
import { Alert } from '@/components/ui/alert'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { ConfirmDialog } from '@/components/ui/confirm-dialog'
import { EmptyState } from '@/components/ui/empty-state'
import { FieldError, FieldHint, Input, Label } from '@/components/ui/form'
import { Sheet, SheetContent, SheetDescription, SheetFooter, SheetHeader, SheetTitle } from '@/components/ui/sheet'
import { Skeleton } from '@/components/ui/skeleton'
import { cn } from '@/lib/utils'
import { normalize } from '@/features/spaces/model'
import { hasPermissionOnSite, useSession } from '@/session/session'
import { type Subject, subjectIcons, type SubjectKind, subjectKinds, subjectsKey } from './model'
import { SubjectDetail } from './SubjectDetail'

const subjectParam = 'sujeto'

/** Personas del sitio: lista con filtro a la izquierda y detalle a la derecha (design de add-access-screens, decisión 1). */
export function SubjectsTab({ site }: { site: Site }) {
  const { t } = useTranslation()
  const session = useSession()
  const organizationId = session.status === 'active' ? session.organizationId : null
  const queryClient = useQueryClient()
  const [params, setParams] = useSearchParams()
  const [query, setQuery] = useState('')
  const [editing, setEditing] = useState<Subject | null>(null)
  const [formOpen, setFormOpen] = useState(false)
  const [deleting, setDeleting] = useState<Subject | null>(null)
  const canManage = hasPermissionOnSite(session, 'access.manage', site.id)
  const subjects = useQuery({
    queryKey: subjectsKey(organizationId, site.id),
    queryFn: async () => unwrap(await api.GET('/api/v1/sites/{siteId}/subjects', { params: { path: { siteId: site.id } } })),
  })
  const selectedId = params.get(subjectParam)
  const selected = subjects.data?.find((s) => s.id === selectedId) ?? null

  function select(id: string | null) {
    setParams((current) => {
      const next = new URLSearchParams(current)
      if (id) next.set(subjectParam, id)
      else next.delete(subjectParam)
      return next
    })
    if (id && typeof window !== 'undefined' && window.innerWidth < 768) {
      requestAnimationFrame(() => document.getElementById('subject-detail')?.scrollIntoView?.({ behavior: 'smooth', block: 'start' }))
    }
  }

  function edit(subject: Subject | null) {
    setEditing(subject)
    setFormOpen(true)
  }

  const remove = useMutation({
    mutationFn: async (subject: Subject) => unwrap(await api.DELETE('/api/v1/subjects/{id}', { params: { path: { id: subject.id } } })),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: subjectsKey(organizationId, site.id) })
      select(null)
      setDeleting(null)
    },
  })

  if (subjects.isPending) {
    return (
      <div aria-busy="true" className="space-y-3">
        <span role="status" className="sr-only">
          {t('app.loading')}
        </span>
        {[0, 1, 2].map((i) => (
          <Skeleton key={i} className="h-12 max-w-sm" />
        ))}
      </div>
    )
  }

  if (subjects.isError) {
    return <ErrorNotice error={subjects.error} onRetry={() => void subjects.refetch()} />
  }

  const wanted = normalize(query.trim())
  const visible = subjects.data.filter((s) => !wanted || normalize(s.alias).includes(wanted))
  const form = (
    <Sheet open={formOpen} onOpenChange={setFormOpen}>
      <SheetContent closeLabel={t('spaces.close')}>
        <SubjectForm
          key={editing?.id ?? 'new'}
          site={site}
          existing={editing}
          onDone={(saved) => {
            setFormOpen(false)
            if (saved) select(saved.id)
          }}
        />
      </SheetContent>
    </Sheet>
  )

  if (subjects.data.length === 0) {
    return (
      <>
        <div className="rounded-2xl border border-dashed bg-card/50">
          <EmptyState
            icon={Users}
            title={t('access.subjects.empty')}
            action={
              canManage && (
                <Button variant="outline" onClick={() => edit(null)}>
                  <Plus />
                  {t('access.subjects.createFirst')}
                </Button>
              )
            }
          >
            {t('access.subjects.emptyDescription')}
          </EmptyState>
        </div>
        {form}
      </>
    )
  }

  return (
    <div className="grid items-start gap-6 md:grid-cols-[minmax(0,2fr)_minmax(0,3fr)]">
      <Card className="space-y-3 p-3">
        <div className="flex gap-2">
          <div className="relative flex-1">
            <Search className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-muted-foreground" aria-hidden="true" />
            <Input
              type="search"
              value={query}
              onChange={(e) => setQuery(e.target.value)}
              placeholder={t('access.subjects.filterPlaceholder')}
              aria-label={t('access.subjects.filter')}
              className="pl-9"
            />
          </div>
          {canManage && (
            <Button size="icon" aria-label={t('access.subjects.create')} onClick={() => edit(null)}>
              <Plus />
            </Button>
          )}
        </div>
        {visible.length === 0 ? (
          <p className="px-2 py-6 text-center text-sm text-muted-foreground">{t('access.subjects.noMatches')}</p>
        ) : (
          <ul aria-label={t('access.subjects.listLabel')} className="space-y-0.5">
            {visible.map((subject) => {
              const Icon = subjectIcons[subject.kind] ?? subjectIcons.person!
              const current = subject.id === selected?.id
              return (
                <li key={subject.id}>
                  <button
                    type="button"
                    aria-current={current ? 'true' : undefined}
                    onClick={() => select(subject.id)}
                    className={cn(
                      'flex w-full items-center gap-2.5 rounded-md px-2.5 py-2 text-left text-sm transition-colors outline-none',
                      'hover:bg-accent hover:text-accent-foreground focus-visible:ring-2 focus-visible:ring-ring',
                      current && 'bg-primary/10 font-medium text-primary hover:bg-primary/15 hover:text-primary',
                    )}
                  >
                    <Icon
                      className="size-4 shrink-0 opacity-80"
                      role="img"
                      aria-label={t(`access.subjects.kinds.${subject.kind}`, { defaultValue: subject.kind })}
                    />
                    <span className="truncate">{subject.alias}</span>
                  </button>
                </li>
              )
            })}
          </ul>
        )}
      </Card>
      <div id="subject-detail" className="scroll-mt-20">
        {selected ? (
          <SubjectDetail
            key={selected.id}
            site={site}
            subject={selected}
            canManage={canManage}
            actions={
              canManage && (
                <>
                  <Button size="sm" variant="ghost" onClick={() => edit(selected)} aria-label={t('spaces.editNamed', { name: selected.alias })}>
                    <Pencil />
                    {t('spaces.edit')}
                  </Button>
                  <Button
                    size="icon-sm"
                    variant="ghost"
                    className="text-destructive hover:bg-destructive/10 hover:text-destructive"
                    aria-label={t('spaces.deleteNamed', { name: selected.alias })}
                    onClick={() => {
                      remove.reset()
                      setDeleting(selected)
                    }}
                  >
                    <Trash2 />
                  </Button>
                </>
              )
            }
          />
        ) : (
          <div className="rounded-xl border border-dashed bg-card/50 px-6 py-12 text-center text-sm text-muted-foreground">
            {t('access.subjects.pick')}
          </div>
        )}
      </div>
      {form}
      <ConfirmDialog
        open={deleting !== null}
        onOpenChange={(open) => !open && setDeleting(null)}
        title={t('spaces.deleteTitle', { name: deleting?.alias ?? '' })}
        description={t('access.subjects.deleteDescription')}
        confirmLabel={t('spaces.delete')}
        cancelLabel={t('spaces.cancel')}
        pending={remove.isPending}
        error={remove.error ? describeError(remove.error, t) : null}
        onConfirm={() => deleting && remove.mutate(deleting)}
      />
    </div>
  )
}

/** Alta y edición de un sujeto: solo tipo y alias, sin datos personales (spec subjects). */
function SubjectForm({ site, existing, onDone }: { site: Site; existing: Subject | null; onDone: (saved: Subject | null) => void }) {
  const { t } = useTranslation()
  const session = useSession()
  const organizationId = session.status === 'active' ? session.organizationId : null
  const queryClient = useQueryClient()
  const [kind, setKind] = useState<SubjectKind>((existing?.kind as SubjectKind | undefined) ?? 'person')
  const [alias, setAlias] = useState(existing?.alias ?? '')
  const [aliasRequired, setAliasRequired] = useState(false)

  const save = useMutation({
    mutationFn: async () =>
      existing
        ? unwrap(await api.PUT('/api/v1/subjects/{id}', { params: { path: { id: existing.id } }, body: { kind, alias: alias.trim() } }))
        : unwrap(await api.POST('/api/v1/sites/{siteId}/subjects', { params: { path: { siteId: site.id } }, body: { kind, alias: alias.trim() } })),
    onSuccess: async (saved) => {
      await queryClient.invalidateQueries({ queryKey: subjectsKey(organizationId, site.id) })
      onDone(saved)
    },
  })

  const error = save.error instanceof ApiError ? save.error : null
  const aliasError = aliasRequired ? t('access.subjects.aliasRequired') : error?.fieldError('alias')
  const general = save.error && !aliasError ? describeError(save.error, t) : null

  function submit(event: FormEvent) {
    event.preventDefault()
    if (!alias.trim()) {
      setAliasRequired(true)
      return
    }
    setAliasRequired(false)
    save.mutate()
  }

  const title = existing ? t('access.subjects.editTitle') : t('access.subjects.createTitle')
  return (
    <form aria-label={title} className="flex min-h-0 flex-1 flex-col" onSubmit={submit} noValidate>
      <SheetHeader>
        <SheetTitle>{title}</SheetTitle>
        <SheetDescription>{t('access.subjects.formDescription')}</SheetDescription>
      </SheetHeader>
      <div className="flex-1 space-y-5 overflow-y-auto px-6 py-5">
        {general && <Alert variant="destructive">{general}</Alert>}
        <fieldset className="space-y-2">
          <legend className="mb-2 text-sm font-medium">{t('access.subjects.kind')}</legend>
          <div className="grid grid-cols-2 gap-2">
            {subjectKinds.map((value) => (
              <label
                key={value}
                className="flex cursor-pointer items-center gap-2.5 rounded-lg border p-3 text-sm transition-colors has-[:checked]:border-primary has-[:checked]:bg-primary/5"
              >
                <input
                  type="radio"
                  name="subject-kind"
                  value={value}
                  checked={kind === value}
                  onChange={() => setKind(value)}
                  className="size-4 accent-primary"
                />
                {t(`access.subjects.kinds.${value}`)}
              </label>
            ))}
          </div>
        </fieldset>
        <div className="space-y-2">
          <Label htmlFor="subject-alias">{t('access.subjects.alias')}</Label>
          <Input
            id="subject-alias"
            value={alias}
            maxLength={100}
            autoComplete="off"
            placeholder={t('access.subjects.aliasPlaceholder')}
            aria-invalid={!!aliasError}
            aria-describedby={aliasError ? 'subject-alias-error subject-alias-hint' : 'subject-alias-hint'}
            onChange={(e) => setAlias(e.target.value)}
          />
          <FieldHint id="subject-alias-hint">{t('access.subjects.aliasHint')}</FieldHint>
          <FieldError id="subject-alias-error" message={aliasError} />
        </div>
        <Alert variant="info">{t('access.subjects.noPersonalData')}</Alert>
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

export function KindBadge({ kind }: { kind: string }) {
  const { t } = useTranslation()
  return <Badge variant="secondary">{t(`access.subjects.kinds.${kind}`, { defaultValue: kind })}</Badge>
}
