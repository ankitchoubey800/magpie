# ADR-0004: An outside sign-in is never linked to an account by email alone

- **Status:** Accepted
- **Date:** 2026-11-16

## Context

Readers can sign in with Google as well as with a password. Sooner or later a
Google identity arrives whose email matches an existing password account.

## Options considered

1. **Link automatically when the emails match.** Smoothest for the reader. But
   then our accounts are only as safe as the weakest provider we ever add: any
   provider that lets someone claim an address they do not own becomes a way
   into existing accounts.
2. **Refuse, and tell the reader to use their password.** Safe, but the reader
   can never connect Google.
3. **Ask for proof first.** Park the outside identity for a few minutes, have
   the reader sign in with their existing password, then link.

## Decision

Option 3. Three cases, enforced in `IdentityExternalSignIn` and pinned by tests:
already linked → sign in; unknown email (verified by the provider) → create an
account; known email, not linked → no sign-in and no link until the reader has
signed in with their password. The outside identity must also be for that same
account's email.

## Consequences

- One extra step, once, for readers who registered with a password first.
- An account created through Google has no password. "Forgot password" lets
  the reader add one later.
- We only ever act on an email address the provider states it has verified.
- Passkeys and further providers follow the same three cases.
