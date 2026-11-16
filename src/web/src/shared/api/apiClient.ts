// The ONLY place in the web app that calls fetch().
// Every request goes to a relative "/api/..." address, so the browser always
// talks to the site it was loaded from (the gateway). No backend address is
// ever written into the React code.
//
// Because the address is same-site, the browser attaches the session cookie
// to every call by itself. This file never sees, stores or sends a password
// or login token: that is the whole point of cookie sign-in.

/** The standard error shape (RFC 9457 "Problem Details") every Magpie service returns. */
export interface ProblemDetails {
  type?: string
  title?: string
  status?: number
  detail?: string
  instance?: string
  /** Added by Magpie.Defaults: which service produced the error. */
  service?: string
  /** Added by Magpie.Defaults: the id to search for in the logs. */
  traceId?: string
  /** A short machine-readable reason, when the server provides one. */
  code?: string
  /** Validation messages, grouped by field or topic. */
  errors?: Record<string, string[]>
}

/** One error type for every failed call, so screens handle failures the same way. */
export class ApiError extends Error {
  readonly status: number
  readonly problem: ProblemDetails

  constructor(status: number, problem: ProblemDetails) {
    super(problem.title ?? `Request failed with status ${status}`)
    this.name = 'ApiError'
    this.status = status
    this.problem = problem
  }
}

/** Turns whatever was thrown into an ApiError, so screens only ever handle one type. */
export function toApiError(error: unknown): ApiError {
  return error instanceof ApiError ? error : new ApiError(0, { title: 'Unexpected error', detail: String(error) })
}

const API_BASE = '/api'
const ANTIFORGERY_HEADER = 'X-XSRF-TOKEN'
const ANTIFORGERY_PROBLEM_CODE = 'antiforgery'

// ---------------------------------------------------------------------------
// Reading
// ---------------------------------------------------------------------------

export function apiGet<T>(path: string, signal?: AbortSignal): Promise<T> {
  return request<T>('GET', path, undefined, signal)
}

// ---------------------------------------------------------------------------
// Changing things (needs the anti-forgery token)
// ---------------------------------------------------------------------------

// The token is kept in memory only. It is not a secret that signs anyone in;
// it only proves "this request was made by a page from our own site".
let antiforgeryToken: string | null = null

/**
 * Forget the token. Call this whenever the signed-in reader changes
 * (sign-in, sign-out), because a token is tied to who was signed in when it
 * was issued.
 */
export function resetAntiforgeryToken(): void {
  antiforgeryToken = null
}

async function getAntiforgeryToken(): Promise<string> {
  if (antiforgeryToken === null) {
    const response = await apiGet<{ token: string }>('/accounts/antiforgery-token')
    antiforgeryToken = response.token
  }
  return antiforgeryToken
}

export function apiPost<T = void>(path: string, body?: unknown): Promise<T> {
  return send<T>('POST', path, body)
}

/** PUT means "replace the whole thing with this". Sending it twice gives the same result as once. */
export function apiPut<T = void>(path: string, body: unknown): Promise<T> {
  return send<T>('PUT', path, body)
}

async function send<T>(method: 'POST' | 'PUT', path: string, body?: unknown): Promise<T> {
  try {
    return await request<T>(method, path, body, undefined, await getAntiforgeryToken())
  } catch (error) {
    // The token can go stale (for example, the reader signed in from another
    // tab). When the server says so, fetch a fresh one and try exactly once more.
    if (error instanceof ApiError && error.problem.code === ANTIFORGERY_PROBLEM_CODE) {
      resetAntiforgeryToken()
      return await request<T>(method, path, body, undefined, await getAntiforgeryToken())
    }
    throw error
  }
}

// ---------------------------------------------------------------------------
// Shared plumbing
// ---------------------------------------------------------------------------

async function request<T>(
  method: 'GET' | 'POST' | 'PUT',
  path: string,
  body?: unknown,
  signal?: AbortSignal,
  antiforgery?: string,
): Promise<T> {
  const headers: Record<string, string> = { Accept: 'application/json' }
  if (body !== undefined) {
    headers['Content-Type'] = 'application/json'
  }
  if (antiforgery !== undefined) {
    headers[ANTIFORGERY_HEADER] = antiforgery
  }

  let response: Response
  try {
    response = await fetch(`${API_BASE}${path}`, {
      method,
      headers,
      body: body === undefined ? undefined : JSON.stringify(body),
      signal,
    })
  } catch (error) {
    // The caller cancelled the request on purpose: let that pass through untouched.
    if (error instanceof DOMException && error.name === 'AbortError') {
      throw error
    }
    // fetch() itself failed: no network, or nothing is listening at the address.
    throw new ApiError(0, {
      title: 'Cannot reach Magpie',
      detail: 'Check that the gateway is running, then try again.',
    })
  }

  if (!response.ok) {
    throw new ApiError(response.status, await readProblem(response))
  }

  // 202 Accepted and 204 No Content carry no body: there is nothing to parse.
  const text = await response.text()
  return (text === '' ? undefined : JSON.parse(text)) as T
}

// A failed response SHOULD carry Problem Details, but we never assume it does
// (a proxy in between may answer with an empty body or an HTML page).
async function readProblem(response: Response): Promise<ProblemDetails> {
  const contentType = response.headers.get('content-type') ?? ''
  if (contentType.includes('application/problem+json')) {
    try {
      return (await response.json()) as ProblemDetails
    } catch {
      // Body was not valid JSON after all: fall through to the generic message.
    }
  }
  return {
    status: response.status,
    title: 'Something went wrong',
    detail: `The server answered with status ${response.status}.`,
  }
}
