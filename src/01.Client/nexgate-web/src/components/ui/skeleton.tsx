import * as React from 'react'
import { cn } from '@/lib/utils'

// Esqueleto de carga: un bloque con la forma del contenido que todavía no llegó. Es decorativo: quien lo usa anuncia la
// carga a las tecnologías de asistencia (aria-busy y un texto oculto).
export function Skeleton({ className, ...props }: React.HTMLAttributes<HTMLDivElement>) {
  return <div aria-hidden="true" className={cn('animate-pulse rounded-md bg-muted', className)} {...props} />
}
