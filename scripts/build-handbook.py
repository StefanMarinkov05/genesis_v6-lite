#!/usr/bin/env python3
"""Build the Genesis Architecture Handbook PDF from docs/.

The Markdown + Mermaid under docs/ stay the single source of truth (.claude/rules/documentation.md);
this script only assembles them into one book:

  1. concatenates the docs in reading order, grouped into parts;
  2. renders every ```mermaid block to a vector PDF with mermaid-cli and embeds it;
  3. rewrites links between docs into in-book cross-references, and links to other repo files
     into GitHub URLs (a PDF has no repo next to it);
  4. typesets the result with pandoc + XeLaTeX: a cover with the project logo, IBM Plex type
     (Serif / Sans / Mono, all with Cyrillic), the GUI's NOVA palette, framed and highlighted
     code blocks.

Usage:  python3 scripts/build-handbook.py [--out dist/dps-nn-handbook.pdf]

Needs pandoc 3.x, XeLaTeX (TeX Live with fvextra, tcolorbox, titlesec, fancyhdr) and the IBM Plex
fonts (TeX Live's `plex` package or the `fonts-ibm-plex` system package). Mermaid rendering uses
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
    ("Getting started", ["docs/explanation/project-overview.md", "docs/tutorial/genesis.md"]),
    ("Architecture", [
        "docs/explanation/architecture-decisions.md",
        "docs/explanation/agent-mode.md",
        "docs/explanation/generated-app-anatomy.md",
        "docs/explanation/gui-architecture.md",
        "docs/explanation/image-pipeline.md",
        "docs/explanation/specifications-consolidation.md",
        "docs/explanation/techniques.md",
        "docs/explanation/testing-policy.md",
    ]),
    ("Reference", [
        "docs/reference/control-flow.md",
        "docs/reference/components.md",
        "docs/reference/cli.md",
        "docs/reference/spec-format.md",
        "docs/reference/seed-format.md",
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
IMAGE = re.compile(r"!\[[^\]]*\]\(([^)\s]+)\)(?:\{width=([\d.]+)%\})?")

# Off when a caller (build-handbook2.py) assembles a subset from a different manuscript directory.
UNLISTED_NOTE = True
FRONT_MATTER = "<!-- front-matter -->"

# (old, new) applied to every doc before typesetting. Empty by default; a second manuscript can use it
# to render imported docs in its own language (e.g. strip Cyrillic from an English edition).
TEXT_TRANSFORMS: list[tuple[str, str]] = []
TEXT_TRANSFORM_FN = None  # optional callable(str) -> str, applied after TEXT_TRANSFORMS


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
    if unlisted and UNLISTED_NOTE:
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
    run([*mmdc, "-q", "-i", str(mmd), "-o", str(pdf), "--pdfFit", "-b", "white", "-p", str(pptr),
         "-c", str(work / "mermaid.json")])
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
            raw = (ROOT / doc).read_text(encoding="utf-8")
            # A doc may mark itself as front matter: its H1 then becomes an unnumbered chapter, so the
            # numbered chapters match the numbers the prose cites.
            front = FRONT_MATTER in raw
            text = HTML_COMMENT.sub("", raw).lstrip()
            for bad, good in GLYPHS.items():
                text = text.replace(bad, good)
            for old, new in TEXT_TRANSFORMS:
                text = text.replace(old, new)
            if TEXT_TRANSFORM_FN is not None:
                text = TEXT_TRANSFORM_FN(text)
            def image(m: re.Match) -> str:
                # emitted as raw LaTeX: pandoc's own image handling copies the file to a temp dir,
                # which xetex then refuses to measure.
                target, pct = m.group(1), m.group(2)
                if re.match(r"^[a-z]+:", target):
                    return m.group(0)
                resolved = (ROOT / target).resolve() if target.startswith("/") \
                    else ((ROOT / doc).parent / target).resolve()
                width = f"{float(pct) / 100:.2f}\\linewidth" if pct else r"0.60\linewidth"
                return ("```{=latex}\n\\begin{center}\\includegraphics[width=" + width +
                        "]{" + resolved.as_posix() + "}\\end{center}\n```")

            text = IMAGE.sub(image, text)

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
            if front:
                def unnumbered(m: re.Match) -> str:
                    title = m.group(1)
                    return ("```{=latex}\n\\chapter*{" + title + "}\n"
                            "\\addcontentsline{toc}{chapter}{" + title + "}\n```")
                text = H1.sub(unnumbered, text, count=1)
            else:
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


# The GUI's default NOVA theme (gui/src/renderer/styles.css): navy canvas, cyan accent. The pure
# accent is too light for text on white, so text-weight uses get a darkened variant.
PALETTE = {
    "navyA": "050B16", "navyB": "0A1830", "navyC": "0E2340",
    "accent": "3FD0FF", "accentdark": "0B6F94",
    "ink": "0E2340", "softtext": "A9C3E6", "mutedtext": "6D84A8", "muted": "7D8CA3",
    "rulecolor": "C9D6E5", "codebg": "F3F7FB", "codeink": "0A5A7A", "notebg": "EEF8FD",
}

# Every cut the book uses. TeX Live's `plex` package has them as .otf; Ubuntu's fonts-ibm-plex as .ttf.
PLEX_CUTS = [f"IBMPlex{fam}-{cut}" for fam in ("Serif", "Sans", "Mono")
             for cut in ("Regular", "SemiBold", "Italic", "SemiBoldItalic")] + \
            ["IBMPlexSans-Bold", "IBMPlexSans-Light"]
PLEX_DIRS = ["/usr/share/fonts/truetype/ibm-plex", "/usr/share/fonts/opentype/ibm-plex"]


def plex_fonts() -> tuple[str, str]:
    """(directory with a trailing slash, extension) of a complete IBM Plex install. fontspec then
    loads each cut by file name from that Path, which works the same for TeX Live's copy (local)
    and the system package (CI) - no fontconfig name lookup involved."""
    candidates: list[Path] = []
    try:
        hit = subprocess.run(["kpsewhich", "IBMPlexSerif-Regular.otf"], capture_output=True,
                             text=True).stdout.strip()
        if hit:
            candidates.append(Path(hit).parent)
    except OSError:
        pass
    candidates += [Path(d) for d in PLEX_DIRS]
    for d in candidates:
        for ext in (".otf", ".ttf"):
            if all((d / f"{c}{ext}").is_file() for c in PLEX_CUTS):
                return d.as_posix() + "/", ext
    sys.exit("build-handbook: IBM Plex fonts not found (TeX Live `plex`, or apt `fonts-ibm-plex`)")


HEADER = r"""
\usepackage{graphicx}  % diagrams are raw \includegraphics, so pandoc won't load it for us
\usepackage{tikz}
\usepackage[most]{tcolorbox}
\usepackage{fvextra}
\usepackage{etoolbox}
\usepackage{titlesec}
\usepackage{titletoc}
\usepackage{fancyhdr}
\usepackage{colortbl}

