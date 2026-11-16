import { useNavigate } from 'react-router'
import { InterestPicker } from './InterestPicker.tsx'

/** The first thing a new reader sees after signing in. */
export function OnboardingPage() {
  const navigate = useNavigate()

  return (
    <section className="card">
      <h2>What are you interested in?</h2>
      <p>Pick at least three topics. Your feed is built from these, and you can change them at any time.</p>
      <InterestPicker initialSelection={[]} saveLabel="Continue" onSaved={() => navigate('/', { replace: true })} />
    </section>
  )
}
