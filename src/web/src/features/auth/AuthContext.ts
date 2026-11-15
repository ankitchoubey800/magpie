import { createContext } from 'react'
import type { Reader } from './api.ts'

// The whole app can be in exactly one of these three states.
export type AuthState =
  | { status: 'loading' } // we have not heard back from "who am I?" yet
  | { status: 'signedOut' }
  | { status: 'signedIn'; reader: Reader }

export interface AuthContextValue {
  state: AuthState
  signIn: (email: string, password: string) => Promise<void>
  signOut: () => Promise<void>
}

// A context is a value that any component below the provider can read
// without it being passed down by hand through every level in between.
export const AuthContext = createContext<AuthContextValue | null>(null)
