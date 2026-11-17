# Genesis v6 — Task Breakdown, Specs & Model/Effort Plan

**Mode:** Feature-on-existing (stack + conventions established; no re-litigating tech choices).
**Cross-cutting rule for ALL tasks:** every change updates the spec JSON + seed data (task 22) and gets a `changelogs/NNN-*.md` entry (changelog-discipline skill). Tasks 4 and 22 are not standalone — they're folded into every package below.

---

## How to read the model/effort columns

- **Model** = which Claude to route the task to when you actually build it (via Claude Code / your agent pipeline).
  - **Haiku** — mechanical, single-file, pattern-copy, low ambiguity.
  - **Sonnet** — standard feature work, multi-file but well-scoped, follows an existing pattern.
  - **Opus** — cross-cutting, architectural, touches the generator's core emission logic, or high ambiguity/design judgement.
- **Effort** = relative build cost (S ≈ <1 file / trivial, M ≈ a few files / one feature, L ≈ many files or a new subsystem).

---

## PACKAGE A — Localization / Bulgarian text (tasks 1, 3, 24-strings)

The single most repeated complaint: English still leaking through. Group them so the fix is systemic, not per-page.

| # | Task | Spec | Model | Effort |
|---|------|------|-------|--------|
| 1 | Page/property names still in English | Add `displayName` to every model property at the spec level; generator already warns on missing ones (see the B4 warning sweep) — turn that warning into a **hard gate** so generation fails if any non-key/FK property lacks a Bulgarian `displayName`. Backfill all missing ones in the spec JSON. | **Sonnet** | M |
| 3a | `Account/Manage/*` pages in English ("Manage your account", "Profile", "Email"…) | These come from ASP.NET Identity's scaffolded Razor pages, not our templates — they need a static replacement map (the spec already has an `identity.localization.staticReplacementMap` hook). Extend that map to cover the full Manage area. | **Opus** | M |
| 24b | Sorting dropdown strings in English | The sort field labels render from raw property names; make them use the `displayName` map. Fixed together with task 1's displayName work. | **Sonnet** | S |

**Why Opus on 3a:** Identity page localization is the one area that's fought back all along (it's scaffolded code we don't fully own); it needs judgement about replacement-map vs. re-scaffolding, not just pattern-copying.

---

## PACKAGE B — Cart & catalogue actions (tasks 2, 5, 18, 19, 25)

| # | Task | Spec | Model | Effort |
|---|------|------|-------|--------|
| 2 | "Add to cart" on `Products` grid + "Add to catalogue" on `Products/Details/{id}` | The grid button was wired in an earlier session (changelog 004) — verify it survived; add the catalogue button to the details page. | **Sonnet** | M |
| 5 | Cart badge always shows 0 | `ViewData["CartCount"]` is read in the layout but **never populated** — needs a component/filter that sets it per-request from the current user's cart. | **Sonnet** | M |
| 18 | Link to `Product/Details/{id}` from catalogue pages | Add the anchor in catalogue item rendering. | **Haiku** | S |
| 19 | User can't edit/delete own catalogues | Add Edit (rename) + Delete owner-gated actions + views. | **Sonnet** | M |
| 25 | Show catalogue owner name (with link) in catalogues section | Add author name + link to their public profile. | **Haiku** | S |

---

## PACKAGE C — Blog posts feature restore + expand (task 10)

| # | Task | Spec | Model | Effort |
|---|------|------|-------|--------|
| 10 | Restore general blog posts (photos in posts, comments, likes) — distinct from the feedback/rating stuff | The `Post`/`PostComment`/`PostVote` templates **exist** but aren't wired into FaceNote's spec. Re-enable the `posts` feature in the spec, add image support to posts, ensure comments + likes render. | **Opus** | L |

**Why Opus + L:** this is a whole feature surface (model + controller + views + seed data + nav), and it interacts with image upload (Package E) and the public profile (Package G). Highest-ambiguity item in the batch.

---

## PACKAGE D — Admin / supervisor surface (tasks 6, 7, 9, 20)

