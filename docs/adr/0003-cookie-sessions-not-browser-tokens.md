# ADR-0003: Cookie sessions for the browser, not tokens in browser storage

- **Status:** Accepted
- **Date:** 2026-11-15

## Context

Readers sign in from a web browser. The React app and every API are served
from one address through the gateway. We must decide where the proof of
"this browser is signed in" lives.

## Options considered

1. **Token (JWT) kept in browser storage and sent in a header.** Simple for
   APIs called from many kinds of client. Any script running on our page,
   including a malicious one injected through a bug or a compromised package,
   can read the token and send it elsewhere. Hard to cancel before it expires.
2. **Session cookie set by the server** with `HttpOnly`, `Secure` and
   `SameSite=Lax`. Scripts cannot read it. The browser attaches it by itself.
   Needs protection against forged cross-site requests, and needs the site and
   API to share an address.
3. **A managed login service** (Amazon Cognito, Auth0). Compared in week 3.

## Decision

Option 2: ASP.NET Core Identity issues an encrypted session cookie. All
state-changing requests also require an anti-forgery token. The browser never
holds a credential that JavaScript can read.

## Consequences

- The gateway arrangement from ADR-0001 (one address for site and API) is now
  load-bearing: breaking it breaks sign-in.
- Signing out removes the cookie from that browser only. A copied cookie stays
  valid until it expires or the reader's security stamp changes (week 3 uses
  this for "sign out everywhere" after a password reset).
- The keys that encrypt cookies must be stored durably and shared by every
  copy of the Accounts service.
- A future mobile app or third-party API client cannot use cookies and will
  need tokens. That is a separate decision when it arrives.
