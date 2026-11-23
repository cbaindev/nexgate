import { ArrowRight, Compass, Lock, Sparkles, WifiOff } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import { LogoMark } from '@/components/brand/Logo'
import { Button } from '@/components/ui/button'
import { EmptyState } from '@/components/ui/empty-state'
import { PageHeader } from '@/components/ui/page-header'
import { useSession } from '@/session/session'
import { allows, sections } from './sections'

export function HomePage() {
  const { t } = useTranslation()
  const session = useSession()
  if (session.status !== 'active') {
    return null
  }

  const organization = session.user.organizations.find((o) => o.id === session.organizationId)
  const shortcuts = sections.filter((section) => section.path !== '/' && allows(session, section.requirement))
  return (
    <div className="space-y-8">
      <div className="relative overflow-hidden rounded-2xl border bg-card p-6 shadow-xs sm:p-8">
        <div
          aria-hidden="true"
          className="absolute inset-0 bg-[radial-gradient(60%_120%_at_100%_0%,color-mix(in_oklch,var(--primary)_14%,transparent),transparent_70%)]"
        />
        <div className="relative space-y-1.5">
          <h1 className="text-2xl font-semibold tracking-tight break-all sm:break-normal">{t('home.welcome', { email: session.user.email })}</h1>
          <p className="text-muted-foreground">
            {organization ? t('home.organization', { organization: organization.name }) : t('home.noOrganization')}
          </p>
        </div>
      </div>

      {shortcuts.length > 0 && (
        <section aria-labelledby="shortcuts" className="space-y-3">
          <h2 id="shortcuts" className="text-sm font-semibold text-muted-foreground">
            {t('home.pick')}
          </h2>
          <ul className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
            {shortcuts.map((section) => (
              <li key={section.id}>
                <Link
                  to={section.path}
                  className="group flex h-full items-start gap-4 rounded-xl border bg-card p-4 shadow-xs transition-[border-color,box-shadow] outline-none hover:border-primary/40 hover:shadow-md focus-visible:ring-2 focus-visible:ring-ring"
                >
                  <span className="flex size-10 shrink-0 items-center justify-center rounded-lg bg-accent text-accent-foreground">
                    <section.icon className="size-5" aria-hidden="true" />
                  </span>
                  <span className="min-w-0 flex-1 space-y-1">
                    <span className="flex items-center gap-1 font-medium">
                      {t(section.labelKey)}
                      <ArrowRight
                        className="size-4 -translate-x-1 text-primary opacity-0 transition-[opacity,transform] group-hover:translate-x-0 group-hover:opacity-100"
                        aria-hidden="true"
                      />
                    </span>
                    <span className="block text-sm text-muted-foreground">{t(section.descriptionKey)}</span>
                  </span>
                </Link>
              </li>
            ))}
          </ul>
        </section>
      )}
    </div>
  )
}

export function NoPermissionPage() {
  const { t } = useTranslation()
  return (
    <EmptyState icon={Lock} headingLevel={1} title={t('pages.noPermissionTitle')} action={<HomeLink />}>
      {t('pages.noPermission')}
    </EmptyState>
  )
}

export function ComingSoonPage({ title }: { title: string }) {
  const { t } = useTranslation()
  return (
    <div className="space-y-6">
      <PageHeader title={title} />
      <div className="rounded-2xl border border-dashed bg-card/50">
        <EmptyState icon={Sparkles} title={t('pages.comingSoonTitle')}>
          {t('pages.comingSoon')}
        </EmptyState>
      </div>
    </div>
  )
}

export function NotFoundPage() {
  const { t } = useTranslation()
  return (
    <EmptyState icon={Compass} headingLevel={1} title={t('pages.notFoundTitle')} action={<HomeLink />}>
      {t('pages.notFound')}
    </EmptyState>
  )
}

export function OfflinePage({ onRetry }: { onRetry: () => void }) {
  const { t } = useTranslation()
  return (
    <main className="flex min-h-dvh flex-col items-center justify-center p-4">
      <EmptyState icon={WifiOff} headingLevel={1} title={t('offline.title')} action={<Button onClick={onRetry}>{t('offline.retry')}</Button>}>
        {t('offline.description')}
      </EmptyState>
    </main>
  )
}

export function LoadingPage() {
  const { t } = useTranslation()
  return (
    <main className="flex min-h-dvh flex-col items-center justify-center gap-4" aria-busy="true">
      <LogoMark className="size-12 animate-pulse" />
      <p role="status" className="text-sm text-muted-foreground">
        {t('app.loading')}
      </p>
    </main>
  )
}

function HomeLink() {
  const { t } = useTranslation()
  return (
    <Button asChild variant="outline">
      <Link to="/">{t('pages.backHome')}</Link>
    </Button>
  )
}
