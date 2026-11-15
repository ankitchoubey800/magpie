import { useSystemStatus } from './useSystemStatus.ts'

export function SystemStatusCard() {
  const { state, reload } = useSystemStatus()

  if (state.status === 'loading') {
    return (
      <section className="card" aria-busy="true">
        <p>Asking the database what time it is…</p>
      </section>
    )
  }

  if (state.status === 'error') {
    const { problem, status } = state.error
    return (
      <section className="card card--error" role="alert">
        <h2>{problem.title ?? 'Something went wrong'}</h2>
        {problem.detail && <p>{problem.detail}</p>}
        <p className="muted">
          Status {status}
          {problem.service && ` · service: ${problem.service}`}
          {problem.traceId && ` · trace: ${problem.traceId}`}
        </p>
        <button type="button" onClick={reload}>
          Try again
        </button>
      </section>
    )
  }

  const { data } = state
  return (
    <section className="card">
      <h2>All connected</h2>
      <dl>
        <dt>Answered by</dt>
        <dd>{data.service}</dd>
        <dt>Database time (UTC)</dt>
        <dd>{data.databaseTimeUtc}</dd>
        <dt>Database round trip</dt>
        <dd>
          {data.roundTripMs} ms{data.isSlow && ' (slow)'}
        </dd>
      </dl>
      <button type="button" onClick={reload}>
        Ask again
      </button>
    </section>
  )
}
