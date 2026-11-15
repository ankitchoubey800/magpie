import { useCallback, useEffect, useState } from 'react'
import { ApiError } from '../../shared/api/apiClient.ts'
import { fetchPing, type PingResponse } from './api.ts'

// A "discriminated union": the screen is in exactly one of these three states.
// TypeScript will not let us read `data` unless status is 'ok'.
export type SystemStatus =
  | { status: 'loading' }
  | { status: 'ok'; data: PingResponse }
  | { status: 'error'; error: ApiError }

export function useSystemStatus(): { state: SystemStatus; reload: () => void } {
  const [state, setState] = useState<SystemStatus>({ status: 'loading' })
  // Changing this number re-runs the effect below. That is all "reload" does.
  const [attempt, setAttempt] = useState(0)

  useEffect(() => {
    // If the component disappears (or reload is pressed) while a request is
    // still travelling, we cancel it so a late answer cannot overwrite a newer one.
    const controller = new AbortController()

    fetchPing(controller.signal)
      .then((data) => setState({ status: 'ok', data }))
      .catch((error: unknown) => {
        if (controller.signal.aborted) {
          return
        }
        const apiError =
          error instanceof ApiError
            ? error
            : new ApiError(0, { title: 'Unexpected error', detail: String(error) })
        setState({ status: 'error', error: apiError })
      })

    return () => controller.abort()
  }, [attempt])

  const reload = useCallback(() => {
    setState({ status: 'loading' })
    setAttempt((current) => current + 1)
  }, [])

  return { state, reload }
}
