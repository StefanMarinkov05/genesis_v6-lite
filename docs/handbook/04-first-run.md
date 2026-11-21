# Your first run

**You need:** Windows with the .NET 10 SDK, SQL Server Express LocalDB, `dotnet-ef` 10.x and
`dotnet-aspnet-codegenerator`. On Linux the generator's own build and tests run, but the generated
app's database does not — expect the run to stop at the first `ef` call, which is the expected
outcome of a Linux demo, not a bug.

Check the machine before blaming the tool:

```bash
dotnet run --project src/Genesis.Cli -- doctor
```

`doctor` prints one line per prerequisite with the exact command that fixes it. The GUI runs it on
every start and shows the report in its debug terminal.

## Build the generator, then generate the fixture

```bash
dotnet build
dotnet test
mkdir -p prompts/in
cp seeds/facenote-rich-seed.json prompts/in/seed-response.json
dotnet run --project src/Genesis.Cli -- run --all --spec fixtures/project-spec.facenote.json --out out
```

Why the copy: the `seed` stage asks an LLM for data unless a response already exists. Placing the
shipped seed where a response would go makes the whole run **offline and key-free** — the manual and
automatic LLM paths are the same file contract, so nothing else changes. Images still download on
the first run; they are cached afterwards.

Watch the console. Each stage announces itself (`=== stage: seed ===`), and the run ends with a
timings table and a banner (drawn in a box on the console; the role names vary per project):

```
FaceNote
  http://localhost:5xxx
  Admin            admin@facenote.bg / <derived password>
  Client           user@facenote.bg / <derived password>
(4 more seed user(s) - full list: logs/FaceNote/seed-credentials.txt)
Database: <database name>
Start: dotnet run --project "out/FaceNote"
```

Unless the spec sets a password, every seeded user's password is **derived**: it comes from the
project's variation seed and the user's email, so the same spec always prints the same credentials,
and two projects never share one. The banner shows one admin and one ordinary user; the full list
is in the credentials file it names.

## If it exits with code 3

Code `3` is not a failure — it means an LLM exchange is waiting for a file. The request is already
written; you either paste its contents into a chat and save the reply as
`prompts/in/<stage>-response.json`, or set `ANTHROPIC_API_KEY` and re-run. Either way you then add
`--resume`. Some steps ask a question on the terminal — `spec build` always asks the timestamp
conventions (`conventions.timestamps.createdAt? [y/n]`), and pickers appear when several seed files
are candidates; `--no-interactive` turns any such question into a failure instead of a prompt.

## What to look at when it finishes

| Path | Why |
|---|---|
| `out/FaceNote/` | the app itself; `dotnet run` inside it and log in with the banner's credentials |
| `out/FaceNote/dps-nn.log.jsonl` | stage boundaries and timings — what the GUI's progress bar tails |
| `out/FaceNote/plan/state.json` | which stages completed; delete it to force a clean re-run |
| `out/FaceNote/wwwroot/output/verify-language-report.txt` | the language gate's verdict, pass **or** skip |
| `logs/registry.json` | this project ← which spec and seed produced it |
| `prompts/out/schemas/` + `prompts/out/seeds/` | the permanent archive of exactly what was used |

## Re-running, and what "resume" means

`run --all` skips stages already recorded in `state.json`. That is usually what you want after a
crash and usually *not* what you want after changing a template — force the part you changed:

```bash
dotnet run --project src/Genesis.Cli -- run --all --spec fixtures/project-spec.facenote.json --from emit-controllers-views
```

`--from` re-runs that stage and everything after it in the runner's seven. `spec-expand` runs first
in every invocation unless its own state entry says done; `spec-build` is not part of the loop at
all (see [Chapter 9](09-workflow.md)).

## The fastest way to break it on purpose

Do this once; it teaches more than any chapter:

1. Open `out/FaceNote/Areas/Client/Views/Products/Index.cshtml` and change a Bulgarian label to
   English.
2. Re-run with `--from verify`.
3. Read the failure: the language report names the page and quotes the offending text.

Then undo the edit and re-run the same command: it passes again. Notice where you had to make the
fix: the output is generated, so in a real change the fix belongs in `templates/`, never in `out/`.
