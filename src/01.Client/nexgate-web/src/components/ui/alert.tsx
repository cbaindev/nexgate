import { cva, type VariantProps } from 'class-variance-authority'
import { CircleAlert, CircleCheck, Info, TriangleAlert } from 'lucide-react'
import * as React from 'react'
import { cn } from '@/lib/utils'

// Aviso con ícono según su tipo (sistema de diseño de Nexgate).
const alertVariants = cva(
  'flex animate-fade-in items-start gap-3 rounded-lg border px-4 py-3 text-sm [&>svg]:mt-0.5 [&>svg]:size-4 [&>svg]:shrink-0',
  {
    variants: {
      variant: {
        default: 'bg-muted/60 text-foreground [&>svg]:text-muted-foreground',
        info: 'border-info/30 bg-info/8 text-foreground [&>svg]:text-info',
        success: 'border-success/30 bg-success/8 text-foreground [&>svg]:text-success',
        warning: 'border-warning/35 bg-warning/10 text-foreground [&>svg]:text-warning',
        destructive: 'border-destructive/35 bg-destructive/8 text-destructive [&>svg]:text-destructive',
      },
    },
    defaultVariants: { variant: 'default' },
  },
)

const icons = { default: Info, info: Info, success: CircleCheck, warning: TriangleAlert, destructive: CircleAlert }

export function Alert({ className, variant, children, ...props }: React.HTMLAttributes<HTMLDivElement> & VariantProps<typeof alertVariants>) {
  const Icon = icons[variant ?? 'default']
  return (
    <div role="alert" className={cn(alertVariants({ variant }), className)} {...props}>
      <Icon aria-hidden="true" />
      <div className="flex min-w-0 flex-1 flex-wrap items-center gap-x-3 gap-y-2">{children}</div>
    </div>
  )
}
