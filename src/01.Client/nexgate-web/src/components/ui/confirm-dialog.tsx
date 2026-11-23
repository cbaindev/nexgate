import * as DialogPrimitive from '@radix-ui/react-dialog'
import type { ReactNode } from 'react'
import { useRef } from 'react'
import { Alert } from './alert'
import { Button } from './button'

/**
 * Confirmación de una acción que no se puede deshacer (design de add-spaces-screens, decisión 6): `role="alertdialog"`,
 * el foco empieza en "Cancelar" y, si el servidor rechaza, el mensaje queda en el diálogo sin cerrarlo.
 */
export function ConfirmDialog({
  open,
  onOpenChange,
  title,
  description,
  confirmLabel,
  cancelLabel,
  pending = false,
  error,
  onConfirm,
}: {
  open: boolean
  onOpenChange: (open: boolean) => void
  title: string
  description: ReactNode
  confirmLabel: string
  cancelLabel: string
  pending?: boolean
  error?: string | null
  onConfirm: () => void
}) {
  const cancel = useRef<HTMLButtonElement>(null)
  return (
    <DialogPrimitive.Root open={open} onOpenChange={onOpenChange}>
      <DialogPrimitive.Portal>
        <DialogPrimitive.Overlay className="fixed inset-0 z-50 bg-foreground/25 backdrop-blur-[2px] data-[state=closed]:animate-fade-out data-[state=open]:animate-fade-in dark:bg-black/60" />
        <DialogPrimitive.Content
          role="alertdialog"
          onOpenAutoFocus={(event) => {
            event.preventDefault()
            cancel.current?.focus()
          }}
          className="fixed top-1/2 left-1/2 z-50 w-[calc(100%-2rem)] max-w-md -translate-x-1/2 -translate-y-1/2 rounded-xl border bg-card p-6 text-card-foreground shadow-lg outline-none data-[state=closed]:animate-exit data-[state=open]:animate-enter"
        >
          <DialogPrimitive.Title className="text-lg font-semibold tracking-tight">{title}</DialogPrimitive.Title>
          <DialogPrimitive.Description className="mt-2 text-sm text-muted-foreground">{description}</DialogPrimitive.Description>
          {error && (
            <Alert variant="destructive" className="mt-4">
              {error}
            </Alert>
          )}
          <div className="mt-6 flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
            <DialogPrimitive.Close asChild>
              <Button ref={cancel} variant="outline">
                {cancelLabel}
              </Button>
            </DialogPrimitive.Close>
            <Button variant="destructive" loading={pending} onClick={onConfirm}>
              {confirmLabel}
            </Button>
          </div>
        </DialogPrimitive.Content>
      </DialogPrimitive.Portal>
    </DialogPrimitive.Root>
  )
}
