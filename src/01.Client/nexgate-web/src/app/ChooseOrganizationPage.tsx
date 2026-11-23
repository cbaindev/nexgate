import { ChevronRight } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { describeError } from '@/api/errors'
import { Alert } from '@/components/ui/alert'
import { Avatar } from '@/components/ui/avatar'
import { Button } from '@/components/ui/button'
import { chooseOrganization, type CurrentUser, signOut } from '@/session/session'
import { AuthLayout } from './AuthLayout'

export function ChooseOrganizationPage({ user, unavailable }: { user: CurrentUser; unavailable: boolean }) {
  const { t } = useTranslation()
  const [message, setMessage] = useState<string | null>(unavailable ? t('organization.unavailable') : null)
  const [busy, setBusy] = useState(false)

  async function choose(id: string) {
    setBusy(true)
    const error = await chooseOrganization(id)
    setBusy(false)
    if (error) {
      setMessage(error.status === 403 ? t('organization.unavailable') : describeError(error, t))
    }
  }

  return (
    <AuthLayout>
      <div className="space-y-2">
        <h1 className="text-2xl font-semibold tracking-tight">{t('organization.chooseTitle')}</h1>
        <p className="text-sm text-muted-foreground">{t('organization.chooseDescription')}</p>
      </div>
      <div className="mt-8 space-y-4">
        {message && <Alert variant="destructive">{message}</Alert>}
        <ul className="space-y-2">
          {user.organizations.map((organization) => (
            <li key={organization.id}>
              <button
                type="button"
                disabled={busy}
                onClick={() => void choose(organization.id)}
                className="group flex w-full items-center gap-3 rounded-lg border bg-card px-3 py-3 text-left text-sm font-medium shadow-xs transition-[border-color,box-shadow,background-color] outline-none hover:border-primary/40 hover:bg-accent/50 hover:shadow-sm focus-visible:ring-2 focus-visible:ring-ring disabled:opacity-50"
              >
                <Avatar text={organization.name} className="size-9 rounded-md" />
                <span className="flex-1 truncate">{organization.name}</span>
                <ChevronRight className="size-4 text-muted-foreground transition-transform group-hover:translate-x-0.5" aria-hidden="true" />
              </button>
            </li>
          ))}
        </ul>
        <p className="text-xs text-muted-foreground">{t('organization.signedInAs', { email: user.email })}</p>
        <Button variant="ghost" className="w-full" onClick={() => void signOut()}>
          {t('user.signOut')}
        </Button>
      </div>
    </AuthLayout>
  )
}
