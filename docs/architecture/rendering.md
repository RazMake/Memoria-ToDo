# Rendering: how task Markdown shows

Status: Decided

Rendering is the shared module that turns a task's Markdown into what the user sees: an HTML page for a description in the web view, and styled text for titles and the editor. It also owns the color palette, the tag colors, and the look of headings, links, code, and italic text, and decides which links may open in the browser. Both apps use it, so a task looks the same on the desktop and the phone.

## Behavior

### Markdig renders descriptions with four extensions

Rendering uses [Markdig](https://github.com/xoofx/markdig), which [How the apps fit together](overview.md#each-app-edits-in-its-own-text-box-with-shared-commands) approves, with these extensions on:

- **Pipe tables**, for the table snippet.
- **Alert blocks**, for info blocks such as `> [!NOTE]`. All five kinds render: note, tip, important, warning, and caution ([alert blocks](https://xoofx.github.io/markdig/docs/extensions/alert-blocks/)). Markdig added them in [0.36.0](https://github.com/xoofx/markdig/releases/tag/0.36.0); later releases fixed alert blocks of more than one paragraph and made the kind's name case-insensitive ([releases](https://github.com/xoofx/markdig/releases)). So Rendering needs Markdig **0.37.0 or later**, and uses [Markdig](https://www.nuget.org/packages/Markdig/1.5.1) 1.5.1, a new dependency of Rendering's project.
- **Autolinks**, so a typed web address becomes a link.
- **Strikethrough**, for `~~text~~`.

Proposed: a single line break in a description shows as a line break, as the user typed it, not joined into one paragraph. This is a decision to confirm.

### Raw HTML shows as text, and `:red[text]` colors text

Rendering escapes all raw HTML in task Markdown, with no exception: `<b>` shows as the text `<b>`, never as bold. Markdig can turn HTML parsing off ([Markdig](https://github.com/xoofx/markdig)).

Colored text puts a color's name after a colon, and the text in brackets: `:red[urgent]`. Only the [palette's](#six-colors-read-well-in-light-and-dark-themes) names work, and an unknown name shows as plain text. Rendering's own Markdig inline extension reads the form.

The form is the text directive of the [generic directives proposal](https://talk.commonmark.org/t/generic-directives-plugins-syntax/444), which [remark-directive](https://github.com/remarkjs/remark-directive) implements. It adds 6 characters for red, and its one end marker, `]`, can't name the wrong color. Nothing in it looks like HTML, so the HTML rule needs no exception. Task files are JSON, so no other Markdown viewer reads them, and rules that differ from remark-directive's break nothing.

The user decided these rules:

- Brackets inside must balance, as in a link's text ([CommonMark](https://spec.commonmark.org/0.31.2/#link-text)), so `:red[see [docs](https://x)]` colors the whole phrase, link included.
- A colon directly after a letter or digit doesn't start a color, so `ratio:red[1]` stays text. After a space, at the start of a line, or after punctuation such as `(`, the colon starts a color: `(:red[urgent])` colors "urgent". The proposal and remark-directive's [syntax](https://github.com/micromark/micromark-extension-directive#syntax) set no rule for the character before the colon. remark-directive's [parser](https://github.com/micromark/micromark-extension-directive/blob/main/dev/lib/directive-text.js) starts a directive after any character except another colon, so it reads `ratio:red[1]` as one.
- Only the bare form works: attributes in braces after the `]`, which the proposal allows, show as plain text. `:red[x]{.big}` shows a red "x", then `{.big}`.

Proposed, each a decision to confirm:

- Names are lowercase only: `:Red[x]` shows as text.
- A color opens and closes within one paragraph, and may hold other inline formatting, such as bold or a link.
- A color that isn't closed, such as `:red[urgent` with no `]`, shows as text.
- Inside inline code or a code block, the form is code, not color.

### Six colors read well in light and dark themes

Proposed: the palette below. Each color has a value for the light theme and one for the dark theme. Every value has a contrast of at least 4.5:1, the minimum for normal text in the Web Content Accessibility Guidelines ([WCAG 2.2](https://www.w3.org/TR/WCAG22/#contrast-minimum)), against the light theme's backgrounds `#ffffff` and `#f3f3f3` and the dark theme's `#1f1f1f` and `#2b2b2b`. This is a decision to confirm.

| Name | Light theme | Dark theme |
| --- | --- | --- |
| red | `#d1242f` | `#ff7b72` |
| orange | `#bc4c00` | `#f0883e` |
| green | `#1a7f37` | `#3fb950` |
| blue | `#0969da` | `#4493f8` |
| purple | `#8250df` | `#ab7df8` |
| gray | `#59636e` | `#9198a1` |

Rendering accepts only these names; there are no hex colors in task text. The editor, titles, and descriptions all take a color's value from this table, for the current theme.

Tags don't use these six. A tag's badge takes one of [30 colors of its own](#thirty-tag-colors-stay-distinct-in-both-themes).

### Headings, links, code, and italic text have one look in each theme

Rendering owns one text look for each theme: the colors of every heading level, links, code, and italic text, the faded background behind code, and how much larger each heading level shows. The description page, the desktop's [formatted titles](desktop/formatted-view.md#it-shows-what-rendering-decides-never-markdown), its [editor](desktop/task-editor.md#the-editor-styles-markdown-in-place-with-no-preview), and [Configure's section and card titles](desktop/configure.md#the-window-shows-its-settings-in-labeled-sections) all take it from here, so a link or a heading looks the same wherever the app shows one. The user decided that the look applies everywhere Markdown shows, and that its colors are fixed values for each theme that pass the contrast rule, not the Windows accent color. Code blocks look like inline code. On the description page a code block also gets 8 pixels of padding and scrolls sideways when a line is too long, so it doesn't widen the page.

The user decided the heading hues: heading 1 orange, heading 2 yellow, heading 3 dark yellow, and headings 4 to 6 blue. The dark values match the user's example picture, sampled as `#ffa500`, `#ffff00`, `#8b8b00`, and `#79c0ff` on `#121314`. The user decided that the dark heading 3 is lightened to `#9a9a00`, so it passes the contrast rule on `#2b2b2b`, where `#8b8b00` measures 3.90:1, and every color keeps one rule. Yellow can't reach 4.5:1 on white and stay bright, so the light theme keeps the hue order with darker shades: a golden yellow for heading 2, and an olive, darker than it, for heading 3. The other values are unchanged. Proposed: the light values, and the values of the other parts, below. This is a decision to confirm.

| Part | Light theme | Dark theme |
| --- | --- | --- |
| Heading 1 | `#bc4c00` | `#ffa500` |
| Heading 2 | `#8a6a00` | `#ffff00` |
| Heading 3 | `#5c5c00` | `#9a9a00` |
| Headings 4 to 6 | `#0550ae` | `#79c0ff` |
| Link | `#0969da` | `#4493f8` |
| Code | `#9a3c00` | `#f0a868` |
| Italic text | `#4f5b93` | `#b4bdf5` |
| Code background | `#808080` at 15% opacity | `#808080` at 25% opacity |

Every color meets the [palette's](#six-colors-read-well-in-light-and-dark-themes) contrast rule, at least 4.5:1 against both of its theme's backgrounds; the lowest is the light heading 1, at 4.54:1. Code text also keeps 4.5:1 on its faded background over both of its theme's backgrounds; the lowest is 5.19:1. Rendering's unit tests check both, as they check the tag colors, so a retuned value can't break them. The heading colors of one theme are also at least 10 apart in CIEDE2000, as [tag colors](#thirty-tag-colors-stay-distinct-in-both-themes) are measured, so the levels stay apart; the closest pair is the light headings 2 and 3, at 13.1.

Proposed, each a decision to confirm:

- **Every heading shows bold**, at 1.6, 1.4, 1.25, 1.15, 1.1, and 1.05 times the text size for levels 1 to 6, the sizes the editor uses today.
- **The link color keeps the palette's blue values**, so links look as they do now, but it's part of the look, not a palette name. `:blue[text]` still takes the palette's blue.
- **Code shows in Consolas, in the code color, on the faded background.** The background is a gray with an opacity rather than a solid color, so it reads on both of a theme's backgrounds.
- **Italic text shows in the italic color.** Bold text keeps the text color.
- **A stretch with more than one style takes one color**, the first that applies: its own palette color, then the link color, the code color, the italic color, then its heading's color. So `:red[_x_]` shows red, and a link inside a heading shows in the link color.

### Thirty tag colors stay distinct in both themes

The user decided that a tag's color is one of 30: ten hues, three shades each, from [Tailwind CSS v3's colors](https://github.com/tailwindlabs/tailwindcss/blob/v3.4.17/src/public/colors.js), a published set whose shades of one hue keep it. Each color has a value for each theme, with the same hue and a darker shade in the dark theme. Rendering keeps them beside the palette, so both apps show the same colors. Each color's name is its hue and shade, such as `red-1`, with 1 the palest. The table lists each hue's three values in each theme, palest first:

| Hue | Light theme | Dark theme |
| --- | --- | --- |
| red | `#fca5a5` `#ef4444` `#b91c1c` | `#f87171` `#dc2626` `#7f1d1d` |
| orange | `#fdba74` `#f97316` `#c2410c` | `#fb923c` `#ea580c` `#9a3412` |
| yellow | `#fde047` `#eab308` `#a16207` | `#facc15` `#ca8a04` `#854d0e` |
| lime | `#bef264` `#84cc16` `#4d7c0f` | `#a3e635` `#65a30d` `#3f6212` |
| green | `#86efac` `#22c55e` `#15803d` | `#4ade80` `#16a34a` `#166534` |
| teal | `#5eead4` `#14b8a6` `#0f766e` | `#2dd4bf` `#0d9488` `#115e59` |
| blue | `#93c5fd` `#3b82f6` `#1d4ed8` | `#60a5fa` `#2563eb` `#1e40af` |
| purple | `#c4b5fd` `#8b5cf6` `#6d28d9` | `#a78bfa` `#7c3aed` `#5b21b6` |
| pink | `#f9a8d4` `#ec4899` `#be185d` | `#f472b6` `#db2777` `#9d174d` |
| gray | `#d6d3d1` `#78716c` `#44403c` | `#a8a29e` `#78716c` `#44403c` |

The light theme uses Tailwind's shades 300, 500, and 700, and the dark theme 400, 600, and 800, except red's darkest, 900, and gray's 400, 500, and 700. Purple is Tailwind's violet, and gray its stone.

Three rules keep the colors apart and readable. They're measured with CIEDE2000, the standard formula for how different two colors look, where 1 is barely visible ([color difference](https://en.wikipedia.org/wiki/Color_difference#CIEDE2000)). Rendering's unit tests check all three, so a changed value can't break them:

- **Any two colors of one theme are at least 10 apart.** The closest pairs measure 10.2.
- **Black or white text reads at least 4.5:1 on every color**, the minimum for normal text in [WCAG 2.2](https://www.w3.org/TR/WCAG22/#contrast-minimum). The lowest measures 4.6:1.
- **Every color is at least 7 apart from its theme's window backgrounds**, the [palette's](#six-colors-read-well-in-light-and-dark-themes) `#ffffff` and `#f3f3f3` in the light theme, and `#1f1f1f` and `#2b2b2b` in the dark. So no badge fades into the window. The closest measures 7.1.

[Tasks](tasks.md#each-tags-color-is-kept-in-one-synced-file) stores a tag's color by its name, so names never change once released, while a value can be retuned within the rules.

Proposed, each a decision to confirm:

- **A badge's text is black or white, whichever has the higher contrast on its color**, and Rendering gives it with each value.
- **Names are lowercase only**, like the palette's: `Red-1` names no color.
- **A tag with no color kept gets a default from its text.** Rendering takes the tag in invariant uppercase, hashes its UTF-8 bytes with 32-bit FNV-1a ([FNV](https://datatracker.ietf.org/doc/html/draft-eastlake-fnv)), and picks the color at that hash modulo 30, counting through the table row by row: `red-1`, `red-2`, `red-3`, `orange-1`, and so on.
  - Uppercase made `Home` and `home` look the same before [Tasks normalized every tag](tasks.md#tags-are-lowercase-with-hyphens-and-search-ignores-case) to lowercase. It stays, since dropping it would recolor every tag with no color kept.
  - .NET's own string hash isn't used, since it changes with each run of the app ([String.GetHashCode](https://learn.microsoft.com/en-us/dotnet/api/system.string.gethashcode)).
  - All 30 colors are used, so two of eight tags share a default about 64% of the time, against 98% with one shade per hue.
  - Changing the rule after release would recolor every tag with no color kept, so a unit test pins the default of a few fixed tags.

### The web view runs no scripts and loads images only from the clone's address

These rules apply to the description's web view in both apps:

- **No scripts run.** Each app turns scripts off in its web view. Proposed: the page Rendering makes also carries a content security policy that allows no scripts, so the rule holds even if one side slips. This is a decision to confirm.
- **Images load only from the clone's address.** The app passes in the clone's base address, and the page loads nothing else, from the network or anywhere on the device. Proposed: an image from anywhere else shows as a link with its alt text, and isn't loaded. This is a decision to confirm.
- **The app serves only image files at that address, read-only**: the clone's images, and on the desktop also this device's images that [wait for an open edit to be saved](tasks.md#images-are-at-most-1-mb-and-live-beside-their-task). The user decided this, so an edit's [Preview](desktop/task-list.md#edit-description-has-an-edit-tab-and-a-preview-tab) shows an image pasted or dropped before Save. The Android app shows no preview, so its address serves only the clone.
- **A waiting image loads from the clone's address too.** Its link is the same relative path it keeps once saved, and the desktop's [web view](desktop/formatted-view.md#the-clones-address-also-serves-an-open-edits-waiting-images) looks in the clone first, then among the waiting images. So Rendering, its page, and its content security policy don't change. This is a decision to confirm.
- **The theme comes from the app**, light or dark, so the page's colors match the window around it.

### An image keeps the width the user gives it

The user decided that an image in a description can be resized, and that its size stays with the task, so it shows at that size on every device. Only the width is kept; the height follows the image's own proportions. An image never shows wider than the page, so a wide image shrinks to fit a narrow window. An image with no width shows at its own size, as today. Rendering owns reading and writing the width, so both apps agree. The desktop's [Task View](desktop/task-list.md#an-image-resizes-by-dragging-its-corner-in-the-edits-preview) says how the user resizes one: only while editing the description, in the edit's Preview.

The user decided that the width is written at the end of the image's alt text, after a `|`, as [Obsidian](https://obsidian.md/help/syntax) writes it: `![chart|300](images/<task-id>/<image-id>.png)` shows the image 300 pixels wide, with the alt text `chart`. The task file's fields don't change, and the width syncs and merges as part of the description. An alt text that really ends in `|` and digits reads as a width; that's an accepted cost.

Proposed, each a decision to confirm:

- **A width is a whole number from 1 to 10000**, in the page's pixels, which are the windows' device-independent pixels. Any other text after the last `|`, such as `|wide` or `|0`, stays part of the alt text, and the image shows at its own size.
- **Rendering's own Markdig extension reads it**, as it reads `:red[…]`, and the page shows the alt text without the `|` and the width.
- **The page numbers each image in the order the Markdown holds them**, from 0, including an image from outside the clone that shows as a link, so the number names the same image in the page and in the Markdown. An app reports a resized image by its number, and Rendering's [width function](#public-interface) writes the new width into that image's alt text, replacing a width already there.
- **Each image the page loads shows in a box**, with the CSS rules `display:inline-block; overflow:hidden; max-width:100%`, and the image fills the box's width. Each box carries its image's number in a `data-image` attribute, which the app's web view reads.
- **Only the page with resizable images lets the user drag a box wider or narrower**: its boxes add the CSS rule `resize:horizontal`, which draws the browser's own resize corner. The user decided that images resize only while a description is edited, so the plain page, which the apps show otherwise, has no corner. No script runs on either page, so the content security policy doesn't change.

### Only web links open in the browser

Clicking a link in a description opens it in the default browser. Proposed: only `http` and `https` addresses open there. A link to another task opens that task in the app, and every other link does nothing. This is a decision to confirm.

The reason is safety: an address can name any program registered on the device, and opening a crafted one has let documents run code before ([CVE-2022-30190](https://nvd.nist.gov/vuln/detail/CVE-2022-30190)). Rendering publishes the check, so both apps apply the same rule. The app tells a task link from the others by asking Tasks, which owns the link's format; Rendering knows nothing of task formats.

### Titles and the editor get styled spans, not HTML

Titles in the task list and the Markdown source in the editor show as native text, not in a web view. For them, Rendering returns styled spans: for each stretch of text, where it starts, how long it is, and its styles, such as bold, italic, code, strikethrough, a link's address, a color name, or a heading level. Each app maps the spans to its own text styles. The spans come in order, and never overlap.

Proposed, each a decision to confirm:

- A title is parsed as inline Markdown only, so `# text` in a title shows as typed, not as a heading.
- The spans cover the whole text, with plain text in spans of no style.
- Markdown's own characters, such as `**`, a link's `[` and `](address)`, an autolink's `<` and `>` (its text carries the address), a `\` before an escaped character, and a color's `:red[` and `]`, come in spans marked as markers. So the editor can dim them, and a title or a task reference can leave them out to show the text as a reader sees it.

### A mention shows on a colored background

The user decided that a mention shows on one soft accent background in each theme, and that its text is rebuilt from the contact's current details each time it shows, as [Contacts](contacts.md#a-mentions-text-is-rebuilt-from-the-contact-each-time-it-shows) decides. Rendering finds the mentions and draws them; it never reads contacts. Proposed, each a decision to confirm:

- **A link whose address is a [contact address](modules.md#a-contact-address-has-one-home) is a mention.** Its text's spans carry a Mention style, beside the link's address, and its `[` and `](contact:… "…")` stay markers. Rendering asks the shared platform's `ContactAddress`, so it knows no contact format of its own.
- **A contact link whose text holds an image isn't a mention**, such as a hand-typed `[![face](images/a.png)](contact:… "…")`. Contacts never writes one. It keeps a link's look, and the browser check makes a click do nothing. So swapping mention text never drops an image, and the images keep their positions, which the [edit's Preview](desktop/task-list.md#an-image-resizes-by-dragging-its-corner-in-the-edits-preview) counts to resize the right one.
- **A contact link with no template in its title isn't a mention either**, such as a hand-typed `[Ana](contact:…)`, since a mention [always names its template](contacts.md#a-mention-is-a-link-that-names-its-contact-and-its-template). It keeps a link's look, as above.
- **A reference-style contact link isn't a mention**, such as a hand-typed `[Ana][p]` with `[p]: contact:… "…"` elsewhere. Contacts only writes a mention with its address in place, and a mention's text can only be swapped where its address follows it. It keeps a link's look, as above. This is a decision to confirm.
- **A mention shows in the normal text color, on the text look's mention background**, one soft color per theme, like code's background: the user chose a purple that suits each theme, a pale lavender in light and a deep purple in dark. Its text keeps any inline style it has: bold, italic, strikethrough, code, or a color. The background has rounded corners of `TextLooks.MentionCornerRadius`, 4 pixels, and leaves room around the text: `TextLooks.MentionPaddingSides`, 4 pixels, left and right, `MentionPaddingTop`, 1 pixel, above, and `MentionPaddingBottom`, 3 pixels, below. The room above and below doesn't make the line taller. This holds in the description page, a title, and an editor chip alike; the user asked for this look. The color itself belongs in code, checked by a unit test to keep normal text at least 4.5:1 on it, as [WCAG 2.2](https://www.w3.org/TR/WCAG22/#contrast-minimum) asks.
- **The description page shows a mention as `<span class="mention">` with no link**, styled by the page's one style block, so there's nothing to click and the [browser check](#only-web-links-open-in-the-browser) never sees its address.
- **Each mention's span carries its number in a `data-mention` attribute**, counted from 0 in the order Rendering's mention list finds the mentions in the same Markdown, as images carry `data-image`. So an app can tell which mention the pointer is over, to show the contact's [card](desktop/formatted-view.md#a-mention-shows-a-card-its-user-makes), without a script on the page and without the address in it. Proposed, a decision to confirm; the page's look, its scripts rule, and its content security policy don't change.
- **Rendering lists each mention's whole extent, address, template, and stored text**, so the editors can draw each one as a single chip. The stored text is the link's text as written, its Markdown included, so a chip can format it. Proposed, a decision to confirm.
- **Rendering swaps each mention's text for a live one** that its caller supplies, as the Markdown it is, so a template's styling shows: [Contacts](contacts.md#a-mention-is-a-link-that-names-its-contact-and-its-template) gives text that fits a link's text, with every Markdown character in its values escaped. The user asked that a template's Markdown show as styling wherever a mention shows, so `{fullName} (_{alias}_)` shows the alias in italic. A mention the caller has no text for keeps its stored text. The screens that show tasks pass the result to the page or the title, so the page and the spans need no contacts. Proposed, a decision to confirm.

## Public interface

Both apps use these parts:

- **The description page**: from the Markdown, the theme, and the clone's base address, a full HTML page for the web view, plain or with [resizable images](#an-image-keeps-the-width-the-user-gives-it).
- **Styled spans**: from a title's or the editor's Markdown, the styled stretches of text.
- **The palette**: each color's name and its values for the light and dark themes.
- **The text look**, proposed: for each theme, the [colors of headings, links, code, and italic text](#headings-links-code-and-italic-text-have-one-look-in-each-theme), code's background and font, and each heading level's size; and the color a styled span shows in. Every heading level has a color, so `HeadingColor` never answers null.
- **The tag colors**: the 30 colors, each with its value and text color for each theme; finding one by name; and a tag's default color.
- **The browser check**: whether an address may open in the default browser.

Rendering promises that the page holds no script and no raw HTML from the task, and that the same input always gives the same output. It reads no files and knows no task formats.

Step 3 of the [desktop delivery backlog](desktop/README.md#delivery-backlog) builds the styled spans and the palette, in `Memoria.ToDo.Shared.Rendering`; step 4 adds the description page and the browser check. Every member is a pure function:

```csharp
namespace Memoria.ToDo.Shared.Rendering;

public enum MarkdownField { Title, Description }

[Flags]
public enum SpanStyle { None = 0, Bold = 1, Italic = 2, Strikethrough = 4, Code = 8, Marker = 16 }

/// One stretch of the text. LinkAddress and Color are null, and HeadingLevel is 0, when they don't apply.
public sealed record StyledSpan(int Start, int Length, SpanStyle Styles, string? LinkAddress, string? Color, int HeadingLevel);

public static class StyledSpans
{
    /// The spans of the field's Markdown, in order, never overlapping.
    public static IReadOnlyList<StyledSpan> Of(string markdown, MarkdownField field);
}

/// A palette color's name and its values, such as #d1242f, for each theme.
public sealed record PaletteColor(string Name, string Light, string Dark);

public static class Palette
{
    /// The palette, in the table's order.
    public static IReadOnlyList<PaletteColor> Colors { get; }
}

public static class DescriptionPage
{
    /// A full HTML page of the description for the web view, in the light or dark theme.
    /// It holds no script and no raw HTML from the task, and loads images only from cloneAddress:
    /// a relative image path, such as images/<task-id>/<image-id>.png, loads from it.
    /// Throws ArgumentException when cloneAddress isn't an absolute http or https address ending in "/".
    public static string Of(string markdown, bool dark, Uri cloneAddress);
}

public static class BrowserCheck
{
    /// Whether the address may open in the default browser: true only for an absolute http or https address.
    public static bool MayOpen(string address);
}
```

Also proposed for step 4, each a decision to confirm:

- The page is UTF-8 and carries the content security policy `default-src 'none'; img-src <clone address>; style-src 'unsafe-inline'`.
- An image whose address doesn't start with the clone's address, once a relative path is resolved against it, shows as a link: its alt text, with the address as the link's address.
- A color shows as a `span` whose `style` sets the palette's value for the theme, such as `<span style="color:#d1242f">`.
- Each link keeps its address as written, so a task link stays `task:<id>`.
- The page's text is `#1f1f1f` in the light theme and `#f3f3f3` in the dark theme, and the page sets no background, so the window's own background shows through the web view. Each text color is the other theme's darkest or lightest background in the [palette's](#six-colors-read-well-in-light-and-dark-themes) contrast check, so it reads well on its own theme's backgrounds.
- The page carries one style block with the theme's [text look](#headings-links-code-and-italic-text-have-one-look-in-each-theme), which the content security policy's `style-src 'unsafe-inline'` already allows. It also sets the system's interface font, `system-ui`, at 14 pixels, the size of the windows' text, so a heading on the page looks like a heading in a window. Today the page names no font, so WebView2 shows a serif one.
- `MayOpen` ignores the case of the scheme, so `HTTPS://` opens too.

Row 8 of the [desktop delivery backlog](desktop/README.md#delivery-backlog) adds the [tag colors](#thirty-tag-colors-stay-distinct-in-both-themes), also pure functions:

```csharp
namespace Memoria.ToDo.Shared.Rendering;

/// A tag color's value in one theme, such as #fca5a5, and its text color: #000000 or #ffffff.
public sealed record TagFill(string Background, string Text);

/// A tag color's name, such as red-1, and its fill in each theme.
public sealed record TagColor(string Name, TagFill Light, TagFill Dark);

public static class TagColors
{
    /// The 30 tag colors, in the table's order: each hue's three shades, palest first.
    public static IReadOnlyList<TagColor> All { get; }

    /// The color with exactly this name, or null when no tag color has it.
    public static TagColor? Find(string name);

    /// A tag's default color, from its text with case ignored; the same tag always gets the same color.
    public static TagColor DefaultFor(string tag);
}
```

Proposed: `Find` and `DefaultFor` throw `ArgumentNullException` for a null name or tag, rather than answer no color, since a null is the caller's mistake, not a tag without a color. This is a decision to confirm.

Proposed: the [text look](#headings-links-code-and-italic-text-have-one-look-in-each-theme), also pure. Colors are `#rrggbb`. The background's opacity is a number of its own because CSS reads an eight-digit color as `#rrggbbaa`, but WPF reads it as `#aarrggbb`. This is a decision to confirm.

```csharp
namespace Memoria.ToDo.Shared.Rendering;

/// The look of headings, links, code, and italic text in one theme.
public sealed class TextLook
{
    public string Heading1 { get; }
    public string Heading2 { get; }
    public string Heading3 { get; }

    /// The color of headings 4, 5, and 6.
    public string Heading4 { get; }
    public string Link { get; }
    public string Code { get; }
    public string Italic { get; }

    /// The gray behind code, and how opaque it is: from 0 to 1.
    public string CodeBackground { get; }
    public double CodeBackgroundOpacity { get; }

    /// A heading's color: Heading4 for levels 4 to 6.
    /// Throws ArgumentOutOfRangeException for a level outside 1 to 6.
    public string HeadingColor(int level);

    /// The color the span shows in, in this theme, or null for the text color: its palette color,
    /// else the link, code, italic, or heading color, the first that applies.
    public string? ColorOf(StyledSpan span);
}

public static class TextLooks
{
    public static TextLook Light { get; }
    public static TextLook Dark { get; }

    /// Light or Dark.
    public static TextLook Of(bool dark);

    /// How many times the text size a heading of this level shows: 1.6 for level 1 down to 1.05 for 6,
    /// and 1 for 0, text that's no heading. Throws ArgumentOutOfRangeException outside 0 to 6.
    public static double HeadingScale(int level);

    /// The monospaced font code shows in.
    public static string CodeFont { get; }
    public static double MentionPaddingSides { get; }
    public static double MentionPaddingTop { get; }
    public static double MentionPaddingBottom { get; }
    public static double MentionCornerRadius { get; }
}
```

Row 11 of the [desktop delivery backlog](desktop/README.md#delivery-backlog) adds the [image width](#an-image-keeps-the-width-the-user-gives-it) function and the page with resizable images, also pure. `DescriptionPage.Of` keeps its shape and makes the plain page, so the Android app, which resizes no image, keeps calling it. Proposed: their shape. This is a decision to confirm.

```csharp
namespace Memoria.ToDo.Shared.Rendering;

public static class ImageWidths
{
    /// The Markdown with its image number `image`, counted from 0 in the order the Markdown holds images,
    /// as the description page numbers them, set to show width pixels wide; a width already there is replaced.
    /// Throws ArgumentOutOfRangeException when the Markdown has no such image, or width is outside 1 to 10000.
    public static string With(string markdown, int image, int width);
}

public static class DescriptionPage
{
    /// The same page as Of, except that each loaded image's box has a resize corner, so the user can drag it
    /// wider or narrower. Throws as Of does.
    public static string WithResizableImages(string markdown, bool dark, Uri cloneAddress);
}
```

Proposed, with the [mention look](#a-mention-shows-on-a-colored-background), a decision to confirm. The span style gains a flag, the text look a color, and a new part finds mentions and swaps their text; nothing else changes:

```csharp
namespace Memoria.ToDo.Shared.Rendering;

[Flags]
public enum SpanStyle { None = 0, Bold = 1, Italic = 2, Strikethrough = 4, Code = 8, Marker = 16, Mention = 32 }

public sealed class TextLook
{
    // The members above stay as they are.

    /// The opaque color behind a mention's text, #rrggbb.
    public string MentionBackground { get; }
}

/// One mention: its whole extent in the Markdown, from "[" to ")", and what the link holds. StoredText is the
/// link's text as written, Markdown and backslashes included.
public sealed record Mention(int Start, int Length, string Address, string Template, string StoredText);

public static class Mentions
{
    /// Every mention, in order. A title is read as inline Markdown only, as its spans are.
    public static IReadOnlyList<Mention> In(string markdown, bool title);

    /// The Markdown with each mention's text replaced by textOf's answer, which must fit a link's text, put in
    /// as it is; a null answer keeps the stored text. Everything else is unchanged.
    public static string WithTexts(string markdown, bool title, Func<Mention, string?> textOf);
}
```

## Failure modes

- **There's no invalid Markdown**: any text renders, and what Markdown can't read shows as text.
- **An image is missing from the clone**, and isn't waiting for an open edit either: the page shows its alt text in its place.
- **A link fails the browser check**: clicking it does nothing.

## Ruled out

The options below were considered and rejected, grouped by decision.

- How text gets colored:
  - **`<red>text</red>`**: 11 characters for red, against 6 for `:red[text]`, and the end tag repeats the name.
  - **`<font color="...">` as the one allowed HTML tag**: 19 more characters for each colored phrase than `:red[…]`, and it needs an exception to the HTML rule.
  - **`<color=red>text</color>`**: 13 more characters for each colored phrase than `:red[…]`, and hex colors aren't needed.
  - **`[text]{red}`**: about as short, 7 characters for red against 6, but the color comes after the text, so it's harder to scan.
  - **One-letter codes, such as `:r[text]`**: green and gray both start with "g".
  - **No colors at all**: the user wants to color text quickly.
- Which HTML renders:
  - **Basic HTML through a sanitizer**: a new dependency to maintain.
  - **Any HTML**: pasted content would run with the app's rights.
- Which renderer: a JavaScript renderer in the web view is ruled out in [How the apps fit together](overview.md#ruled-out).
- Tag colors:
  - **The six text colors' hues, five shades each**: five shades of one hue come close, with the closest pairs only 4.1 to 6.8 apart, so the user would confuse them.
  - **One set of 30 for both themes**: one value per color, and it passes every check in both themes, but the user asked for a set per theme.
- A tag's default color:
  - **One shade per hue, from 10 colors**: two of eight tags would share a default far more often.
  - **A color picked at random and kept in `tags.json` when a tag is first used**: every new tag would write the file, and tags from before the file would still need a default.
- The text look:
  - **Heading 1 follows the Windows accent color**: Rendering is shared with Android and can't read Windows, and no test could promise its contrast.
  - **Code blocks keep the text color**: the page and the editor would need a second code rule.
  - **Only the link color in titles and the editor**: `code` and _italic_ would look different in the editor than in the description.
  - **The dark heading 3 in the picture's exact `#8b8b00`**: it measures 3.90:1 on `#2b2b2b`, so headings would need their own, looser rule, 3:1, which WCAG allows only for large text, and heading 3, bold at 1.25 times the text size, is below WCAG's large-text size.
- Where an image's width is written:
  - **Markdig's generic attributes, `![chart](images/…png){width=300}`**: the extension allows any attribute, such as a class or an id, on any element, so Rendering would have to strip all but this one, and the form shows as text in other viewers.
  - **A new field in the task file**, a width for each image: the task file's format, its merge rules, and the Android app would all change, and a width would outlive the image's text if the user deleted it.
- How an edit's Preview loads images that wait for Save:
  - **A second address for waiting images, in the page and its content security policy**: Rendering reads no files, so it can't tell a waiting image from a saved one. The app would have to pass the list of waiting images, through Tasks, the Task list, and a new `DescriptionPage` argument, and the link would change address when the edit is saved.
- How a page gets its resize corners:
  - **Every page, always**: the user chose to resize images only while editing.
  - **A flag on `Of`**: every caller, the Android app's included, would pass a value it doesn't care about, and the public API files would show a removed and an added `Of`.
- How a mention shows:
  - **No background, only the link's look**: the user chose one soft accent background per theme.
  - **Rendering reads contacts itself**: Rendering would depend on Contacts, and the Formatted view would need them too. The screens swap the text first instead.
  - **The live text passed into the page and the spans as a new argument**: every caller of `Of` and the spans would change, for what one swap before them does.
- A contact link whose text holds an image:
  - **A mention like any other**: swapping its text drops the image, so the Preview's image positions no longer match the draft's, and a resize there can change the wrong image.
  - **A mention, but the swap keeps its text**: the mention would show text that isn't the contact's current details.
- A contact link with no template:
  - **A mention with an empty template**: Contacts would be asked for text from an empty template, which no stored mention has.
- How a mention's live text goes into the Markdown:
  - **Escaping every Markdown character, so it shows as written**: the first design; the user asked that a template's Markdown show as styling.

## Open questions

None.
