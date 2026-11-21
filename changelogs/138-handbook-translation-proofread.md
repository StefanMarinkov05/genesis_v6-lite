# Handbook bg/de translations — full proofread, duplicate sections removed

**Type:** Docs
**Model:** Opus — a full read of both translations against the approved English edition 2.

## Summary

Edition 2 (`handbook2/`) was approved by the author. Then the Bulgarian and German manuscripts were
read in full, 00–25. The pass fixed structure, grammar, invented words and mixed-language text. The
English manuscript was **not** edited, because it is the approved text. Its weaknesses were
reported to the author instead.

## Root cause of the duplicates

The changelog 137 addendum added "five missing translated sections" to chapters 8, 11 and 13 in both
languages. It did not remove the condensed sections already standing in their place, so each of
those chapters said the same thing twice. In bg/08 the condensed copy also carried cross-edition
links (`../docs/reference/cli.md`) that point outside the translated book.

| File | Removed (condensed copy) | Kept (full section) |
|---|---|---|
| bg/08, de/08 | condensed "what sets it apart" + "how to read the code" table | the six numbered claims and the full table |
| de/08 | condensed tools table | full tools table, reordered as in English: tools → claims → how to read |
| bg/11, de/11 | one-paragraph "why the contracts are files" | the four-bullet section |
| bg/13, de/13 | headingless 5 200-line paragraph | the one under "scale for orientation" |

## Bulgarian

- **Latin words in Cyrillic prose replaced:**
  - preset → пресет / дизайн-пресет (matching the glossary)
  - bespoke → индивидуална
  - private → в тайна
  - overlay → слой от токени
  - feature фрагменти → фрагменти на функции
  - endpoint → адрес на API
  - native UI → нативен интерфейс
  - binary → изпълним файл
  - Desktop → настолното
  - lockout → заключване на акаунти
  - widget → уиджет
  - framework → фреймуърк
  - hot-linking → директни връзки към външни снимки
  - main процес → главен процес
  - the ch21 trap names (helper → помощни функции, node → възел; the "MSBuild node reuse" row had been mistranslated as "Регистър")
  - mock as a verb
  - AI-driven development lifecycle
- **Non-words and calques:**
  - изводна → производна
  - обаждания → извиквания (call ≠ phone call)
  - решения профил → вече изчисления профил ("разрешения" would read as "permissions")
  - кожа → облик (skin)
  - Резерва → Резервни изображения
- **Grammar:**
  - две корени → два корена
  - една и същ шаблон → един и същ шаблон
  - Всеки `manyToOne` → Всяка `manyToOne` връзка
  - биха искали → биха изисквали
- **Word order:** "Само за четене инсталационен корен" → "Инсталационен корен само за четене" (ADR-014).
- **Kept on purpose** (term policy): seed, build, verify, scaffold, skill, prompt, stub, runner, lock,
  navbar, CRUD, partial (glossed once), and quoted English error strings.

## German

- **Grammar:**
  - zum → zur Repository-Wurzel
  - eine → einer von drei Werten
  - Konstante → Konstant bleibt
  - Kein → Keine SPA
  - der → den Installer gibt es
- **Typos and false friends:**
  - Kontinurierliche → Kontinuierliche
  - Testharnisch → Prüfstand
  - "keine Rauchentwicklung" (literal "smoke") → kein angehängter Smoke-Test
  - "Eingaben kommen aus dem Banner" → Anmeldedaten
  - "Sich wiederholende Zeit" → Wiederkehrende Termine
  - "geschlossener Kreis, der auf das Schließen wartet" → Regelkreis, dem das letzte Glied fehlt
  - Token-Overlay → Token-Schicht
- **English left in German prose:** "owner-or-admin" (de/05, de/23) → "nur Eigentümer oder Admin".

## Verification

- `build-handbook2.py --lang bg` → 75 pages; `--lang de` → 73 pages. Both builds had 0 Missing
  character warnings.
- In the `pdftotext` output:
  - no Cyrillic in de;
  - none of the replaced forms left in bg;
  - no duplicate H2 heading in any bg/de chapter;
  - no `../docs/` link in either edition.
