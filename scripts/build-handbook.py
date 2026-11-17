#!/usr/bin/env python3
"""Build the ДПС-НН Architecture Handbook PDF from docs/.

The Markdown + Mermaid under docs/ stay the single source of truth (.claude/rules/documentation.md);
this script only assembles them into one book:

  1. concatenates the docs in reading order, grouped into parts;
  2. renders every ```mermaid block to a vector PDF with mermaid-cli and embeds it;
  3. rewrites links between docs into in-book cross-references, and links to other repo files
     into GitHub URLs (a PDF has no repo next to it);
  4. typesets the result with pandoc + XeLaTeX (DejaVu fonts, for the Cyrillic).

Usage:  python3 scripts/build-handbook.py [--out dist/dps-nn-handbook.pdf]

Needs pandoc 3.x, XeLaTeX (TeX Live with fvextra) and DejaVu fonts. Mermaid rendering uses
$MMDC if set (a mermaid-cli command line), else `npx -y @mermaid-js/mermaid-cli@11.17.1`.
mermaid-cli drives a headless browser via Puppeteer; set PUPPETEER_EXECUTABLE_PATH to reuse an
existing Chromium instead of letting Puppeteer download one.
"""
from __future__ import annotations

import argparse
import json
import os
import re
import shlex
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
REPO_URL = "https://github.com/StefanMarinkov05/genesis_v6"
MMDC_DEFAULT = "npx -y @mermaid-js/mermaid-cli@11.17.1"

# Reading order. Explanation comes before reference: a handbook reader wants the shape and the
# "why" first, the lookup tables second. ADRs close the book as the decision record.
PARTS: list[tuple[str, list[str]]] = [
    ("Getting started", ["docs/01-tutorial-genesis.md"]),
    ("Architecture", [
        "docs/explanation/architecture-decisions.md",
        "docs/explanation/agent-mode.md",
        "docs/explanation/gui-architecture.md",
        "docs/explanation/image-pipeline.md",
        "docs/explanation/specifications-consolidation.md",
        "docs/explanation/techniques.md",
    ]),
    ("Reference", [
        "docs/reference/control-flow.md",
        "docs/reference/cli.md",
        "docs/reference/spec-format.md",
        "docs/reference/file-map.md",
    ]),
    ("How-to guides", ["docs/how-to/add-a-stage.md", "docs/how-to/add-a-template.md", "docs/how-to/build-the-installer.md", "docs/how-to/mirror-the-lite-edition.md"]),
    ("Architecture decision records", None),  # filled from docs/adr/ below
]

# Glyphs DejaVu lacks; substituted so XeLaTeX doesn't silently drop them.
GLYPHS = {"✅": "yes", "❌": "no"}

HTML_COMMENT = re.compile(r"<!--.*?-->", re.S)
MERMAID = re.compile(r"^```mermaid\s*\n(.*?)^```\s*$", re.S | re.M)
LINK = re.compile(r"(!?\[[^\]]*\])\(([^)\s]+)\)")
H1 = re.compile(r"^# (.+)$", re.M)


def run(cmd: list[str], **kw) -> None:
    print("  $", " ".join(shlex.quote(c) for c in cmd), flush=True)
    subprocess.run(cmd, check=True, **kw)


def slug_for(rel: str) -> str:
    return "doc-" + re.sub(r"[^a-z0-9]+", "-", Path(rel).stem.lower()).strip("-")


def collect_parts() -> list[tuple[str, list[str]]]:
    adrs = sorted(p.relative_to(ROOT).as_posix() for p in (ROOT / "docs/adr").glob("adr-*.md"))
    parts = [(title, docs if docs is not None else adrs) for title, docs in PARTS]
    for _, docs in parts:
        for d in docs:
            if not (ROOT / d).is_file():
                sys.exit(f"build-handbook: listed doc is missing: {d}")
    listed = {d for _, docs in parts for d in docs}
    unlisted = sorted(p.relative_to(ROOT).as_posix() for p in (ROOT / "docs").rglob("*.md")
                      if p.relative_to(ROOT).as_posix() not in listed)
    if unlisted:
        print(f"build-handbook: note - not in the handbook: {', '.join(unlisted)}")
    return parts


EDGE = re.compile(r"-->|-\.->|==>")


def for_print(src: str) -> str:
    """Long left-to-right chains read well on a wide screen but shrink to unreadable on a portrait
    page; draw those top-to-bottom in the book (the source doc keeps LR for GitHub)."""
    if len(EDGE.findall(src)) >= 6:
        return re.sub(r"^(\s*(?:flowchart|graph))\s+LR\b", r"\1 TD", src, count=1, flags=re.M)
    return src


