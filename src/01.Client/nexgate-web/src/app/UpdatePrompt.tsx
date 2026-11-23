import { Sparkles } from 'lucide-react'
import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { registerSW } from 'virtual:pwa-register'
import { Button } from '@/components/ui/button'

/**
 * Registra el service worker (solo guarda los archivos del build) y avisa cuando hay una versión nueva (design de
 * add-admin-panel-shell, decisión 7).
 */
export function UpdatePrompt() {
  const { t } = useTranslation()
  const [update, setUpdate] = useState<((reload?: boolean) => Promise<void>) | null>(null)

  useEffect(() => {
    const updateSW = registerSW({ onNeedRefresh: () => setUpdate(() => updateSW) })
  }, [])

  if (!update) {
    return null
  }

  return (
    <div
      role="status"
      className="fixed inset-x-4 bottom-20 z-50 mx-auto flex max-w-md animate-rise items-center gap-3 rounded-xl border bg-popover p-3 pl-4 text-popover-foreground shadow-lg md:bottom-6"
    >
      <Sparkles className="size-4 shrink-0 text-primary" aria-hidden="true" />
      <span className="flex-1 text-sm">{t('update.available')}</span>
      <Button size="sm" onClick={() => void update(true)}>
        {t('update.reload')}
      </Button>
    </div>
  )
}
