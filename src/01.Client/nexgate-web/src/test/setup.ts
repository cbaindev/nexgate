import '@testing-library/jest-dom/vitest'
import { cleanup, configure } from '@testing-library/react'
import { setupServer } from 'msw/node'
import { afterAll, afterEach, beforeAll, beforeEach } from 'vitest'
import '@/i18n'
import { resetSessionForTests } from '@/session/session'
import { FakeApi } from './fakeApi'

// Con todas las pruebas en paralelo, la primera de cada archivo tarda más que el segundo por defecto en montar todo
// (y en cargar su sección, que es lazy).
configure({ asyncUtilTimeout: 5000 })

export let fake = new FakeApi()
export const server = setupServer()

beforeAll(() => server.listen({ onUnhandledRequest: 'error' }))

beforeEach(() => {
  fake = new FakeApi()
  server.resetHandlers(...fake.handlers())
  resetSessionForTests()
})

afterEach(() => {
  cleanup()
  resetSessionForTests()
})

afterAll(() => server.close())
