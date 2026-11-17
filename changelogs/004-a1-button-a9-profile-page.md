# A1 add-to-cart button + A9 public profile page

**Type:** Feature / change
**Model:** Opus — cross-file feature spanning template + scaffold-adjacent linking, high ambiguity with no compiler available in-session.

## Summary
The product card's "Добави" button was a dead stub (a bare `<button>` posting nowhere) - rewired it to a real form posting to Cart/Add, gated on the orders feature. Built a new public, read-only user profile page (name/avatar + their posts + their public catalogues), linked from a post's author name. Along the way, found A8's author-delete-on-posts was already fully implemented from an earlier session (no work needed).

## Techniques
- Card button rewired to a real `[HttpPost]` form (antiforgery handled by the tag helper)
- New `ProfilesController` (`[AllowAnonymous] Public(string id)`) + a defensive view that checks which ApplicationUser extra properties actually exist before referencing them
- Author link added by UserId (no controller-query change needed on the posts side)

## Affected files
- `templates/views/card_grid.sbn`
- `templates/controllers/profiles_controller.sbn`
- `templates/views/profiles_public.sbn`
- `templates/views/posts_details.sbn`
- `src/Genesis.Core/Stages/EmitControllersViewsStage.cs`
