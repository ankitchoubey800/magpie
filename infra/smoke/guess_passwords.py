"""Week 3 break-it experiment: guess passwords fast and watch two defences start.

    python3 infra/smoke/guess_passwords.py [attempts]     # default 30

It creates and confirms a fresh account, then tries wrong passwords as fast
as it can and prints what the server answered each time.

  401 = "Sign-in failed" (a guess was checked, or the account is locked: from
        outside you cannot tell which, on purpose)
  429 = "Too many attempts" (the rate limit refused to even look at the guess)
"""
import json
import re
import sys
import time
import urllib.parse
import urllib.request

from browser import Browser

SITE = "http://localhost:8080"
MAILPIT = "http://localhost:8025"
ATTEMPTS = int(sys.argv[1]) if len(sys.argv) > 1 else 30
EMAIL = f"guess-{int(time.time())}@example.com"
PASSWORD = "correct horse battery"

browser = Browser(SITE)
assert browser.call("POST", "/api/accounts/register", {"email": EMAIL, "password": PASSWORD})[0] == 202

link = None
for _ in range(20):
    inbox = json.load(urllib.request.urlopen(f"{MAILPIT}/api/v1/search?query=" + urllib.parse.quote(f"to:{EMAIL}")))
    if inbox["messages"]:
        text = json.load(urllib.request.urlopen(f"{MAILPIT}/api/v1/message/{inbox['messages'][0]['ID']}"))["Text"]
        link = re.search(r"readerId=([^&\s]+)&token=(\S+)", text)
        break
    time.sleep(0.5)
assert link, "no confirmation email arrived"
assert browser.call("POST", "/api/accounts/confirm-email",
                    {"readerId": urllib.parse.unquote(link.group(1)), "token": urllib.parse.unquote(link.group(2))})[0] == 204

print(f"account: {EMAIL}")
print("two requests (register, confirm) already count towards this minute's limit")
statuses = []
started = time.time()
for attempt in range(1, ATTEMPTS + 1):
    status, _ = browser.call("POST", "/api/accounts/sign-in", {"email": EMAIL, "password": f"wrong guess {attempt}"})
    statuses.append(status)
elapsed = time.time() - started

print(f"{ATTEMPTS} wrong guesses in {elapsed:.1f} seconds: " + " ".join(str(status) for status in statuses))
first_429 = statuses.index(429) + 1 if 429 in statuses else None
print(f"guesses that reached the password check (401): {statuses.count(401)}")
print(f"guesses refused by the rate limit (429): {statuses.count(429)}")
print(f"first 429 at guess number: {first_429}")

status, _ = browser.call("POST", "/api/accounts/sign-in", {"email": EMAIL, "password": PASSWORD})
print(f"the CORRECT password straight afterwards: {status}")
print(f"GUESS_EMAIL={EMAIL}")
