import { useState } from 'react'
import { Link } from 'react-router'
import { toApiError } from '../../shared/api/apiClient.ts'
import { ErrorNotice } from '../auth/ErrorNotice.tsx'
import { InterestPicker } from './InterestPicker.tsx'
import { useMyInterests } from './useInterests.ts'

/** Where a reader changes their topics later. */
export function InterestsSettingsPage() {
  const interests = useMyInterests()
  const [justSaved, setJustSaved] = useState(false)

  return (
    <section className="card">
      <h2>Your topics</h2>
      {interests.isPending && <p aria-busy="true">Loading your topics…</p>}
      {interests.isError && <ErrorNotice error={toApiError(interests.error)} />}
      {interests.isSuccess && (
        // The picker starts from what the server says is saved.
        <InterestPicker
          initialSelection={interests.data}
          saveLabel="Save changes"
          onSaved={() => setJustSaved(true)}
        />
      )}
      {justSaved && <p role="status">Saved.</p>}
      <p>
        <Link to="/">Back to home</Link>
      </p>
    </section>
  )
}
