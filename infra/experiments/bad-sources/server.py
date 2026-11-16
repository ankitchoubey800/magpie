"""Week 5 break-it kit: a tiny web server full of badly behaved "sources".

    python3 infra/experiments/bad-sources/server.py        # listens on port 9099

  /good.xml   a normal little feed with three items
  /garbage    an HTML error page pretending to be a feed
  /hang       accepts the connection and never answers
  /flaky      fails twice with 503, then behaves like /good.xml
  /huge       an endless answer (tests the size limit)

Point the collector at it with infra/experiments/bad-sources/sources.json.
From inside Docker, this machine is called "host.docker.internal".
"""
import time
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

GOOD = b"""<?xml version="1.0" encoding="UTF-8"?>
<rss version="2.0"><channel><title>Good feed</title>
<item><title>First item</title><link>https://example.com/1</link><guid>good-1</guid><description>One.</description></item>
<item><title>Second item</title><link>https://example.com/2</link><guid>good-2</guid><description>Two.</description></item>
<item><title>Third item</title><link>https://example.com/3</link><guid>good-3</guid><description>Three.</description></item>
</channel></rss>"""

flaky_calls = 0


class Handler(BaseHTTPRequestHandler):
    def do_GET(self):
        global flaky_calls
        print(f"request: {self.path}   user-agent: {self.headers.get('User-Agent')}", flush=True)

        if self.path == "/good.xml":
            self.answer(200, "application/rss+xml", GOOD)
        elif self.path == "/garbage":
            self.answer(200, "text/html", b"<html><body><h1>It works!</h1><p>This is not a feed.")
        elif self.path == "/hang":
            time.sleep(3600)
        elif self.path == "/flaky":
            flaky_calls += 1
            if flaky_calls <= 2:
                self.answer(503, "text/plain", b"try again later")
            else:
                self.answer(200, "application/rss+xml", GOOD.replace(b"good-", b"flaky-"))
        elif self.path == "/huge":
            self.send_response(200)
            self.send_header("Content-Type", "application/rss+xml")
            self.end_headers()
            try:
                while True:
                    self.wfile.write(b"<x>" + b"a" * 65536 + b"</x>")
            except OSError:
                pass  # the collector hung up, as it should
        else:
            self.answer(404, "text/plain", b"not found")

    def answer(self, status, content_type, body):
        self.send_response(status)
        self.send_header("Content-Type", content_type)
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def log_message(self, *args):
        pass  # we print our own, shorter line above


if __name__ == "__main__":
    print("bad-sources server listening on http://0.0.0.0:9099", flush=True)
    ThreadingHTTPServer(("0.0.0.0", 9099), Handler).serve_forever()
