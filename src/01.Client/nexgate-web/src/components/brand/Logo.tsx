import { useTranslation } from 'react-i18next'
import { cn } from '@/lib/utils'

// Logo de Nexgate (design de refine-admin-panel-design, decisión 8). El isotipo es public/brand/nexgate-mark.svg,
// generado por scripts/icons.mjs.

export function LogoMark({ className }: { className?: string }) {
  return <img src="/brand/nexgate-mark.svg" alt="" width={32} height={32} className={cn('size-8 shrink-0 rounded-[22%]', className)} />
}

/** Isotipo y logotipo. `inverted` es para fondos de color de la marca. */
export function Logo({ className, inverted = false }: { className?: string; inverted?: boolean }) {
  const { t } = useTranslation()
  return (
    <span className={cn('flex items-center gap-2.5', className)}>
      <LogoMark className={cn(inverted && 'ring-1 ring-white/25')} />
      <span className={cn('text-[17px] font-semibold tracking-tight', inverted && 'text-white')}>{t('app.name')}</span>
    </span>
  )
}
