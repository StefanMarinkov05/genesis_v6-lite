#!/usr/bin/env python3
"""Build the lite edition's file tree: every git-tracked file minus the music (docs/how-to/mirror-the-lite-edition.md).

Usage:  python3 scripts/lite-tree.py <dest-dir>

Run from a checkout of the full repo. <dest-dir> is emptied (except its .git/) and refilled, so
pointing it at a clone of the lite repo turns "what changed" into a plain `git status`.

What differs from the full repo, and nothing else:
  - design/music/**: media dropped; the three folders keep their .gitkeep (the GUI's music
    player already treats an empty folder as "no track").
  - .gitattributes: the Git LFS rules dropped (the lite repo has no LFS objects).
  - README.md: replaced by README.lite.md, the lite repo's own README (edited by hand, kept in
    the full repo so both READMEs live in one place).
  - The handbook is English only: the translated/retold sources (docs/handbook/{bg,de,trump,
    trump-bg}/) and every PDF in dist/ except dps-nn-handbook-v2.pdf are dropped. The English chapters,
    that PDF and the build scripts (which carry the LaTeX header, cover and filters) stay.
The full repo's `lite-mirror.yml` is dropped as well: it only makes sense in the source repo.
"""
from __future__ import annotations

import shutil
import subprocess
import sys
from pathlib import Path

MEDIA = (".mp3", ".mp4", ".webm", ".mov")
DROP = {".github/workflows/lite-mirror.yml", "README.md", "README.lite.md"}
HANDBOOK_TRANSLATIONS = tuple(f"docs/handbook/{d}/" for d in ("bg", "de", "trump", "trump-bg"))
ENGLISH_PDFS = {"dist/dps-nn-handbook-v2.pdf"}


def tracked_files(repo: Path) -> list[str]:
    out = subprocess.run(["git", "ls-files", "-z"], cwd=repo, check=True, capture_output=True).stdout
    return [p for p in out.decode("utf-8").split("\0") if p]


def keep(path: str) -> bool:
    if path in DROP or path.startswith(HANDBOOK_TRANSLATIONS):
        return False
    if path.startswith("dist/") and path not in ENGLISH_PDFS:
        return False
    return not (path.startswith("design/music/") and path.lower().endswith(MEDIA))


def lite_gitattributes(text: str) -> str:
    kept = [l for l in text.splitlines() if "filter=lfs" not in l]
    while kept and (not kept[-1].strip() or kept[-1].startswith("#")):  # the LFS block's comment
        kept.pop()
    return "\n".join(kept) + "\n"


def main() -> int:
    args = sys.argv[1:]
    if not args:
        print(__doc__)
        return 2
    dest = Path(args[0]).resolve()
    repo = Path(__file__).resolve().parent.parent

    dest.mkdir(parents=True, exist_ok=True)
    for child in dest.iterdir():
        if child.name == ".git":
            continue
        shutil.rmtree(child) if child.is_dir() and not child.is_symlink() else child.unlink()

    files = tracked_files(repo)
    kept = [f for f in files if keep(f)]
    for rel in kept:
        src, dst = repo / rel, dest / rel
        if not src.exists():  # deleted in the working tree but not yet committed
            continue
        dst.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(src, dst, follow_symlinks=False)

    lite_readme = repo / "README.lite.md"
    if not lite_readme.exists():
        print("lite-tree: README.lite.md is missing - the lite repo needs its own README", file=sys.stderr)
        return 1
    shutil.copy2(lite_readme, dest / "README.md")
    attrs = dest / ".gitattributes"
    attrs.write_text(lite_gitattributes(attrs.read_text(encoding="utf-8")), encoding="utf-8")

    print(f"lite-tree: {len(kept)} of {len(files)} tracked files -> {dest} ({len(files) - len(kept)} dropped)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
