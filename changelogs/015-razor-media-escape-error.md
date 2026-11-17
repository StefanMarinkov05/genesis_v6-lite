# Build error: unescaped @media inside the design overlay's <style> block

**Type:** Error fix
**Model:** Opus — root-cause diagnosis of a well-known but non-obvious Razor parsing gotcha.

## Where
`templates/views/design_overlay.sbn`, the `@media (prefers-reduced-motion: reduce)` rule.

## How it happened
Razor does not treat inline `<style>` blocks as raw, un-parsed text the way it does `<script>` blocks. A bare `@media` inside embedded CSS gets parsed as a Razor code transition (`@` followed by the identifier `media`), which the C# compiler then rejects because no such identifier exists.

## Why
This is a well-known ASP.NET Core Razor gotcha (CSS at-rules starting with `@` need to be escaped as `@@` inside `.cshtml` markup) that had never been hit before because this template had never actually been compiled.

## What it affects
Blocks the generated project's own build entirely - `_Layout.cshtml` fails to compile for every single generated project, since the design overlay is always emitted.

## How to reproduce
Generate any project and run `dotnet build` on it. Fails with: `error CS0103: The name 'media' does not exist in the current context` at `_Layout.cshtml`.

## How to fix
Escaped it to `@@media`, Razor's own escape sequence for a literal `@` in rendered output. Confirmed this is the only place in the codebase where one template's rendered output gets embedded as a raw string inside another `.cshtml` file, so no sibling instances of this exact bug exist elsewhere.
