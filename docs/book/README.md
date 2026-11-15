# The Magpie book

One chapter per weekly block. Each chapter is generated from a template so the
code shown in the book is always the code in the repository.

| Chapter | Week | File |
|---|---|---|
| 1 | Project skeleton | [chapter-01-the-walking-skeleton.md](chapter-01-the-walking-skeleton.md) |
| 2 | Login and signup, part 1 | [chapter-02-who-goes-there.md](chapter-02-who-goes-there.md) |

To rebuild a chapter against a commit:

```bash
python3 docs/book/_build.py . docs/book/_chapter-01.template.md docs/book/chapter-01-the-walking-skeleton.md <commit-sha> <ci-run-url>
```

Diagrams are written in Mermaid, which GitHub renders directly. To make a PDF,
use a Markdown-to-PDF tool that supports Mermaid.
