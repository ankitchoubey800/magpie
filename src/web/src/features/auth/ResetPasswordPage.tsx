import { useState, type FormEvent } from 'react'
import { Link, useSearchParams } from 'react-router'
import { toApiError, type ApiError } from '../../shared/api/apiClient.ts'
import { resetPassword } from './api.ts'
import { ErrorNotice } from './ErrorNotice.tsx'

/** The page the reset email opens. */
export function ResetPasswordPage() {
  const [searchParams] = useSearchParams()
  const readerId = searchParams.get('readerId')
  const token = searchParams.get('token')

  const [password, setPassword] = useState('')
  const [submitting, setSubmitting] = useState(false)
  const [done, setDone] = useState(false)
  const [error, setError] = useState<ApiError | null>(null)

  if (!readerId || !token) {
    return (
      <section className="card">
        <h2>This link is incomplete</h2>
        <p>
          Open the link from your email again, or <Link to="/forgot-password">ask for a new one</Link>.
        </p>
      </section>
    )
  }

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setSubmitting(true)
    setError(null)
    try {
      await resetPassword(readerId!, token!, password)
      setDone(true)
    } catch (caught) {
      setError(toApiError(caught))
    } finally {
      setSubmitting(false)
    }
  }

  if (done) {
    return (
      <section className="card">
        <h2>Password changed</h2>
        <p>You have been signed out everywhere. Use your new password from now on.</p>
        <p>
          <Link to="/sign-in">Sign in</Link>
        </p>
      </section>
    )
  }

  return (
    <section className="card">
      <h2>Choose a new password</h2>
      <form onSubmit={handleSubmit}>
        <label>
          New password
          <input
            type="password"
            autoComplete="new-password"
            required
            minLength={10}
            value={password}
            onChange={(event) => setPassword(event.target.value)}
          />
        </label>
        <p className="muted">At least 10 characters. A few random words work well.</p>
        <ErrorNotice error={error} />
        <button type="submit" disabled={submitting}>
          {submitting ? 'Saving…' : 'Save new password'}
        </button>
      </form>
    </section>
  )
}
