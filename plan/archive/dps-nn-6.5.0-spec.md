# DPS-NN 6.5.0 - Genesis Modularization, Documentation & GUI

> Rewritten and structured from the raw 6.5.0 update note. Uses hyphens throughout.
> Sections marked **[BLOCKED]** cannot be finalized until the Open Decisions (Section 4) are answered.
> Model routing follows the standard rule: Opus for architectural / high-ambiguity / one-way-door work, Sonnet for well-specified implementation.

---

## 0. Framing

- Genesis is no longer a standalone tool. It becomes a **module of the larger app, DPS-NN**.
- Genesis already contains ~90% of the relevant code, so modularization is a repositioning + light refactor, not a rewrite.
- Root going forward: `./DPS-NN/`, with Genesis living as a submodule/subproject beneath it.

### Task 0 - Modularization (do first, small)
- Reposition the existing Genesis project under `./DPS-NN/` as a module.
- Create `./DPS-NN/images/` (for the logo, flags, wallpapers, test images).
- Create `./DPS-NN/design/design-specs.md` (theme/design specification file - populated in Task B).
- Create `./DPS-NN/design/footer.md` (footer content file - Stefan fills later; GUI reads it at runtime so it can vary).
- Keep the Genesis CLI entry point intact - the GUI wraps it, it does not replace it.

**Model:** Sonnet, thinking: medium, effort: low. (Mechanical restructure; the only judgment call is module boundaries, which are already clear.)

---

## 1. Task A - Genesis Documentation

Applies the tiered / Diataxis / anti-drift architecture (not the flat "4 big docs" version). Serves **both** a human reader and a fresh Claude Code session from one source - no double-writing.

> **Detailed design (done, Opus):** `plan/task-a-doc-architecture.md`. The A.1 layout below is the
> original aspirational sketch; the design record reconciles it with the docs that now exist (the
> 11 ADRs stay the decision store, `docs/architecture.md` is split+retired, spec-format is a
> concepts companion, FAILED-ATTEMPTS is scoped to structural dead-ends). Follow the design record,
> not the raw A.1 tree, where they differ.

### A.1 Documentation layout
```
./DPS-NN/CLAUDE.md                       # TINY router (~150 lines max). Auto-loaded every CC session.
                                         # 3-sentence "what Genesis is" + a routing table
                                         # ("doing X? read docs/Y.md") + critical DO-NOT rules only.
./DPS-NN/README.md                       # Human narrative + top-level Mermaid overview diagram.
./DPS-NN/docs/
  01-tutorial-genesis.md                 # One-time end-to-end orientation (human + AI).
  reference/
    control-flow.md                      # 9-stage pipeline: Mermaid state diagram + stage table.
    spec-format.md                       # project-spec.json schema, field by field.
    file-map.md                          # GENERATED from per-file header comments. Do NOT hand-edit.
  explanation/
    architecture-decisions.md            # WHY the pipeline is staged/resumable this way (ADR-style).
    techniques.md                        # Scriban patterns, @@media Razor escaping, EF migration
                                         # ordering - the hard-won failure knowledge. Highest value.
  how-to/
    add-a-stage.md
    add-a-template.md
  FAILED-ATTEMPTS.md                     # what was tried and why it failed; pruned when an approach
                                         # becomes structurally impossible.
```

### A.2 Rules baked into the docs (anti-drift)
- **CLAUDE.md is a router, not a container** - it points to on-demand docs, it does not hold their content.
- **file-map.md is generated** from per-file header comments (a script greps headers). Never hand-maintained.
- **Single source of truth per fact** - no fact documented in two places.
- **Same-commit rule** - a doc changes in the same commit as the code it describes.
- **Staleness stamp** - each doc header carries `last-verified: <commit-sha>`.
- **All diagrams are Mermaid** (state machine, control flow, stage DAG) - text-based, diffable, agent-readable.

### A.3 Subtask routing
| Subtask | Model | Thinking | Notes |
|---|---|---|---|
| Design doc tree + CLAUDE.md router content | Opus | high | Architectural; sets structure everything else fills. Mostly designed already - port it. |
| control-flow.md + Mermaid state diagram | Sonnet | medium | Needs the real stage list in context. |
| spec-format.md (field-by-field) | Sonnet | low | Mechanical from project-spec.json schema. |
| techniques.md + FAILED-ATTEMPTS.md | Sonnet | high | High value; needs deep code + history in context. If output looks shallow, escalate to Opus. |
| architecture-decisions.md | Sonnet | medium | |
| file-map.md generator script + per-file headers | Sonnet | medium | Script + a pass adding header comments to each source file. |
| README + tutorial | Sonnet | low | Narrative layer. |

---

## 2. Task B - Genesis GUI

**Stack: Electron web-shell.** Genesis stays a headless .NET CLI; the GUI spawns it as a child process and streams its output. Rationale: the two hardest required features (embedded Debug terminal via xterm.js + node-pty, and live process streaming via child_process) are trivial in Electron and custom elsewhere; the 5-theme animated aesthetic is a pure web-frontend strength where the UI skills transfer fully. Tauri is the fallback only if a lean binary outweighs the more custom terminal path.

A desktop-style control surface wrapping the Genesis CLI. High-tech, robotic, "cool" aesthetic, strong theme consistency. Uses the `ui-design` + `frontend-distinctive-design` skills.

### B.0 Build phasing (do NOT build all themes/languages up front)
1. **MVP - one theme, EN only:** spec/seed input + list-select + dedup check + Start/Stop + progress bar + Debug terminal + generation screen. Must generate end-to-end before anything else.
2. **Then:** Pause/Resume (checkpoint-based), the "Claude" seed button (API).
3. **Only then:** the other 4 themes, the 5 additional languages, flag picker, per-project wallpapers.

