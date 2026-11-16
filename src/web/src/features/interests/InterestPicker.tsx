import { useState, type FormEvent } from 'react'
import { toApiError } from '../../shared/api/apiClient.ts'
import { ErrorNotice } from '../auth/ErrorNotice.tsx'
import { useCategories, useSaveInterests } from './useInterests.ts'

// Shown as a hint only. The server enforces the real rule (InterestSelection in the Domain).
const MINIMUM = 3

interface InterestPickerProps {
  /** Topics that should start ticked. */
  initialSelection: number[]
  saveLabel: string
  onSaved: () => void
}

/**
 * The tick-box list of topics, used by both the onboarding page and the
 * settings page. It owns only "what is ticked right now"; everything that
 * comes from the server is handled by the hooks.
 */
export function InterestPicker({ initialSelection, saveLabel, onSaved }: InterestPickerProps) {
  const categories = useCategories()
  const save = useSaveInterests()
  const [selected, setSelected] = useState<ReadonlySet<number>>(() => new Set(initialSelection))

  if (categories.isPending) {
    return <p aria-busy="true">Loading topics…</p>
  }

  if (categories.isError) {
    return <ErrorNotice error={toApiError(categories.error)} />
  }

  function toggle(topicId: number) {
    setSelected((current) => {
      const next = new Set(current)
      if (next.has(topicId)) {
        next.delete(topicId)
      } else {
        next.add(topicId)
      }
      return next
    })
  }

  function handleSubmit(event: FormEvent) {
    event.preventDefault()
    save.mutate([...selected], { onSuccess: onSaved })
  }

  const stillNeeded = MINIMUM - selected.size

  return (
    <form onSubmit={handleSubmit}>
      {categories.data.map((group) => (
        <fieldset key={group.id} className="topic-group">
          <legend>{group.name}</legend>
          {group.topics.map((topic) => (
            <label key={topic.id} className="topic">
              <input type="checkbox" checked={selected.has(topic.id)} onChange={() => toggle(topic.id)} />
              {topic.name}
            </label>
          ))}
        </fieldset>
      ))}
      <p className="muted" role="status">
        {stillNeeded > 0 ? `Choose ${stillNeeded} more to continue.` : `${selected.size} chosen.`}
      </p>
      <ErrorNotice error={save.isError ? toApiError(save.error) : null} />
      <button type="submit" disabled={stillNeeded > 0 || save.isPending}>
        {save.isPending ? 'Saving…' : saveLabel}
      </button>
    </form>
  )
}
