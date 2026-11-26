#!/usr/bin/env python3
"""Build edition 2 of the Genesis handbook, in one of four languages (plus the Trump edition).

    python3 scripts/build-handbook2.py --lang en   # dist/dps-nn-handbook-v2.pdf
    python3 scripts/build-handbook2.py --lang bg   # dist/dps-nn-handbook-bg.pdf
    python3 scripts/build-handbook2.py --lang de   # dist/dps-nn-handbook-de.pdf
    python3 scripts/build-handbook2.py --lang xx   # dist/dps-nn-handbook-galactic.pdf
    python3 scripts/build-handbook2.py --lang trump  # dist/dps-nn-handbook-trump.pdf
    python3 scripts/build-handbook2.py --lang trump-bg  # dist/dps-nn-handbook-trump-bg.pdf

English (`docs/handbook/`) is the source edition. `docs/handbook/bg/` and `docs/handbook/de/` are translations
with the same chapter files, so the reading order, numbering and diagrams stay identical. `xx` is
the English text rendered in a substitution alphabet: every Latin letter is mapped to a Unicode
character from a non-Latin script, code spans and code blocks are left alone, and the cover gets a
starfield and an amber palette. `trump` is the English text retold in a bombastic stump-speech
voice (`docs/handbook/trump/`, all 26 chapters, facts unchanged), with a navy/red/gold reskin, Playfair
Display headings and the MDGA mark on the cover.

The heavy lifting (LaTeX header, palette, cover, Mermaid, table weighting) is reused from
build-handbook.py; this script only chooses the manuscript, the language styling and the output.
"""
from __future__ import annotations

import argparse
import importlib.util
import re
import sys
from pathlib import Path

sys.dont_write_bytecode = True

ROOT = Path(__file__).resolve().parent.parent
_spec = importlib.util.spec_from_file_location("build_handbook", ROOT / "scripts" / "build-handbook.py")
bh = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(bh)  # __name__ != "__main__", so main() is not run on import

D = "docs/handbook"

CHAPTERS = {
    0: "00-preface", 1: "01-what-it-is", 2: "02-a-generation-story",
    3: "03-what-a-generated-app-is", 4: "04-first-run", 5: "05-paper-to-spec",
    6: "06-spec-to-seed", 7: "07-agent-mode", 8: "08-architecture-in-one-picture",
    9: "09-workflow", 10: "10-generated-app-architecture", 11: "11-contracts",
    12: "12-principles", 13: "13-components", 14: "14-quality",
    15: "15-how-the-project-is-built", 16: "16-runtime",
    17: "17-cli", 18: "18-gui-architecture", 19: "19-image-pipeline",
    20: "20-specifications-page", 21: "21-techniques",
    22: "22-limits", 23: "23-the-delivered-application", 24: "24-glossary",
    25: "25-decisions",
}

DEEP_DIVES_EN = [
    "docs/reference/cli.md",
    "docs/explanation/gui-architecture.md",
    "docs/explanation/image-pipeline.md",
    "docs/explanation/specifications-consolidation.md",
    "docs/explanation/techniques.md",
]

PARTS = [
    ("Overview", [0, 1, 2, 3]),
    ("Using it", [4, 5, 6, 7]),
    ("Inside", [8, 9, 10, 11, 12]),
    ("Architecture in depth", [13, 14, 15, 16]),
    ("Deep dives", [17, 18, 19, 20, 21]),
    ("Reference", [22, 23, 24, 25]),
]

# ---- language styling ---------------------------------------------------------------------------

# Flags drawn at 0.95 x 0.33 in cover coordinates, y growing up: the top band is the highest range.
FLAG_BG = r"""
\begin{tikzpicture}[baseline=-0.5ex]
  \node[anchor=east,font=\sffamily\footnotesize,text=softtext] (t) at (0,0) {БЪЛГАРСКО ИЗДАНИЕ};
  \begin{scope}[shift={(t.east)},xshift=6pt]
    \fill[white]       (0,0.28) rectangle (0.95,0.39);
    \fill[green!55!black] (0,0.17) rectangle (0.95,0.28);
    \fill[red!85!black]   (0,0.06) rectangle (0.95,0.17);
    \draw[softtext,line width=0.3pt] (0,0.06) rectangle (0.95,0.39);
  \end{scope}
\end{tikzpicture}
"""

FLAG_DE = r"""
\begin{tikzpicture}[baseline=-0.5ex]
  \node[anchor=east,font=\sffamily\footnotesize,text=softtext] (t) at (0,0) {DEUTSCHE AUSGABE};
  \begin{scope}[shift={(t.east)},xshift=6pt]
    \fill[black]          (0,0.28) rectangle (0.95,0.39);
    \fill[red!85!black]   (0,0.17) rectangle (0.95,0.28);
    \fill[yellow!85!black](0,0.06) rectangle (0.95,0.17);
    \draw[softtext,line width=0.3pt] (0,0.06) rectangle (0.95,0.39);
  \end{scope}
\end{tikzpicture}
"""