def render_mermaid(src: str, name: str, work: Path, mmdc: list[str], pptr: Path) -> Path:
    src = for_print(src)
    mmd, pdf = work / f"{name}.mmd", work / f"{name}.pdf"
    mmd.write_text(src, encoding="utf-8")
    run([*mmdc, "-q", "-i", str(mmd), "-o", str(pdf), "--pdfFit", "-b", "white", "-p", str(pptr)])
    return pdf


def rewrite_links(text: str, doc: str, anchors: dict[str, str]) -> str:
    base = (ROOT / doc).parent

    def fix(m: re.Match) -> str:
        label, target = m.group(1), m.group(2)
        if re.match(r"^[a-z]+:", target) or target.startswith("#"):
            return m.group(0)
        path = target.split("#", 1)[0]
        resolved = (base / path).resolve()
        try:
            rel = resolved.relative_to(ROOT).as_posix()
        except ValueError:
            return m.group(0)
        if rel in anchors:
            return f"{label}(#{anchors[rel]})"
        if resolved.exists():
            kind = "tree" if resolved.is_dir() else "blob"
            return f"{label}({REPO_URL}/{kind}/master/{rel})"
        return m.group(0)

    return LINK.sub(fix, text)


SEP_ROW = re.compile(r"^\|?(\s*:?-+:?\s*\|)+\s*:?-+:?\s*\|?\s*$")


def cells(row: str) -> list[str]:
    return [c.strip() for c in row.strip().strip("|").split("|")]


def weight_tables(text: str) -> str:
    """Pandoc sizes a wide pipe table's columns by the dash counts in its separator row; the docs
    use equal `|---|---|` everywhere, which squeezes prose columns. Re-weight by actual content."""
    lines, out, fence = text.split("\n"), [], False
    for i, line in enumerate(lines):
        if line.lstrip().startswith("```"):
            fence = not fence
        if fence or i == 0 or not SEP_ROW.match(line):
            out.append(line)
            continue
        rows = [cells(lines[i - 1])]
        for nxt in lines[i + 1:]:
            if not nxt.strip().startswith("|"):
                break
            rows.append(cells(nxt))
        aligns = cells(line)
        avg = [sum(len(r[c]) if c < len(r) else 0 for r in rows) / len(rows) for c in range(len(aligns))]
        # A column must still fit its longest unbreakable token (identifiers, stage names).
        longest = [max((len(t) for r in rows if c < len(r) for t in r[c].split()), default=0)
                   for c in range(len(aligns))]
        seps = []
        for a, al in zip(avg, aligns):
            dashes = "-" * max(4, round(min(a, 120) / 2), min(longest[len(seps)], 28))
            seps.append((":" if al.startswith(":") else "") + dashes + (":" if al.endswith(":") and len(al) > 1 else ""))
        out.append("|" + "|".join(seps) + "|")
    return "\n".join(out)


def assemble(parts, work: Path, mmdc: list[str], pptr: Path) -> str:
    anchors = {d: slug_for(d) for _, docs in parts for d in docs}
    out: list[str] = []
    n_diagrams = 0
    for title, docs in parts:
        out.append(f"\n```{{=latex}}\n\\part{{{title}}}\n```\n")
        for doc in docs:
            text = HTML_COMMENT.sub("", (ROOT / doc).read_text(encoding="utf-8")).lstrip()
            for bad, good in GLYPHS.items():
                text = text.replace(bad, good)
            text = rewrite_links(text, doc, anchors)

            def diagram(m: re.Match) -> str:
                nonlocal n_diagrams
                n_diagrams += 1
                pdf = render_mermaid(m.group(1), f"{anchors[doc]}-{n_diagrams}", work, mmdc, pptr)
                return ("```{=latex}\n\\begin{center}\\includegraphics[width=\\linewidth,"
                        f"height=0.72\\textheight,keepaspectratio]{{{pdf.as_posix()}}}\\end{{center}}\n```")

            text = MERMAID.sub(diagram, text)
            text = weight_tables(text)
            if not H1.search(text):
                sys.exit(f"build-handbook: {doc} has no '# ' title")
            text = H1.sub(lambda m: f"# {m.group(1)} {{#{anchors[doc]}}}", text, count=1)
            out.append(text.rstrip() + "\n")
    print(f"build-handbook: {sum(len(d) for _, d in parts)} docs, {n_diagrams} diagrams")
    return "\n".join(out)


