import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router'
import { describe, expect, it } from 'vitest'
import App from '../../app/App.tsx'
import { startFakeServer, type FakeServer } from '../../test/fakeServer.ts'
import { AuthProvider } from './AuthProvider.tsx'

const asha = { id: 'reader-1', email: 'asha@example.com' }
const ping = { service: 'accounts', databaseTimeUtc: '2026-11-15T10:00:00+00:00', roundTripMs: 3, isSlow: false }

function renderAppAt(address: string) {
  render(
    <MemoryRouter initialEntries={[address]}>
      <AuthProvider>
        <App />
      </AuthProvider>
    </MemoryRouter>,
  )
}

function signedOutServer(): FakeServer {
  return startFakeServer().on('GET', '/api/accounts/me', { status: 401, body: { title: 'Unauthorized' } })
}

describe('forgot password', () => {
  it('asks the server for a reset link and shows the same message for any address', async () => {
    const server = signedOutServer().on('POST', '/api/accounts/forgot-password', { status: 202 })

    renderAppAt('/forgot-password')
    await userEvent.type(await screen.findByLabelText('Email'), 'asha@example.com')
    await userEvent.click(screen.getByRole('button', { name: 'Send reset link' }))

    expect(await screen.findByRole('heading', { name: 'Check your email' })).toBeInTheDocument()
    expect(screen.getByText(/if/i)).toHaveTextContent('If asha@example.com has a Magpie account')
    expect(server.calls.find((call) => call.path === '/api/accounts/forgot-password')?.body).toEqual({
      email: 'asha@example.com',
    })
  })

  it('tells the reader to slow down when the server limits attempts', async () => {
    signedOutServer().on('POST', '/api/accounts/forgot-password', {
      status: 429,
      body: { title: 'Too many attempts', detail: 'Please wait a minute before trying again.', code: 'rate_limited' },
    })

    renderAppAt('/forgot-password')
    await userEvent.type(await screen.findByLabelText('Email'), 'asha@example.com')
    await userEvent.click(screen.getByRole('button', { name: 'Send reset link' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Too many attempts')
  })
})

describe('reset password', () => {
  it('sends the reader id and token from the link together with the new password', async () => {
    const server = signedOutServer().on('POST', '/api/accounts/reset-password', { status: 204 })

    renderAppAt('/reset-password?readerId=reader-1&token=abc123')
    await userEvent.type(await screen.findByLabelText('New password'), 'brand new staple gun')
    await userEvent.click(screen.getByRole('button', { name: 'Save new password' }))

    expect(await screen.findByRole('heading', { name: 'Password changed' })).toBeInTheDocument()
    expect(server.calls.find((call) => call.path === '/api/accounts/reset-password')?.body).toEqual({
      readerId: 'reader-1',
      token: 'abc123',
      newPassword: 'brand new staple gun',
    })
  })

  it('shows the server message when the link was already used', async () => {
    signedOutServer().on('POST', '/api/accounts/reset-password', {
      status: 400,
      body: { title: 'This reset link does not work', detail: 'It may have expired or already been used.' },
    })

    renderAppAt('/reset-password?readerId=reader-1&token=abc123')
    await userEvent.type(await screen.findByLabelText('New password'), 'brand new staple gun')
    await userEvent.click(screen.getByRole('button', { name: 'Save new password' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('This reset link does not work')
  })

  it('explains when the link is missing its parts', async () => {
    signedOutServer()

    renderAppAt('/reset-password')

    expect(await screen.findByRole('heading', { name: 'This link is incomplete' })).toBeInTheDocument()
  })
})

describe('continue with Google', () => {
  it('shows no Google button when the provider is switched off', async () => {
    signedOutServer()

    renderAppAt('/sign-in')

    expect(await screen.findByRole('heading', { name: 'Sign in' })).toBeInTheDocument()
    expect(screen.queryByRole('link', { name: /continue with/i })).not.toBeInTheDocument()
  })

  it('shows a link that leaves the app for the provider when it is switched on', async () => {
    signedOutServer().on('GET', '/api/accounts/external/providers', { status: 200, body: ['Google'] })

    renderAppAt('/sign-in')

    const link = await screen.findByRole('link', { name: 'Continue with Google' })
    expect(link).toHaveAttribute('href', '/api/accounts/external/Google/start?returnUrl=%2F')
  })

  it('links the waiting Google identity after the reader proves their password', async () => {
    let signedIn = false
    const server = startFakeServer()
      .on('GET', '/api/accounts/me', () =>
        signedIn ? { status: 200, body: asha } : { status: 401, body: { title: 'Unauthorized' } },
      )
      .on('POST', '/api/accounts/sign-in', () => {
        signedIn = true
        return { status: 204 }
      })
      .on('POST', '/api/accounts/external/link', { status: 204 })
      .on('GET', '/api/accounts/ping', { status: 200, body: ping })

    renderAppAt('/sign-in?link=pending')
    expect(await screen.findByRole('status')).toHaveTextContent('already exists')
    await userEvent.type(screen.getByLabelText('Email'), 'asha@example.com')
    await userEvent.type(screen.getByLabelText('Password'), 'correct horse battery')
    await userEvent.click(screen.getByRole('button', { name: 'Sign in' }))

    expect(await screen.findByText('Signed in as', { exact: false })).toBeInTheDocument()
    const paths = server.calls.map((call) => call.path)
    expect(paths.indexOf('/api/accounts/external/link')).toBeGreaterThan(paths.indexOf('/api/accounts/sign-in'))
  })

  it('explains when the provider sign-in failed', async () => {
    signedOutServer()

    renderAppAt('/sign-in?error=external')

    expect(await screen.findByRole('alert')).toHaveTextContent('did not work')
  })
})