# A starfield as the emblem of a language that has no country.
def flag_xx(label: str) -> str:
    return (r"""
\begin{tikzpicture}[baseline=-0.5ex]
  \node[anchor=east,font=\sffamily\footnotesize,text=accent] (t) at (0,0) {""" + label + r"""};
  \begin{scope}[shift={(t.east)},xshift=8pt]
    \fill[white] (0.02,0.20) circle (0.012);
    \fill[white] (0.14,0.30) circle (0.009);
    \fill[white] (0.26,0.12) circle (0.010);
    \fill[white] (0.38,0.27) circle (0.007);
    \fill[white] (0.50,0.18) circle (0.013);
    \fill[white] (0.62,0.32) circle (0.008);
    \fill[white] (0.72,0.10) circle (0.011);
    \fill[white] (0.10,0.08) circle (0.007);
    \fill[white] (0.58,0.05) circle (0.006);
    \draw[accent,line width=0.5pt] (0.10,0.08) -- (0.26,0.12) -- (0.38,0.27) -- (0.62,0.32);
  \end{scope}
\end{tikzpicture}
""")

STRIPE_BG = r"""
  \fill[red!85!black]  (current page.south west) rectangle ([yshift=0.7cm]current page.south east);
  \fill[green!55!black]([yshift=0.7cm]current page.south west) rectangle ([yshift=1.4cm]current page.south east);
  \fill[white]         ([yshift=1.4cm]current page.south west) rectangle ([yshift=2.1cm]current page.south east);
"""

STRIPE_DE = r"""
  \fill[yellow!85!black](current page.south west) rectangle ([yshift=0.7cm]current page.south east);
  \fill[red!85!black]  ([yshift=0.7cm]current page.south west) rectangle ([yshift=1.4cm]current page.south east);
  \fill[black]         ([yshift=1.4cm]current page.south west) rectangle ([yshift=2.1cm]current page.south east);
"""

# Part titles and the fixed book furniture, per language. Numbers stay Arabic in every edition.
PARTS_EN = ["Overview", "Using it", "Inside", "Architecture in depth", "Deep dives", "Reference"]
PARTS_BG = ["Преглед", "Използване", "Отвътре", "Архитектура в дълбочина", "Задълбочени глави", "Справочник"]
PARTS_DE = ["Überblick", "Benutzung", "Innenleben", "Architektur im Detail", "Vertiefungen", "Referenz"]

# ---- type and the mark, per edition ---------------------------------------------------------------

# All three marks are transparent PNGs: the original ДПС-НН artwork, plus the German and galactic
# variants the author supplied. They sit directly on the cover gradient and in the footer.
DPSNN_LOGO = dict(logo="dpsnn", header_extra="",
                  cover_logo=r"\includegraphics[width=0.72\paperwidth]{@LOGO@}",
                  foot_logo=r"\newcommand{\footlogo}{\raisebox{-5mm}{\tikz\node[opacity=0.92,inner sep=0pt]"
                            r"{\includegraphics[height=16mm]{@LOGO@}};}}")


GALACTIC_FONTS = r"""% ---- type: FreeSans / FreeSerif, between them the 26 galactic symbols ------------------------
% No installed face carries the symbols *and* a bold or italic weight, so weight and slant are
% synthesised rather than switched to a face that is missing the glyphs.
\setmainfont{FreeSans}[BoldFont={FreeSans}, ItalicFont={FreeSans}, BoldItalicFont={FreeSans},
  BoldFeatures={FakeBold=2}, ItalicFeatures={FakeSlant=0.2},
  BoldItalicFeatures={FakeBold=2,FakeSlant=0.2}]
\setsansfont{FreeSans}[BoldFont={FreeSans}, ItalicFont={FreeSans}, BoldItalicFont={FreeSans},
  BoldFeatures={FakeBold=2}, ItalicFeatures={FakeSlant=0.2},
  BoldItalicFeatures={FakeBold=2,FakeSlant=0.2}]
\setmonofont{DejaVu Sans Mono}[Scale=0.86]
\newfontfamily\displayfont{FreeSans}[BoldFont={FreeSans}, BoldFeatures={FakeBold=3},
  ItalicFont={FreeSans}, ItalicFeatures={FakeSlant=0.2}]
\newfontfamily\lightsans{FreeSans}
\newfontfamily\galsans{FreeSans}[BoldFont={FreeSans}, BoldFeatures={FakeBold=3},
  ItalicFont={FreeSans}, ItalicFeatures={FakeSlant=0.2}]
\newfontfamily\galserif{FreeSerif}[BoldFont={FreeSerif}, BoldFeatures={FakeBold=3},
  ItalicFont={FreeSerif}, ItalicFeatures={FakeSlant=0.2}]
\newfontfamily\galcjk{Noto Sans CJK JP}
"""

