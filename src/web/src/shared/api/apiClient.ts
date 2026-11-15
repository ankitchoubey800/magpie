// The ONLY place in the web app that calls fetch().
// Every request goes to a relative "/api/..." address, so the browser always
// talks to the site it was loaded from (the gateway). No backend address is
// ever written into the React code.

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

const API_BASE = '/api'

export async function apiGet<T>(path: string, signal?: AbortSignal): Promise<T> {
  let response: Response
  try {
    response = await fetch(`${API_BASE}${path}`, {
      headers: { Accept: 'application/json' },
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

  return (await response.json()) as T
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
