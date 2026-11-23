import { cn } from '@/lib/utils'

/** Iniciales de un correo o un nombre: "ana.perez@dominio" da "AP" y "Torre Alvear" da "TA". */
export function initials(text: string) {
  const base = text.includes('@') ? text.split('@')[0]! : text
  const words = base.split(/[\s._-]+/).filter(Boolean)
  const letters = words.length > 1 ? words[0]!.charAt(0) + words[1]!.charAt(0) : base.slice(0, 2)
  return letters.toUpperCase()
}

// Avatar con iniciales: sin fotos ni servicios externos (proposal de refine-admin-panel-design, privacidad).
export function Avatar({ text, className }: { text: string; className?: string }) {
  return (
    <span
      aria-hidden="true"
      className={cn(
        'inline-flex size-8 shrink-0 items-center justify-center rounded-full bg-accent text-xs font-semibold text-accent-foreground ring-1 ring-primary/15',
        className,
      )}
    >
      {initials(text)}
    </span>
  )
}
