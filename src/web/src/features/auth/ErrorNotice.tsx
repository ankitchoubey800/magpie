import type { ApiError } from '../../shared/api/apiClient.ts'

/** Shows any ApiError the same way on every form. */
export function ErrorNotice({ error }: { error: ApiError | null }) {
  if (error === null) {
    return null
  }

  const { problem } = error
  const messages = Object.values(problem.errors ?? {}).flat()

  return (
    <div className="notice notice--error" role="alert">
      <strong>{problem.title ?? 'Something went wrong'}</strong>
      {messages.length > 0 ? (
        <ul>
          {messages.map((message) => (
            <li key={message}>{message}</li>
          ))}
        </ul>
      ) : (
        problem.detail && <p>{problem.detail}</p>
      )}
    </div>
  )
}
