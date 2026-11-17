# GUI design preview: 6-language picker + five component specs

**Type:** Feature / change

## Summary
Extended the Task 2 visual design system (`design/design-specs.md`) and its live preview
(`design/preview.html`) with the interaction surfaces the token system named but hadn't specified,
plus a working 6-language picker. Also tightened the Imperator theme definition to VM-photos-only
(logo permanently suppressed) and reassigned the German look to a Nova skin. No generator/pipeline
code touched — this is design-record + preview work for the not-yet-built 6.5.0 GUI.

## Techniques
- **6-language picker** (BG/EN/DE/RU/Rromani/Galactic) per `gui-architecture.md` i18n note: emoji
  flags where they exist, a hand-drawn inline-SVG flag for Rromani (no emoji exists), and a glyph
  badge for the Standard Galactic Alphabet (a font-swap over English, not a place). Keyboard-closable.
- **Five component specs** added to `design-specs.md §5`, each written in semantic tokens and tied to
  its IPC channel/ADR-010 contract:
  - *Duplicate-detection dialog* — the UI ADR-010's GUI-side dedup implied but never specified;
    three actions ranked by destructiveness, Enter=quiet-default/Esc=cancel, warning-styled regen.
  - *Toasts* — `toast` z-layer; info/success/warning/danger via `state.*` (not the run palette);
    auto-dismiss + hover-pause + reduced-motion fade; explicitly a mirror of events shown elsewhere.
  - *Completion/report readout* — `build:done`→`report:get`; copyable URL/creds/DB/start-command.
  - *Error/retry state* — `build:error`; progress→danger, terminal failure pin, retry-from-stage as
    default (safe per ADR-010 idempotent+checkpointed stages).
  - *Recent-projects list* — reads `logs/registry.json`, rehydrates setup without re-uploading.
  - *Settings panel* — write-only masked API key, genesis-command override (the "CLI stays
    first-class" seam), output dir.
- **Preview** gained a demo-control strip to trigger each state, in-app gear/recent entry points, and
  full JS wiring (toast lifecycle, overlay/sheet management, Esc-to-close). Div balance verified.

## Affected files
- `design/design-specs.md` (§4 Imperator/German, §5 five new component specs, §8 resolved decisions)
- `design/preview.html` (language dropdown, duplicate dialog, toasts, report/error screens, recent +
  settings sheets, demo strip, wiring)
- `changelogs/072-gui-preview-components-and-languages.md` (this entry)
