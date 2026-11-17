# Profile reviews show product name + post details show author name (items 3 + 20)

**Type:** Feature / change

## Item 3 - public profile's Feedbacks (Reviews) list now shows the reviewed product's name.
- `templates/controllers/user_profile_service.sbn`: the Reviews query now `.Include()`s the
  reviewed model's navigation property - resolved generically by looking up `Review`'s own
  `manyToOne` relation in `spec.models` (works for whatever model the rating feature targets, not
  hardcoded to "Product").
- `templates/views/user_profile_content_partial.sbn`: the "към обявата" (to the listing) link now
  shows the listing's actual display title (reusing the file's existing `listing_title` variable,
  the same one already used elsewhere in this file for card titles) instead of a static caption,
  falling back to the old text if the navigation didn't load.

## Item 20 - Post Details now shows the author's actual name, not just a bare "Профил на автора" link.
- `templates/controllers/posts_controller.sbn`'s `Details` action now fetches the author via
  `_userManager.FindByIdAsync(post.UserId)` and puts their `FirstName` (falling back to `UserName`)
  in `ViewData["AuthorName"]`.
- `templates/views/posts_details.sbn`: the author link's text is now that name, with "Профил на
  автора" as a fallback if it's ever null.

## Verification
`dotnet build` + `dotnet test`: 111/111. Not yet live-regenerated (batching several small items
before the next full pipeline verify, per the session's time constraints).

## Affected files
- `templates/controllers/user_profile_service.sbn`, `templates/views/user_profile_content_partial.sbn`
- `templates/controllers/posts_controller.sbn`, `templates/views/posts_details.sbn`
