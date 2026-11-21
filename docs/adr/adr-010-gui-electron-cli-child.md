<!-- last-verified: 5821ff6f31c37384b3bf306ddf9ee24276324030 -->
# ADR-010: GUI is an Electron shell over the headless CLI

**Status**: accepted (design); not yet implemented

**Context**: 6.5.0 (`plan/archive/dps-nn-6.5.0-spec.md`) adds a desktop GUI. Genesis is an existing headless
.NET pipeline with ~90% of the relevant logic already in place. The two hardest required features —
a live debug terminal and streamed process output — plus a 5-theme animated aesthetic, all favour a
web-shell stack. Decision log #1 already resolved the stack to Electron.

**Decision**: the GUI (`gui/`) is an Electron app that spawns the **existing CLI unchanged** as a
child process and wraps its lifecycle. Pipeline logic never moves into Node. Key contracts: cwd =
Genesis root; `--no-interactive` always; API key injected via child env from the main process only;
progress from the structured `dps-nn.log.jsonl` stage feed; pause = kill-after-checkpoint, resume =
respawn `run --all` (safe because stages are idempotent + checkpointed in `state.json`); dedup is
GUI-side (spec+seed hash → own manifest) with no CLI change; manual-LLM handled via exit code 3 +
the ADR-005 file contract.

**Full design**: `docs/explanation/gui-architecture.md` (process model, IPC channel surface, run-
control state machine, theming/i18n/security seams). This ADR is the index entry; that doc is the
single source of truth for the detail.

**Consequences**: the CLI stays independently usable and testable; the GUI adapts to the CLI's
contract, not vice versa; a broken/updated CLI can't be silently forked by GUI reimplementation.
Tripwire: if any GUI feature ever needs pipeline behaviour the CLI doesn't expose, add it to the
CLI (headless, testable) and have the GUI call it — never reimplement it in Node.
