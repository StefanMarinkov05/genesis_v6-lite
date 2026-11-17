# Warnings: 9 (then 2 more found) properties rendering English field names

**Type:** Error fix
**Model:** Opus — sweep across every feature fragment + fixture - needed judgement about which properties are legitimately exempt (keys/FKs/computed).

## Where
`feature-templates/orders.json`, `posts.json`, `rating.json`, `follows.json`, and (found during a sweep, not from the original warning list) `cart.json`.

## How it happened
Several model properties injected by feature fragments (`CartItem.UserId`, `Order.UserId`, `OrderItem.Quantity`, `PostComment.UserId`, `PostVote.UserId`/`Value`, `Review.AuthorUserId`, `Follow.FollowerUserId`/`FollowedUserId`) had no `displayName`, so the generated UI would show the raw English C# property name instead of a Bulgarian label - breaking the Bulgarian-only-UI requirement.

## Why
These properties were added across several earlier feature-fragment sessions without a `displayName`, and nothing had exercised a real pipeline run against them until this one, so the warnings were never actually seen before.

## What it affects
Every generated project that enables `orders`, `posts`, `rating`, or `follows` - the affected fields render in English on forms/tables/details pages instead of Bulgarian.

## How to reproduce
Run the pipeline against a spec enabling those features; the console prints `[dps-nn] [warn] model X.Y lacks displayName - renders an English property name` for each one.

## How to fix
Added a Bulgarian `displayName` to every flagged property, matching the wording already used for related fields in the same fragment. A full sweep of every fragment + every fixture (not just the 9 originally flagged) caught 2 more never-triggered gaps in `cart.json` (a separate `CartItem` from the one FaceNote actually exercises) - fixed those too. Everything is now displayName-clean.
