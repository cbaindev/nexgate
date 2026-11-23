import { readdirSync, readFileSync, statSync } from 'node:fs'
import { join } from 'node:path'
import { describe, expect, it } from 'vitest'
import { ApiError, describeError } from '@/api/errors'
import i18n from '@/i18n'
import es from '@/i18n/es.json'

const root = join(__dirname, '..')

function sources(directory: string): string[] {
  return readdirSync(directory).flatMap((name) => {
    const path = join(directory, name)
    if (statSync(path).isDirectory()) {
      return name === 'test' ? [] : sources(path)
    }

    return /\.(ts|tsx)$/.test(name) && !name.endsWith('.d.ts') ? [path] : []
  })
}

function has(key: string) {
  return key.split('.').reduce<unknown>((node, part) => (node as Record<string, unknown> | undefined)?.[part], es) !== undefined
}

describe('Catálogo de textos', () => {
  it('tiene todas las claves que usan las pantallas', () => {
    const used = new Set<string>()
    for (const file of sources(root)) {
      const text = readFileSync(file, 'utf8')
      for (const match of text.matchAll(/\bt\(\s*['"]([\w.]+)['"]/g)) used.add(match[1]!)
      for (const match of text.matchAll(/(?:labelKey|descriptionKey):\s*'([\w.]+)'/g)) used.add(match[1]!)
    }

    expect(used.size).toBeGreaterThan(20)
    expect([...used].filter((key) => !has(key))).toEqual([])
  })

  it('tiene el nombre de cada tipo de sitio', () => {
    for (const type of ['building', 'gated_community', 'company', 'house']) {
      expect(has(`sites.types.${type}`)).toBe(true)
    }
  })
})

describe('Traductor de errores', () => {
  const t = i18n.t.bind(i18n)

  it.each([
    [new ApiError(0), 'Sin conexión con el servidor.'],
    [new ApiError(403), 'No tenés permiso para esta operación.'],
    [new ApiError(404), 'No encontramos lo que buscabas.'],
    [new ApiError(409, { title: 'Ya hay un sitio con ese nombre.' }), 'Ya hay un sitio con ese nombre.'],
    [new ApiError(429), 'Demasiados intentos. Probá de nuevo en un minuto.'],
    [new ApiError(500, { correlationId: 'abc' }), 'Ocurrió un error inesperado.'],
    [new ApiError(400, { errors: { Name: ['x'] } }), 'Revisá los datos marcados.'],
    [new Error('otra cosa'), 'Ocurrió un error inesperado.'],
  ])('%#', (error, message) => {
    expect(describeError(error, t)).toBe(message)
  })

  it('encuentra el mensaje de un campo sin importar la capitalización', () => {
    const error = new ApiError(400, { errors: { TimeZone: ['Zona horaria inválida.'] } })

    expect(error.fieldError('timeZone')).toBe('Zona horaria inválida.')
    expect(error.fieldError('name')).toBeUndefined()
  })
})
