import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { CalendarClock, Pencil, Plus, Trash2, X } from 'lucide-react'
import { type FormEvent, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { api } from '@/api/client'
import { ApiError, describeError, unwrap } from '@/api/errors'
import { ErrorNotice } from '@/app/ErrorNotice'
import type { Site } from '@/components/site-picker/SitePicker'
import { Alert } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { ConfirmDialog } from '@/components/ui/confirm-dialog'
import { EmptyState } from '@/components/ui/empty-state'
import { FieldError, Input, Label } from '@/components/ui/form'
import { Sheet, SheetContent, SheetDescription, SheetFooter, SheetHeader, SheetTitle } from '@/components/ui/sheet'
import { Skeleton } from '@/components/ui/skeleton'
import { cn } from '@/lib/utils'
import { hasPermissionOnSite, useSession } from '@/session/session'
import { type Day, dayInitials, dayName, days, describeWindow, MaxWindows, type Schedule, schedulesKey } from './model'

/** Horarios del sitio con sus ventanas en palabras (design de add-access-screens, decisión 6). */
export function SchedulesTab({ site }: { site: Site }) {
  const { t } = useTranslation()
  const session = useSession()
  const organizationId = session.status === 'active' ? session.organizationId : null
  const queryClient = useQueryClient()
  const [editing, setEditing] = useState<Schedule | null>(null)
  const [open, setOpen] = useState(false)
  const [deleting, setDeleting] = useState<Schedule | null>(null)
  const canManage = hasPermissionOnSite(session, 'access.manage', site.id)
  const schedules = useQuery({
    queryKey: schedulesKey(organizationId, site.id),
    queryFn: async () => unwrap(await api.GET('/api/v1/sites/{siteId}/schedules', { params: { path: { siteId: site.id } } })),
  })
  const remove = useMutation({
    mutationFn: async (schedule: Schedule) => unwrap(await api.DELETE('/api/v1/schedules/{id}', { params: { path: { id: schedule.id } } })),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: schedulesKey(organizationId, site.id) })
      setDeleting(null)
    },
  })

  function edit(schedule: Schedule | null) {
    setEditing(schedule)
    setOpen(true)
  }

  const sheet = (
    <Sheet open={open} onOpenChange={setOpen}>
      <SheetContent closeLabel={t('spaces.close')} className="sm:max-w-lg">
        <ScheduleForm key={editing?.id ?? 'new'} site={site} existing={editing} onDone={() => setOpen(false)} />
      </SheetContent>
    </Sheet>
  )

  if (schedules.isPending) {
    return (
      <div aria-busy="true" className="grid gap-4 sm:grid-cols-2">
        <span role="status" className="sr-only">
          {t('app.loading')}
        </span>
        <Skeleton className="h-28 rounded-xl" />
        <Skeleton className="h-28 rounded-xl" />
      </div>
    )
  }

  if (schedules.isError) return <ErrorNotice error={schedules.error} onRetry={() => void schedules.refetch()} />

  const sorted = [...schedules.data].sort((a, b) => a.name.localeCompare(b.name, 'es'))
  return (
    <div className="space-y-4">
      {sorted.length === 0 ? (
        <div className="rounded-2xl border border-dashed bg-card/50">
          <EmptyState
            icon={CalendarClock}
            title={t('access.schedules.empty')}
            action={
              canManage && (
                <Button variant="outline" onClick={() => edit(null)}>
                  <Plus />
                  {t('access.schedules.createFirst')}
                </Button>
              )
            }
          >
            {t('access.schedules.emptyDescription')}
          </EmptyState>
        </div>
      ) : (
        <>
          {canManage && (
            <div className="flex justify-end">
              <Button onClick={() => edit(null)}>
                <Plus />
                {t('access.schedules.create')}
              </Button>
            </div>
          )}
          <ul className="grid gap-4 sm:grid-cols-2" aria-label={t('access.schedules.listLabel', { site: site.name })}>
            {sorted.map((schedule) => (
              <li key={schedule.id}>
                <Card className="flex h-full items-start gap-4 p-5">
                  <span className="flex size-10 shrink-0 items-center justify-center rounded-lg bg-accent text-accent-foreground">
                    <CalendarClock className="size-5" aria-hidden="true" />
                  </span>
                  <div className="min-w-0 flex-1 space-y-1.5">
                    <h2 className="font-semibold leading-snug">{schedule.name}</h2>
                    <ul className="space-y-0.5 text-sm text-muted-foreground tabular-nums">
                      {schedule.windows.map((window, index) => (
                        <li key={index}>{describeWindow(window)}</li>
                      ))}
                    </ul>
                  </div>
                  {canManage && (
                    <div className="flex gap-1">
                      <Button size="icon-sm" variant="ghost" aria-label={t('spaces.editNamed', { name: schedule.name })} onClick={() => edit(schedule)}>
                        <Pencil />
                      </Button>
                      <Button
                        size="icon-sm"
                        variant="ghost"
                        className="text-destructive hover:bg-destructive/10 hover:text-destructive"
                        aria-label={t('spaces.deleteNamed', { name: schedule.name })}
                        onClick={() => {
                          remove.reset()
                          setDeleting(schedule)
                        }}
                      >
                        <Trash2 />
                      </Button>
                    </div>
                  )}
                </Card>
              </li>
            ))}
          </ul>
        </>
      )}
      {sheet}
      <ConfirmDialog
        open={deleting !== null}
        onOpenChange={(value) => !value && setDeleting(null)}
        title={t('spaces.deleteTitle', { name: deleting?.name ?? '' })}
        description={t('access.schedules.deleteDescription')}
        confirmLabel={t('spaces.delete')}
        cancelLabel={t('spaces.cancel')}
        pending={remove.isPending}
        error={remove.error ? describeError(remove.error, t) : null}
        onConfirm={() => deleting && remove.mutate(deleting)}
      />
    </div>
  )
}

