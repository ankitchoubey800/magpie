import { QueryClientProvider } from '@tanstack/react-query'
import { render } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import App from '../app/App.tsx'
import { createQueryClient } from '../app/queryClient.ts'
import { AuthProvider } from '../features/auth/AuthProvider.tsx'

/**
 * Renders the whole app at a given address, with the same providers as
 * main.tsx but a test router and a fresh, empty cache for every test.
 */
export function renderAppAt(address: string) {
  return render(
    <QueryClientProvider client={createQueryClient({ forTests: true })}>
      <MemoryRouter initialEntries={[address]}>
        <AuthProvider>
          <App />
        </AuthProvider>
      </MemoryRouter>
    </QueryClientProvider>,
  )
}
