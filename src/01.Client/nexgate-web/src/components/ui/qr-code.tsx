import { useMemo } from 'react'
import { encode } from 'uqr'
import { cn } from '@/lib/utils'

/**
 * Código QR dibujado como SVG (design de add-device-and-event-screens, decisión 4): `uqr` da la matriz y acá se arma un
 * camino por fila. Siempre oscuro sobre claro, también en el tema oscuro, porque los lectores lo necesitan así.
 */
export function QrCode({ value, label, className }: { value: string; label: string; className?: string }) {
  const { size, path } = useMemo(() => {
    const { data, size } = encode(value, { ecc: 'M', border: 2 })
    let d = ''
    data.forEach((row, y) => {
      row.forEach((dark, x) => {
        if (dark) d += `M${x} ${y}h1v1h-1z`
      })
    })
    return { size, path: d }
  }, [value])

  return (
    <div className={cn('inline-flex rounded-lg bg-white p-2 text-black shadow-xs ring-1 ring-black/10', className)}>
      <svg role="img" aria-label={label} viewBox={`0 0 ${size} ${size}`} className="size-full" shapeRendering="crispEdges">
        <path d={path} fill="currentColor" />
      </svg>
    </div>
  )
}