@COLORS@

@FONTSETUP@
\linespread{1.08}
\setlength{\emergencystretch}{3em}

% ---- headings: numbered chapters (not sections), sans, navy with a cyan rule -------------------
\setcounter{secnumdepth}{0}
\titleformat{\part}[display]
  {\thispagestyle{empty}%
   % a navy page via the shipout hook - a tikzpicture here would start a paragraph (titlesec error)
   \AddToHookNext{shipout/background}{%
     \put(0pt,-\paperheight){\textcolor{navyB}{\rule{\paperwidth}{\paperheight}}}%
     \put(2.5cm,-12.2cm){\textcolor{accent}{\rule{3cm}{1pt}}}}%
   \raggedright\color{white}}
  {\vspace*{5.2cm}\sffamily\large\color{accent}\addfontfeatures{LetterSpace=20}@PARTLABEL@~\thepart}
  {14pt}{\displayfont\fontsize{34}{40}\selectfont}
\titleformat{\chapter}[display]
  {\sffamily\color{ink}\raggedright\hyphenpenalty=10000}
  {\color{accentdark}\small\addfontfeatures{LetterSpace=20}@CHAPTERLABEL@~\thechapter}
  {6pt}{\displayfont\fontsize{24}{29}\selectfont}
  [\vspace{6pt}{\color{accent}\titlerule[1.4pt]}]
