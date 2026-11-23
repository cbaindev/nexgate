/** "hace 3 minutos", "hace 2 horas", "en 5 días": la distancia entre un instante y ahora, en español. */
const format = new Intl.RelativeTimeFormat('es', { numeric: 'auto' })

const units: [Intl.RelativeTimeFormatUnit, number][] = [
  ['year', 365 * 24 * 3600],
  ['month', 30 * 24 * 3600],
  ['day', 24 * 3600],
  ['hour', 3600],
  ['minute', 60],
]

export function relativeTime(iso: string, now: Date = new Date()): string {
  const seconds = Math.round((Date.parse(iso) - now.getTime()) / 1000)
  const abs = Math.abs(seconds)
  if (abs < 60) {
    return seconds <= 0 ? 'hace instantes' : 'en instantes'
  }

  const [unit, size] = units.find(([, s]) => abs >= s)!
  return format.format(Math.trunc(seconds / size), unit)
}

/** Minutos desde un instante hasta ahora (negativo si es futuro). */
export function minutesSince(iso: string, now: Date = new Date()): number {
  return (now.getTime() - Date.parse(iso)) / 60000
}
