import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { QrCode } from '@/components/ui/qr-code'
import { relativeTime } from '@/lib/relative-time'

const now = new Date('2026-11-23T12:00:00Z')

describe('Tiempo relativo', () => {
  it.each([
    ['2026-11-23T11:59:30Z', 'hace instantes'],
    ['2026-11-23T11:57:00Z', 'hace 3 minutos'],
    ['2026-11-23T10:00:00Z', 'hace 2 horas'],
    ['2026-11-22T12:00:00Z', 'ayer'],
    ['2026-11-18T12:00:00Z', 'hace 5 días'],
    ['2026-12-03T12:00:00Z', 'dentro de 10 días'],
  ])('%s → %s', (iso, text) => {
    expect(relativeTime(iso, now)).toBe(text)
  })
})

describe('Código QR', () => {
  it('dibuja una matriz cuadrada con nombre accesible', () => {
    render(<QrCode value="ABCD-1234-EFGH" label="Código QR del código de reclamo" />)

    const svg = screen.getByRole('img', { name: 'Código QR del código de reclamo' })
    const [, , width, height] = svg.getAttribute('viewBox')!.split(' ').map(Number)
    expect(width).toBe(height)
    expect(width).toBeGreaterThanOrEqual(21)
    expect(svg.querySelector('path')!.getAttribute('d')!.length).toBeGreaterThan(100)
  })
})
