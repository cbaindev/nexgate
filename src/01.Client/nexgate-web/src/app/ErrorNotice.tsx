import { RotateCw } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { ApiError, describeError } from '@/api/errors'
import { Alert } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'

/** Error de una consulta: mensaje en español, código de referencia si fue inesperado y reintentar. */
export function ErrorNotice({ error, onRetry }: { error: unknown; onRetry?: () => void }) {
  const { t } = useTranslation()
  const correlation = error instanceof ApiError && error.status >= 500 ? error.correlationId : undefined
  return (
    <Alert variant="destructive">
      <span className="font-medium">{describeError(error, t)}</span>
      {correlation && <span className="font-mono text-xs opacity-80">{t('errors.correlation', { id: correlation })}</span>}
      {onRetry && (
        <Button size="sm" variant="outline" className="ml-auto text-foreground" onClick={onRetry}>
          <RotateCw />
          {t('offline.retry')}
        </Button>
      )}
    </Alert>
  )
}
