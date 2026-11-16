import { Link, Navigate } from 'react-router'
import { useAuth } from '../auth/useAuth.ts'
import { useMyInterests } from '../interests/useInterests.ts'
import { SystemStatusCard } from '../system-status/SystemStatusCard.tsx'

/** The first page behind the login. The feed moves in here in week 10. */
export function HomePage() {
  const { state, signOut } = useAuth()
  const interests = useMyInterests()

  // ProtectedRoute guarantees a signed-in reader; this check satisfies TypeScript.
  if (state.status !== 'signedIn') {
    return null
  }

  // A reader who has not chosen any topics yet does that first.
  if (interests.isSuccess && interests.data.length === 0) {
    return <Navigate to="/onboarding" replace />
  }

  return (
    <>
      <section className="card">
        <h2>Welcome</h2>
        <p>
          Signed in as <strong>{state.reader.email}</strong>
        </p>
        {interests.isSuccess && (
          <p>
            You follow {interests.data.length} topics. <Link to="/settings/interests">Change your topics</Link>
          </p>
        )}
        <button type="button" onClick={() => void signOut()}>
          Sign out
        </button>
      </section>
      <SystemStatusCard />
    </>
  )
}
