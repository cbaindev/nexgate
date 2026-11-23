import { Lock, Sparkles, WifiOff } from 'lucide-react'
import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { useSession } from '@/session/session'

function Message({ icon, title, children, action }: { icon: ReactNode; title: string; children: ReactNode; action?: ReactNode }) {
  return (
    <Card className="mx-auto mt-8 max-w-lg text-center">
      <CardHeader className="items-center">
        <div className="mb-2 text-muted-foreground [&_svg]:size-10">{icon}</div>
        <CardTitle>{title}</CardTitle>
        <CardDescription>{children}</CardDescription>
      </CardHeader>
      {action && <CardContent>{action}</CardContent>}
    </Card>
  )
}

export function HomePage() {
  const { t } = useTranslation()
  const session = useSession()
  if (session.status !== 'active') {
    return null
  }

  const organization = session.user.organizations.find((o) => o.id === session.organizationId)
  return (
    <div className="space-y-2">
      <h1 className="text-2xl font-semibold">{t('home.welcome', { email: session.user.email })}</h1>
      <p className="text-muted-foreground">
        {organization ? t('home.organization', { organization: organization.name }) : t('home.noOrganization')}
      </p>
      <p className="text-muted-foreground">{t('home.pick')}</p>
    </div>
  )
}

export function NoPermissionPage() {
  const { t } = useTranslation()
  return (
    <Message icon={<Lock />} title={t('pages.noPermissionTitle')} action={<HomeLink />}>
      {t('pages.noPermission')}
    </Message>
  )
}

export function ComingSoonPage({ title }: { title: string }) {
  const { t } = useTranslation()
  return (
    <div className="space-y-4">
      <h1 className="text-2xl font-semibold">{title}</h1>
      <Message icon={<Sparkles />} title={t('pages.comingSoonTitle')}>
        {t('pages.comingSoon')}
      </Message>
    </div>
  )
}

export function NotFoundPage() {
  const { t } = useTranslation()
  return (
    <Message icon={<Lock />} title={t('pages.notFoundTitle')} action={<HomeLink />}>
      {t('pages.notFound')}
    </Message>
  )
}

export function OfflinePage({ onRetry }: { onRetry: () => void }) {
  const { t } = useTranslation()
  return (
    <main className="flex min-h-dvh items-center justify-center p-4">
      <Message icon={<WifiOff />} title={t('offline.title')} action={<Button onClick={onRetry}>{t('offline.retry')}</Button>}>
        {t('offline.description')}
      </Message>
    </main>
  )
}

export function LoadingPage() {
  const { t } = useTranslation()
  return (
    <main className="flex min-h-dvh items-center justify-center text-muted-foreground" aria-busy="true">
      {t('app.loading')}
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