PALETTE_EN = {}
PALETTE_BG = {  # Bulgarian tricolour: green carries the text, red appears in the band
    "navyA": "07120C", "navyB": "0C2117", "navyC": "103520",
    "accent": "12925B", "accentdark": "0B6A42",
    "ink": "12211A", "softtext": "C6DCCB", "mutedtext": "7C9385", "muted": "6F8678",
    "rulecolor": "CFDCD2", "codebg": "F2F7F3", "codeink": "0A6B47", "notebg": "EDF7F0",
}
PALETTE_DE = {  # black-red-gold, on charcoal
    "navyA": "1A120E", "navyB": "2A2018", "navyC": "3C2E24",
    "accent": "E0B341", "accentdark": "8A5F00",
    "ink": "241A14", "softtext": "D8CBB0", "mutedtext": "A6977A", "muted": "9A8A6E",
    "rulecolor": "CBB98C", "codebg": "F7F3E8", "codeink": "6B4A00", "notebg": "FBF4E2",
}
PALETTE_XX = {  # grass, stone and dirt
    "navyA": "0F150D", "navyB": "1B2A18", "navyC": "2A3D22",
    "accent": "6FBF4A", "accentdark": "3E7A2E",
    "ink": "1C2A1A", "softtext": "CBDCC0", "mutedtext": "8AA07E", "muted": "7C8F72",
    "rulecolor": "AFC49F", "codebg": "F1F5EA", "codeink": "2F5D24", "notebg": "EDF4E4",
}
MERMAID_EN = None
MERMAID_BG = {"primaryColor": "#EAF6EE", "primaryBorderColor": "#0B6A42", "lineColor": "#3C6B53"}
MERMAID_DE = {"primaryColor": "#FBF3DF", "primaryBorderColor": "#8A5F00", "lineColor": "#6B5A33"}
MERMAID_XX = {"primaryColor": "#EDF4E4", "primaryBorderColor": "#3E7A2E", "lineColor": "#4C6B3E",
              "fontFamily": "FreeSans, FreeSerif, Noto Sans CJK JP, sans-serif"}

STRIPE_BG = r"""
  \fill[red!85!black]  (current page.south west) rectangle ([yshift=0.7cm]current page.south east);
  \fill[green!55!black]([yshift=0.7cm]current page.south west) rectangle ([yshift=1.4cm]current page.south east);
  \fill[white]         ([yshift=1.4cm]current page.south west) rectangle ([yshift=2.1cm]current page.south east);
"""

STRIPE_DE = r"""
  \fill[yellow!85!black](current page.south west) rectangle ([yshift=0.7cm]current page.south east);
  \fill[red!85!black]  ([yshift=0.7cm]current page.south west) rectangle ([yshift=1.4cm]current page.south east);
  \fill[black]         ([yshift=1.4cm]current page.south west) rectangle ([yshift=2.1cm]current page.south east);
"""

# A grass-and-dirt edge, in the flat two-tone bands a block game would use.
STRIPE_XX = r"""
  \fill[brown!45!black] (current page.south west) rectangle ([yshift=0.9cm]current page.south east);
  \fill[green!45!black] ([yshift=0.9cm]current page.south west) rectangle ([yshift=1.35cm]current page.south east);
  \fill[green!35!black] ([yshift=1.35cm]current page.south west) rectangle ([yshift=1.5cm]current page.south east);
"""

# Part titles and the fixed book furniture, per language. Numbers stay Arabic in every edition.
PARTS_EN = ["Overview", "Using it", "Inside", "Architecture in depth", "Deep dives", "Reference"]
PARTS_BG = ["Преглед", "Използване", "Отвътре", "Архитектура в дълбочина", "Задълбочени глави", "Справочник"]
PARTS_DE = ["Überblick", "Benutzung", "Innenleben", "Architektur im Detail", "Vertiefungen", "Referenz"]

# ---- the Trump edition: navy, flag red and gold; Playfair Display Black for the big words ------------
PALETTE_TRUMP = {
    "navyA": "0A1430", "navyB": "10204A", "navyC": "1A3166",
    "accent": "D4AF37", "accentdark": "8C6A12",
    "ink": "141A2E", "softtext": "EDE3C6", "mutedtext": "B3A889", "muted": "857E68",
    "rulecolor": "D9C88F", "codebg": "FBF7EC", "codeink": "7A1420", "notebg": "FFF5DC",
    "flagred": "B22234",
}
MERMAID_TRUMP = {"primaryColor": "#FFF8E6", "primaryBorderColor": "#8C6A12", "lineColor": "#1A3166"}

