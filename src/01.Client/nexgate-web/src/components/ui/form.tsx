import * as LabelPrimitive from '@radix-ui/react-label'
import { ChevronDown } from 'lucide-react'
import * as React from 'react'
import { cn } from '@/lib/utils'

// Componentes de shadcn/ui: campo de texto, selector, etiqueta, ayuda y mensaje de error de un campo.

const field =
  'flex h-10 w-full rounded-md border border-input bg-card px-3 py-2 text-sm shadow-xs transition-[border-color,box-shadow] ' +
  'placeholder:text-muted-foreground outline-none focus-visible:border-ring focus-visible:ring-3 focus-visible:ring-ring/30 ' +
  'disabled:cursor-not-allowed disabled:opacity-50 ' +
  'aria-invalid:border-destructive aria-invalid:focus-visible:ring-destructive/30'

export function Input({ className, ...props }: React.InputHTMLAttributes<HTMLInputElement>) {
  return <input className={cn(field, className)} {...props} />
}

export function Select({ className, ...props }: React.SelectHTMLAttributes<HTMLSelectElement>) {
  return (
    <div className="relative">
      <select className={cn(field, 'appearance-none pr-9', className)} {...props} />
      <ChevronDown className="pointer-events-none absolute top-1/2 right-3 size-4 -translate-y-1/2 text-muted-foreground" aria-hidden="true" />
    </div>
  )
}

export function Label({ className, ...props }: React.ComponentProps<typeof LabelPrimitive.Root>) {
  return <LabelPrimitive.Root className={cn('text-sm font-medium leading-none', className)} {...props} />
}

export function FieldHint({ className, ...props }: React.HTMLAttributes<HTMLParagraphElement>) {
  return <p className={cn('text-xs text-muted-foreground', className)} {...props} />
}

export function FieldError({ id, message }: { id: string; message?: string }) {
  if (!message) {
    return null
  }

  return (
    <p id={id} role="alert" className="animate-fade-in text-sm text-destructive">
      {message}
    </p>
  )
}
