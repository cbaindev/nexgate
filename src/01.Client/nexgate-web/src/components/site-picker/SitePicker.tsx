import { useQuery } from '@tanstack/react-query'
import { Building2, MapPinned } from 'lucide-react'
import { type ReactNode, useEffect, useId } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useSearchParams } from 'react-router'
import { api } from '@/api/client'
import { unwrap } from '@/api/errors'
import type { components } from '@/api/schema'
import { ErrorNotice } from '@/app/ErrorNotice'
import { Alert } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { EmptyState } from '@/components/ui/empty-state'
import { Select } from '@/components/ui/form'
import { Skeleton } from '@/components/ui/skeleton'
import { sitesKey } from '@/features/sites/SitesPage'
import { hasPermissionOnSite, useSession } from '@/session/session'

export type Site = components['schemas']['SiteDto']

/** Parámetro de la dirección con el sitio elegido (design de add-spaces-screens, decisión 1). */
export const siteParam = 'sitio'

export type SelectedSite =
  | { status: 'loading' }
  | { status: 'error'; error: Error; retry: () => void }
  | { status: 'noSites' }
  | { status: 'choose'; sites: Site[] }
  | { status: 'unavailable'; sites: Site[] }
  | { status: 'ready'; sites: Site[]; site: Site }

/**
 * Sitio elegido en la dirección (`?sitio=`), entre los sitios visibles de la organización activa. Con un solo sitio
 * visible lo elige solo. Usa la misma consulta que la pantalla de sitios, así no hay un pedido extra.
 */
export function useSelectedSite(): SelectedSite & { choose: (siteId: string) => void } {
  const session = useSession()
  const organizationId = session.status === 'active' ? session.organizationId : null
  const [params, setParams] = useSearchParams()
  const sites = useQuery({
    queryKey: sitesKey(organizationId),
    queryFn: async () => unwrap(await api.GET('/api/v1/sites')),
  })
  const requested = params.get(siteParam)
  const only = sites.data?.length === 1 ? sites.data[0] : undefined

  const choose = (siteId: string) =>
    setParams(
      (current) => {
        // Otro sitio: lo que estaba elegido dentro del anterior ya no aplica.
        const next = new URLSearchParams(current)
        next.set(siteParam, siteId)
        next.delete('espacio')
        return next
      },
      { replace: requested === null },
    )

  useEffect(() => {
    if (only && requested === null) {
      setParams(
        (current) => {
          const next = new URLSearchParams(current)
          next.set(siteParam, only.id)
          return next
        },
        { replace: true },
      )
    }
  }, [only, requested, setParams])

  if (sites.isPending) return { status: 'loading', choose }
  if (sites.isError) return { status: 'error', error: sites.error, retry: () => void sites.refetch(), choose }
  const list = sites.data
  if (list.length === 0) return { status: 'noSites', choose }
  const id = requested ?? only?.id
  if (id === undefined) return { status: 'choose', sites: list, choose }
  const site = list.find((s) => s.id === id)
  return site ? { status: 'ready', sites: list, site, choose } : { status: 'unavailable', sites: list, choose }
}

/** Desplegable de sitio para el encabezado de las secciones por sitio. */
export function SitePicker({ selected }: { selected: ReturnType<typeof useSelectedSite> }) {
  const { t } = useTranslation()
  const id = useId()
  if (selected.status !== 'ready' && selected.status !== 'choose' && selected.status !== 'unavailable') {
    return null
  }

  const value = selected.status === 'ready' ? selected.site.id : ''
  return (
    <div className="flex items-center gap-2">
      <label htmlFor={id} className="flex items-center gap-1.5 text-sm font-medium text-muted-foreground">
        <MapPinned className="size-4" aria-hidden="true" />
        {t('sitePicker.label')}
      </label>
      <Select id={id} className="w-56" value={value} onChange={(e) => selected.choose(e.target.value)}>
        {value === '' && (
          <option value="" disabled>
            {t('sitePicker.placeholder')}
          </option>
        )}
        {selected.sites.map((site) => (
          <option key={site.id} value={site.id}>
            {site.name}
          </option>
        ))}
      </Select>
    </div>
  )
}

/**
 * Lo que muestra una sección por sitio antes de tener un sitio: carga, error, organización sin sitios, elegir uno o
 * sitio no disponible. Con un sitio elegido, muestra `children`.
 */
export function SiteGate({ selected, children }: { selected: ReturnType<typeof useSelectedSite>; children: (site: Site) => ReactNode }) {
  const { t } = useTranslation()
  const session = useSession()
  switch (selected.status) {
    case 'loading':
      return (
        <div aria-busy="true" className="space-y-3">
          <span role="status" className="sr-only">
            {t('app.loading')}
          </span>
          <Skeleton className="h-10 w-full max-w-md" />
          <Skeleton className="h-64 w-full" />
        </div>
      )
    case 'error':
      return <ErrorNotice error={selected.error} onRetry={selected.retry} />
    case 'noSites':
      return (
        <div className="rounded-2xl border border-dashed bg-card/50">
          <EmptyState
            icon={Building2}
            title={t('sitePicker.noSites')}
            action={
              hasPermissionOnSite(session, 'sites.manage') && (
                <Button asChild variant="outline">
                  <Link to="/sitios">{t('sitePicker.goToSites')}</Link>
                </Button>
              )
            }
          >
            {t('sitePicker.noSitesDescription')}
          </EmptyState>
        </div>
      )
    case 'choose':
      return (
        <div className="rounded-2xl border border-dashed bg-card/50">
          <EmptyState icon={MapPinned} title={t('sitePicker.choose')}>
            {t('sitePicker.chooseDescription')}
          </EmptyState>
        </div>
      )
    case 'unavailable':
      return <Alert variant="warning">{t('sitePicker.unavailable')}</Alert>
    case 'ready':
      return children(selected.site)
  }
}
