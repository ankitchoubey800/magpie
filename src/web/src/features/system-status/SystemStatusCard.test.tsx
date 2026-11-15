import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { SystemStatusCard } from './SystemStatusCard.tsx'

const ping = {
  service: 'accounts',
  databaseTimeUtc: '2026-11-15T10:00:00+00:00',
  roundTripMs: 3,
  isSlow: false,
}

function okResponse(): Response {
  return new Response(JSON.stringify(ping), { status: 200, headers: { 'content-type': 'application/json' } })
}

function unavailableResponse(): Response {
  const problem = {
    title: 'A dependency is unavailable',
    detail: 'The accounts service cannot reach postgresql right now.',
    status: 503,
    service: 'accounts',
    traceId: 'trace-123',
  }
  return new Response(JSON.stringify(problem), {
    status: 503,
    headers: { 'content-type': 'application/problem+json' },
  })
}

describe('SystemStatusCard', () => {
  it('shows the value read from the database', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(okResponse()))

    render(<SystemStatusCard />)

    expect(screen.getByText(/asking the database/i)).toBeInTheDocument()
    expect(await screen.findByText('2026-11-15T10:00:00+00:00')).toBeInTheDocument()
    expect(screen.getByText('accounts')).toBeInTheDocument()
  })

  it('shows the Problem Details title, service and trace id when the call fails', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(unavailableResponse()))

    render(<SystemStatusCard />)

    const alert = await screen.findByRole('alert')
    expect(alert).toHaveTextContent('A dependency is unavailable')
    expect(alert).toHaveTextContent('service: accounts')
    expect(alert).toHaveTextContent('trace: trace-123')
  })

  it('recovers when "Try again" is pressed and the service is back', async () => {
    const fetchMock = vi.fn().mockResolvedValueOnce(unavailableResponse()).mockResolvedValueOnce(okResponse())
    vi.stubGlobal('fetch', fetchMock)

    render(<SystemStatusCard />)
    await userEvent.click(await screen.findByRole('button', { name: /try again/i }))

    expect(await screen.findByText('All connected')).toBeInTheDocument()
    expect(fetchMock).toHaveBeenCalledTimes(2)
  })
})
