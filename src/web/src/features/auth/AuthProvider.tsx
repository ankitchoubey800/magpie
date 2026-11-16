import { useQueryClient } from '@tanstack/react-query'
import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react'
import { resetAntiforgeryToken } from '../../shared/api/apiClient.ts'
import * as authApi from './api.ts'
import { AuthContext, type AuthContextValue, type AuthState } from './AuthContext.ts'

/**
 * The ONE place that knows who is signed in. It never stores a password or a
 * token. It simply asks the server "who am I?"; the browser attaches the
 * session cookie to that question by itself.
 */
export function AuthProvider({ children }: { children: ReactNode }) {
  const [state, setState] = useState<AuthState>({ status: 'loading' })
  const queryClient = useQueryClient()

  // On first load: is there already a valid session cookie?
  useEffect(() => {
    const controller = new AbortController()

    authApi
      .fetchCurrentReader(controller.signal)
      .then((reader) => setState(reader ? { status: 'signedIn', reader } : { status: 'signedOut' }))
      .catch(() => {
        if (!controller.signal.aborted) {
          // The server could not be reached. Treat the visitor as signed out;
          // the sign-in page will show the real error if they try.
          setState({ status: 'signedOut' })
        }
      })

    return () => controller.abort()
  }, [])

  const signIn = useCallback(async (email: string, password: string) => {
    await authApi.signIn(email, password)
    // The signed-in reader changed, so the old anti-forgery token is no longer valid.
    resetAntiforgeryToken()
    // A different person may now be using this tab: forget everything fetched before.
    queryClient.clear()
    const reader = await authApi.fetchCurrentReader()
    setState(reader ? { status: 'signedIn', reader } : { status: 'signedOut' })
  }, [queryClient])

  const signOut = useCallback(async () => {
    await authApi.signOut()
    resetAntiforgeryToken()
    // Without this, the NEXT reader to sign in on this computer could briefly
    // see the previous reader's data from the cache.
    queryClient.clear()
    setState({ status: 'signedOut' })
  }, [queryClient])

  const value = useMemo<AuthContextValue>(() => ({ state, signIn, signOut }), [state, signIn, signOut])

  return <AuthContext value={value}>{children}</AuthContext>
}
