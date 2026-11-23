import type { LucideIcon } from 'lucide-react'
import type { ReactNode } from 'react'
import { cn } from '@/lib/utils'

/**
 * Ilustración de los estados vacíos: SVG en línea con los tokens de color (se adapta al tema), con anillos concéntricos,
 * puntos de la marca y el ícono de la sección en una tarjeta al centro.
 */
export function Illustration({ icon: Icon, className }: { icon: LucideIcon; className?: string }) {
  return (
    <div aria-hidden="true" className={cn('relative size-36', className)}>
      <svg viewBox="0 0 144 144" className="absolute inset-0 size-full">
        <circle cx="72" cy="72" r="70" className="fill-primary/[0.04] stroke-primary/10" strokeDasharray="3 5" />
        <circle cx="72" cy="72" r="52" className="fill-primary/[0.06] stroke-primary/15" />
        <circle cx="72" cy="72" r="34" className="fill-primary/[0.08]" />
        <circle cx="122" cy="40" r="4" className="fill-brand-accent" />
        <circle cx="24" cy="98" r="3" className="fill-primary/40" />
        <circle cx="112" cy="112" r="2.5" className="fill-primary/30" />
        <rect x="18" y="36" width="14" height="6" rx="3" className="fill-primary/20" />
      </svg>
      <div className="absolute inset-0 flex items-center justify-center">
        <div className="flex size-14 rotate-[-4deg] items-center justify-center rounded-2xl border bg-card text-primary shadow-md">
          <Icon className="size-7" strokeWidth={1.75} />
        </div>
      </div>
    </div>
  )
}

// Estado vacío: ilustración, título, explicación y la acción principal (solo si el usuario puede realizarla).
export function EmptyState({
  icon,
  title,
  children,
  action,
  className,
  headingLevel = 2,
}: {
  icon: LucideIcon
  title: string
  children?: ReactNode
  action?: ReactNode
  className?: string
  headingLevel?: 1 | 2
}) {
  const Heading = headingLevel === 1 ? 'h1' : 'h2'
  return (
    <div className={cn('flex animate-rise flex-col items-center px-4 py-12 text-center', className)}>
      <Illustration icon={icon} />
      <Heading className="mt-4 text-lg font-semibold tracking-tight">{title}</Heading>
      {children && <p className="mt-1.5 max-w-sm text-sm text-balance text-muted-foreground">{children}</p>}
      {action && <div className="mt-6 flex flex-wrap justify-center gap-2">{action}</div>}
    </div>
  )
}
