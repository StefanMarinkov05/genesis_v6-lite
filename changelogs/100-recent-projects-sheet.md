# 100 — GUI: recent-projects sheet off `logs/registry.json`

**Type:** Feature (GUI)
**Model:** Sonnet, low effort, thinking off — per the Phase 3 routing breakdown (no design
ambiguity, standard implementation following the existing sheet/panel pattern).

## What shipped

A new toolbar button ("Recent projects") opens a sheet (reusing the existing `.sheet-backdrop`/
`.sheet` component the Settings sheet already uses) listing every project the CLI has ever
recorded a completed run for, newest first, each with a "Open folder" action.

- **New IPC channel** `recentProjects:list` (main → typed request/response, no args). Read-only:
  the GUI never writes `logs/registry.json` — only the headless CLI's `RecordProjectRegistry`
  (`Program.cs`) does. Added to the same-commit IPC doc (`docs/explanation/gui-architecture.md`
  "IPC channel surface" section).
- **`gui/src/main/registry-parser.ts`** (new): the actual parsing logic (`parseRegistrySnapshot`)
  — deliberately has ZERO import of `./paths` (which pulls in Electron's `app` module), so it's
  directly unit-testable under plain `node --test`, same reasoning as `report.ts` staying
  Electron-free. Malformed JSON returns an empty list rather than throwing (matches the CLI's own
  "registry is best-effort" stance for this file).
- **`gui/src/main/recent-projects.ts`** (new): the thin filesystem/`genesisRoot()`-dependent
  wrapper — `existsSync` + `readFileSync` + delegates to `parseRegistrySnapshot`.
- **Renderer**: `main.ts` gained `renderRecentList`/`openRecentSheet`, wired to the new toolbar
  button, Esc-to-close, and `retranslate()` (so a live language switch re-paints an open sheet).
  "Open folder" reuses the existing `shell:openPath` channel — no new shell-access surface.
- **i18n**: new `toolbar.recent` + `recent.{title,empty,lastBuilt,open}` keys. Filled by hand in
  the 6 real/font-swap tables (en, bg, de, ru, tr, romani — `sga` re-exports `en` verbatim so it
  inherited automatically) and the one hand-written joke table (`yoda`). The 6 mechanically
  generated joke languages (morseFull/hex/sign/pirate/leet/flip) picked the new keys up
  automatically via `fromEn`'s deep-map over the `en` table — no edits needed there, confirmed by
  `tsc` passing clean on the first try (the `Strings`-typed guarantee working as intended).

## Verified

`tsc --noEmit` clean. `electron-vite build` all 3 targets green. `node --test`: 34/34 (31
existing + 3 new — `parseRegistrySnapshot`'s sort order, malformed-JSON fallback, empty-registry
case, using a real registry.json-shaped fixture matching `Program.cs`'s actual record shape).
Not verified: on-screen render (the standing `npm run dev` manual gate, same boundary as every
other GUI session — nothing about this feature changes that boundary).

## Affected files

- `gui/src/shared/ipc-types.ts` (new `RecentProject` type, `recentProjectsList` method, `CH` entry)
- `gui/src/main/registry-parser.ts` (new), `gui/src/main/recent-projects.ts` (new)
- `gui/src/main/ipc.ts`, `gui/src/preload/index.ts` (channel wiring)
- `gui/src/renderer/main.ts`, `index.html`, `styles.css` (sheet UI)
- `gui/src/renderer/i18n/{en,bg,de,ru,tr,romani,yoda}.ts` (new keys)
- `gui/test/recent-projects.test.ts` (new)
- `docs/explanation/gui-architecture.md` (IPC surface entry)
- `tasks.md`
