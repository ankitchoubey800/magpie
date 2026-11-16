"""A very small pretend browser shared by the smoke scripts.

It remembers cookies and sends the anti-forgery header, by hand, so that you
can see exactly what a real browser does for you.
"""
import json
import urllib.error
import urllib.request


class Browser:
    """A very small pretend browser: remembers cookies, sends the anti-forgery header."""

    def __init__(self, site):
        self.site = site
        self.cookies = {}
        self.token = None
        self.last_set_cookie = []

    def call(self, method, path, body=None):
        if method == "POST" and self.token is None:
            # Fetch the token FIRST: this call also stores the anti-forgery cookie,
            # which must travel with the POST below.
            self.token = self.call("GET", "/api/accounts/antiforgery-token")[1]["token"]
        headers = {"Accept": "application/json"}
        if self.cookies:
            headers["Cookie"] = "; ".join(f"{name}={value}" for name, value in self.cookies.items())
        data = None
        if method == "POST":
            headers["X-XSRF-TOKEN"] = self.token
            headers["Content-Type"] = "application/json"
            data = json.dumps(body or {}).encode()
        request = urllib.request.Request(self.site + path, data=data, headers=headers, method=method)
        try:
            response = urllib.request.urlopen(request, timeout=20)
        except urllib.error.HTTPError as error:
            response = error
        self.last_set_cookie = response.headers.get_all("Set-Cookie") or []
        for line in self.last_set_cookie:
            name, value = line.split(";")[0].split("=", 1)
            if value:
                self.cookies[name] = value
            else:
                self.cookies.pop(name, None)
        text = response.read().decode()
        return response.status, (json.loads(text) if text else None)