\titleformat{name=\chapter,numberless}[display]
  {\sffamily\color{ink}\raggedright\hyphenpenalty=10000}{}{0pt}{\displayfont\fontsize{24}{29}\selectfont}
  [\vspace{6pt}{\color{accent}\titlerule[1.4pt]}]
\titlespacing*{\chapter}{0pt}{-12pt}{20pt}
\titleformat{\section}{\sffamily\Large\bfseries\color{ink}\raggedright\hyphenpenalty=10000}{}{0pt}{}
\titleformat{\subsection}{\sffamily\large\bfseries\color{ink}\raggedright\hyphenpenalty=10000}{}{0pt}{}
\titleformat{\subsubsection}{\sffamily\normalsize\bfseries\color{accentdark}}{}{0pt}{}
\titlespacing*{\section}{0pt}{18pt plus 4pt minus 2pt}{6pt plus 2pt}
\titlespacing*{\subsection}{0pt}{14pt plus 4pt minus 2pt}{4pt plus 2pt}

% ---- contents ----------------------------------------------------------------------------------
\titlecontents{part}[0pt]
  {\addvspace{14pt}\sffamily\small\bfseries\color{accentdark}}
  {\addfontfeatures{LetterSpace=12}@PARTLABEL@~\thecontentslabel\quad}{\addfontfeatures{LetterSpace=12}}{}[\addvspace{4pt}]
\titlecontents{chapter}[2.4em]
  {\addvspace{1pt}\sffamily\color{ink}}
  {\contentslabel[\color{accentdark}\thecontentslabel]{2.4em}}{\hspace*{-2.4em}}
  {\color{rulecolor}\titlerule*[0.7pc]{.}\color{ink}\contentspage}

