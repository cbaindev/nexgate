import { useTranslation } from 'react-i18next'
import { ApiError, describeError } from '@/api/errors'
import { Button } from '@/components/ui/button'
import { Alert } from '@/components/ui/card'

/** Error de una consulta: mensaje en español, código de referencia si fue inesperado y reintentar. */
export function ErrorNotice({ error, onRetry }: { error: unknown; onRetry?: () => void }) {
  const { t } = useTranslation()
  const correlation = error instanceof ApiError && error.status >= 500 ? error.correlationId : undefined
  return (
    <Alert variant="destructive" className="flex flex-wrap items-center gap-3">
      <span>{describeError(error, t)}</span>
      {correlation && <span className="text-xs">{t('errors.correlation', { id: correlation })}</span>}
      {onRetry && (
        <Button size="sm" variant="outline" onClick={onRetry}>
          {t('offline.retry')}
        </Button>
      )}
    </Alert>
  )
}
