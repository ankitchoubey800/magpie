import sys, time, json, urllib.request
sys.path.insert(0, 'infra/smoke')
from browser import Browser
b = Browser("http://localhost:8080")
slow = []
for i in range(60):
    t = time.time(); s, _ = b.call("POST", "/api/accounts/register", {"email": f"stress-{i}-{int(t)}@example.com", "password": "correct horse battery"}); d = time.time() - t
    if d > 1 or s != 202: slow.append((i, s, round(d, 2)))
    if i in (20, 40): time.sleep(12)
print("STRESS slow-or-failed sends:", slow)
