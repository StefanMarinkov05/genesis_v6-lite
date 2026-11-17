# A6: order details view

**Type:** Feature / change
**Model:** Opus — new scaffolded-controller feature needing scaffold-transform judgement; pre-Claude-Code era, all sessions ran on Opus.

## Summary
Added a read-only order-details page reachable from the orders list. Own-order-only (ownership check by UserId), respects the soft-delete filter on Order (a deleted order 404s), shows line items with quantity/unit price.

## Techniques
- New `OrderDetails(int id)` action on the template-authored CartController
- New Scriban view template mirroring the existing cart view conventions
- Wired into the emission stage's orders block; "Детайли" link added to the orders list

## Affected files
- `templates/controllers/cart_controller.sbn`
- `templates/views/cart_order_details.sbn`
- `templates/views/cart_orders.sbn`
- `src/Genesis.Core/Stages/EmitControllersViewsStage.cs`
