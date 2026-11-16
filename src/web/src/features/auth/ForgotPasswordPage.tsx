import { useState, type FormEvent } from 'react'
import { Link } from 'react-router'
import { toApiError, type ApiError } from '../../shared/api/apiClient.ts'
import { requestPasswordReset } from './api.ts'
import { ErrorNotice } from './ErrorNotice.tsx'

export function ForgotPasswordPage() {
  const [email, setEmail] = useState('')
  const [submitting, setSubmitting] = useState(false)
  const [sent, setSent] = useState(false)
  const [error, setError] = useState<ApiError | null>(null)

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setSubmitting(true)
    setError(null)
    try {
      await requestPasswordReset(email)
      setSent(true)
    } catch (caught) {
      setError(toApiError(caught))
    } finally {
      setSubmitting(false)
    }
  }

  if (sent) {
    // The same words whether or not the address has an account.
    return (
      <section className="card">
        <h2>Check your email</h2>
        <p>
          If <strong>{email}</strong> has a Magpie account, we have sent it a link to choose a new password.
        </p>
        <p className="muted">The link works for 1 hour and only once.</p>
        <p>
          <Link to="/sign-in">Back to sign in</Link>
        </p>
      </section>
    )
  }

  return (
    <section className="card">
      <h2>Forgot your password?</h2>
      <p>Enter your email and we will send you a link to choose a new one.</p>
      <form onSubmit={handleSubmit}>
        <label>
          Email
          <input
            type="email"
            autoComplete="email"
            required
            value={email}
            onChange={(event) => setEmail(event.target.value)}
          />
        </label>
        <ErrorNotice error={error} />
        <button type="submit" disabled={submitting}>
          {submitting ? 'Sending…' : 'Send reset link'}
        </button>
      </form>
      <p>
        <Link to="/sign-in">Back to sign in</Link>
      </p>
    </section>
  )
}
