# 098 — Outgoing Identity confirmation email English text: closed as non-issue

**Type:** Investigation / scope close (no code change)
**Model:** Sonnet, low effort — scope-confirmation question, not an implementation task.

## Background

Changelog 097 flagged, but deliberately did not fix, that `Email.cshtml.cs`'s
`OnPostChangeEmailAsync`/`OnPostSendVerificationEmailAsync` send outgoing mail via
`_emailSender.SendEmailAsync` with English subject ("Confirm your email") and body ("Please
confirm your account by clicking here."). Queued in `tasks.md` pending a scope confirmation from
Stefan: is a Bulgarian-language generated app expected to send Bulgarian confirmation emails?

## What was found

Asked Stefan directly. His answer: disable email confirmation as a requirement entirely, not
translate the email text.

Checked whether that's already true in the generator:

- `templates/project/program.sbn:35` already unconditionally sets
  `options.SignIn.RequireConfirmedAccount = false` — generated apps never gate sign-in on email
  confirmation, for any project, today.
- `IEmailSender` is never registered in `program.sbn` (grepped the whole templates tree — no
  hits). ASP.NET Identity's UI scaffolding falls back to its own built-in default `IEmailSender`,
  which is a no-op — `SendEmailAsync` never actually sends anything.

So the two English strings live in code that (a) never gates anything user-visible and (b) never
actually sends mail. There is nothing to translate or wire up — the outcome Stefan wants
("skip email confirmation totally, accept every email") was already the generator's behavior
before this session started.

## Resolution

No code change. Closed the `tasks.md` entry as a non-issue with the reasoning above, so a future
session doesn't re-open it or attempt a translation pass on dead-letter strings.

## Affected files

- `tasks.md` (entry removed from Open, summary added to Done)