# Body, tables and code stay IBM Plex (full glyph coverage); only the display face and the cover's
# light line change. Playfair and Montserrat ship with TeX Live, so they are found by file name.
TRUMP_FONTS = r"""% ---- type: IBM Plex for reading, Playfair Display Black for the headlines ----------------------
\setmainfont{IBMPlexSerif}[Path=@PLEX@, Extension=@EXT@, UprightFont=*-Regular,
  BoldFont=*-SemiBold, ItalicFont=*-Italic, BoldItalicFont=*-SemiBoldItalic]
\setsansfont{IBMPlexSans}[Path=@PLEX@, Extension=@EXT@, UprightFont=*-Regular,
  BoldFont=*-SemiBold, ItalicFont=*-Italic, BoldItalicFont=*-SemiBoldItalic]
\setmonofont{IBMPlexMono}[Path=@PLEX@, Extension=@EXT@, Scale=0.86, UprightFont=*-Regular,
  BoldFont=*-SemiBold, ItalicFont=*-Italic, BoldItalicFont=*-SemiBoldItalic]
\newfontfamily\displayfont{PlayfairDisplay}[Extension=.otf, UprightFont=*-Black,
  BoldFont=*-Black, ItalicFont=*-BlackItalic]
\newfontfamily\lightsans{Montserrat}[Extension=.otf, UprightFont=*-Light, ItalicFont=*-LightItalic]"""


def _star(x: float, y: float, r: float, color: str) -> str:
    """A filled five-point star (pentagram path, nonzero fill), in cm, as a TikZ command."""
    import math
    pts = " -- ".join(f"({x + r * math.cos(math.radians(a)):.3f},{y + r * math.sin(math.radians(a)):.3f})"
                      for a in (90, 234, 18, 162, 306))
    return rf"\fill[{color}] {pts} -- cycle;"


def _stars(n: int, r: float, gap: float, color: str = "accent") -> str:
    return " ".join(_star(i * gap, 0, r, color) for i in range(n))


BADGE_TRUMP = (r"""
\begin{tikzpicture}[baseline=-0.5ex]
  \node[anchor=east,font=\sffamily\footnotesize\bfseries,text=accent] (t) at (0,0)
    {\addfontfeatures{LetterSpace=12}THE TRUMP EDITION};
  \begin{scope}[shift={(t.east)},xshift=10pt,yshift=0.4ex]
    """ + _stars(3, 0.11, 0.30) + r"""
  \end{scope}
\end{tikzpicture}
""")

# Thirteen red and white stripes under a gold band carrying a row of white stars.
STRIPE_TRUMP = "\n".join(
    rf"  \fill[{'flagred' if i % 2 == 0 else 'white'}] ([yshift={i * 0.12:.2f}cm]current page.south west) "
    rf"rectangle ([yshift={(i + 1) * 0.12:.2f}cm]current page.south east);"
    for i in range(13)) + r"""
  \fill[accent] ([yshift=1.56cm]current page.south west) rectangle ([yshift=1.90cm]current page.south east);
  \begin{scope}[shift={([xshift=-1.8cm,yshift=1.73cm]current page.south)}]
    """ + _stars(7, 0.12, 0.60, "white") + r"""
  \end{scope}
"""

PARTS_TRUMP = ["The Big, Beautiful Overview", "Using It — So Easy", "Inside — Where It Happens",
               "Architecture — The Finest", "Deep Dives — Very Deep", "Reference — Total Winners"]

# The Bulgarian Trump edition: the same navy-and-gold book, but the cover's stripes are the tricolour
# (red, green, white, bottom up) under the same gold band and white stars.
# The Bulgarian retelling keeps the cover voice but not the edition label: it is documentation.
BADGE_TRUMP_BG = BADGE_TRUMP.replace("THE TRUMP EDITION", "ИЗДАНИЕ ЗА ПОБЕДИТЕЛИ")
STRIPE_TRUMP_BG = "\n".join(
    rf"  \fill[{c}] ([yshift={i * 0.52:.2f}cm]current page.south west) "
    rf"rectangle ([yshift={(i + 1) * 0.52:.2f}cm]current page.south east);"
    for i, c in enumerate(("red!85!black", "green!55!black", "white"))) + "\n" + STRIPE_TRUMP.split("\n", 13)[13]
