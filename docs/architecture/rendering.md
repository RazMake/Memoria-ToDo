# Rendering: how task Markdown shows

Status: Decided

Rendering is the shared module that turns a task's Markdown into what the user sees: an HTML page for a description in the web view, and styled text for titles and the editor. It also owns the color palette and decides which links may open in the browser. Both apps use it, so a task looks the same on the desktop and the phone.

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

### The web view runs no scripts and loads only images from the clone

These rules apply to the description's web view in both apps:

- **No scripts run.** Each app turns scripts off in its web view. Proposed: the page Rendering makes also carries a content security policy that allows no scripts, so the rule holds even if one side slips. This is a decision to confirm.
- **Images load only from the local clone.** The app passes in the clone's base address, and the page loads nothing else, from the network or anywhere on the device. Proposed: an image from anywhere else shows as a link with its alt text, and isn't loaded. This is a decision to confirm.
- **The theme comes from the app**, light or dark, so the page's colors match the window around it.

### Only web links open in the browser

Clicking a link in a description opens it in the default browser. Proposed: only `http` and `https` addresses open there. A link to another task opens that task in the app, and every other link does nothing. This is a decision to confirm.

The reason is safety: an address can name any program registered on the device, and opening a crafted one has let documents run code before ([CVE-2022-30190](https://nvd.nist.gov/vuln/detail/CVE-2022-30190)). Rendering publishes the check, so both apps apply the same rule. The app tells a task link from the others by asking Tasks, which owns the link's format; Rendering knows nothing of task formats.

### Titles and the editor get styled spans, not HTML

Titles in the task list and the Markdown source in the editor show as native text, not in a web view. For them, Rendering returns styled spans: for each stretch of text, where it starts, how long it is, and its styles, such as bold, italic, code, strikethrough, a link's address, a color name, or a heading level. Each app maps the spans to its own text styles. The spans come in order, and never overlap.

Proposed, each a decision to confirm:

- A title is parsed as inline Markdown only, so `# text` in a title shows as typed, not as a heading.
- The spans cover the whole text, with plain text in spans of no style.
- Markdown's own characters, such as `**`, a link's `[` and `](address)`, an autolink's `<` and `>` (its text carries the address), a `\` before an escaped character, and a color's `:red[` and `]`, come in spans marked as markers. So the editor can dim them, and a title or a task reference can leave them out to show the text as a reader sees it.

## Public interface

Both apps use these parts:

- **The description page**: from the Markdown, the theme, and the clone's base address, a full HTML page for the web view.
- **Styled spans**: from a title's or the editor's Markdown, the styled stretches of text.
- **The palette**: each color's name and its values for the light and dark themes.
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
- `MayOpen` ignores the case of the scheme, so `HTTPS://` opens too.

## Failure modes

- **There's no invalid Markdown**: any text renders, and what Markdown can't read shows as text.
- **An image is missing from the clone**: the page shows its alt text in its place.
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
