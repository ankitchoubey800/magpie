import { useEffect, useState, type FormEvent } from 'react'
import { Link, Navigate, useLocation, useNavigate, useSearchParams } from 'react-router'
import { toApiError, type ApiError } from '../../shared/api/apiClient.ts'
import { externalStartUrl, fetchExternalProviders, linkExternalLogin } from './api.ts'
import { ErrorNotice } from './ErrorNotice.tsx'
import { useAuth } from './useAuth.ts'

export function SignInPage() {
  const { state, signIn } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const [searchParams] = useSearchParams()
  // Where ProtectedRoute said the reader was heading; the home page otherwise.
  const destination = (location.state as { from?: string } | null)?.from ?? '/'

  // Set by the server when it sends the browser back here after "Continue with Google".
  const linkPending = searchParams.get('link') === 'pending'
  const externalFailed = searchParams.get('error') === 'external'

  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState<ApiError | null>(null)
  const [providers, setProviders] = useState<string[]>([])

  // Ask which outside providers are switched on. If the question fails, we
  // simply show no extra buttons: password sign-in must never depend on it.
  useEffect(() => {
    const controller = new AbortController()
    fetchExternalProviders(controller.signal)
      .then(setProviders)
      .catch(() => undefined)
    return () => controller.abort()
  }, [])

  // While we are in the middle of submitting, the form handles navigation itself.
  if (state.status === 'signedIn' && !submitting) {
    return <Navigate to={destination} replace />
  }

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setSubmitting(true)
    setError(null)
    try {
      await signIn(email, password)
      if (linkPending) {
        // The password has just proven the account is theirs: now it is safe to link.
        // If linking fails (the five-minute window passed), they are still signed in.
        await linkExternalLogin().catch(() => undefined)
      }
      navigate(destination, { replace: true })
    } catch (caught) {
      setError(toApiError(caught))
      setSubmitting(false)
    }
  }

  return (
    <section className="card">
      <h2>Sign in</h2>
      {linkPending && (
        <p className="notice notice--info" role="status">
          An account with that email already exists. Sign in with your password once, and we will connect
          Google to it.
        </p>
      )}
      {externalFailed && (
        <p className="notice notice--error" role="alert">
          Signing in with that provider did not work. Try again, or use your email and password.
        </p>
      )}
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
        <label>
          Password
          <input
            type="password"
            autoComplete="current-password"
            required
            value={password}
            onChange={(event) => setPassword(event.target.value)}
          />
        </label>
        <ErrorNotice error={error} />
        <button type="submit" disabled={submitting}>
          {submitting ? 'Signing in…' : 'Sign in'}
        </button>
      </form>
      {providers.map((provider) => (
        // A plain link on purpose: the browser must leave our page and go to
        // the provider. fetch() cannot do that.
        <p key={provider}>
          <a className="button-link" href={externalStartUrl(provider, destination)}>
            Continue with {provider}
          </a>
        </p>
      ))}
      <p>
        <Link to="/forgot-password">Forgot your password?</Link>
      </p>
      <p>
        New here? <Link to="/sign-up">Create an account</Link>
      </p>
    </section>
  )
}
