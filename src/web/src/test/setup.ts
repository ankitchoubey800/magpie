import '@testing-library/jest-dom/vitest'
import { cleanup } from '@testing-library/react'
import { afterEach, vi } from 'vitest'
import { resetAntiforgeryToken } from '../shared/api/apiClient.ts'

// After every test: remove rendered components, undo any fake fetch(),
// and forget the cached anti-forgery token so tests cannot affect each other.
afterEach(() => {
  cleanup()
  vi.unstubAllGlobals()
  resetAntiforgeryToken()
})
