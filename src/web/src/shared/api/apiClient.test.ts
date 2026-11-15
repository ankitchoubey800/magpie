import { describe, expect, it, vi } from 'vitest'
import { startFakeServer } from '../../test/fakeServer.ts'
import { ApiError, apiGet, apiPost } from './apiClient.ts'

describe('apiGet', () => {
  it('calls the gateway under /api and returns the parsed body', async () => {
    const server = startFakeServer().on('GET', '/api/accounts/ping', { status: 200, body: { hello: 'magpie' } })

    const result = await apiGet<{ hello: string }>('/accounts/ping')

    expect(result).toEqual({ hello: 'magpie' })
    expect(server.calls[0].path).toBe('/api/accounts/ping')
  })

  it('turns a Problem Details answer into an ApiError that carries it', async () => {
    const problem = { title: 'A dependency is unavailable', status: 503, service: 'accounts', traceId: 'abc' }
    startFakeServer().on('GET', '/api/accounts/ping', { status: 503, body: problem })

    const error = await apiGet('/accounts/ping').catch((e: unknown) => e)

    expect(error).toBeInstanceOf(ApiError)
    expect((error as ApiError).status).toBe(503)
    expect((error as ApiError).problem).toEqual(problem)
  })

  it('still produces a readable ApiError when the failure is not Problem Details', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response('', { status: 502 })))

    const error = (await apiGet('/accounts/ping').catch((e: unknown) => e)) as ApiError

    expect(error).toBeInstanceOf(ApiError)
    expect(error.status).toBe(502)
    expect(error.problem.title).toBe('Something went wrong')
  })

  it('reports status 0 when the network call itself fails', async () => {
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new TypeError('Failed to fetch')))

    const error = (await apiGet('/accounts/ping').catch((e: unknown) => e)) as ApiError

    expect(error.status).toBe(0)
    expect(error.problem.title).toBe('Cannot reach Magpie')
  })
})

describe('apiPost', () => {
  it('fetches an anti-forgery token first and sends it in the X-XSRF-TOKEN header', async () => {
    const server = startFakeServer().on('POST', '/api/accounts/sign-out', { status: 204 })

    await apiPost('/accounts/sign-out')

    expect(server.calls.map((call) => `${call.method} ${call.path}`)).toEqual([
      'GET /api/accounts/antiforgery-token',
      'POST /api/accounts/sign-out',
    ])
    expect(server.calls[1].headers['X-XSRF-TOKEN']).toBe('test-token')
  })

  it('reuses the token for the next request instead of fetching it again', async () => {
    const server = startFakeServer().on('POST', '/api/accounts/sign-out', { status: 204 })

    await apiPost('/accounts/sign-out')
    await apiPost('/accounts/sign-out')

    expect(server.calls.filter((call) => call.path.endsWith('/antiforgery-token'))).toHaveLength(1)
  })

  it('gets a fresh token and retries once when the server says the token is stale', async () => {
    let attempts = 0
    const server = startFakeServer().on('POST', '/api/accounts/sign-out', () => {
      attempts += 1
      return attempts === 1
        ? { status: 400, body: { title: 'Security check failed', code: 'antiforgery' } }
        : { status: 204 }
    })

    await apiPost('/accounts/sign-out')

    expect(attempts).toBe(2)
    expect(server.calls.filter((call) => call.path.endsWith('/antiforgery-token'))).toHaveLength(2)
  })

  it('does not retry other errors', async () => {
    let attempts = 0
    startFakeServer().on('POST', '/api/accounts/sign-in', () => {
      attempts += 1
      return { status: 401, body: { title: 'Sign-in failed' } }
    })

    const error = (await apiPost('/accounts/sign-in', {}).catch((e: unknown) => e)) as ApiError

    expect(error.status).toBe(401)
    expect(attempts).toBe(1)
  })
})
