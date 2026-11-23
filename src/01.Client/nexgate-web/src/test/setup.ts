import '@testing-library/jest-dom/vitest'
import { cleanup } from '@testing-library/react'
import { setupServer } from 'msw/node'
import { afterAll, afterEach, beforeAll, beforeEach } from 'vitest'
import '@/i18n'
import { resetSessionForTests } from '@/session/session'
import { FakeApi } from './fakeApi'

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