PARTS_TRUMP_BG = ["Големият, прекрасен преглед", "Използване — толкова лесно",
                  "Отвътре — там, където става всичко", "Архитектура — най-добрата",
                  "Задълбочени глави — много дълбоки", "Справочник — само победители"]

TRUMP_LOGO = dict(
    logo="mdga", header_extra="",
    cover_logo=r"\includegraphics[width=0.62\paperwidth,height=0.30\paperheight,keepaspectratio]{@LOGO@}",
    # gold stars instead of the mark in the footer: the cover carries the logo, every page the stars
    foot_logo=r"\newcommand{\footlogo}{\raisebox{0.2mm}{\tikz{" + _stars(3, 0.09, 0.26) + r"}}}",
)

LANGUAGES = {
    "en": dict(
        dir=None, title="Genesis", parts=PARTS_EN,
        subtitle="A spec-driven generator for Bulgarian-UI ASP.NET Core MVC apps",
        toplabel=r"GENESIS~\textperiodcentered~APPLICATION~GENERATOR",
        runhead="Genesis", badge="", handbook="Architecture Handbook",
        handbook_upper=r"ARCHITECTURE\hspace{0.5em}HANDBOOK",
        part_label="PART", chapter_label="CHAPTER", toc="Contents",
        tags=r"Explanation \textperiodcentered{} Reference \textperiodcentered{} How-to \textperiodcentered{} ADRs",
        license="MIT License", version_suffix="  \u00b7  edition 2 proposal",
        stripe="", palette=PALETTE_EN, mermaid=MERMAID_EN, fonts=None,
        out="dist/dps-nn-handbook-v2.pdf", deep_dives=DEEP_DIVES_EN, **DPSNN_LOGO,
    ),
    "bg": dict(
        dir="bg", title="ДПС-НН", parts=PARTS_BG,
        subtitle="Генератор на ASP.NET Core MVC приложения по спецификация",
        toplabel=r"ДПС-НН~\textperiodcentered~ГЕНЕРАТОР~НА~ПРИЛОЖЕНИЯ",
        runhead="ДПС-НН", badge=FLAG_BG, handbook="Архитектурен справочник",
        handbook_upper=r"АРХИТЕКТУРЕН\hspace{0.5em}СПРАВОЧНИК",
        part_label="ЧАСТ", chapter_label="ГЛАВА", toc="Съдържание",
        tags=r"Обяснение \textperiodcentered{} Справочник \textperiodcentered{} Ръководства \textperiodcentered{} ADR",
        license="MIT лиценз", version_suffix="  \u00b7  българско издание",
        stripe=STRIPE_BG, palette=PALETTE_BG, mermaid=MERMAID_BG, fonts=None,
        out="dist/dps-nn-handbook-bg.pdf", deep_dives=None, **DPSNN_LOGO,
    ),
    "de": dict(
        dir="de", title="Genesis", parts=PARTS_DE,
        subtitle="Ein spezifikationsgetriebener Generator für ASP.NET-Core-MVC-Anwendungen",
        toplabel=r"GENESIS~\textperiodcentered~ANWENDUNGSGENERATOR",
        runhead="Genesis", badge=FLAG_DE, handbook="Architekturhandbuch",
        handbook_upper=r"ARCHITEKTURHANDBUCH",
        part_label="TEIL", chapter_label="KAPITEL", toc="Inhalt",
        tags=r"Erklärung \textperiodcentered{} Referenz \textperiodcentered{} Anleitung \textperiodcentered{} ADR",
        license="MIT-Lizenz", version_suffix="  \u00b7  deutsche Ausgabe",
        stripe=STRIPE_DE, palette=PALETTE_DE, mermaid=MERMAID_DE, fonts=None,
        out="dist/dps-nn-handbook-de.pdf", deep_dives=None,
        logo_file="gui/src/renderer/public/logo/logo-de.png", **DPSNN_LOGO,
    ),
    "xx": dict(
        dir=None, title="Genesis", parts=PARTS_EN,
        subtitle="A spec-driven generator for Bulgarian-UI ASP.NET Core MVC apps",
        toplabel="", runhead="", badge=None, handbook="", handbook_upper="",
        part_label="", chapter_label="", toc="", tags="", license="",
        version_suffix="  \u00b7  galactic edition",
        stripe=STRIPE_XX, palette=PALETTE_XX, mermaid=MERMAID_XX, fonts=GALACTIC_FONTS,
        out="dist/dps-nn-handbook-galactic.pdf", deep_dives=DEEP_DIVES_EN,
        logo_file="gui/src/renderer/public/logo/logo-xx.png",
        **DPSNN_LOGO,
    ),
    "trump": dict(
        dir="trump", title="Genesis", parts=PARTS_TRUMP,
        subtitle=r"A spec-driven generator for Bulgarian-UI ASP.NET Core MVC apps.\\[3pt]The greatest one. Believe me.",
        toplabel=r"MAKE~DIPLOMA~PROJECTS~GREAT~AGAIN",
        runhead=r"Genesis \textperiodcentered{} The Trump Edition", badge=BADGE_TRUMP,
        handbook="The Tremendous Handbook",
        handbook_upper=r"THE\hspace{0.5em}TREMENDOUS\hspace{0.5em}HANDBOOK",
        part_label="PART", chapter_label="CHAPTER", toc="Contents — All Winners",
        tags=r"Explanation \textperiodcentered{} Reference \textperiodcentered{} How-to \textperiodcentered{} ADRs \textperiodcentered{} Believe me",
        license="MIT License — the best license", version_suffix="  ·  the Trump edition",
        stripe=STRIPE_TRUMP, palette=PALETTE_TRUMP, mermaid=MERMAID_TRUMP, fonts=TRUMP_FONTS,
        out="dist/dps-nn-handbook-trump.pdf", deep_dives=None,
        logo_file="gui/src/renderer/public/logo/MDGA.png", **TRUMP_LOGO,
    ),
    "trump-bg": dict(
        dir="trump-bg", title="ДПС-НН", parts=PARTS_TRUMP_BG,
        subtitle=r"Генератор на дипломни проекти: ASP.NET Core MVC приложения по спецификация.\\[3pt]Най-добрият. Повярвайте ми.",
        toplabel=r"ДА~НАПРАВИМ~ДИПЛОМНИТЕ~ПРОЕКТИ~ОТНОВО~ВЕЛИКИ",
        runhead="ДПС-НН", badge=BADGE_TRUMP_BG,
        handbook="Невероятният справочник",
        handbook_upper=r"НЕВЕРОЯТНИЯТ\hspace{0.5em}СПРАВОЧНИК",
        part_label="ЧАСТ", chapter_label="ГЛАВА", toc="Съдържание — само победители",
        tags=r"Обяснение \textperiodcentered{} Справочник \textperiodcentered{} Ръководства \textperiodcentered{} ADR \textperiodcentered{} Повярвайте ми",
        license="MIT лиценз — най-добрият лиценз", version_suffix="  ·  българско издание",
        stripe=STRIPE_TRUMP_BG, palette=PALETTE_TRUMP, mermaid=MERMAID_TRUMP, fonts=TRUMP_FONTS,
        out="dist/dps-nn-handbook-trump-bg.pdf", deep_dives=None,
        logo_file="gui/src/renderer/public/logo/MDGA_bg.png", **TRUMP_LOGO,
    ),
}

