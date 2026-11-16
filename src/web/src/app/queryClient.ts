import { QueryClient } from '@tanstack/react-query'
import { ApiError } from '../shared/api/apiClient.ts'

/**
 * The query client is the cache for everything we fetch from the server.
 * One per browser tab; a fresh one per test.
 */
export function createQueryClient(options: { forTests?: boolean } = {}): QueryClient {
  return new QueryClient({
    defaultOptions: {
      queries: {
        // For 30 seconds a fetched answer counts as fresh: moving between
        // pages re-uses it instead of asking the server again.
        staleTime: 30_000,
        // Retry a failed fetch twice, but never when the server gave a clear
        // "no" (4xx): asking again will not change a 401 or a 404.
        retry: options.forTests
          ? false
          : (failureCount, error) =>
              failureCount < 2 && !(error instanceof ApiError && error.status >= 400 && error.status < 500),
      },
    },
  })
}
