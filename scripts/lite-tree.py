#!/usr/bin/env python3
"""Build the lite edition's file tree: every git-tracked file minus the music (docs/how-to/mirror-the-lite-edition.md).

Usage:  python3 scripts/lite-tree.py <dest-dir> [--lite-repo OWNER/NAME]

Run from a checkout of the full repo. <dest-dir> is emptied (except its .git/) and refilled, so
pointing it at a clone of the lite repo turns "what changed" into a plain `git status`.

What differs from the full repo, and nothing else:
  - design/music/**: media dropped; the three folders keep their .gitkeep (the GUI's music
    player already treats an empty folder as "no track").
  - .gitattributes: the Git LFS rules dropped (the lite repo has no LFS objects).
  - README.md: a lite-edition note under the title; Releases links point at the lite repo and the
    installer name is the Lite one (release.yml names it that when there is no music).
The full repo's `lite-mirror.yml` is dropped as well: it only makes sense in the source repo.
"""
from __future__ import annotations

import shutil
import subprocess
import sys
from pathlib import Path

FULL_REPO = "StefanMarinkov05/genesis_v6"
MEDIA = (".mp3", ".mp4", ".webm", ".mov")
DROP = {".github/workflows/lite-mirror.yml"}


def tracked_files(repo: Path) -> list[str]:
    out = subprocess.run(["git", "ls-files", "-z"], cwd=repo, check=True, capture_output=True).stdout
    return [p for p in out.decode("utf-8").split("\0") if p]


def keep(path: str) -> bool:
    if path in DROP:
        return False
    return not (path.startswith("design/music/") and path.lower().endswith(MEDIA))


def lite_readme(text: str, lite_repo: str) -> str:
    note = (
        f"> **Lite edition.** The same code as [{FULL_REPO}](https://github.com/{FULL_REPO}), minus the\n"
        f"> ~1 GB of music the GUI's IMPERATOR theme can play. Mirrored automatically from there on every\n"
        f"> push - please open issues and pull requests in the full repo.\n"
    )
    lines = text.split("\n")
    title = next(i for i, l in enumerate(lines) if l.startswith("# "))
    lines[title + 1:title + 1] = ["", note.rstrip("\n")]
    text = "\n".join(lines)
    text = text.replace(f"github.com/{FULL_REPO}/releases", f"github.com/{lite_repo}/releases")
    return text.replace("DPS-NN-Setup-<version>.exe", "DPS-NN-Lite-Setup-<version>.exe")


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
    lite_repo = args[args.index("--lite-repo") + 1] if "--lite-repo" in args else f"{FULL_REPO}-lite"
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

    readme = dest / "README.md"
    readme.write_text(lite_readme(readme.read_text(encoding="utf-8"), lite_repo), encoding="utf-8")
    attrs = dest / ".gitattributes"
    attrs.write_text(lite_gitattributes(attrs.read_text(encoding="utf-8")), encoding="utf-8")

    print(f"lite-tree: {len(kept)} of {len(files)} tracked files -> {dest} ({len(files) - len(kept)} dropped)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
