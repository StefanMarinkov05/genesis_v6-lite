# Four generator bugs from the handbook audit fixed (preset hash, strict verify + authorization, personalization, cart)

**Type:** Error fix (4 bugs + 1 security finding)
**Model:** Opus — cross-cutting: one change touches variation, two stages, the verify gate and six
templates, and the authorization gap was a root-cause finding made while fixing verify.

The claim audit (changelog 139) reported four code issues without fixing them. The author chose to
**repair** `cart` and `personalization` rather than remove them. Each fix was done red → green →
revert → green (testing-policy). Suite now 191 tests, all green.

## 1. Design preset picked with `GetHashCode()`

- **Where:** `src/Genesis.Core/Util/TemplateEngine.cs`, `ResolveDesignPreset`.
- **How it happened:** an unpinned `meta.style.preset` was picked with
  `variation.Seed.GetHashCode() % names.Count`.
- **Why it's a bug:** `string.GetHashCode()` is randomised per process in .NET, so two runs of one
  spec could get different presets. That breaks ADR-004's reproducibility guarantee.
- **What it affects:** the look of every project that does not pin `meta.style.preset`. FaceNote
  pins it, so it was not affected.
- **Fix:** the preset is now picked with `VariationResolver.DeterministicIndex(seed, "designPreset", count)`,
  the same SHA-256 pick the axes use.
- **Tests:**
  - `Unpinned_design_preset_is_sha256_of_seed` checks six golden values computed independently in
    Python;
  - `Pinned_design_preset_wins_over_derivation`.

## 2. Lenient `verify`, and no check on anonymous access, which exposed missing `[Authorize]`

- **Where:**
  - `VerifyAndReportStages.cs` (verify);
  - `EmitControllersViewsStage.cs` (authorization).
- **How it happened:** before this fix:
  - only `/` and the admin login were hard failures;
  - any other declared route that answered non-200 was logged (`language scan skipping …`) and
    skipped;
  - nothing checked that anonymous users are refused protected pages.
- **Security finding:** while adding that check, I found that scaffolded controllers carried **no
  `[Authorize]` at all**. The scaffolder emits none, and no deviation added one. This applied to:
  - admin-area CRUD controllers, including the admin-supervisor surface;
  - client-area Create/Edit/Delete.

  So an anonymous user could open the admin CRUD pages. Only the templated feature controllers were
  protected.
- **Fix, authorization:** the new `InjectAreaAuthorization` stamps, idempotently and failing loud if
  the class is not found:
  - admin area: `[Authorize(Roles = "<admin role>")]`;
  - other areas: `[Authorize]`, plus `[AllowAnonymous]` on `Index`/`Details`.

  It runs after the deviations on scaffolded controllers, and on templated admin controllers.
- **Fix, verify:** `DiscoverRoutes` now also returns the routes anonymous users must be refused, and
  verify GETs each of them anonymously with no redirect-following.
  - A route passes only on 401/403 or a 302 to Login/AccessDenied (`IsAnonymousRefusal`).
  - Every declared route that does not answer 200 is now a failure.
  - Failures are collected and thrown once, as a full list.
- **Decision reversed:** the Session 3 decision "feature/admin route fetches are best-effort" (see
  `plan/decisions.md`).
- **Tests:**
  - scaffold `[Authorize]` injection, admin and client;
  - route discovery for the refuse list;
  - `IsAnonymousRefusal` truth table.

## 3. `cart` feature unusable

- **Where:**
  - `feature-templates/cart.json`;
  - `SpecExpandStage.MergeFragment`;
  - the cart emission in `EmitControllersViewsStage`;
  - `cart_controller.sbn`, `cart_index.sbn`, `layout.sbn`, `card_grid.sbn`, `product_details.sbn`.
- **How it happened:**
  - **cart + orders:** both fragments define `CartItem`, so the merge threw
    "duplicate models entry 'CartItem'".
  - **cart alone:** `cart.json` injected a template `CartController` with no `Cart/Index` page, so
    semantic validation failed.
  - Even if it had passed, the whole cart UI was gated on `orders` only.
- **Fix:**
  - `cart.json`'s `CartItem` is now identical to the one in `orders.json` (FK and relation to
    `$primaryModel`), and its `controllers` entry is gone.
  - `MergeFragment` skips a duplicate whose definition is identical (`JsonNode.DeepEquals`) and still
    throws on one that differs.
  - A new template flag, `has_cart` (cart || orders), gates the CartController, `Cart/Index`, the
    navbar badge and the add-to-cart buttons.
  - Checkout, `Orders` and `OrderDetails` (controller actions, views, the checkout form and the
    "Поръчки" link) stay on `features.orders`, so a cart-only project has no `Order` reference.
  - `DiscoverRoutes` adds `/Client/Cart` for either feature, and `/Cart/Orders` only with `orders`.
- **Tests:**
  - the real `SpecExpandStage` over FaceNote with cart-only and cart+orders, each yielding a valid,
    re-validated lock;
  - fragment `CartItem` deep-equality;
  - identical duplicates skipped, conflicting duplicates rejected;
  - a cart-only controller, index, layout and card grid contain no `Order` or `Checkout`.

## 4. `personalization` inert

- **Where:** `templates/views/layout.sbn`, `feature-templates/personalization.json` (comment).
- **How it happened:** the fragment was empty, and nothing read `features.personalization`.
- **Fix:** the flag now gates the logged-in navbar widget: the "Здравей, <name>" greeting and the
  avatar link. With `posts` or `catalogues` on, it also adds a "Моят профил" link to the user's public
  profile. The cart badge and logout are outside the gate.
  - FaceNote enables personalization, posts and catalogues, so it keeps the widget and gains the
    profile link.
- **Tests:** personalization on, off, and on without profile features.

## Not verified here

`dotnet build` and `dotnet test` run on Linux. The generated-app `verify` needs SQL Server LocalDB
(Windows), so these were **not** run end to end:

- the strict route check;
- the anonymous-refusal check against real `[Authorize]`;
- a cart-only build;
- the personalization layout.

A full Windows `run --all` on FaceNote plus a cart-only spec is still required. If verify now fails
on a route that used to be silently skipped, that is the gate working: fix the route, not the gate.

## Docs

- Handbook2, **en + de only**; bg left untouched by request, and `trump/` derives from en: ch01, 02,
  03, 08, 09, 10, 12, 22, 23, 25 (de also 21).
- README, README.lite, `control-flow.md`, `techniques.md`, `generated-app-anatomy.md`,
  `spec-format.md`.
- The PDFs were rebuilt.
