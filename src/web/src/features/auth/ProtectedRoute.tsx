import { Navigate, Outlet, useLocation } from 'react-router'
import { useAuth } from './useAuth.ts'

/**
 * Wraps pages that need a signed-in reader. This is a convenience for honest
 * visitors, NOT security: anyone can edit JavaScript in their own browser.
 * The real lock is on the server, which answers 401 without a valid cookie.
 */
export function ProtectedRoute() {
  const { state } = useAuth()
  const location = useLocation()

  if (state.status === 'loading') {
    // Without this, a signed-in reader who refreshes the page would be
    // bounced to the sign-in page for a split second.
    return <p aria-busy="true">Checking your session…</p>
  }

  if (state.status === 'signedOut') {
    // Remember where they were heading, so sign-in can send them back there.
    return <Navigate to="/sign-in" replace state={{ from: location.pathname }} />
  }

  return <Outlet />
}
