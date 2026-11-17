## 048 — Task 11: Infinite scroll replacing the pager — Package I

**Type:** feature
**Model:** Opus — per the v6 task-breakdown's own model routing (task 11 = Opus, generator-level emission change).
**Date:** 2026-07-10
**Task completed:** v6 Task 11 (replace numbered paging with scroll-triggered lazy loading on card grids)

### What changed (`templates/views/card_grid.sbn`)

- Removed the numbered `<nav class="pagination">` pager.
- Added a bottom `#scrollSentinel` div carrying `data-current-page` / `data-total-pages`, with a spinner
  shown while more pages remain.
- Added an `IntersectionObserver` (rootMargin 200px) that, when the sentinel scrolls into view and more
  pages remain, fetches the **same Index route** with `?page=N+1` (preserving the existing
  `search`/`sortBy`/`sortDir` query params), parses the returned HTML with `DOMParser`, and appends the
  `#cardContainer .card-item` nodes. Stops (and disconnects) when the last page is reached or a fetch
  returns no cards.

No new controller endpoint: it reuses the card grid's existing server-side pagination (the Index action
already accepts `page`), so the change is confined to the view template — the generator-level card-grid
emission — with no controller/scaffold-transform risk.

### Client search still works on lazily-loaded cards

The existing client-side search filter was extracted into a reusable `filterCards(scope)` function and is
re-applied to each appended batch, so infinite-scrolled cards respect the active search box.

### Verified

`run --all` → **verify PASSED** (build, db, boot, http, manifest, language). Generated
`Areas/Client/Views/Products/Index.cshtml` contains the sentinel + observer and no `pagination`/`page-link`
markup. 1 unit test (sentinel + IntersectionObserver present, pager gone). 79/79.