# ---- the substitution alphabet -------------------------------------------------------------------
# The Standard Galactic Alphabet, the cipher Minecraft draws its enchanting-table glyphs with. No
# installed face carries all 26 symbols, so each letter is a macro bound to the one font that draws
# it: the book text stays deterministic whatever weight or size surrounds it. (pandoc passes
# \command{} through; code is never transliterated.)
SGA = {
    "a": ("sans", "\u1511"), "b": ("sans", "\u0296"), "c": ("sans", "\u1435"),
    "d": ("sans", "\u21b8"), "e": ("sans", "\u14b7"), "f": ("serif", "\u2393"),
    "g": ("sans", "\u22a3"), "h": ("serif", "\u2351"), "i": ("sans", "\u254e"),
    "j": ("sans", "\u22ee"), "k": ("sans", "\ua58c"), "l": ("sans", "\ua58e"),
    "m": ("sans", "\u14b2"), "n": ("cjk", "\u30ea"), "o": ("serif", "\U0001d679"),
    "p": ("sans", "!\u00a1"), "q": ("sans", "\u1451"), "r": ("sans", "\u2237"),
    "s": ("sans", "\u14ed"), "t": ("sans", "\u2138\u0323"), "u": ("sans", "\u268d"),
    "v": ("serif", "\u234a"), "w": ("sans", "\u2234"), "x": ("sans", "/\u0307"),
    "y": ("sans", "||"), "z": ("sans", "\u2a05"),
}


def sga_macros() -> str:
    """One macro per letter, each pinned to a font that actually has the glyph."""
    lines = []
    for letter, (fam, glyph) in SGA.items():
        lines.append(r"\newcommand{\sg%s}{{\gal%s %s}}" % (letter, fam, glyph))
    pdf = "".join(r"\def\sg%s{%s}" % (l, l.upper()) for l in SGA)
    # the letters survive in the PDF bookmarks, in Latin, instead of being stripped with the macros
    # hyperref is loaded after this header, so the override is deferred to the start of the document
    return "\n".join(lines) + "\n\\AtBeginDocument{\\pdfstringdefDisableCommands{%s}}\n" % pdf