% ---- running heads -----------------------------------------------------------------------------
\pagestyle{fancy}
\fancyhf{}
\renewcommand{\chaptermark}[1]{\markboth{#1}{}}
\fancyhead[L]{\sffamily\scriptsize\color{muted}\leftmark}
\fancyhead[R]{\sffamily\scriptsize\color{muted}@RUNHEAD@ \textperiodcentered{} @HANDBOOK@}
% the project logo, faded, bottom centre of every content page (cover and part pages use the
% empty style, so they stay clean)
@FOOTLOGO@
\fancyfoot[C]{\footlogo}
\fancyfoot[R]{\sffamily\small\color{ink}\thepage}
\renewcommand{\headrulewidth}{0.4pt}
\renewcommand{\headrule}{{\color{rulecolor}\hrule height\headrulewidth}}
\fancypagestyle{plain}{\fancyhf{}\fancyfoot[C]{\footlogo}\fancyfoot[R]{\sffamily\small\color{ink}\thepage}%
  \renewcommand{\headrulewidth}{0pt}}
\setlength{\headheight}{14pt}

% ---- code: every block in a tinted box with a cyan edge; a language tag when there is one -----
\fvset{breaklines,breakanywhere,fontsize=\small}
\DefineVerbatimEnvironment{Highlighting}{Verbatim}{breaklines,breakanywhere,fontsize=\small,commandchars=\\\{\}}
\RecustomVerbatimEnvironment{verbatim}{Verbatim}{breaklines,breakanywhere,fontsize=\small}
\tcbset{codestyle/.style={enhanced jigsaw, breakable, colback=codebg, frame hidden, sharp corners,
  borderline west={2.4pt}{0pt}{accent}, left=10pt, right=8pt, top=6pt, bottom=6pt,
  before skip=8pt plus 2pt, after skip=10pt plus 2pt}}
\newtcolorbox{codebox}{codestyle}
\newtcolorbox{codeboxlang}[1]{codestyle, top=2pt, title={#1}, attach title to upper,
  fonttitle=\sffamily\scriptsize\bfseries\color{accentdark}\addfontfeatures{LetterSpace=10},
  after title={\hfill\par\vspace{1pt}}}
\newcommand{\code}[1]{\leavevmode{\color{codeink}\texttt{#1}}}  % \leavevmode: a \color opening a table cell would push it down a line

% ---- block quotes: notes in a light cyan panel ---------------------------------------------------
\newtcolorbox{notebox}{enhanced jigsaw, breakable, colback=notebg, frame hidden, sharp corners,
  borderline west={2.4pt}{0pt}{accentdark}, left=10pt, right=8pt, top=6pt, bottom=6pt,
  before skip=8pt, after skip=10pt}
\renewenvironment{quote}{\begin{notebox}}{\end{notebox}}

% ---- tables: sans, light rules ---------------------------------------------------------------
\arrayrulecolor{rulecolor}
\AtBeginEnvironment{longtable}{\small\sffamily}

@HEADEREXTRA@
"""

COVER = r"""
\begin{titlepage}
\thispagestyle{empty}
\begin{tikzpicture}[remember picture,overlay]
  \shade[top color=@COVERTOP@, bottom color=@COVERBOTTOM@] (current page.north west) rectangle (current page.south east);
  @COVERSTRIPE@
  \draw[accent,line width=0.8pt] ([xshift=2.4cm,yshift=-2.2cm]current page.north west) -- ([xshift=-2.4cm,yshift=-2.2cm]current page.north east);
  \node[anchor=north west,font=\sffamily\scriptsize,text=mutedtext] at ([xshift=2.4cm,yshift=-1.55cm]current page.north west)
    {\addfontfeatures{LetterSpace=20}@TOPLABEL@};
  \node[anchor=north east] at ([xshift=-2.4cm,yshift=-1.35cm]current page.north east) {@COVERBADGE@};
  \node[@COVERLOGOSTYLE@] at ([yshift=5.0cm]current page.center) {@COVERLOGO@};
  \node[anchor=north,align=center,text width=0.84\paperwidth] at ([yshift=-1.0cm]current page.center) {%
    {\displayfont@TITLESIZE@\selectfont\color{white}@TITLE@}\\[22pt]
    {\sffamily\Large\color{accent}\addfontfeatures{LetterSpace=22}@HANDBOOKUPPER@}\\[14pt]
    {\lightsans\large\color{softtext}@SUBTITLE@}};
  \draw[accent,line width=0.8pt] ([xshift=2.4cm,yshift=3.4cm]current page.south west) -- ([xshift=-2.4cm,yshift=3.4cm]current page.south east);
  \node[anchor=north west,align=left,font=\sffamily\small] at ([xshift=2.4cm,yshift=3.0cm]current page.south west)
    {\color{white}@AUTHOR@\\[2pt]\color{mutedtext}@VERSION@};
  \node[anchor=north east,align=right,font=\sffamily\small] at ([xshift=-2.4cm,yshift=3.0cm]current page.south east)
    {\color{softtext}@COVERTAGS@\\[2pt]\color{mutedtext}@LICENSELABEL@};
\end{tikzpicture}
\end{titlepage}
"""

# Inline code (paths, commands) is never hyphenated by TeX, so a long path runs off the page.
# Re-emit it as \code (coloured mono) with explicit break points after / _ . - , (headings left
# alone: they also feed the PDF bookmarks, which can't take raw LaTeX). Code blocks get wrapped
# in a box environment, table header cells are set bold.
FILTER = r"""
local esc = { ['\\'] = '\\textbackslash{}', ['{'] = '\\{', ['}'] = '\\}', ['$'] = '\\$',
  ['&'] = '\\&', ['#'] = '\\#', ['^'] = '\\^{}', ['_'] = '\\_', ['%'] = '\\%',
  ['~'] = '\\textasciitilde{}' }
local function breakable(text)
  local out = text:gsub('[\\{}$&#^_%%~]', esc)
  if #text < 16 then return out end
  return (out:gsub('([/%.,%-]+)', '%1\\allowbreak{}'):gsub('(\\_)', '%1\\allowbreak{}'))
end
local LABELS = { bash = 'SHELL', sh = 'SHELL', powershell = 'POWERSHELL', json = 'JSON',
  csharp = 'C\\#', cs = 'C\\#', xml = 'XML', html = 'HTML', yaml = 'YAML', ts = 'TYPESCRIPT',
  typescript = 'TYPESCRIPT', js = 'JAVASCRIPT', javascript = 'JAVASCRIPT', sql = 'SQL' }
return {
  { traverse = 'topdown',
    Header = function(h) return h, false end,
    Code = function(c)
      return pandoc.RawInline('latex', '\\code{' .. breakable(c.text) .. '}')
    end,
    CodeBlock = function(b)
      local lang = b.classes[1]
      local open = '\\begin{codebox}'
      local close = '\\end{codebox}'
      if lang then
        open = '\\begin{codeboxlang}{' .. (LABELS[lang] or lang:upper()) .. '}'
        close = '\\end{codeboxlang}'
      end
      return { pandoc.RawBlock('latex', open), b, pandoc.RawBlock('latex', close) }, false
    end,
    Table = function(t)
      for _, row in ipairs(t.head.rows) do
        for _, cell in ipairs(row.cells) do
          cell.contents = cell.contents:walk({
            Plain = function(p) return pandoc.Plain({ pandoc.Strong(p.content) }) end })
        end
      end
      return t
    end }
}
"""

# Syntax colours for light code boxes, in the palette (pandoc --syntax-highlighting theme file).
SYNTAX = {
    "text-color": None, "background-color": None,
    "line-number-color": "#9AA7B8", "line-number-background-color": None,
    "text-styles": {
        "Keyword": {"text-color": "#0B6F94", "bold": True},
        "ControlFlow": {"text-color": "#0B6F94", "bold": True},
        "DataType": {"text-color": "#7A4FB0"},
        "BuiltIn": {"text-color": "#7A4FB0"},
        "Function": {"text-color": "#1F4E9C"},
        "Variable": {"text-color": "#0E2340"},
        "String": {"text-color": "#2F7D4A"},
        "VerbatimString": {"text-color": "#2F7D4A"},
        "SpecialString": {"text-color": "#2F7D4A"},
        "Char": {"text-color": "#2F7D4A"},
        "SpecialChar": {"text-color": "#B5561D"},
        "DecVal": {"text-color": "#B5561D"}, "BaseN": {"text-color": "#B5561D"},
        "Float": {"text-color": "#B5561D"}, "Constant": {"text-color": "#B5561D"},
        "Comment": {"text-color": "#5F7088", "italic": True},
        "Documentation": {"text-color": "#5F7088", "italic": True},
        "CommentVar": {"text-color": "#5F7088", "italic": True},
        "Annotation": {"text-color": "#5F7088", "italic": True},
        "Attribute": {"text-color": "#7A4FB0"},
        "Operator": {"text-color": "#41597A"},
        "Preprocessor": {"text-color": "#B5561D"},
        "Import": {"text-color": "#0B6F94"},
        "Extension": {"text-color": "#1F4E9C"},
        "Other": {"text-color": "#1F4E9C"},
        "Alert": {"text-color": "#C0392B", "bold": True},
        "Error": {"text-color": "#C0392B", "bold": True},
        "Warning": {"text-color": "#B5561D", "italic": True},
        "Information": {"text-color": "#5F7088", "italic": True},
    },
}

# Mermaid "base" theme recoloured to the palette.
MERMAID_CONFIG = {
    "theme": "base",
    "themeVariables": {
        "fontFamily": "IBM Plex Sans, DejaVu Sans, sans-serif",
        "primaryColor": "#E6F6FD", "primaryBorderColor": "#0B6F94", "primaryTextColor": "#0E2340",
        "secondaryColor": "#F1F5FA", "secondaryBorderColor": "#9FB6CF",
        "tertiaryColor": "#FBFCFE", "tertiaryBorderColor": "#C9D6E5",
        "lineColor": "#41597A", "textColor": "#0E2340",
        "clusterBkg": "#F4F8FC", "clusterBorder": "#9FB6CF", "edgeLabelBackground": "#FFFFFF",
        "noteBkgColor": "#FFF8E1", "noteBorderColor": "#D9B44A", "noteTextColor": "#0E2340",
        "actorBkg": "#E6F6FD", "actorBorder": "#0B6F94", "actorTextColor": "#0E2340",
        "signalColor": "#41597A", "signalTextColor": "#0E2340",
    },
}

AUTHOR = "Stefan Marinkov"
BOOK_TITLE = "Genesis"
BOOK_SUBTITLE = "A spec-driven generator for Bulgarian-UI ASP.NET Core MVC apps"
BOOK_TOPLABEL = "GENESIS~\\textperiodcentered~APPLICATION~GENERATOR"
COVER_BADGE = ""  # optional TikZ language badge, supplied per edition
HEADER_EXTRA = ""  # optional LaTeX added to the header (fonts, macros)
BOOK_PART_LABEL = "PART"
BOOK_CHAPTER_LABEL = "CHAPTER"
BOOK_HANDBOOK = "Architecture Handbook"
BOOK_HANDBOOK_UPPER = r"ARCHITECTURE\hspace{0.5em}HANDBOOK"  # letterspacing eats a tie
BOOK_TOC_TITLE = "Contents"
BOOK_COVER_TAGS = r"Explanation \textperiodcentered{} Reference \textperiodcentered{} How-to \textperiodcentered{} ADRs"
BOOK_LICENSE_LABEL = "MIT License"
FONTSETUP = r"""% ---- type: IBM Plex, all three cuts carry Cyrillic --------------------------------------------
\setmainfont{IBMPlexSerif}[Path=@PLEX@, Extension=@EXT@, UprightFont=*-Regular,
  BoldFont=*-SemiBold, ItalicFont=*-Italic, BoldItalicFont=*-SemiBoldItalic]
\setsansfont{IBMPlexSans}[Path=@PLEX@, Extension=@EXT@, UprightFont=*-Regular,
  BoldFont=*-SemiBold, ItalicFont=*-Italic, BoldItalicFont=*-SemiBoldItalic]
\setmonofont{IBMPlexMono}[Path=@PLEX@, Extension=@EXT@, Scale=0.86, UprightFont=*-Regular,
  BoldFont=*-SemiBold, ItalicFont=*-Italic, BoldItalicFont=*-SemiBoldItalic]
\newfontfamily\displayfont{IBMPlexSans}[Path=@PLEX@, Extension=@EXT@, UprightFont=*-Bold]
\newfontfamily\lightsans{IBMPlexSans}[Path=@PLEX@, Extension=@EXT@, UprightFont=*-Light]"""

COVERLOGO = r"\includegraphics[width=0.78\paperwidth]{@LOGO@}"
COVERLOGOSTYLE = ""  # node options, e.g. a light plate behind dark artwork
FOOTLOGO = r"""\newcommand{\footlogo}{\raisebox{-5mm}{\tikz\node[opacity=0.92,inner sep=0pt]{\includegraphics[height=16mm]{@LOGO@}};}}"""
COVER_TITLE_SIZE = r"\fontsize{50}{56}"  # per-edition: the galactic title needs less
BOOK_COVER_TITLE = ""  # defaults to BOOK_TITLE
BOOK_META_TITLE = ""   # defaults to BOOK_TITLE
BOOK_HANDBOOK_META = ""  # defaults to BOOK_HANDBOOK (PDF metadata stays plain text)

COVER_TOP = "navyC"
COVER_BOTTOM = "navyA"
COVER_STRIPE = ""  # optional bottom band (national colours) drawn after the gradient
BOOK_RUNHEAD = "Genesis"
LOGO = ROOT / "gui/src/renderer/public/logo/logo.png"


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
    (work / "mermaid.json").write_text(json.dumps(MERMAID_CONFIG), encoding="utf-8")
    mmdc = shlex.split(os.environ.get("MMDC", MMDC_DEFAULT))

    book = assemble(collect_parts(), work, mmdc, pptr)
    src = work / "handbook.md"
    src.write_text(book, encoding="utf-8")
    colors = "\n".join(f"\\definecolor{{{k}}}{{HTML}}{{{v}}}" for k, v in PALETTE.items())
    header = work / "header.tex"
    plex_path, plex_ext = plex_fonts()
    header.write_text(HEADER.replace("@FONTSETUP@", FONTSETUP)
                      .replace("@COLORS@", colors).replace("@PLEX@", plex_path).replace("@EXT@", plex_ext)
                      .replace("@RUNHEAD@", BOOK_RUNHEAD).replace("@HEADEREXTRA@", HEADER_EXTRA)
                      .replace("@PARTLABEL@", BOOK_PART_LABEL)
                      .replace("@CHAPTERLABEL@", BOOK_CHAPTER_LABEL)
                      .replace("@HANDBOOK@", BOOK_HANDBOOK)
                      .replace("@HANDBOOKUPPER@", BOOK_HANDBOOK_UPPER)
                      .replace("@FOOTLOGO@", FOOTLOGO)
                      .replace("@LOGO@", LOGO.as_posix()), encoding="utf-8")
    cover = work / "cover.tex"
    cover.write_text(COVER.replace("@AUTHOR@", AUTHOR)
                     .replace("@COVERLOGOSTYLE@", COVERLOGOSTYLE)
                     .replace("@COVERLOGO@", COVERLOGO.replace("@LOGO@", LOGO.as_posix()))
                     .replace("@TOPLABEL@", BOOK_TOPLABEL)
                     .replace("@HANDBOOKUPPER@", BOOK_HANDBOOK_UPPER)
                     .replace("@TITLE@", BOOK_COVER_TITLE or BOOK_TITLE)
                     .replace("@TITLESIZE@", COVER_TITLE_SIZE)
                     .replace("@SUBTITLE@", BOOK_SUBTITLE)
                     .replace("@COVERBADGE@", COVER_BADGE)
                     .replace("@COVERTOP@", COVER_TOP).replace("@COVERBOTTOM@", COVER_BOTTOM)
                     .replace("@COVERSTRIPE@", COVER_STRIPE)
                     .replace("@COVERTAGS@", BOOK_COVER_TAGS)
                     .replace("@LICENSELABEL@", BOOK_LICENSE_LABEL)
                     .replace("@VERSION@", version_line()), encoding="utf-8")
    lua = work / "handbook.lua"
    lua.write_text(FILTER, encoding="utf-8")
    theme = work / "syntax.theme"
    theme.write_text(json.dumps(SYNTAX, indent=1), encoding="utf-8")

    out = (ROOT / args.out).resolve()
    out.parent.mkdir(parents=True, exist_ok=True)
    run([
        "pandoc", str(src), "-f", "markdown+lists_without_preceding_blankline", "-o", str(out),
        "--pdf-engine=xelatex", "--toc", "--toc-depth=1", "--top-level-division=chapter",
        "--syntax-highlighting", str(theme),
        "-H", str(header), "-B", str(cover), "--lua-filter", str(lua),
        # No title metadata: the cover replaces \maketitle (pandoc's \subtitle would append to it).
        "-V", f"title-meta={(BOOK_META_TITLE or BOOK_TITLE)} - {(BOOK_HANDBOOK_META or BOOK_HANDBOOK)}", "-V", f"author-meta={AUTHOR}",
        "-V", "documentclass=report", "-V", "classoption=oneside", "-V", "fontsize=10pt",
        "-V", "geometry:a4paper,top=2.6cm,bottom=3.5cm,left=2.5cm,right=2.5cm,headsep=0.8cm,footskip=1.8cm",
        "-V", "colorlinks=true", "-V", "linkcolor=accentdark", "-V", "urlcolor=accentdark",
        "-V", "toccolor=ink", "-V", f"toc-title={BOOK_TOC_TITLE}",
    ], cwd=ROOT)
    print(f"build-handbook: wrote {out.relative_to(ROOT) if out.is_relative_to(ROOT) else out}")


if __name__ == "__main__":
    main()