interface WindowDraft {
  key: number
  days: Day[]
  from: string
  to: string
  /** "Hasta el final del día": envía 24:00, que un campo de hora no admite. */
  endOfDay: boolean
}

let nextKey = 0
const newWindow = (): WindowDraft => ({ key: nextKey++, days: ['monday', 'tuesday', 'wednesday', 'thursday', 'friday'], from: '08:00', to: '17:00', endOfDay: false })

function windowProblem(window: WindowDraft): string | null {
  if (window.days.length === 0) return 'access.schedules.noDays'
  if (!window.from || (!window.endOfDay && (!window.to || window.to <= window.from))) return 'access.schedules.endBeforeStart'
  return null
}

function ScheduleForm({ site, existing, onDone }: { site: Site; existing: Schedule | null; onDone: () => void }) {
  const { t } = useTranslation()
  const session = useSession()
  const organizationId = session.status === 'active' ? session.organizationId : null
  const queryClient = useQueryClient()
  const [name, setName] = useState(existing?.name ?? '')
  const [windows, setWindows] = useState<WindowDraft[]>(() =>
    existing
      ? existing.windows.map((w) => ({
          key: nextKey++,
          days: days.filter((d) => w.days.includes(d)),
          from: w.from,
          to: w.to === '24:00' ? '' : w.to,
          endOfDay: w.to === '24:00',
        }))
      : [newWindow()],
  )
  const [checked, setChecked] = useState(false)

  const save = useMutation({
    mutationFn: async () => {
      const body = {
        name: name.trim(),
        windows: windows.map((w) => ({ days: w.days, from: w.from, to: w.endOfDay ? '24:00' : w.to })),
      }
      return existing
        ? unwrap(await api.PUT('/api/v1/schedules/{id}', { params: { path: { id: existing.id } }, body }))
        : unwrap(await api.POST('/api/v1/sites/{siteId}/schedules', { params: { path: { siteId: site.id } }, body }))
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: schedulesKey(organizationId, site.id) })
      onDone()
    },
  })

  const error = save.error instanceof ApiError ? save.error : null
  const nameError = checked && !name.trim() ? t('access.schedules.nameRequired') : error?.fieldError('name')
  const windowsError = error?.fieldError('windows')
  const general = save.error && !nameError && !windowsError ? describeError(save.error, t) : null

  function update(key: number, change: Partial<WindowDraft>) {
    setWindows((current) => current.map((w) => (w.key === key ? { ...w, ...change } : w)))
  }

  function submit(event: FormEvent) {
    event.preventDefault()
    setChecked(true)
    if (!name.trim() || windows.some((w) => windowProblem(w))) return
    save.mutate()
  }

  const title = existing ? t('access.schedules.editTitle') : t('access.schedules.createTitle')
  return (
    <form aria-label={title} className="flex min-h-0 flex-1 flex-col" onSubmit={submit} noValidate>
      <SheetHeader>
        <SheetTitle>{title}</SheetTitle>
        <SheetDescription>{t('access.schedules.formDescription', { timeZone: site.timeZone })}</SheetDescription>
      </SheetHeader>
      <div className="flex-1 space-y-5 overflow-y-auto px-6 py-5">
        {general && <Alert variant="destructive">{general}</Alert>}
        <div className="space-y-2">
          <Label htmlFor="schedule-name">{t('access.schedules.name')}</Label>
          <Input
            id="schedule-name"
            value={name}
            maxLength={100}
            placeholder={t('access.schedules.namePlaceholder')}
            aria-invalid={!!nameError}
            aria-describedby={nameError ? 'schedule-name-error' : undefined}
            onChange={(e) => setName(e.target.value)}
          />
          <FieldError id="schedule-name-error" message={nameError} />
        </div>
        {windowsError && <Alert variant="destructive">{windowsError}</Alert>}
        {windows.map((window, index) => {
          const problemKey = checked ? windowProblem(window) : null
          const legend = t('access.schedules.window', { number: index + 1 })
          return (
            <fieldset
              key={window.key}
              className={cn('relative space-y-3 rounded-lg border p-4', problemKey && 'border-destructive/50')}
              aria-describedby={problemKey ? `window-${window.key}-error` : undefined}
            >
              {/* La leyenda va directo en el fieldset: así nombra al grupo. */}
              <legend className="float-left mb-3 text-sm font-medium">{legend}</legend>
              {windows.length > 1 && (
                <Button
                  type="button"
                  size="icon-sm"
                  variant="ghost"
                  className="absolute top-2.5 right-2.5"
                  aria-label={t('access.schedules.removeWindow', { number: index + 1 })}
                  onClick={() => setWindows((current) => current.filter((w) => w.key !== window.key))}
                >
                  <X />
                </Button>
              )}
              <div className="clear-both" />
              <div className="flex flex-wrap gap-1.5" role="group" aria-label={t('access.schedules.days')}>
                {days.map((day) => {
                  const on = window.days.includes(day)
                  return (
                    <label
                      key={day}
                      title={dayName(day)}
                      className="flex size-9 cursor-pointer items-center justify-center rounded-md border text-sm font-medium transition-colors has-[:checked]:border-primary has-[:checked]:bg-primary has-[:checked]:text-primary-foreground has-[:focus-visible]:ring-2 has-[:focus-visible]:ring-ring"
                    >
                      <input
                        type="checkbox"
                        className="sr-only"
                        checked={on}
                        aria-label={dayName(day)}
                        onChange={() => update(window.key, { days: on ? window.days.filter((d) => d !== day) : days.filter((d) => d === day || window.days.includes(d)) })}
                      />
                      <span aria-hidden="true">{dayInitials[day]}</span>
                    </label>
                  )
                })}
              </div>
              <div className="flex flex-wrap items-end gap-3">
                <div className="space-y-1.5">
                  <Label htmlFor={`window-${window.key}-from`}>{t('access.schedules.from')}</Label>
                  <Input id={`window-${window.key}-from`} type="time" className="w-32" value={window.from} onChange={(e) => update(window.key, { from: e.target.value })} />
                </div>
                <div className="space-y-1.5">
                  <Label htmlFor={`window-${window.key}-to`}>{t('access.schedules.to')}</Label>
                  <Input
                    id={`window-${window.key}-to`}
                    type="time"
                    className="w-32"
                    value={window.endOfDay ? '' : window.to}
                    disabled={window.endOfDay}
                    onChange={(e) => update(window.key, { to: e.target.value })}
                  />
                </div>
                <label className="flex h-10 items-center gap-2 text-sm">
                  <input type="checkbox" className="size-4 accent-primary" checked={window.endOfDay} onChange={(e) => update(window.key, { endOfDay: e.target.checked })} />
                  {t('access.schedules.endOfDay')}
                </label>
              </div>
              <FieldError id={`window-${window.key}-error`} message={problemKey ? t(problemKey) : undefined} />
            </fieldset>
          )
        })}
        {windows.length < MaxWindows && (
          <Button type="button" variant="outline" onClick={() => setWindows((current) => [...current, newWindow()])}>
            <Plus />
            {t('access.schedules.addWindow')}
          </Button>
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
