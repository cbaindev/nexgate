import { Eye, EyeOff } from 'lucide-react'
import { type FormEvent, useState } from 'react'
import { useTranslation } from 'react-i18next'
import type { ApiError } from '@/api/errors'
import { describeError } from '@/api/errors'
import { Alert } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Input, Label } from '@/components/ui/form'
import { signIn } from '@/session/session'
import { AuthLayout } from './AuthLayout'

export function SignInPage({ expired }: { expired: boolean }) {
  const { t } = useTranslation()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [showPassword, setShowPassword] = useState(false)
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
    <AuthLayout>
      <div className="space-y-2">
        <h1 className="text-2xl font-semibold tracking-tight">{t('signIn.title')}</h1>
        <p className="text-sm text-muted-foreground">{t('signIn.description')}</p>
      </div>
      <form className="mt-8 space-y-5" onSubmit={submit} noValidate>
        {expired && !message && <Alert variant="info">{t('signIn.expired')}</Alert>}
        {message && <Alert variant="destructive">{message}</Alert>}
        <div className="space-y-2">
          <Label htmlFor="email">{t('signIn.email')}</Label>
          <Input
            id="email"
            type="email"
            autoComplete="username"
            placeholder={t('signIn.emailPlaceholder')}
            value={email}
            onChange={(e) => setEmail(e.target.value)}
          />
        </div>
        <div className="space-y-2">
          <Label htmlFor="password">{t('signIn.password')}</Label>
          <div className="relative">
            <Input
              id="password"
              type={showPassword ? 'text' : 'password'}
              autoComplete="current-password"
              className="pr-11"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
            />
            <Button
              type="button"
              variant="ghost"
              size="icon-sm"
              className="absolute top-1 right-1 text-muted-foreground"
              aria-label={showPassword ? t('signIn.hidePassword') : t('signIn.showPassword')}
              aria-pressed={showPassword}
              onClick={() => setShowPassword((value) => !value)}
            >
              {showPassword ? <EyeOff /> : <Eye />}
            </Button>
          </div>
        </div>
        <Button type="submit" size="lg" className="w-full" loading={submitting}>
          {submitting ? t('signIn.submitting') : t('signIn.submit')}
        </Button>
      </form>
    </AuthLayout>
  )
}
