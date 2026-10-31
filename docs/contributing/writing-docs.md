# How to write documentation

Documentation here is for people first: a developer who joined last week should understand any page on the first read, without asking anyone. The same page must also give a teammate one fact fast, and give an AI agent clear statements and source links.

Five rules carry most of the weight:

1. **Summary first.** At every level, the summary comes first and the detail below.
2. **Plain words.** Short sentences and common words.
3. **Few acronyms.** Write the words.
4. **Short.** Every sentence earns its place, or it goes.
5. **One story.** One page answers one question, in a straight line.

## Put the summary first, at every level

Each level summarizes and links to the level below it:

| Level | Its summary | Its detail |
| --- | --- | --- |
| Repository | [README.md](../../README.md): what this is and where to go | Chapters under `docs/` |
| Chapter | Entry page: what it covers and which page answers what | Topic pages |
| Page | Title and a one- or two-sentence purpose line | Sections |
| Section | Heading and its first sentence, which gives the answer | Paragraphs, lists, tables |

- **The stop test.** A reader who stops at any level still holds a correct, simplified picture. Reading only the headings and first sentences gives a page's main message.
- **Push detail down, never up.** A summary links to its detail and never repeats it. Normally use at most two levels: an overview, then detail pages.
- **Keep groups small**: three to seven list items, sections, or diagram boxes. Past nine, group them or split the page.
- **Keep the outline shallow**: `#` for the title, `##` for sections, `###` for subsections. Need a fourth level? Split the page.
- **Make headings tell the story.** Each says what its section answers, such as "Retries stop after three attempts", never "Details".

## Say it plainly

- **One idea per sentence**, about 15 to 20 words. Paragraphs of four sentences or fewer.
- **Common words**: "use", not "utilize"; "to", not "in order to". Keep a technical term only when it's the real name of something, such as a class, a setting, or an error code. Explain it once.
- **Active voice, present tense, "you"**: "The handler checks the request", not "The request is checked". Use the imperative for instructions: "Run the build".
- **Concrete over abstract**: real paths, commands, names, and values. "The service rejects files over 4 MB", not "Large files may fail".

## Avoid acronyms

Readers often land mid-page from a search, without the context that defined an acronym.

- **Write the full words.** Keep an acronym only when readers meet it elsewhere, such as in code, the user interface, or error messages. Also keep one that's better known than its expansion, such as AI, API, HTTP, JSON, or ID.
- **Expand it on first use in each page**: "request units (RU)". Pages are read alone.
- **Never invent one** to save space.

## Keep it short

- **The deletion test.** Delete every sentence the reader wouldn't miss. Cut warm-ups such as "This section describes…", closing summaries, and hedges such as "basically". Cut history the reader doesn't need, and anything another page already says: link to it instead.
- **Use the shortest shape**: a table for anything looked up, a numbered list for steps done in order, bullets for options or parts. Use prose only to explain why, and how things connect.
- **Size limits**: a purpose line is one or two sentences, a section about one screen, and a page about 1,200 words of prose.

## Tell one story

- **One page, one question**, named in the title: "How modules depend on each other", not "Module notes". Two questions make two pages.
- **A predictable order.** To explain something: what it is, why it matters, how it works, what can go wrong, where to go next. For a task: the goal, what you need first, the steps, how to check, and what to do if it failed.
- **Introduce before you use.** Define a term before the first sentence that needs it, and use the same name for the same thing everywhere.
- **Turn nested conditions into a table of cases**, one row per situation.
- **No dead ends.** Say what to do next: the page to open, the setting to check, or the command to run.

## Back every claim

- **Link each claim about behavior** to the code or test that settles it.
- **Keep four kinds of statement apart**, so no one mistakes a plan for a fact:

  | Kind | How to write it |
  | --- | --- |
  | Current behavior | A plain statement, linked to its source |
  | Recommendation | "We recommend…", with the reason |
  | Proposal | "Proposed: … This is not implemented." |
  | Unknown | "Not confirmed. No test or setting defines it." |

- **Never paste secrets**, tokens, or personal data. Use a placeholder such as `<your-token>`, and say where the real value comes from.

## Format for scanning

- **Tables**: introduce each with a sentence saying what it lists, and put the lookup key in the first column.
- **Code blocks**: tag the language, show one command per block when readers run them one at a time, and state the working directory.
- **Warnings**: one short bold line, used rarely.

## Check a page before you finish

1. Do the title, purpose line, and headings alone tell the story?
2. Would a new teammate follow it without asking a question?
3. Did every sentence pass the deletion test?
4. Does every claim about behavior link to its source?
5. Can the reader see where to go next?
