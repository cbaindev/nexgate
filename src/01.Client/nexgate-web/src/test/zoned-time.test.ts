import { describe, expect, it } from 'vitest'
import { formatSiteDateTime, siteDayAndMinute, toSiteInstant, toSiteLocal } from '@/lib/zoned-time'

// vitest corre con TZ=Asia/Tokyo (vite.config.ts): nada de esto puede depender de la zona del proceso.
describe('Hora del sitio', () => {
  it('las pruebas corren en una zona distinta a la de los sitios', () => {
    expect(new Date('2026-12-01T00:00:00Z').getTimezoneOffset()).toBe(-540)
  })

  it('convierte la hora de Córdoba a UTC y vuelve', () => {
    expect(toSiteInstant('2026-12-01T08:00', 'America/Argentina/Cordoba')).toBe('2026-12-01T11:00:00Z')
    expect(toSiteLocal('2026-12-01T11:00:00Z', 'America/Argentina/Cordoba')).toBe('2026-12-01T08:00')
    expect(formatSiteDateTime('2026-12-01T11:00:00Z', 'America/Argentina/Cordoba')).toBe('01/12/2026 08:00')
  })

  it('respeta el cambio de horario de la zona del sitio', () => {
    // Santiago de Chile: UTC-3 en verano (septiembre a abril) y UTC-4 en invierno.
    expect(toSiteInstant('2026-01-15T09:00', 'America/Santiago')).toBe('2026-01-15T12:00:00Z')
    expect(toSiteInstant('2026-07-15T09:00', 'America/Santiago')).toBe('2026-07-15T13:00:00Z')
    expect(toSiteLocal('2026-07-15T13:00:00Z', 'America/Santiago')).toBe('2026-07-15T09:00')
  })

  it('pasa medianoche al día anterior cuando corresponde', () => {
    expect(toSiteLocal('2026-12-01T02:30:00Z', 'America/Argentina/Cordoba')).toBe('2026-11-30T23:30')
    expect(siteDayAndMinute('2026-12-01T02:30:00Z', 'America/Argentina/Cordoba')).toEqual({ day: 'monday', minute: 23 * 60 + 30 })
  })

  it('rechaza un valor que no es fecha y hora', () => {
    expect(() => toSiteInstant('mañana', 'UTC')).toThrow()
  })
})
