import { Building } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { describeError } from '@/api/errors'
import { Button } from '@/components/ui/button'
import { Alert, Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { chooseOrganization, type CurrentUser, signOut } from '@/session/session'

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
    <main className="flex min-h-dvh items-center justify-center p-4">
      <Card className="w-full max-w-md">
        <CardHeader>
          <CardTitle>{t('organization.chooseTitle')}</CardTitle>
          <CardDescription>{t('organization.chooseDescription')}</CardDescription>
        </CardHeader>
        <CardContent className="space-y-3">
          {message && <Alert variant="destructive">{message}</Alert>}
          <ul className="space-y-2">
            {user.organizations.map((organization) => (
              <li key={organization.id}>
                <Button variant="outline" className="w-full justify-start" disabled={busy} onClick={() => void choose(organization.id)}>
                  <Building />
                  {organization.name}
                </Button>
              </li>
            ))}
          </ul>
          <Button variant="ghost" className="w-full" onClick={() => void signOut()}>
            {t('user.signOut')}
          </Button>
        </CardContent>
      </Card>
    </main>
  )
}