| # | Task | Spec | Model | Effort |
|---|------|------|-------|--------|
| 9 | Admin as total supervisor — sees every model (users, products, posts, orders, everything) | Architectural: a generated admin area with full CRUD/read over every model in the spec, not a hand-picked subset. This is a **generator-level** change to how the admin area is emitted. | **Opus** | L |
| 7 | `admin/users/details/{id}` full user page (followers, following, listings, comments, pfp, data; every FK link goes to that record's details) | New detail view aggregating across many models; the "every reference becomes a link" rule is a reusable emission pattern. | **Opus** | L |
| 6 | `admin/users`: show user id, links to followers/following/their listings; block admin deleting admin accounts | Extend the users table + add the self/admin-delete guard (partly done — changelog 004 added self-delete guard; extend to all-admins). | **Sonnet** | M |
| 20 | Admin image columns render URL text, not `<img>` | Wrap image fields in `<img>` in `admin_table.sbn`. | **Haiku** | S |

**Why Opus on 9 & 7:** both change how the generator *emits* the admin surface for arbitrary models — core template-engine work, the highest-leverage and highest-risk part of v6.

---

## PACKAGE E — Images & uploads (tasks 12, 14, 15, 16)

| # | Task | Spec | Model | Effort |
|---|------|------|-------|--------|
| 14 | Products with multiple images + UI to handle them | Model already supports `images: multiple`; build a carousel/gallery UI + multi-file handling. | **Sonnet** | M |
| 15 | Admin can't upload profile pic — add it | Wire file upload into the admin user edit form. | **Sonnet** | M |
| 16 | Feedback can't attach image/video — fix | Add media upload to the feedback/rating form. | **Sonnet** | M |
| 12 | Download random people photos for user seed data | Extend the existing image-fetch (Pexels/Pixabay) to pull avatar-style photos for seeded users. | **Haiku** | S |

**Note:** 15 + 16 share the upload plumbing with task 3's pfp-upload page — build the upload helper once, reuse across all three.

---

## PACKAGE F — Orders (task 8)

| # | Task | Spec | Model | Effort |
|---|------|------|-------|--------|
| 8 | Orders have no address or receiver name | Add `ReceiverName` + address fields to the Order model (spec + migration + form + display). | **Sonnet** | M |

---

## PACKAGE G — Public user profile (task 26)

| # | Task | Spec | Model | Effort |
|---|------|------|-------|--------|
| 26 | Public profile shows photo, pics, followers, following, listed products, feedbacks, posts | Big aggregation page — the public mirror of task 7's admin detail page; build them to share components. | **Opus** | L |

---

## PACKAGE H — Followers/following UI (task 17)

| # | Task | Spec | Model | Effort |
|---|------|------|-------|--------|
| 17 | Keep followers + following on a single page (two lists / two divs; DTO if needed) | New combined view + a `FollowersFollowingViewModel` DTO. Feeds tasks 6, 7, 26. | **Sonnet** | M |

**Build this early** — three other packages (D, G) depend on the followers/following list component.

---

## PACKAGE I — Listing UX (tasks 11, 13, 24-sort)

| # | Task | Spec | Model | Effort |
|---|------|------|-------|--------|
| 11 | Replace paging with infinite scroll / lazy loading | Swap the pager for scroll-triggered append (JS + a paged JSON endpoint). Generator-level change to `card_grid`/list emission. | **Opus** | M |
| 13 | `admin/product` sort-by broken | Debug the existing sort (debugging-protocol — it's a real bug, not a rewrite). | **Sonnet** | M |
| 24a | `Client/Products` has no descending sort | Add desc direction toggle to the sort. | **Sonnet** | S |

---

## PACKAGE J — Layout (tasks 21)

| # | Task | Spec | Model | Effort |
|---|------|------|-------|--------|
| 21 | Sticky header, always visible on scroll | CSS `position: sticky` on the header in `layout.sbn` + `design_overlay.sbn`. | **Haiku** | S |

---

## PACKAGE K — Seed data & schema (tasks 4, 22) — folded into every package

| # | Task | Spec | Model | Effort |
|---|------|------|-------|--------|
| 4 | 5 users (1 admin, 4 regular) + full seed data (products, orders, pics) | Expand `identity.seedUsers` to 5 + regenerate seed JSON covering every model. | **Sonnet** | M |
| 22 | Keep schema JSON + seed data updated | Not a standalone task — the definition-of-done for every other task. | — | — |

---

## Recommended build order (dependency-first)

1. **K + A** (seed users + localization) — foundation everything else displays.
2. **H** (followers/following component) — D, G depend on it.
3. **E** (upload plumbing) — 3, 15, 16, 14 all share it.
4. **B, F, I, J** (cart, orders, listing UX, layout) — independent, parallelizable.
5. **C** (blog posts) — needs E (images).
6. **D, G** (admin supervisor + public profile) — the big aggregation pages, need almost everything above.

## Model/effort summary

- **Opus (7 tasks):** 3a, 9, 7, 10, 26, 11 + the localization-gate design in 1. The architectural/generator-core/high-ambiguity work.
- **Sonnet (13 tasks):** the bulk — standard multi-file features following existing patterns.
- **Haiku (5 tasks):** 18, 25, 20, 12, 21. Mechanical, single-file, pattern-copy.

Numbering note: your list skips 23 — no missing task, just a gap in your numbering.
