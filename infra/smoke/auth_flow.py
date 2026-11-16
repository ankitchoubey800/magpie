"""Walks the whole sign-up and sign-in journey against a running Magpie,
the way a browser would, and fails loudly if any step misbehaves.

    python3 infra/smoke/auth_flow.py            # uses http://localhost:8080 and Mailpit on :8025

It needs nothing but Python 3. It keeps cookies by hand so that you can see
exactly what a browser does for you.
"""
import json
import re
import sys
import time
import urllib.parse
import urllib.request

SITE = sys.argv[1] if len(sys.argv) > 1 else "http://localhost:8080"
MAILPIT = sys.argv[2] if len(sys.argv) > 2 else "http://localhost:8025"
EMAIL = f"smoke-{int(time.time())}@example.com"
PASSWORD = "correct horse battery"


from browser import Browser


def expect(label, actual, wanted):
    ok = actual == wanted
    print(f"{'ok  ' if ok else 'FAIL'} {label}: {actual}")
    if not ok:
        sys.exit(f"expected {wanted}")


def confirmation_link_from_mailpit():
    for _ in range(20):
        inbox = json.load(urllib.request.urlopen(f"{MAILPIT}/api/v1/search?query=" + urllib.parse.quote(f"to:{EMAIL}")))
        if inbox["messages"]:
            message_id = inbox["messages"][0]["ID"]
            text = json.load(urllib.request.urlopen(f"{MAILPIT}/api/v1/message/{message_id}"))["Text"]
            match = re.search(r"(http\S+)/confirm-email\?readerId=([^&\s]+)&token=(\S+)", text)
            return match.group(1), urllib.parse.unquote(match.group(2)), urllib.parse.unquote(match.group(3))
        time.sleep(0.5)
    sys.exit("no confirmation email arrived in Mailpit")


browser = Browser(SITE)
expect("who-am-I before signing in", browser.call("GET", "/api/accounts/me")[0], 401)
expect("register", browser.call("POST", "/api/accounts/register", {"email": EMAIL, "password": PASSWORD})[0], 202)
expect("sign in before confirming", browser.call("POST", "/api/accounts/sign-in", {"email": EMAIL, "password": PASSWORD})[0], 401)

link_site, reader_id, token = confirmation_link_from_mailpit()
expect("link in the email points at the public address", link_site, SITE)
expect("confirm email", browser.call("POST", "/api/accounts/confirm-email", {"readerId": reader_id, "token": token})[0], 204)
expect("sign in with a wrong password", browser.call("POST", "/api/accounts/sign-in", {"email": EMAIL, "password": "wrong"})[0], 401)
expect("sign in", browser.call("POST", "/api/accounts/sign-in", {"email": EMAIL, "password": PASSWORD})[0], 204)

session_cookie = next(line for line in browser.last_set_cookie if line.startswith("magpie.session="))
flags = sorted(part.strip().lower() for part in session_cookie.split(";")[1:] if not part.strip().lower().startswith("expires"))
expect("session cookie flags", flags, ["httponly", "path=/", "samesite=lax", "secure"])
browser.token = None  # the signed-in reader changed, so fetch a fresh anti-forgery token

status, me = browser.call("GET", "/api/accounts/me")
expect("who-am-I after signing in", (status, me["email"]), (200, EMAIL))

if "--keep-session" in sys.argv:
    # Used by CI to check that a session survives a restart of the Accounts service.
    print("SESSION_COOKIE=" + browser.cookies["magpie.session"])
    sys.exit(0)

copied_cookie = browser.cookies["magpie.session"]
expect("sign out", browser.call("POST", "/api/accounts/sign-out")[0], 204)
expect("who-am-I after signing out", browser.call("GET", "/api/accounts/me")[0], 401)

thief = Browser(SITE)
thief.cookies["magpie.session"] = copied_cookie
print("note who-am-I with a COPIED cookie after sign-out:", thief.call("GET", "/api/accounts/me")[0],
      "(200 here is the week 2 break-it finding, see the chapter)")
print("All steps passed.")
