import { describe, expect, it, vi } from 'vitest'
import { ApiError, apiGet } from './apiClient.ts'

function jsonResponse(body: unknown, status: number, contentType: string): Response {
  return new Response(JSON.stringify(body), { status, headers: { 'content-type': contentType } })
}

describe('apiGet', () => {
  it('calls the gateway under /api and returns the parsed body', async () => {
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse({ hello: 'magpie' }, 200, 'application/json'))
    vi.stubGlobal('fetch', fetchMock)

    const result = await apiGet<{ hello: string }>('/accounts/ping')

    expect(result).toEqual({ hello: 'magpie' })
    expect(fetchMock.mock.calls[0][0]).toBe('/api/accounts/ping')
  })

  it('turns a Problem Details answer into an ApiError that carries it', async () => {
    const problem = { title: 'A dependency is unavailable', status: 503, service: 'accounts', traceId: 'abc' }
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(jsonResponse(problem, 503, 'application/problem+json')))

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
