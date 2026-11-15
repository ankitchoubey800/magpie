import { useState } from 'react'
import { Link, useLocation } from 'react-router'
import { toApiError, type ApiError } from '../../shared/api/apiClient.ts'
import { resendConfirmation } from './api.ts'
import { ErrorNotice } from './ErrorNotice.tsx'

export function CheckEmailPage() {
  const location = useLocation()
  const email = (location.state as { email?: string } | null)?.email
  const [resent, setResent] = useState(false)
  const [error, setError] = useState<ApiError | null>(null)

  async function handleResend() {
    if (!email) {
      return
    }
    setError(null)
    try {
      await resendConfirmation(email)
      setResent(true)
    } catch (caught) {
      setError(toApiError(caught))
    }
  }

  return (
    <section className="card">
      <h2>Check your email</h2>
      <p>
        If {email ? <strong>{email}</strong> : 'that address'} can be used for a new account, we have sent it a
        link. Open the link to finish signing up.
      </p>
      <p className="muted">The link works for 24 hours. Look in your spam folder too.</p>
      <ErrorNotice error={error} />
      {resent && <p role="status">Sent again.</p>}
      {email && (
        <button type="button" onClick={handleResend}>
          Send the email again
        </button>
      )}
      <p>
        <Link to="/sign-in">Go to sign in</Link>
      </p>
    </section>
  )
}
