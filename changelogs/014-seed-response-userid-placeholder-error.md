# Data bug: seed-response UserId placeholder wouldn't resolve to a real user

**Type:** Error fix
**Model:** Opus — cross-referencing a hand-built seed dataset against the generator's own schema-derivation logic - needed to trace SeedStage's exact rules.

## Where
The manually-pasted `seed-response.json` (all `Product`/`Post` rows) and, mechanically, `templates/models/seed_data.sbn`'s `ResolveUserRef` function it needed to satisfy.

## How it happened
Every `UserId` in the pasted data was the literal string `"seed-user-placeholder"`. `ResolveUserRef` only resolves strings that literally start with `"$seedUser:"` to a real Identity GUID - anything else is inserted into the database exactly as written.

## Why
The placeholder text doesn't match that prefix, so every seeded product/post would have been permanently "owned" by a phantom id that matches no real user.

## What it affects
Every ownership check in the generated app (edit/delete buttons, "my posts", etc.) would silently never match, and the public profile page (`/Client/Profiles/Public/seed-user-placeholder`) would 404 for every single seeded post's author link.

## How to reproduce
Seed the database with the pasted file as-is, then click any seeded post's author link, or try to edit/delete a seeded item as the seed user who "owns" it.

## How to fix
Replaced every occurrence with a real `$seedUser:0`/`$seedUser:1`, alternating across FaceNote's 2 actual seed users. Also discovered the pasted response was missing a `refKey` on `Product`/`Post` (both become parents once features are expanded) and was missing all 9 feature-injected models entirely (Catalogue, CartItem, Order, Follow, Review, PostComment, PostVote, etc.) - built those from scratch and cross-reference-validated every `parentRef_*`/`$seedUser:N` before writing the file.
