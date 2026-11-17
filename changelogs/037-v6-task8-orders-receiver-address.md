# v6 Task 8: Orders — receiver name + delivery address fields

**Type:** Feature
**Model:** Sonnet — per the v6 task-breakdown's own model routing (task 8 = Sonnet).

## Summary
The `Order` model now captures who ordered and where it should go. Previously "1-click checkout"
collected no delivery information at all.

## Changes

### `feature-templates/orders.json`
Added two **nullable** string fields to the `Order` model (nullable so existing seed rows and
existing generated migrations stay valid without breaking anything):
```json
{ "name": "ReceiverName", "type": "string", "displayName": "Получател", "nullable": true },
{ "name": "DeliveryAddress", "type": "string", "displayName": "Адрес за доставка", "nullable": true }
```
Both have Bulgarian `displayName` entries (displayName hard gate passes).

### `templates/views/cart_index.sbn`
Replaced the single "Поръчай с 1 клик" button with a delivery form:
- `receiverName` text input (Получател, maxlength 200)
- `deliveryAddress` textarea (Адрес за доставка, rows 2, maxlength 500)
- Total + "Поръчай" submit button below the inputs
The entire checkout form is one `<form asp-action="Checkout">` block. Fields are optional
(no `required` attribute) to preserve the single-click spirit for users who skip them.

### `templates/controllers/cart_controller.sbn`
`Checkout` now accepts the two delivery params:
```csharp
public async Task<IActionResult> Checkout([FromForm] string? receiverName, [FromForm] string? deliveryAddress)
```
Both are `.Trim()`'d before assignment to `Order.ReceiverName` / `Order.DeliveryAddress`.

### `templates/views/cart_order_details.sbn`
Adds receiver and address to the order detail `<dl>` (guarded on `IsNullOrWhiteSpace` so orders
placed without delivery info don't show empty rows).

## Seed data
No seed update needed: the new fields are nullable strings → `BuildSeedSchema` does not mark
them as required → existing seed rows stay valid without changes.

## Migration note
A real `run --all` will auto-generate an EF migration adding the two nullable columns
(`ReceiverName`, `DeliveryAddress`) to the `Orders` table.

## Verification
- `dotnet test` 60/60. New tests:
  - `Orders_feature_template_has_receiver_and_address_fields`
  - `Cart_checkout_view_has_delivery_form_fields`
  - `Cart_controller_passes_delivery_fields_to_order`

## Affected files
- `feature-templates/orders.json`
- `templates/views/cart_index.sbn`
- `templates/controllers/cart_controller.sbn`
- `templates/views/cart_order_details.sbn`
