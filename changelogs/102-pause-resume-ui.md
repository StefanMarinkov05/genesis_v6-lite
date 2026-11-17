# 102 — GUI: Pause/Resume wired to the UI (Phase 2)

**Type:** Feature (GUI)
**Model:** Sonnet, low effort, thinking off — per the Phase 2 routing breakdown. Pure UI wiring
to an already-declared IPC surface and already-implemented main-process logic (`GenesisRunner.
pause`/`resume` existed since changelog 073); nothing new to design.

## Background

Pause/Resume buttons existed in the DOM and their IPC channels (`build:pause`/`build:resume`)
were declared since the MVP, but stayed UI-disabled behind `MVP_PAUSE_RESUME_ENABLED = false`
pending Stefan's manual `npm run dev` eyeball gate. That gate cleared 2026-07-16 (GUI renders
correctly across all 5 themes, fully responsive, a real end-to-end generation succeeded) —
unblocking this task.

## What shipped

- **A real `'paused'` RunState** (`gui/src/renderer/state.ts`), distinct from `'stopped'`. This
  was the one actual design decision: Stop means "abandon this run, Start begins a fresh one";
  Pause means "suspend, expecting Resume." The old `enablement()` gated Resume on `state ===
  'stopped'` (an MVP-era placeholder, since Pause was disabled anyway) — collapsing both onto one
  state would have made Resume appear after a plain Stop too, which is wrong. `pause`/`resume`
  enablement is now `state === 'running'` / `state === 'paused'` respectively, with no MVP flag
  gating them off anymore.
- **`main.ts`**: new `doPause()`/`doResume()` functions (mirroring `doStop()`'s shape) call
  `gui.buildPause`/`gui.buildResume`, transition `runState`, and toast. Click handlers wired for
  `run-pause`/`run-resume` (previously inert — the buttons existed but had no listener).
- **i18n**: the `run.phase2` key (a single "coming in phase 2" tooltip shared by both buttons)
  split into real `run.pauseHint`/`run.resumeHint` strings, plus new `toast.paused`/
  `toast.resumed` entries — filled across all 7 hand-written language tables (en/bg/de/ru/tr/
  romani/yoda); joke-tier + SGA picked them up automatically via `fromEn`/re-export, same as
  every prior i18n addition.
- **Doc correction** (`docs/explanation/gui-architecture.md`): found and fixed a pre-existing
  doc/code mismatch while verifying the pause path — the doc claimed Pause "waits for the
  checkpoint" (the current stage's `"end"` log line) before killing the child; the actual
  `GenesisRunner.pause()` (unchanged since changelog 073) always kills immediately, same as Stop.
  This is still CORRECT behavior (every stage is idempotent, so an immediate kill just discards
  one stage's partial work, re-run from scratch on Resume) — but the doc described an aspiration
  that was never implemented. Corrected the doc, the mermaid diagram's transition label, and the
  misleading code comment on `pause()` itself to describe reality, rather than leave a false
  claim in a same-commit-rule doc.

## Verified

`tsc --noEmit` clean. `electron-vite build` all 3 targets green. `node --test`: 42/42 (37 prior +
5 new — a new `gui/test/state.test.ts` pins the `enablement()`/`activeButton()` state machine
exhaustively across all 7 `RunState` values, specifically guarding the paused-vs-stopped
distinction this change introduced). Not verified: on-screen render of an actual pause/resume
click sequence against a live pipeline run — the standing `npm run dev` gate covers rendering and
one full generation, not this specific interaction; a live pause/resume click-through is a
reasonable next manual check if Stefan wants to exercise it, not treated as done here.

## Affected files

- `gui/src/renderer/state.ts` (new `'paused'` RunState, enablement logic)
- `gui/src/renderer/main.ts` (`doPause`/`doResume`, click wiring, string application)
- `gui/src/renderer/i18n/{en,bg,de,ru,tr,romani,yoda}.ts` (`pauseHint`/`resumeHint`,
  `toast.paused`/`toast.resumed`; `phase2` key removed)
- `gui/src/main/genesis-runner.ts` (comment correction only, no behavior change)
- `gui/test/state.test.ts` (new)
- `docs/explanation/gui-architecture.md` (pause-checkpoint doc correction)
- `tasks.md`
