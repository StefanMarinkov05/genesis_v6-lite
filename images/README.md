# images/

Generic image fallbacks copied into every generated app's `wwwroot/images/` by the scaffold stage
(`BuildStages.cs`, ADR-011 / `docs/explanation/image-pipeline.md`):

- `avatar-default.svg` — shown when a user has no profile picture
- `product-default.svg` — shown when a product has no image (also the seed stage's last-resort fallback)

GUI assets (logo, icon, backgrounds) live under `design/`, not here.
