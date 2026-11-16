import { expect, test, type APIRequestContext, type Page } from '@playwright/test'
import { linkFromEmail, newEmail } from './mailbox.ts'

const PASSWORD = 'correct horse battery'

// Small helpers that read like the steps a person would take.

async function signUpAndConfirm(page: Page, request: APIRequestContext, email: string) {
  await page.goto('/sign-up')
  await page.getByLabel('Email').fill(email)
  await page.getByLabel('Password', { exact: true }).fill(PASSWORD)
  await page.getByRole('button', { name: 'Create account' }).click()
  await expect(page.getByRole('heading', { name: 'Check your email' })).toBeVisible()

  await page.goto(await linkFromEmail(request, email, 'confirm-email'))
  await page.getByRole('button', { name: 'Confirm my email' }).click()
  await expect(page.getByRole('heading', { name: 'Email confirmed' })).toBeVisible()
}

async function signIn(page: Page, email: string, password: string) {
  await page.goto('/sign-in')
  await page.getByLabel('Email').fill(email)
  await page.getByLabel('Password', { exact: true }).fill(password)
  await page.getByRole('button', { name: 'Sign in' }).click()
}

test('a signed-out visitor is sent to the sign-in page', async ({ page }) => {
  await page.goto('/')

  await expect(page.getByRole('heading', { name: 'Sign in' })).toBeVisible()
})

test('sign up, confirm by email, sign in, stay signed in, sign out', async ({ page, request, context }) => {
  const email = newEmail()
  await signUpAndConfirm(page, request, email)

  await signIn(page, email, PASSWORD)
  await expect(page.getByText('Signed in as')).toContainText(email)

  // The three safety flags, as the browser itself reports them.
  const session = (await context.cookies()).find((cookie) => cookie.name === 'magpie.session')
  expect(session, 'the session cookie should exist').toBeDefined()
  expect(session!.httpOnly).toBe(true)
  expect(session!.secure).toBe(true)
  expect(session!.sameSite).toBe('Lax')

  // HttpOnly made visible: page scripts cannot see the cookie at all.
  expect(await page.evaluate(() => document.cookie)).not.toContain('magpie.session')

  // A refresh must not sign the reader out.
  await page.reload()
  await expect(page.getByText('Signed in as')).toContainText(email)

  await page.getByRole('button', { name: 'Sign out' }).click()
  await expect(page.getByRole('heading', { name: 'Sign in' })).toBeVisible()
})

test('a wrong password shows the generic message and stays on the page', async ({ page, request }) => {
  const email = newEmail()
  await signUpAndConfirm(page, request, email)

  await signIn(page, email, 'definitely not the password')

  await expect(page.getByRole('alert')).toContainText('Sign-in failed')
  await expect(page.getByRole('heading', { name: 'Sign in' })).toBeVisible()
})

test('forgot password: reset by email, old password stops working, new one works', async ({ page, request }) => {
  const email = newEmail()
  const newPassword = 'brand new staple gun'
  await signUpAndConfirm(page, request, email)

  await page.goto('/sign-in')
  await page.getByRole('link', { name: 'Forgot your password?' }).click()
  await page.getByLabel('Email').fill(email)
  await page.getByRole('button', { name: 'Send reset link' }).click()
  await expect(page.getByRole('heading', { name: 'Check your email' })).toBeVisible()

  await page.goto(await linkFromEmail(request, email, 'reset-password'))
  await page.getByLabel('New password').fill(newPassword)
  await page.getByRole('button', { name: 'Save new password' }).click()
  await expect(page.getByRole('heading', { name: 'Password changed' })).toBeVisible()

  await signIn(page, email, PASSWORD)
  await expect(page.getByRole('alert')).toContainText('Sign-in failed')

  await signIn(page, email, newPassword)
  await expect(page.getByText('Signed in as')).toContainText(email)
})