SGA_RUN = re.compile(r"[A-Za-z][A-Za-z'\u2019]*")
FENCE = re.compile(r"(```.*?```)", re.S)
INLINE_CODE = re.compile(r"(`[^`]*`)")


def _transliterate(raw: str) -> str:
    return SGA_RUN.sub(lambda m: "".join(r"\sg%s{}" % c.lower() for c in m.group(0)
                                        if c.lower() in SGA), raw)


DIAG_QUOTED = re.compile(r'"([^"\n]*)"')
DIAG_EDGE = re.compile(r"\|([^|\n]+)\|")


DIAG_TAG = re.compile(r"<[^>]*>")  # <br/> and friends are syntax, not words


def _raw(text: str) -> str:
    return SGA_RUN.sub(lambda m: "".join(SGA[c.lower()][1] if c.lower() in SGA else c for c in m.group(0)),
                       text)


def _plain(text: str) -> str:
    """Raw galactic glyphs, for places that are not LaTeX (Mermaid label text)."""
    out, pos = [], 0
    for m in DIAG_TAG.finditer(text):
        out += [_raw(text[pos:m.start()]), m.group(0)]
        pos = m.end()
    out.append(_raw(text[pos:]))
    return "".join(out)


def _diagram(body: str) -> str:
    """Only the label text changes; node ids and the Mermaid syntax must stay ASCII."""
    held: list[str] = []

    def keep(m: "re.Match[str]") -> str:
        held.append(_plain(m.group(1)))
        return f'"@@{len(held) - 1}@@"'  # keep the quotes so the edge pass sees a quoted label

    body = DIAG_QUOTED.sub(keep, body)
    # the quoted form keeps label text containing | (the galactic "y" is ||) from closing the edge
    def edge(m: "re.Match[str]") -> str:
        inner = m.group(1)
        if inner.startswith('"') and inner.endswith('"'):
            return "|" + inner + "|"  # already quoted in the source
        return '|"' + _plain(inner) + '"|'

    body = DIAG_EDGE.sub(edge, body)
    for i, label in enumerate(held):
        body = body.replace(f"@@{i}@@", label)
    return body


def galactic(text: str) -> str:
    """Transliterate prose and diagram labels; code and Mermaid syntax stay Latin."""
    parts = FENCE.split(text)
    for i, part in enumerate(parts):
        if part.startswith("```mermaid"):
            parts[i] = "```mermaid\n" + _diagram(part.partition("\n")[2])
            continue
        if part.startswith("```"):
            continue
        segments = INLINE_CODE.split(part)
        for j, seg in enumerate(segments):
            if not seg.startswith("`"):
                segments[j] = _transliterate(seg)
        parts[i] = "".join(segments)
    return "".join(parts)


# -------------------------------------------------------------------------------------------------

