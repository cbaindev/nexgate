import { type FormEvent, useState } from 'react'
import { useTranslation } from 'react-i18next'
import type { ApiError } from '@/api/errors'
import { describeError } from '@/api/errors'
import { Button } from '@/components/ui/button'
import { Alert, Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Input, Label } from '@/components/ui/form'
import { signIn } from '@/session/session'

export function SignInPage({ expired }: { expired: boolean }) {
  const { t } = useTranslation()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [submitting, setSubmitting] = useState(false)
  const [message, setMessage] = useState<string | null>(null)

  async function submit(event: FormEvent) {
    event.preventDefault()
    if (!email.trim() || !password) {
      setMessage(t('signIn.required'))
      return
    }

    setSubmitting(true)
    setMessage(null)
    const error = await signIn(email.trim(), password)
    setSubmitting(false)
    if (error) {
      setMessage(messageFor(error))
    }
  }

  function messageFor(error: ApiError) {
    // Credenciales inválidas, cuenta inexistente o deshabilitada: un solo mensaje, sin decir cuál.
    return error.status === 401 ? t('signIn.invalidCredentials') : describeError(error, t)
  }

  return (
    <main className="flex min-h-dvh items-center justify-center p-4">
      <Card className="w-full max-w-sm">
        <CardHeader>
          <div className="mb-2 flex items-center gap-2 text-primary">
            <img src="/favicon.svg" alt="" className="size-8" />
            <span className="text-xl font-semibold">{t('app.name')}</span>
          </div>
          <CardTitle>{t('signIn.title')}</CardTitle>
          <CardDescription>{t('signIn.description')}</CardDescription>
        </CardHeader>
        <CardContent>
          <form className="space-y-4" onSubmit={submit} noValidate>
            {expired && !message && <Alert>{t('signIn.expired')}</Alert>}
            {message && <Alert variant="destructive">{message}</Alert>}
            <div className="space-y-2">
              <Label htmlFor="email">{t('signIn.email')}</Label>
              <Input id="email" type="email" autoComplete="username" value={email} onChange={(e) => setEmail(e.target.value)} />
            </div>
            <div className="space-y-2">
              <Label htmlFor="password">{t('signIn.password')}</Label>
              <Input
                id="password"
                type="password"
                autoComplete="current-password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
              />
            </div>
            <Button type="submit" className="w-full" disabled={submitting}>
              {submitting ? t('signIn.submitting') : t('signIn.submit')}
            </Button>
          </form>
        </CardContent>
      </Card>
    </main>
  )
}
