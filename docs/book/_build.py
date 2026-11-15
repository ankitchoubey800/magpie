"""Expands the chapter template: {{file:path}} -> fenced code from the repo,
{{gh:a|b|c}} -> permalinks at a fixed commit (each path verified to exist there)."""
import re, subprocess, sys

REPO_DIR, TEMPLATE, OUT, SHA, RUN_URL = sys.argv[1:6]
BASE = f"https://github.com/ankitchoubey800/magpie/blob/{SHA}/"
LANG = {".cs": "csharp", ".csproj": "xml", ".json": "json", ".ts": "ts", ".tsx": "tsx", ".css": "css",
        ".yml": "yaml", ".md": "markdown", ".conf": "nginx", ".example": "bash"}

def git_show(path):
    r = subprocess.run(["git", "-C", REPO_DIR, "show", f"{SHA}:{path}"], capture_output=True, text=True)
    if r.returncode != 0:
        sys.exit(f"NOT IN COMMIT: {path}")
    return r.stdout.lstrip("﻿").rstrip("\n")

def lang_for(path):
    name = path.rsplit("/", 1)[-1]
    if name in ("Dockerfile",): return "dockerfile"
    if name in (".dockerignore", ".gitignore"): return "text"
    for ext, lang in LANG.items():
        if name.endswith(ext): return lang
    return "text"

def file_block(m):
    path = m.group(1)
    body = git_show(path)
    fence = "````" if "```" in body else "```"
    return f"**`{path}`**\n\n{fence}{lang_for(path)}\n{body}\n{fence}"

def gh_block(m):
    paths = m.group(1).split("|")
    for p in paths: git_show(p)
    links = " · ".join(f"[`{p.rsplit('/', 1)[-1]}`]({BASE}{p})" for p in paths)
    return f"> **GitHub (verified at commit `{SHA[:7]}`):** {links}"

text = open(TEMPLATE, encoding="utf-8").read()
text = re.sub(r"\{\{file:([^}]+)\}\}", file_block, text)
text = re.sub(r"\{\{gh:([^}]+)\}\}", gh_block, text)
text = text.replace("{{sha7}}", SHA[:7]).replace("{{run}}", RUN_URL)
left = re.findall(r"\{\{[^}]*\}\}", text)
if left: sys.exit(f"UNEXPANDED: {left}")
open(OUT, "w", encoding="utf-8").write(text)
print(f"wrote {OUT}: {len(text.split())} words, {text.count(BASE)} verified links")
