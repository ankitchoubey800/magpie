import { ApiError, apiGet, apiPost } from '../../shared/api/apiClient.ts'

/** Mirrors CurrentReaderResponse in Magpie.Accounts.Api. */
export interface Reader {
  id: string
  email: string
}

export function register(email: string, password: string): Promise<void> {
  return apiPost('/accounts/register', { email, password })
}

export function resendConfirmation(email: string): Promise<void> {
  return apiPost('/accounts/resend-confirmation', { email })
}

export function confirmEmail(readerId: string, token: string): Promise<void> {
  return apiPost('/accounts/confirm-email', { readerId, token })
}

export function signIn(email: string, password: string): Promise<void> {
  return apiPost('/accounts/sign-in', { email, password })
}

/** Always succeeds from the caller's point of view: the server never says whether the email exists. */
export function requestPasswordReset(email: string): Promise<void> {
  return apiPost('/accounts/forgot-password', { email })
}

export function resetPassword(readerId: string, token: string, newPassword: string): Promise<void> {
  return apiPost('/accounts/reset-password', { readerId, token, newPassword })
}

/** Names of the outside sign-in providers that are switched on, for example ["Google"]. */
export function fetchExternalProviders(signal?: AbortSignal): Promise<string[]> {
  return apiGet<string[]>('/accounts/external/providers', signal)
}

/** Where the browser must NAVIGATE (not fetch) to begin signing in with an outside provider. */
export function externalStartUrl(provider: string, returnTo: string): string {
  return `/api/accounts/external/${encodeURIComponent(provider)}/start?returnUrl=${encodeURIComponent(returnTo)}`
}

/** Attaches the outside identity that is waiting (see ?link=pending) to the signed-in reader. */
export function linkExternalLogin(): Promise<void> {
  return apiPost('/accounts/external/link')
}

export function signOut(): Promise<void> {
  return apiPost('/accounts/sign-out')
}

/** "Who am I?" Returns null when nobody is signed in (the server answers 401). */
export async function fetchCurrentReader(signal?: AbortSignal): Promise<Reader | null> {
  try {
    return await apiGet<Reader>('/accounts/me', signal)
  } catch (error) {
    if (error instanceof ApiError && error.status === 401) {
      return null
    }
    throw error
  }
}
