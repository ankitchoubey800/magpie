import { SystemStatusCard } from '../features/system-status/SystemStatusCard.tsx'

// App is the "shell": it arranges features on the page and owns no business
// logic of its own. Each feature lives in its own folder under src/features.
export default function App() {
  return (
    <main>
      <h1>Magpie</h1>
      <p>Week 1 skeleton: one request from this page to PostgreSQL and back.</p>
      <SystemStatusCard />
    </main>
  )
}
