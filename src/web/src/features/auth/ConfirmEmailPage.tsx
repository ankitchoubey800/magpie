import { useState } from 'react'
import { Link, useSearchParams } from 'react-router'
import { toApiError, type ApiError } from '../../shared/api/apiClient.ts'
import { confirmEmail } from './api.ts'
import { ErrorNotice } from './ErrorNotice.tsx'

/**
 * The page the email link opens. The reader presses a button to confirm.
 * We do not confirm automatically on page load: some email programs open
 * every link in a message to scan it, and that must not count as "the owner
 * clicked".
 */
export function ConfirmEmailPage() {
  const [searchParams] = useSearchParams()
  const readerId = searchParams.get('readerId')
  const token = searchParams.get('token')

  const [status, setStatus] = useState<'idle' | 'working' | 'done'>('idle')
  const [error, setError] = useState<ApiError | null>(null)

  if (!readerId || !token) {
    return (
      <section className="card">
        <h2>This link is incomplete</h2>
        <p>Open the link from your email again, or copy the whole address.</p>
      </section>
    )
  }

  async function handleConfirm() {
    setStatus('working')
    setError(null)
    try {
      await confirmEmail(readerId!, token!)
      setStatus('done')
    } catch (caught) {
      setError(toApiError(caught))
      setStatus('idle')
    }
  }

  if (status === 'done') {
    return (
      <section className="card">
        <h2>Email confirmed</h2>
        <p>
          You can now <Link to="/sign-in">sign in</Link>.
        </p>
      </section>
    )
  }

  return (
    <section className="card">
      <h2>Confirm your email</h2>
      <ErrorNotice error={error} />
      <button type="button" onClick={handleConfirm} disabled={status === 'working'}>
        {status === 'working' ? 'Confirming…' : 'Confirm my email'}
      </button>
    </section>
  )
}