def version_line() -> str:
    version = (ROOT / "VERSION").read_text(encoding="utf-8").strip()
    try:
        sha = subprocess.run(["git", "-C", str(ROOT), "rev-parse", "--short", "HEAD"],
                             capture_output=True, text=True, check=True).stdout.strip()
    except (OSError, subprocess.CalledProcessError):
        sha = "unknown"
    return f"Version {version} ({sha})"


HEADER = r"""
\usepackage{graphicx}  % diagrams are raw \includegraphics, so pandoc won't load it for us
\usepackage{fvextra}
\DefineVerbatimEnvironment{Highlighting}{Verbatim}{breaklines,breakanywhere,commandchars=\\\{\}}
\fvset{breaklines,breakanywhere}
\RecustomVerbatimEnvironment{verbatim}{Verbatim}{breaklines,breakanywhere}  % un-highlighted blocks too
\usepackage{etoolbox}
\AtBeginEnvironment{longtable}{\small}
\setlength{\emergencystretch}{3em}
"""

# Inline code (paths, commands) is never hyphenated by TeX, so a long path runs off the page.
# Re-emit it as \texttt with explicit break points after / _ . - , (headings left alone: they
# also feed the PDF bookmarks, which can't take raw LaTeX).
BREAKABLE_CODE = r"""
local esc = { ['\\'] = '\\textbackslash{}', ['{'] = '\\{', ['}'] = '\\}', ['$'] = '\\$',
  ['&'] = '\\&', ['#'] = '\\#', ['^'] = '\\^{}', ['_'] = '\\_', ['%'] = '\\%',
  ['~'] = '\\textasciitilde{}' }
local function breakable(text)
  local out = text:gsub('[\\{}$&#^_%%~]', esc)
  return (out:gsub('([/%.,%-]+)', '%1\\allowbreak{}'):gsub('(\\_)', '%1\\allowbreak{}'))
end
return {
  { traverse = 'topdown',
    Header = function(h) return h, false end,
    Code = function(c)
      if #c.text < 16 then return nil end
      return pandoc.RawInline('latex', '\\texttt{' .. breakable(c.text) .. '}')
    end }
}
"""


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("--out", default="dist/dps-nn-handbook.pdf")
    args = ap.parse_args()

    work = ROOT / "build" / "handbook"
    work.mkdir(parents=True, exist_ok=True)
    pptr = work / "puppeteer.json"
    cfg: dict = {"args": ["--no-sandbox"]}  # CI runners (Ubuntu 23.10+) restrict unprivileged sandboxes
    if exe := os.environ.get("PUPPETEER_EXECUTABLE_PATH"):
        cfg["executablePath"] = exe
    pptr.write_text(json.dumps(cfg), encoding="utf-8")
    mmdc = shlex.split(os.environ.get("MMDC", MMDC_DEFAULT))

    book = assemble(collect_parts(), work, mmdc, pptr)
    src = work / "handbook.md"
    src.write_text(book, encoding="utf-8")
    header = work / "header.tex"
    header.write_text(HEADER, encoding="utf-8")
    lua = work / "breakable-code.lua"
    lua.write_text(BREAKABLE_CODE, encoding="utf-8")

    out = (ROOT / args.out).resolve()
    out.parent.mkdir(parents=True, exist_ok=True)
    run([
        "pandoc", str(src), "-f", "markdown", "-o", str(out),
        "--pdf-engine=xelatex", "--toc", "--toc-depth=1", "--top-level-division=chapter",
        "-H", str(header), "--lua-filter", str(lua),
        "-M", "title=ДПС-НН (dps-nn)",
        "-M", "subtitle=Architecture Handbook",
        "-M", f"date={version_line()}",
        "-M", "author=Stefan Marinkov",
        "-V", "documentclass=report", "-V", "classoption=oneside", "-V", "fontsize=10pt",
        "-V", "geometry:a4paper,margin=2.2cm",
        "-V", "mainfont=DejaVu Serif", "-V", "sansfont=DejaVu Sans", "-V", "monofont=DejaVu Sans Mono",
        "-V", "colorlinks=true", "-V", "linkcolor=blue", "-V", "urlcolor=blue", "-V", "toccolor=black",
    ], cwd=ROOT)
    print(f"build-handbook: wrote {out.relative_to(ROOT) if out.is_relative_to(ROOT) else out}")


if __name__ == "__main__":
    main()
