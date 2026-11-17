<!-- last-verified: 1b5fea4319805d9a0571619a0318462c896ee4ba -->
# ADR-002: Identity page localization via static replacement map

**Status**: accepted

**Context**: scaffolded Identity Razor pages are English; the requirement is Bulgarian-only UI without .resx.

**Decision**: two distinct mechanisms. (a) Page markup: deterministic EN->BG replacement map (`templates/identity/identity-bg-map.json`), versioned per sdkPin, applied longest-key-first over `Areas/Identity/Pages/**/*.cshtml`. (b) Runtime error strings: `BgIdentityErrorDescriber : IdentityErrorDescriber` registered via `.AddErrorDescriber<>()`.

**Rejected**: LLM translation of scaffolded pages (non-deterministic, can corrupt Razor); .resx (requirement excludes it).

**Consequences**: an SDK bump requires re-calibrating the map (the verify English-leak scan catches drift loudly).
