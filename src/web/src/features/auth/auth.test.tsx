import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { startFakeServer, type FakeServer } from '../../test/fakeServer.ts'
import { renderAppAt } from '../../test/renderApp.tsx'

const asha = { id: 'reader-1', email: 'asha@example.com' }
const ping = { service: 'accounts', databaseTimeUtc: '2026-11-15T10:00:00+00:00', roundTripMs: 3, isSlow: false }

function signedOutServer(): FakeServer {
  return startFakeServer().on('GET', '/api/accounts/me', { status: 401, body: { title: 'Unauthorized' } })
}

describe('protected routes', () => {
  it('sends a signed-out visitor from the home page to sign-in', async () => {
    signedOutServer()

    renderAppAt('/')

    expect(await screen.findByRole('heading', { name: 'Sign in' })).toBeInTheDocument()
  })

  it('shows the home page to a reader who already has a session', async () => {
    startFakeServer()
      .on('GET', '/api/accounts/me', { status: 200, body: asha })
      .on('GET', '/api/accounts/ping', { status: 200, body: ping })

    renderAppAt('/')

    expect(await screen.findByText('asha@example.com')).toBeInTheDocument()
  })
})

describe('sign in', () => {
  it('signs in, then shows the home page with the reader email', async () => {
    let signedIn = false
    const server = startFakeServer()
      .on('GET', '/api/accounts/me', () =>
        signedIn ? { status: 200, body: asha } : { status: 401, body: { title: 'Unauthorized' } },
      )
      .on('POST', '/api/accounts/sign-in', () => {
        signedIn = true
        return { status: 204 }
      })
      .on('GET', '/api/accounts/ping', { status: 200, body: ping })

    renderAppAt('/sign-in')
    await userEvent.type(await screen.findByLabelText('Email'), 'asha@example.com')
    await userEvent.type(screen.getByLabelText('Password'), 'correct horse battery')
    await userEvent.click(screen.getByRole('button', { name: 'Sign in' }))

    expect(await screen.findByText('Signed in as', { exact: false })).toBeInTheDocument()
    const signInCall = server.calls.find((call) => call.path === '/api/accounts/sign-in')
    expect(signInCall?.body).toEqual({ email: 'asha@example.com', password: 'correct horse battery' })
  })

  it('shows the server message and stays on the page when sign-in fails', async () => {
    signedOutServer().on('POST', '/api/accounts/sign-in', {
      status: 401,
      body: { title: 'Sign-in failed', detail: 'Check your email and password.' },
    })

    renderAppAt('/sign-in')
    await userEvent.type(await screen.findByLabelText('Email'), 'asha@example.com')
    await userEvent.type(screen.getByLabelText('Password'), 'wrong password!')
    await userEvent.click(screen.getByRole('button', { name: 'Sign in' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Sign-in failed')
    expect(screen.getByRole('heading', { name: 'Sign in' })).toBeInTheDocument()
  })
})

describe('sign up', () => {
  it('moves to "check your email" after a successful registration', async () => {
    signedOutServer().on('POST', '/api/accounts/register', { status: 202 })

    renderAppAt('/sign-up')
    await userEvent.type(await screen.findByLabelText('Email'), 'asha@example.com')
    await userEvent.type(screen.getByLabelText('Password'), 'correct horse battery')
    await userEvent.click(screen.getByRole('button', { name: 'Create account' }))

    expect(await screen.findByRole('heading', { name: 'Check your email' })).toBeInTheDocument()
    expect(screen.getByText('asha@example.com')).toBeInTheDocument()
  })

  it('lists the validation messages the server sends back', async () => {
    signedOutServer().on('POST', '/api/accounts/register', {
      status: 400,
      body: {
        title: 'One or more validation errors occurred.',
        errors: { registration: ['Passwords must be at least 10 characters.'] },
      },
    })

    renderAppAt('/sign-up')
    await userEvent.type(await screen.findByLabelText('Email'), 'asha@example.com')
    await userEvent.type(screen.getByLabelText('Password'), 'tencharsok')
    await userEvent.click(screen.getByRole('button', { name: 'Create account' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Passwords must be at least 10 characters.')
  })
})

describe('confirm email', () => {
  it('sends the reader id and token from the link, then offers sign-in', async () => {
    const server = signedOutServer().on('POST', '/api/accounts/confirm-email', { status: 204 })

    renderAppAt('/confirm-email?readerId=reader-1&token=abc123')
    await userEvent.click(await screen.findByRole('button', { name: 'Confirm my email' }))

    expect(await screen.findByRole('heading', { name: 'Email confirmed' })).toBeInTheDocument()
    const call = server.calls.find((entry) => entry.path === '/api/accounts/confirm-email')
    expect(call?.body).toEqual({ readerId: 'reader-1', token: 'abc123' })
  })

  it('explains when the link is missing its parts', async () => {
    signedOutServer()

    renderAppAt('/confirm-email')

    expect(await screen.findByRole('heading', { name: 'This link is incomplete' })).toBeInTheDocument()
  })
})

describe('sign out', () => {
  it('returns to the sign-in page', async () => {
    startFakeServer()
      .on('GET', '/api/accounts/me', { status: 200, body: asha })
      .on('GET', '/api/accounts/ping', { status: 200, body: ping })
      .on('POST', '/api/accounts/sign-out', { status: 204 })

    renderAppAt('/')
    await userEvent.click(await screen.findByRole('button', { name: 'Sign out' }))

    expect(await screen.findByRole('heading', { name: 'Sign in' })).toBeInTheDocument()
  })
})
