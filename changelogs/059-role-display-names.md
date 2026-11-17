# Bulgarian role display names in admin Users list (item 16)

**Type:** Feature / change

## Summary
Round-2 fix batch item 16: the admin Users table showed the raw ASP.NET Identity role name
("Administrator"/"Potrebitel"/etc., whichever the variation resolver picked) instead of a Bulgarian
label. Fixed display-only, without touching the underlying role name used by `User.IsInRole(...)`.

## Techniques
- `templates/views/users_index.sbn` builds a `roleDisplayNames` dictionary from `spec.identity.roles`
  (`{{ if r.is_admin }}Администратор{{ else }}Потребител{{ end }}`, keyed by each role's actual
  resolved name) and looks the stored role name up through it at the one cell that displays it.
- Deliberately did NOT touch any `User.IsInRole("...")` call site (layout.sbn's admin-menu gate,
  etc.) - those need the real ASP.NET role name to keep authorization working; only the visible
  table cell changes.

## Verification
`dotnet build` + `dotnet test` 111/111. Full BG regenerate (bundled with item 1's checkpoint-1
regenerate in the same session) confirmed the admin Users page compiles with the new dictionary and
build stayed green throughout.

## Affected files
- `templates/views/users_index.sbn`
