# 091 — Product details: always show star rating, default to 0 stars

**Type:** Bug fix
**Model:** Sonnet, low effort, thinking off — a trivial, isolated template fix.

## Summary

`product_details.sbn` previously hid the star-rating row entirely (`@if (avg is not null)`) when
a product had zero reviews. Now the row always renders: with reviews, unchanged behavior (rounds
the real average, shows `(4.3 от 7 отзива)`); with zero reviews, shows `☆☆☆☆☆` (0 stars) and
`(няма отзиви)` instead of hiding the block.

Queued as the trivial half of a two-part ask; the larger half (average rating on the
`Client/Products` card-grid listing, which needs a new bulk-average transform on the scaffolded
Index action — currently `AverageRating` is only computed inside the Details action via
`InjectReviewLoadingIntoDetails`) is deferred to a follow-up session.

## Verification

Existing render tests (`Product_details_has_catalogue_modal_button_when_catalogues_enabled`,
`Product_details_feedback_form_supports_image_attachment`) still pass, confirming valid Scriban/
Razor syntax. Full suite: 116/116. No full regenerate run for this one — low-risk enough that
render-test coverage is sufficient (a plain conditional-display change, no controller/C# touched).

## Affected files

- `templates/views/product_details.sbn`
