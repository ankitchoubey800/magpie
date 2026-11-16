import { vi } from 'vitest'

// A tiny pretend backend for tests. You describe what each address should
// answer; anything you did not describe fails loudly, so a test can never
// pass by accident because of a call you did not expect.

type Reply = { status: number; body?: unknown; problem?: boolean }
type Handler = Reply | ((body: unknown) => Reply)

export interface FakeServer {
  /** Every call made, in order, for assertions. */
  calls: { method: string; path: string; body: unknown; headers: Record<string, string> }[]
  on(method: 'GET' | 'POST' | 'PUT', path: string, handler: Handler): FakeServer
}

/** A small tree in the same shape the real server returns. */
export const sampleCategories = [
  {
    id: 1,
    name: 'Technology',
    topics: [
      { id: 101, slug: 'programming', name: 'Programming' },
      { id: 102, slug: 'ai-machine-learning', name: 'AI & Machine Learning' },
      { id: 103, slug: 'cloud-devops', name: 'Cloud & DevOps' },
      { id: 104, slug: 'security', name: 'Security' },
    ],
  },
  {
    id: 2,
    name: 'Science',
    topics: [
      { id: 201, slug: 'space', name: 'Space' },
      { id: 202, slug: 'physics', name: 'Physics' },
    ],
  },
]

export function startFakeServer(): FakeServer {
  const routes = new Map<string, Handler>()
  const server: FakeServer = {
    calls: [],
    on(method, path, handler) {
      routes.set(`${method} ${path}`, handler)
      return server
    },
  }

  // Sensible defaults that almost every test needs.
  server.on('GET', '/api/accounts/antiforgery-token', { status: 200, body: { token: 'test-token' } })
  server.on('GET', '/api/accounts/external/providers', { status: 200, body: [] })
  // A reader who has already chosen topics, so tests about other things are
  // not sent to the onboarding page. Tests about interests override these.
  server.on('GET', '/api/accounts/me/interests', { status: 200, body: { categoryIds: [101, 102, 103] } })
  server.on('GET', '/api/accounts/categories', { status: 200, body: sampleCategories })

  vi.stubGlobal(
    'fetch',
    vi.fn(async (input: string, init?: RequestInit) => {
      const method = init?.method ?? 'GET'
      const body = typeof init?.body === 'string' ? JSON.parse(init.body) : undefined
      server.calls.push({ method, path: input, body, headers: (init?.headers ?? {}) as Record<string, string> })

      const handler = routes.get(`${method} ${input}`)
      if (!handler) {
        throw new Error(`fakeServer: no reply set up for ${method} ${input}`)
      }
      const reply = typeof handler === 'function' ? handler(body) : handler
      const isError = reply.status >= 400
      return new Response(reply.body === undefined ? null : JSON.stringify(reply.body), {
        status: reply.status,
        headers: { 'content-type': isError ? 'application/problem+json' : 'application/json' },
      })
    }),
  )

  return server
}
