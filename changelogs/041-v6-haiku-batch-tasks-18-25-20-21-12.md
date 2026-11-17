# v6 Haiku batch — Tasks 18, 25, 20, 21, 12

**Type:** Feature
**Model:** Haiku — per the v6 task-breakdown's own model routing - all five tasks (18, 25, 20, 21, 12) rated mechanical/single-file/pattern-copy.

## Summary
Five small UI/presentation tasks bundled into one changelog:

## Task 18 — Link product from catalogue item

**`templates/views/catalogues_public_details.sbn`**
Catalogue items (which are references to Products) now render as clickable links to
`/Client/Products/Details/{id}` instead of plain text. Wrapped the item display in an
`<a>` tag with proper null-guard (shows "(изтрита обява)" if the referenced product is deleted).

## Task 25 — Show catalogue owner name + link

**`templates/views/catalogues_public.sbn`**
Added the catalogue owner's name (FirstName or email fallback) as a small subtitle on each
public catalogue card, linking to their public profile (`/Client/Profiles/Public/{userId}`).
Helps users discover who curated each catalogue.

## Task 20 — Admin image columns show `<img>` instead of URL

**`templates/views/admin_table.sbn`**
Image fields (properties ending with "Path") now render as `<img>` elements with `max-width:
100px; max-height: 100px; object-fit: cover` instead of raw URL text. Falls back to "—" for
empty/null values. Makes admin product/user/review tables much more visual.

## Task 21 — Sticky header

**`templates/views/layout.sbn`**
Added `position: sticky; top: 0; z-index: 1000;` to `<header>` via an inline `<style>` block.
Header now stays visible at the top of the viewport while scrolling.

## Task 12 — Seed real avatar photos

**`templates/identity/identity_seeder.sbn`**
Changed seed user avatars from generated initials (ui-avatars.com) to random real photos via
Picsum Photos API (`https://picsum.photos/150/150?random={index}`). Each seeded user gets a
deterministic-but-different photo based on their seed index, no API calls needed at runtime.

## Verification
- `dotnet test` 60/60 (no new tests needed — all are template changes, verified by template-emission tests)
- Not verified here: generated project compile + browser rendering (needs `2-generate.bat` run)

## Affected files
- `templates/views/catalogues_public_details.sbn`
- `templates/views/catalogues_public.sbn`
- `templates/views/admin_table.sbn`
- `templates/views/layout.sbn`
- `templates/identity/identity_seeder.sbn`
