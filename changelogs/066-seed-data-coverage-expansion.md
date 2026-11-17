# Seed data coverage expansion (item 8, partial)

**Type:** Feature / change

## Summary
Extended FaceNote's seed data to close user-coverage gaps and fill previously-empty fields, per
Stefan's specific asks: every user gets a cart item, every user gets an order, every order gets a
receiver name + delivery address. Scoped to pure data changes this pass - the "2 images per some
products" sub-ask is explicitly deferred (see below), since it needs a real `SeedStage.cs` code
change, not just data.

## What changed (`prompts/in/seed-response.json` + its archived mirror)
- **Every existing Order** (3 rows) got `ReceiverName`/`DeliveryAddress` filled in (previously null
  on all 3, despite the checkout flow and view already correctly handling them - confirmed during
  item 4's investigation that this was purely a seed-data gap, not a code bug).
- **3 new Orders** added for the users who previously had none (`$seedUser:0`, `:3`, `:5`) - all 6
  seed users now have at least one order, each with receiver/address, each with a matching
  `OrderItem` row.
- **2 new CartItems** for the previously-uncovered users (`$seedUser:0`, `:5`) - all 6 users now
  have at least one cart item.
- **User 5 (petar@facenote.bg, added mid-session as the second admin) was completely absent** from
  Post/PostComment/PostVote/Review/Follow - added one row referencing him in each (a new Post, a
  comment, a vote, a review, and two Follow edges) so the newest seed user isn't a ghost in the
  generated data.

## Deliberately NOT done this pass
- **2 images per product**: `SeedStage.cs`'s image-download loop and `seed_data.sbn`'s per-row
  Image-table insert are both hardcoded to exactly 1 image per row (`imageIndex` increments once
  per row, one `{Model}Image` insert per row) - giving some products a 2nd image needs an actual
  code change (a new optional `imageCount`/`imageKeywords` field, a per-row download loop instead
  of a flat counter, and a matching insert loop in the template), not just more JSON rows. Flagging
  as still open rather than quietly skipping it.
- Did not do a full blanket "double everything" pass on Post/Catalogue/PostComment/PostVote/Review
  row counts beyond what was needed to close the user-5 gap - prioritized Stefan's concrete asks
  (cart/order coverage, receiver/address) over raw volume given the session's time constraints.

## Verification
- Self-validated against `SeedStage.BuildSeedSchema`'s actual rules via script (refKey uniqueness,
  every `parentRef_*`/FK resolves, `$seedUser:N` indices in range, Catalogue's scaled minimum still
  met, every Order has both fields, full user-coverage check per model) before touching the CLI.
- Full clean `run --all` regenerate, verify PASSED. Inspected the actual runtime
  `out/FaceNote/FaceNote/Data/seed-data.json` (not just the source JSON) directly: 6 Orders, 6
  CartItems, all 6 seed users represented with receiver name + delivery address on every order.

## Affected files
- `prompts/in/seed-response.json`, `prompts/out/seeds/facenote-seed.json` (both gitignored/local
  only, not part of this commit - same as prior seed-data sessions)
