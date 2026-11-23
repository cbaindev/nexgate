import { cva, type VariantProps } from 'class-variance-authority'
import * as React from 'react'
import { cn } from '@/lib/utils'

// Insignia (sistema de diseño de Nexgate): tipo, estado o etiqueta corta.
const badgeVariants = cva('inline-flex items-center gap-1 rounded-full border px-2 py-0.5 text-xs font-medium whitespace-nowrap [&_svg]:size-3', {
  variants: {
    variant: {
      default: 'border-transparent bg-accent text-accent-foreground',
      secondary: 'border-transparent bg-secondary text-secondary-foreground',
      outline: 'text-muted-foreground',
      success: 'border-success/25 bg-success/10 text-success',
      warning: 'border-warning/25 bg-warning/10 text-warning',
      info: 'border-info/25 bg-info/10 text-info',
      destructive: 'border-destructive/25 bg-destructive/10 text-destructive',
    },
  },
  defaultVariants: { variant: 'default' },
})

export function Badge({ className, variant, ...props }: React.HTMLAttributes<HTMLSpanElement> & VariantProps<typeof badgeVariants>) {
  return <span className={cn(badgeVariants({ variant }), className)} {...props} />
}
