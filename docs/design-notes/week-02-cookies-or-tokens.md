# Week 2 design note: cookies or tokens for a browser app?

**Status:** draft written with the chapter. Ankit to edit and add the result of the break-it experiment before the week closes.

## What we need

A reader signs in once and stays signed in for about two weeks. Stolen
credentials should be hard to obtain and possible to cancel.

## The two options side by side

| Question | Token in browser storage | Server-set cookie (our choice) |
|---|---|---|
| Where is it kept? | `localStorage` or `sessionStorage` | The browser's cookie jar |
| Can JavaScript read it? | Yes | No (`HttpOnly`) |
| What can a malicious script on our page do? | Copy the token and use it from anywhere until it expires | Make requests while the page is open, but cannot take the credential away |
| Who attaches it to requests? | Our code, on every call | The browser, automatically |
| Main attack to defend against | Script injection (XSS) stealing the token | Forged requests from other sites (CSRF) |
| Defence | Never have an XSS bug (hard to promise) | `SameSite` cookie plus an anti-forgery token (mechanical) |
| How does sign-out work? | Delete it locally; the token itself stays valid until expiry unless we keep a block list | Delete the cookie locally; a copy stays valid until expiry unless we change the security stamp |
| Works for a mobile app or other API clients? | Yes | No |

## Rough numbers

- Session lifetime: 14 days, renewed while the reader keeps visiting.
- Lockout: 5 wrong passwords, then 5 minutes.
- Confirmation link lifetime: 24 hours (Identity's default).

## Choice and reason

Cookies. Our only client is our own website served from the same address, so
the cookie's one real limitation does not apply, and its main risk (CSRF) has a
mechanical defence. The token's main risk (XSS theft) depends on never making a
mistake anywhere in the frontend or its dependencies.

## What the break-it experiment showed

(To be filled in by Ankit.) Expected: a cookie copied to another browser keeps
working after the first browser signs out.

## What would change our mind

A mobile app or public API. Then tokens are added for those clients; the
browser keeps its cookie.