### B.1 Confirmed requirements (from the 6.5.0 note)
**Branding / chrome**
- Logo `dps-nn.png` at the top (Stefan uploads manually into `./DPS-NN/images/`).
- Fully stylized custom window chrome: minimize, maximize, close - matched to the theme, not OS-default.
- Footer read at runtime from `./DPS-NN/design/footer.md` (so it can vary).

**Spec + seed input**
- Button: **upload spec**, or **select spec from list**.
- On list-select: default the seed from the existing `project -> json` mapping.
- Alternatives for seed: **upload new seed**, or **"Claude" button** = Claude generates the seed and writes it to the correct folder. *(See Q4 - this implies an Anthropic API call from the GUI.)*
- **Dedup check:** when a (spec, seed) pair is loaded, check whether a project was already built from that pair; if so, skip regeneration. (Keyed by a hash of spec+seed against a build manifest / the timestamped output folders.)

**Themes / i18n**
- Theme selector button. **5 themes** (consistency across all five is a hard requirement).
- Languages: **EN, BG, DE, RU, Romani, and Minecraft Enchanting Table** (Standard Galactic Alphabet - a display font applied to the English strings, not a real translation).
- Language picker via flag PNGs (Stefan uploads flags later into `./DPS-NN/images/`).
- Design specs for the themes live in `./DPS-NN/design/design-specs.md`.

**Run controls**
- Buttons: **Start, Pause, Resume, Stop**. *(See Q6 - pause/resume semantics.)*
- **Debug Mode checkbox** - when on, opens an embedded terminal panel showing live generation output.
- **Progress bar** - approximate is fine (stage N of 9). *(Note: stage-level progress is free; it does not slow generation. Only fine-grained sub-stage progress would - we won't do that.)*

**Generation screen**
- Once a valid (spec, seed) pair starts generating, the GUI switches to a **generation/loading screen** with a per-project wallpaper (image link provided inside that project's spec, different per wallpaper) plus a loading animation (approximate, not precise).
- Use uploaded reference image for direction; use **test images** as placeholders until Stefan supplies real assets.

### B.2 Flagged design/architecture notes
- **Pause/Resume rides the existing resumable pipeline.** Genesis is already a 9-stage resumable pipeline, so "pause" = stop cleanly after the current stage's checkpoint, "resume" = continue from that checkpoint. True mid-stage freeze is much harder and not worth it. Confirm this reading (Q6).
- **Debug terminal** = pipe the Genesis process stdout/stderr into an embedded panel. Trivial for a web-shell stack, needs a PTY control for native.
- **"Claude" seed button** = an API integration (key handling, model choice, where the seed file lands). Small but real scope; needs its own mini-design.

### B.3 Subtask routing
| Subtask | Model | Thinking | Notes |
|---|---|---|---|
| App architecture (Electron shell + CLI child process) | Opus | high | Stack resolved (Electron). Design the main/renderer split, IPC, process control. |
| Visual design system: theme tokens, robotic hi-tech language, consistency rules | Opus | high | Establishes the tokens every theme implements. |
| Implement each theme against tokens | Sonnet | medium | Parallelizable once tokens exist. |
| Custom window chrome (min/max/close) | Sonnet | medium | |
| i18n scaffolding + 6 language string tables + enchant-table font | Sonnet | medium | Font swap for SGA, real strings for the rest. |
| Flag-based language picker | Sonnet | low | |
| GUI -> Genesis CLI wiring (Start/Pause/Resume/Stop, progress, debug terminal) | Sonnet | high | Process control + pause/resume against checkpoints. |
| Dedup check (spec+seed hash -> build manifest) | Sonnet | medium | |
| "Claude" seed generation (API call + write to folder) | Opus (design) / Sonnet (impl) | high/medium | API integration mini-design first. |
| Generation/loading screen + per-project wallpaper | Sonnet | low | Test images first. |

---

## 3. Consolidated model routing

- **Opus, high thinking:** doc-tree design, GUI tech-stack + app architecture, visual design-system/token design, Claude-seed API design.
- **Sonnet, high thinking:** techniques.md/FAILED-ATTEMPTS.md, GUI->CLI process wiring.
- **Sonnet, medium/low:** everything else (theme implementation, chrome, i18n, generated docs, narrative docs, dedup, loading screen).
- Escalate any Sonnet task to Opus if output is shallow, confidently wrong, or the task turns out more ambiguous than scoped.

---

## 4. Decision log (RESOLVED)

1. **GUI tech stack: Electron web-shell** (Genesis stays a CLI, spawned as a child process). Fallback: Tauri only if a lean binary outweighs the more custom terminal path. Avalonia rejected - stack purity does not justify capping the aesthetic or stranding the web UI skills.
2. **Theme count: 5** (consistency across all five is a hard requirement).
3. **"Roma" = Romani.**
4. **"Claude" seed button: yes, live Anthropic API call.** Key from env var / local config, never hardcoded. Call made from Electron's main process, not the renderer - the key never reaches the web layer.
5. **Enchanting-table: Standard Galactic Alphabet font over the English strings** (visual gimmick, not a translation).
6. **Pause/Resume: checkpoint-based** via the existing resumable pipeline. Pause = stop cleanly at the next stage checkpoint; resume = continue from it. No mid-stage freeze.

## 5. Assumptions (correct me if wrong; otherwise settled)
- Genesis stays resumable and the GUI reuses that for pause/resume.
- Progress is stage-granular (N/9) and costs nothing.
- Test images are placeholders; real assets swap in later.
- Footer and per-project wallpapers are runtime-loaded so they can vary without a rebuild.
- GUI is built in the phases defined in B.0 - MVP generating end-to-end before themes/languages are expanded.
