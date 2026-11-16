import type { APIRequestContext } from '@playwright/test'

const MAILPIT = process.env.MAILPIT_URL ?? 'http://localhost:8025'

/** A fresh address for every test, so tests never see each other's accounts or emails. */
export function newEmail(): string {
  return `e2e-${Date.now()}-${Math.floor(Math.random() * 1_000_000)}@example.com`
}

/**
 * Waits for an email to `to` that contains a link to the given page and
 * returns that link as a path on our site (for example "/confirm-email?...").
 * This is the test doing what a person does: open the inbox, find the link.
 */
export async function linkFromEmail(
  request: APIRequestContext,
  to: string,
  page: 'confirm-email' | 'reset-password',
): Promise<string> {
  const pattern = new RegExp(`https?://[^/\\s]+(/${page}\\?\\S+)`)

  for (let attempt = 0; attempt < 40; attempt++) {
    const search = await request.get(`${MAILPIT}/api/v1/search`, { params: { query: `to:${to}` } })
    const inbox = (await search.json()) as { messages: { ID: string }[] }

    for (const summary of inbox.messages) {
      const message = await request.get(`${MAILPIT}/api/v1/message/${summary.ID}`)
      const { Text } = (await message.json()) as { Text: string }
      const match = pattern.exec(Text)
      if (match) {
        return match[1]
      }
    }

    await new Promise((resolve) => setTimeout(resolve, 250))
  }

  throw new Error(`No email with a ${page} link arrived for ${to}`)
}
