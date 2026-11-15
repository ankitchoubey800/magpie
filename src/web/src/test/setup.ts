import '@testing-library/jest-dom/vitest'
import { cleanup } from '@testing-library/react'
import { afterEach, vi } from 'vitest'

// After every test: remove rendered components and undo any fake fetch().
afterEach(() => {
  cleanup()
  vi.unstubAllGlobals()
})
