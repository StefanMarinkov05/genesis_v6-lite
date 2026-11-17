<!-- last-verified: 1b5fea4319805d9a0571619a0318462c896ee4ba -->
# ADR-002b: Core Identity page markup emitted wholesale (supersedes the map as primary)

**Status**: accepted (calibrated against the TravelMap reference project)

**Context**: the reference project's Identity pages are custom-written Bulgarian Razor (card shell,
form-floating, bi-icons) - not translated scaffolder output. A replacement map can translate, but
cannot restyle.

**Decision**: after `aspnet-codegenerator identity`, genesis overwrites `Login.cshtml`,
`Register.cshtml`, `Logout.cshtml` MARKUP from templates matching the reference style, keeping the
scaffolded `.cshtml.cs` code-behind untouched (that is where the auth logic lives). The static
replacement map (ADR-002) remains as fallback for any additionally scaffolded pages.

**Consequences**: professor-visible pages match the reference aesthetic; the SDK-version fragility
of the map now only affects secondary pages; runtime error strings remain BgIdentityErrorDescriber's job.
