import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { describe, expect, it } from 'vitest'

// Contraste WCAG 2.1 AA de los pares de tokens de los dos temas (design de refine-admin-panel-design, decisión 1): lee
// src/index.css, así un cambio de color que baje el contraste rompe la verificación.

const css = readFileSync(join(__dirname, '..', 'index.css'), 'utf8')

function block(selector: string): Record<string, string> {
  const start = css.indexOf(`${selector} {`)
  const body = css.slice(start, css.indexOf('}', start))
  return Object.fromEntries([...body.matchAll(/--([\w-]+):\s*([^;]+);/g)].map((m) => [m[1]!, m[2]!.trim()]))
}

const light = block(':root')
const themes = { claro: light, oscuro: { ...light, ...block('.dark') } }

/** Luminancia relativa de un color `oklch(L C H)` (OKLab → sRGB lineal). */
function luminance(value: string): number {
  const match = /oklch\(([\d.]+) ([\d.]+) ([\d.]+)\)/.exec(value)
  if (!match) throw new Error(`Color no soportado: ${value}`)
  const [L, C, H] = [Number(match[1]), Number(match[2]), (Number(match[3]) * Math.PI) / 180]
  const a = C * Math.cos(H)
  const b = C * Math.sin(H)
  const l = (L + 0.3963377774 * a + 0.2158037573 * b) ** 3
  const m = (L - 0.1055613458 * a - 0.0638541728 * b) ** 3
  const s = (L - 0.0894841775 * a - 1.291485548 * b) ** 3
  const clamp = (x: number) => Math.min(1, Math.max(0, x))
  const r = clamp(4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s)
  const g = clamp(-1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s)
  const bl = clamp(-0.0041960863 * l - 0.7034186147 * m + 1.707614701 * s)
  return 0.2126 * r + 0.7152 * g + 0.0722 * bl
}

function ratio(foreground: string, background: string) {
  const [a, b] = [luminance(foreground), luminance(background)].sort((x, y) => y - x)
  return (a! + 0.05) / (b! + 0.05)
}

// Texto sobre su fondo: 4.5:1.
const text: [string, string][] = [
  ['foreground', 'background'],
  ['card-foreground', 'card'],
  ['popover-foreground', 'popover'],
  ['muted-foreground', 'background'],
  ['muted-foreground', 'card'],
  ['muted-foreground', 'muted'],
  ['primary-foreground', 'primary'],
  ['primary', 'background'],
  ['primary', 'card'],
  ['secondary-foreground', 'secondary'],
  ['accent-foreground', 'accent'],
  ['destructive-foreground', 'destructive'],
  ['destructive', 'card'],
  ['success', 'card'],
  ['success-foreground', 'success'],
  ['warning', 'card'],
  ['warning-foreground', 'warning'],
  ['info', 'card'],
  ['info-foreground', 'info'],
  ['sidebar-foreground', 'sidebar'],
  ['sidebar-accent-foreground', 'sidebar-accent'],
]

// Indicador de foco contra los fondos: 3:1.
const focus: [string, string][] = [
  ['ring', 'background'],
  ['ring', 'card'],
  ['ring', 'sidebar'],
]

it('la conversión de color es correcta (blanco sobre negro 21:1, azul sRGB 8.6:1 sobre blanco)', () => {
  expect(ratio('oklch(1 0 0)', 'oklch(0 0 0)')).toBeCloseTo(21, 1)
  expect(ratio('oklch(0.452 0.313 264.05)', 'oklch(1 0 0)')).toBeCloseTo(8.59, 1)
})

describe.each(Object.entries(themes))('Contraste del tema %s', (_, tokens) => {
  it.each(text)('%s sobre %s llega a 4.5:1', (fg, bg) => {
    expect(ratio(tokens[fg]!, tokens[bg]!)).toBeGreaterThanOrEqual(4.5)
  })

  it.each(focus)('%s sobre %s llega a 3:1', (fg, bg) => {
    expect(ratio(tokens[fg]!, tokens[bg]!)).toBeGreaterThanOrEqual(3)
  })
})
