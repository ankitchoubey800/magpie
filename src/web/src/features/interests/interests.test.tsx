import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { startFakeServer } from '../../test/fakeServer.ts'
import { renderAppAt } from '../../test/renderApp.tsx'

const asha = { id: 'reader-1', email: 'asha@example.com' }
const ping = { service: 'accounts', databaseTimeUtc: '2026-11-15T10:00:00+00:00', roundTripMs: 3, isSlow: false }

/** A pretend server that remembers what was saved, like the real one. */
function signedInServer(savedTopics: number[]) {
  let saved = savedTopics
  return startFakeServer()
    .on('GET', '/api/accounts/me', { status: 200, body: asha })
    .on('GET', '/api/accounts/ping', { status: 200, body: ping })
    .on('GET', '/api/accounts/me/interests', () => ({ status: 200, body: { categoryIds: saved } }))
    .on('PUT', '/api/accounts/me/interests', (body) => {
      saved = (body as { categoryIds: number[] }).categoryIds
      return { status: 204 }
    })
}

describe('onboarding', () => {
  it('sends a reader with no topics from the home page to onboarding', async () => {
    signedInServer([])

    renderAppAt('/')

    expect(await screen.findByRole('heading', { name: 'What are you interested in?' })).toBeInTheDocument()
  })

  it('keeps the button disabled until three topics are ticked, then saves and goes home', async () => {
    const server = signedInServer([])

    renderAppAt('/onboarding')
    const button = await screen.findByRole('button', { name: 'Continue' })
    expect(button).toBeDisabled()
    expect(screen.getByRole('status')).toHaveTextContent('Choose 3 more')

    await userEvent.click(screen.getByLabelText('Programming'))
    await userEvent.click(screen.getByLabelText('Space'))
    expect(button).toBeDisabled()
    await userEvent.click(screen.getByLabelText('Security'))
    expect(button).toBeEnabled()
    await userEvent.click(button)

    expect(await screen.findByText('You follow 3 topics.', { exact: false })).toBeInTheDocument()
    const put = server.calls.find((call) => call.method === 'PUT')
    expect([...(put!.body as { categoryIds: number[] }).categoryIds].sort()).toEqual([101, 104, 201])
    expect(put!.headers['X-XSRF-TOKEN']).toBe('test-token')
  })

  it('groups topics under their group name', async () => {
    signedInServer([])

    renderAppAt('/onboarding')

    const technology = await screen.findByRole('group', { name: 'Technology' })
    expect(within(technology).getByLabelText('Programming')).toBeInTheDocument()
    expect(within(technology).queryByLabelText('Space')).not.toBeInTheDocument()
  })

  it('shows the server message when the choice is refused', async () => {
    signedInServer([]).on('PUT', '/api/accounts/me/interests', {
      status: 400,
      body: { title: 'One or more validation errors occurred.', errors: { categoryIds: ['Choose at most 15 topics.'] } },
    })

    renderAppAt('/onboarding')
    for (const topic of ['Programming', 'Space', 'Security']) {
      await userEvent.click(await screen.findByLabelText(topic))
    }
    await userEvent.click(screen.getByRole('button', { name: 'Continue' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Choose at most 15 topics.')
  })
})

describe('changing topics later', () => {
  it('starts with the saved topics ticked and saves the new choice', async () => {
    const server = signedInServer([101, 102, 103])

    renderAppAt('/settings/interests')
    expect(await screen.findByLabelText('Programming')).toBeChecked()
    expect(screen.getByLabelText('Space')).not.toBeChecked()

    await userEvent.click(screen.getByLabelText('Programming')) // untick
    await userEvent.click(screen.getByLabelText('Space')) // tick
    await userEvent.click(screen.getByRole('button', { name: 'Save changes' }))

    expect(await screen.findByText('Saved.')).toBeInTheDocument()
    const put = server.calls.find((call) => call.method === 'PUT')
    expect([...(put!.body as { categoryIds: number[] }).categoryIds].sort()).toEqual([102, 103, 201])
  })

  it('shows the new count on the home page without fetching again', async () => {
    const server = signedInServer([101, 102, 103])

    renderAppAt('/settings/interests')
    await userEvent.click(await screen.findByLabelText('Space'))
    await userEvent.click(screen.getByRole('button', { name: 'Save changes' }))
    await screen.findByText('Saved.')
    const fetchesBefore = server.calls.filter((call) => call.method === 'GET' && call.path.endsWith('/me/interests')).length
    await userEvent.click(screen.getByRole('link', { name: 'Back to home' }))

    expect(await screen.findByText('You follow 4 topics.', { exact: false })).toBeInTheDocument()
    const fetchesAfter = server.calls.filter((call) => call.method === 'GET' && call.path.endsWith('/me/interests')).length
    expect(fetchesAfter).toBe(fetchesBefore)
  })
})
