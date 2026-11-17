---
name: changelog-discipline
description: Mandatory changelog discipline for any iterative build/fix/design work on a project - write one changelog entry after EVERY request that changes the project (code, config, aesthetics, docs, structure - not just bugs), and check existing changelog/decision history BEFORE proposing any fix so a previously-tried-and-failed theory never gets re-proposed. Use whenever a changelogs/ folder (or equivalent running log) exists in the project, or should - trigger on every build error, stack trace, "fix this", feature request, aesthetic/design change, refactor, or any multi-turn technical session with a person, even if they don't explicitly ask for documentation. Especially critical for recurring/flaky bugs across multiple turns - always read the log before guessing a next theory.
---

# Changelog Discipline

Two obligations, both mandatory, both apply to every single request that touches the project - not just bugs, not just when asked:

1. **Document it.** After finishing any request that changes the project, write one changelog entry.
2. **Check before guessing.** Before proposing any fix, read the existing log for prior attempts at this exact problem. Never re-propose a theory already tried and failed.

If no changelog exists yet in the project, create one on the very first substantive change - don't wait to be asked twice.

## Folder and file convention

`changelogs/` at the project root. One `.md` file per distinct change, numbered sequentially and zero-padded so they sort naturally in any file browser: `001-short-slug.md`, `002-short-slug.md`, etc. A `changelogs/README.md` explains the convention for anyone else who opens the folder. Never bundle multiple unrelated changes into one file - if one request produced both a bug fix and an aesthetic tweak, that's two entries.

Two templates, chosen by what the change actually was:

**Error / bug fix:**
```
# <title>

**Type:** Error fix

## Where
<exact file/location>

## How it happened
<the mechanism - what code path, what triggered it>

## Why
<root cause, not just the symptom>

## What it affects
<blast radius - which flows/users/builds this breaks>

## How to reproduce
<concrete repro steps>

## How to fix
<what was actually changed, and why that's the fix>
```

**Feature / aesthetic / refactor** (kept brief - no need for the full error template when nothing was broken):
```
# <title>

**Type:** Feature / change

## Summary
<what changed, briefly>

## Techniques
<bullet list - how it was done>

## Affected files
<bullet list>
```

If a single request is genuinely both (a bug fix that also involved a design decision), write two entries rather than force-fitting one template.

## The document-every-request rule

Every request that changes something in the project gets an entry, full stop:
- A crash, a failing test, a warning, an unhandled exception → error template.
- A new feature, a UI/design tweak, a rename, a refactor, a dependency bump, a config/infra change (scripts, folder structure, CI) → feature template.
- A request that turned out to need no code change (pure Q&A, "what's left", a clarifying question) does NOT get an entry - only requests that actually changed the project.
- A finding made *while* investigating something else (e.g. "also discovered X is a dead code path") still gets its own entry or a clearly-separated section - don't let it disappear into a paragraph about something else.

Write the entry as part of finishing the turn, not as an afterthought the person has to request. If several distinct things happened in one turn (a fix + a new feature), write separate numbered entries for each.

## The check-before-guessing rule

This is the half of the discipline that's easiest to skip under time pressure, and the most costly to skip. Before proposing a fix for ANY reported problem:

1. **Read the changelog** (and any running decision log the project keeps) for entries touching this area, this error signature, or this symptom.
2. If a theory was already tried and it failed, **do not propose it again**, even reworded, even "just to double check." If you're not sure whether a new idea is really the same as an old failed one, name the old one explicitly and explain what's actually different this time.
3. **Count the attempts out loud.** If you're about to propose a fix and the log shows this is the 3rd+ theory for the same symptom, say so plainly - "three different fixes have already failed for this" - before offering another one. That fact should change what you do next (see below), not just be mentioned in passing.
4. **After 2-3 failed theories, stop proposing fixes and get better data instead.** Add logging/instrumentation, ask for a specific diagnostic command, or ask the person to compare a working case against the failing one directly. A fourth confident guess with no new evidence is worse than admitting the current evidence is insufficient. This mirrors debugging-protocol's "two failed fix attempts → escalate, don't take a fresh confident third guess" - the difference here is the check has to be against a *persistent, written* log, not just what's still in the current conversation's memory, because the whole point is surviving across sessions and context resets.
5. When a fix DOES land after prior failures, the changelog entry for it should say what was tried before and why this one is actually different - future-you (or future-Claude) reading the log needs that contrast, not just the final answer.

## Why this exists

Without a persistent, written record, every new session (or even a long conversation that's drifted) re-derives context from scratch, and it's easy to re-propose something already ruled out - burning the person's time and trust on repeat guesses that sound plausible but are already known to be wrong. The log is what makes "have we tried this?" a 30-second read instead of a re-investigation. Treat the changelog as a debugging tool for future-you, not paperwork for the person.
