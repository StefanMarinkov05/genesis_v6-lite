# v6 Task 5: Cart badge reads real DB count instead of always showing 0

**Type:** Error fix
**Model:** Sonnet — per the v6 task-breakdown's own model routing (task 5 = Sonnet).

## Summary
`ViewData["CartCount"]` was read by layout.sbn's cart icon badge but was never set by
anyone — the controller and filter simply didn't exist. The badge displayed `0` on every page.

## Root cause
The layout expected a convention (`ViewData["CartCount"]`) that was never honoured. No controller,
action filter, or view component populated it. The badge code had a silent fallback (`?? 0`) that
made the bug invisible (no exception, just always-zero count).

## Fix
Instead of adding an action filter (which requires cross-stage emission wiring, like the
FileUploadService pattern), the layout now computes the count directly:

1. **`templates/views/layout.sbn`**: injects `{{ meta.project_name }}Context NavCartContext`
   (gated on `features.orders`, beside the existing `UserManagerNav` inject) and replaces the
   `try { cartCount = ViewData["CartCount"] ... } catch { }` block with a direct synchronous
   LINQ count against the injected context:
   ```csharp
   var cartCount = navUser is not null
       ? NavCartContext.CartItems.Count(i => i.UserId == navUser.Id)
       : 0;
   ```
   `navUser` is already computed above (via `UserManagerNav.GetUserAsync`), so this is a single
   extra `SELECT COUNT(*)` per authenticated page load — acceptable for a generated app.

## Verification
- `dotnet test` 60/60 (was 50, +10 this session). New test:
  `Layout_injects_cart_context_and_queries_count_when_orders_enabled` — asserts
  `NavCartContext` inject and `.Count(` are present; asserts `ViewData["CartCount"]` is gone.

## Affected files
- `templates/views/layout.sbn`
