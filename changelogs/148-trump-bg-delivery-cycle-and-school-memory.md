# Trump-bg handbook — the delivery cycle, the agent chain, and the per-school memory

**Type:** Docs (`docs/handbook/trump-bg/`, 9 chapters touched)
**Model:** Opus — the author described what the product actually does around the generator; the book said
only "four documents".

## What the author corrected

1. **The defence material is produced by a chain of agents**, not by one step: the first Sonnet writes
   the specification and hands off to Haiku (seed), a second Sonnet (presentation), a third
   (documentation), a fourth (questions with answers). A final Sonnet runs the built application, walks
   it in a browser, collects screenshots and code, and draws the database schema in LaTeX.
2. **The terminology list is one recycled file**, given to every client, because those questions
   ("what is MVC", "authorization vs authentication") are common to the subject, not to the project.
3. **The material is deliberately outside the project folder** so the student does not upload it with
   the application.
4. **The answers carry code references** — a file and a line range, not "in the controller".
5. **Documentation is deliberately absent from the public repository**: personal data, institution
   templates, GDPR.
6. **Delivery is a package in a private cloud** (MEGA or Proton), handed over as a link and key against
   a deposit.
7. **A local order/seed database keyed by school** makes the agent choose a seed, a design preset and a
   document/presentation format that the school has never received before — across years, not just
   within one term.

## Where it landed

| Chapter | Change |
|---|---|
| 00 предговор | one paragraph: the chain, the private-cloud delivery, under twenty minutes |
| 01 Проблемът и обещанието | new **Пълният цикъл** table (who produces what, 8 rows); new **Училището помни какво е получавало**; materials-outside-the-folder rule |
| 06 spec → seed | new **Преди да пише редове, seed-ът пита какво е било** |
| 12 принципи | new **Осите не решават сами - решава паметта за училището**, with the honest boundary |
| 15 как е построен | time as `T(n) = O(n)` with a 15-20 minute ceiling and the "the constant is large" caveat; token cost **0,20-0,70 €** for the whole chain (a clean cycle stays at ~17 ¢); buyer price **200-400 €** with the four factors |
| 22 граници | the school memory prevents similarity, it does not prove plagiarism; marks are not generated |
| 23 доставеното приложение | **Пакетът за защитата** rewritten as a seven-step agent chain, with subsections on code-carrying answers, the recycled terminology file, the manual built by an agent that actually browses the app, why the material lives outside the folder, and the private-cloud handover; the Word/PowerPoint and SQL Server/SSMS joke |

Seven rhetorical devices added, each used once: "не е сложно, а е много неща, подредени" (ch8), "тайна,
която не е тайна" (ch9), "никой не знаеше, че един CSS блок може да е толкова сложен" (ch21), "ще ви
омръзне да защитавате" (ch23), "ще видим какво ще стане" (ch22), "просто е, не е лесно" (ch14),
"проверете ме" (ch1). The "хората ме питат" device stays at three chapters.

## Honesty guards

- The defence chain is described as **around** the generator, not inside it: `verify` still checks only
  the application, and the book says so twice.
- The repository has no code for defence questions, `.docx`/`.pptx` or institution templates — `grep`
  over `src/`, `templates/`, `utils/` returns nothing. The chain and the school database are therefore
  documented as seller-side workflow, like the watermark.
- Token cost: the measured clean cycle (~17 ¢) is kept and the wider 0,20-0,70 € range is explained as
  iterations, not as a contradiction.

## Verification

`build-handbook2.py --lang trump-bg` → **116 pages**, no missing glyphs, zero `—` in the extracted text,
every new section rendered and inspected (pages 4, 8, 30, 59, 106-107). No other edition touched.
