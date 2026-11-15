import { apiGet } from '../../shared/api/apiClient.ts'

/** Mirrors the PingResponse record in Magpie.Accounts.Application. */
export interface PingResponse {
  service: string
  databaseTimeUtc: string
  roundTripMs: number
  isSlow: boolean
}

export function fetchPing(signal?: AbortSignal): Promise<PingResponse> {
  return apiGet<PingResponse>('/accounts/ping', signal)
}
