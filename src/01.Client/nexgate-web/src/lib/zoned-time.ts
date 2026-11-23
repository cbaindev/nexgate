/**
 * Fechas y horas en la zona horaria del sitio, no en la del navegador (design de add-access-screens, decisión 3). Solo
 * `Intl`, sin bibliotecas: dos conversiones y un formato.
 */

const partsFormatters = new Map<string, Intl.DateTimeFormat>()

function partsIn(instant: number, timeZone: string) {
  let formatter = partsFormatters.get(timeZone)
  if (!formatter) {
    formatter = new Intl.DateTimeFormat('en-US', {
      timeZone,
      hourCycle: 'h23',
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit',
      weekday: 'long',
    })
    partsFormatters.set(timeZone, formatter)
  }

  const parts = formatter.formatToParts(new Date(instant))
  const get = (type: Intl.DateTimeFormatPartTypes) => parts.find((p) => p.type === type)!.value
  return {
    year: Number(get('year')),
    month: Number(get('month')),
    day: Number(get('day')),
    hour: Number(get('hour')),
    minute: Number(get('minute')),
    second: Number(get('second')),
    weekday: get('weekday').toLowerCase(),
  }
}

/** Minutos que la zona está adelantada respecto de UTC en ese instante (Córdoba: -180). */
function offsetMinutes(instant: number, timeZone: string) {
  const p = partsIn(instant, timeZone)
  const asUtc = Date.UTC(p.year, p.month - 1, p.day, p.hour, p.minute, p.second)
  return Math.round((asUtc - Math.floor(instant / 1000) * 1000) / 60000)
}

/**
 * De la hora del sitio (`YYYY-MM-DDTHH:mm`, el valor de un `datetime-local`) a un instante ISO en UTC. Corrige una vez
 * el desplazamiento por si un cambio de horario cae entre la estimación y el resultado.
 */
export function toSiteInstant(local: string, timeZone: string): string {
  const match = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})/.exec(local)
  if (!match) {
    throw new Error('Fecha y hora inválidas')
  }

  const [, y, m, d, h, min] = match.map(Number)
  const guess = Date.UTC(y!, m! - 1, d!, h!, min!)
  const first = offsetMinutes(guess, timeZone)
  let instant = guess - first * 60000
  const second = offsetMinutes(instant, timeZone)
  if (second !== first) {
    instant = guess - second * 60000
  }

  return new Date(instant).toISOString().replace('.000Z', 'Z')
}

const pad = (n: number) => String(n).padStart(2, '0')

/** De un instante ISO a la hora del sitio, como valor de un `datetime-local`. */
export function toSiteLocal(iso: string, timeZone: string): string {
  const p = partsIn(Date.parse(iso), timeZone)
  return `${p.year}-${pad(p.month)}-${pad(p.day)}T${pad(p.hour)}:${pad(p.minute)}`
}

/** "01/12/2026 08:00" en la hora del sitio; con `seconds`, "01/12/2026 08:00:15". */
export function formatSiteDateTime(iso: string, timeZone: string, options: { seconds?: boolean } = {}): string {
  const p = partsIn(Date.parse(iso), timeZone)
  const time = `${pad(p.hour)}:${pad(p.minute)}${options.seconds ? `:${pad(p.second)}` : ''}`
  return `${pad(p.day)}/${pad(p.month)}/${p.year} ${time}`
}

/** Día de la semana (`monday` … `sunday`) y minuto del día en la hora del sitio. */
export function siteDayAndMinute(iso: string, timeZone: string): { day: string; minute: number } {
  const p = partsIn(Date.parse(iso), timeZone)
  return { day: p.weekday, minute: p.hour * 60 + p.minute }
}