def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("--lang", default="en", choices=sorted(LANGUAGES))
    ap.add_argument("--out", default=None)
    args = ap.parse_args()

    lang = LANGUAGES[args.lang]
    if lang["dir"]:
        base = f"{D}/{lang['dir']}"
        chapters = {n: f"{base}/{stem}.md" for n, stem in CHAPTERS.items()}
        deep: list[str] = []
    else:
        # English/xx: the deep dives are the repository docs, included verbatim.
        chapters = {n: f"{D}/{stem}.md" for n, stem in CHAPTERS.items() if not 17 <= n <= 21}
        deep = list(lang["deep_dives"])

    if args.lang == "xx":
        g = _transliterate
        titles = [g(x) for x in lang["parts"]]
        cover_title, meta_title = g("Genesis"), "Genesis"
        subtitle = g(lang["subtitle"])
        toplabel = g("GENESIS") + r"~\textperiodcentered~" + g("APPLICATION~GENERATOR")
        runhead, handbook = g("Genesis"), g("Architecture Handbook")
        handbook_upper = g("ARCHITECTURE") + r"\hspace{0.5em}" + g("HANDBOOK")
        part_label, chapter_label = g("PART"), g("CHAPTER")
        toc, license_label = g("Contents"), g("MIT License")
        tags = r" \textperiodcentered{} ".join(
            g(x) for x in ("Explanation", "Reference", "How-to", "ADRs"))
        title_size, badge = r"\fontsize{26}{30}", flag_xx(g("GALACTIC EDITION"))
        # the macros (and the bookmark overrides) go last in the preamble, after hyperref is loaded
        bh.HEADER_EXTRA = sga_macros()
        bh.BOOK_HANDBOOK_META = "Architecture Handbook"  # PDF metadata must stay plain text
    else:
        titles = lang["parts"]
        cover_title = meta_title = lang["title"]
        subtitle, toplabel, runhead = lang["subtitle"], lang["toplabel"], lang["runhead"]
        handbook, handbook_upper = lang["handbook"], lang["handbook_upper"]
        part_label, chapter_label, toc = lang["part_label"], lang["chapter_label"], lang["toc"]
        tags, license_label = lang["tags"], lang["license"]
        title_size, badge = None, lang["badge"]
        bh.HEADER_EXTRA = lang["header_extra"]

    bh.PARTS = [(titles[i],
                 [chapters[n] for n in nums if n in chapters] + (deep if i == 4 else []))
                for i, (_, nums) in enumerate(PARTS)]
    bh.UNLISTED_NOTE = False

    bh.BOOK_TITLE = lang["title"]
    bh.BOOK_COVER_TITLE = cover_title
    bh.BOOK_META_TITLE = meta_title
    bh.BOOK_SUBTITLE = subtitle
    bh.BOOK_TOPLABEL = toplabel
    bh.BOOK_RUNHEAD = runhead
    if title_size:
        bh.COVER_TITLE_SIZE = title_size
    if lang.get("palette"):
        bh.PALETTE = dict(bh.PALETTE, **lang["palette"])
    if lang.get("mermaid"):
        bh.MERMAID_CONFIG = {**bh.MERMAID_CONFIG,
                             "themeVariables": {**bh.MERMAID_CONFIG["themeVariables"], **lang["mermaid"]}}
    if lang.get("fonts"):
        bh.FONTSETUP = lang["fonts"]
    bh.COVER_STRIPE = lang.get("stripe", "")
    bh.COVER_BADGE = badge
    if lang.get("logo_file"):
        bh.LOGO = (bh.ROOT / lang["logo_file"]).resolve()
    bh.COVERLOGOSTYLE = lang.get("cover_logo_style", "")
    bh.COVERLOGO = lang["cover_logo"]
    bh.FOOTLOGO = lang["foot_logo"]
    bh.BOOK_HANDBOOK = handbook
    bh.BOOK_HANDBOOK_UPPER = handbook_upper
    bh.BOOK_PART_LABEL = part_label
    bh.BOOK_CHAPTER_LABEL = chapter_label
    bh.BOOK_TOC_TITLE = toc
    bh.BOOK_COVER_TAGS = tags
    bh.BOOK_LICENSE_LABEL = license_label
    _version = bh.version_line
    bh.version_line = lambda: _version() + lang["version_suffix"]

    if args.lang in ("bg", "de", "trump", "trump-bg"):
        # the translated and retold editions import no repository docs, so their term map is moot
        bh.TEXT_TRANSFORMS = []
    else:
        bh.TEXT_TRANSFORMS = [
            ('*The page is labelled "\u0421\u043f\u0435\u0446\u0438\u0444\u0438\u043a\u0430\u0446\u0438\u0438" (Bulgarian for "Specifications") in the generated apps\' UI.*',
             '*The page is labelled "Specifications" in the generated apps\' UI.*'),
            ("\u0421\u043f\u0435\u0446\u0438\u0444\u0438\u043a\u0430\u0446\u0438\u0438", "Specifications"),
            ("\u0425\u0430\u0440\u0430\u043a\u0442\u0435\u0440\u0438\u0441\u0442\u0438\u043a\u0438", "Characteristics"),
            ("\u041e\u0442\u0437\u0438\u0432\u0438", "Reviews"),
            ("\u0421\u044a\u0437\u0434\u0430\u0439", "Create"),
            ("\u0424\u0438\u043b\u0442\u0440\u0438\u0440\u0430\u0439", "Filter"),
            ("\u0418\u0437\u0447\u0438\u0441\u0442\u0438", "Clear"),
            ("**Veselin Marinov theme suppresses the \u0414\u041f\u0421-\u041d\u041d logo**",
             "**Veselin Marinov theme suppresses the product logo**"),
            ('`"Email" \u2192 "\u0418\u043c\u0435\u0439\u043b"` turned `EmailModel` into\n`\u0418\u043c\u0435\u0439\u043bModel` and `ManageNavPages.Email` into `ManageNavPages.\u0418\u043c\u0435\u0439\u043b` \u2014 both compile errors.',
             '"Email" mapped to its Bulgarian word turned `EmailModel` into a broken identifier and\n`ManageNavPages.Email` into a broken member reference \u2014 both compile errors.'),
        ]

    bh.TEXT_TRANSFORM_FN = galactic if args.lang == "xx" else None

    sys.argv = ["build-handbook2.py", "--out", args.out or lang["out"]]
    bh.main()


if __name__ == "__main__":
    main()
