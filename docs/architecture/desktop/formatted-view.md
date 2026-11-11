# Formatted view: how the desktop app shows a task as a reader sees it

Status: Decided

The Formatted view is the desktop module that shows a task's title and description formatted, with no Markdown characters, and opens their web links in the default browser. View Tasks uses it now, and Add's preview will use it later.

## Behavior

### It shows what Rendering decides, never Markdown

The user decided that View Tasks shows titles and descriptions formatted, as a reader sees them, and that this part is common, so Add can later show a preview with it. [Rendering](../rendering.md) stays the one home of the formatting rules; the Formatted view only shows its output in WPF:

- **A title shows as native WPF text**, from Rendering's [styled spans](../rendering.md#titles-and-the-editor-get-styled-spans-not-html) without the spans marked as markers. Bold, italic, code, strikethrough, links, and colors show, with each color's value from the [palette](../rendering.md#six-colors-read-well-in-light-and-dark-themes) for the current theme. A title needs no web view.
- **A description shows in a web view**, as Rendering's [description page](../rendering.md#public-interface). The web view runs no scripts and loads only images from the clone, as [Rendering](../rendering.md#the-web-view-runs-no-scripts-and-loads-only-images-from-the-clone) requires.
- **Both follow the theme**: when Windows switches between light and dark, a shown title and description switch too.

The [Task editor](task-editor.md#the-editor-styles-markdown-in-place-with-no-preview) doesn't use the Formatted view. It shows Markdown source, with its markers dimmed, which changes for different reasons than what a reader sees. Add's preview isn't built yet: it will reopen the editor's no-preview rule, and will then add a dependency of the Task editor on the Formatted view.

Proposed, each a decision to confirm:

- Code in a title shows in a monospaced font.
- A shown title and description stop following the theme while they're off screen, so a closed View Tasks holds no subscription.

### A title's name is the text it shows

The user decided that a title's name, which screen readers read and which elements around it can take, is the text it shows, without markers. The formatted title sets WPF's standard name, `AutomationProperties.Name`, in the same pass that shows its Markdown, as soon as the Markdown is set. So the name is right even before WPF lays the title out; a name taken from WPF's own text of the title isn't.

The title owns its name: a name set by its user is replaced the next time the title shows its Markdown.

### Web links open in the default browser, and other links are reported

Clicking a link in a title or a description applies Rendering's [browser check](../rendering.md#only-web-links-open-in-the-browser): an address that passes opens in the default browser, as the user confirmed. Any other address, such as a link to another task, opens nothing; the Formatted view reports it, so its user decides. The Task list expands a linked task, and ignores the rest.

The Formatted view knows no task formats: it doesn't tell a task link from other links; its user asks [Tasks](../tasks.md#a-time-ordered-id-names-each-task-its-file-and-links-to-it).

Proposed: links in titles follow the same rule as links in descriptions, and clicking one doesn't also select the row around it. This is a decision to confirm.

### One web view serves a window, and releases its browser

A web view costs memory and start-up time, so each window that shows descriptions uses one, and releases it, with its browser, when the window closes. The Formatted view's description takes a web view when it's made and releases it when it's released. The [Task list](task-list.md#one-list-in-rank-order-with-one-task-open-at-a-time) makes one when View Tasks opens.

A description shown before the browser has started waits, and shows once it has.

### The app supplies the web view and the browser

The Formatted view doesn't create its web view or open the browser itself. It publishes two small interfaces, the description web view and the default browser, and the desktop app implements both beside its composition root, as [Modules](../modules.md#the-platform-serves-every-module-and-knows-none) decides for an app's implementations of a module's interface. [UX tests](../technology-stack.md#wpf-screens-get-ux-tests-on-the-pc-and-android-screens-on-an-emulator) pass [fakes](../technology-stack.md#each-port-has-one-hand-written-fake-shared-by-every-test) instead, since WebView2's browser runs outside the test process and a test can't open a real browser. These are ports, which the [patterns](../overview.md#each-pattern-serves-one-requirement) allow where a test needs one.

The web view also gives the clone's address: Rendering's page needs the address the clone's images load from, and only the web view knows it, since it serves the clone's files.

The desktop app's web view uses `WebView2CompositionControl`, a drop-in WebView2 control, stable since January 2025 ([SDK 1.0.2957.106](https://learn.microsoft.com/microsoft-edge/webview2/release-notes/sdk/1-0-2957-106)). The standard control is a separate window that always draws on top of WPF content, the airspace problem; this one scrolls and clips with the list. It copies the browser's output, which can lower its frame rate ([control spec](https://github.com/MicrosoftEdge/WebView2Feedback/blob/main/specs/WPF_WebView2CompositionControl.md)); a description doesn't animate, so that's acceptable. It needs [Microsoft.Web.WebView2](https://www.nuget.org/packages/Microsoft.Web.WebView2) 1.0.4314.35, the current stable release, a new dependency of the desktop app's composition root project only.

The control is built for a Windows 10 target and needs the Windows API projection, the assembly through which .NET code calls Windows Runtime APIs. A plain Windows target doesn't include it, so without it the control fails when it starts ([WebView2 issue 5436](https://github.com/MicrosoftEdge/WebView2Feedback/issues/5436)). The user decided that the composition root project and its test project target the Windows version the control is built for, and no other project does. That target has one home, `MemoriaAppTargetFramework` in the root [`Directory.Build.props`](../../../Directory.Build.props), built from the default .NET target; the .NET SDK then supplies the projection. The version sets which Windows APIs the app can call. It doesn't promise which Windows versions the app runs on, and the projects keep .NET's oldest-supported-Windows setting at its default, which only drives build warnings.

The desktop app's web view:

- serves the clone's folder at the clone's address, through WebView2's [virtual host mapping](https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.core.corewebview2.setvirtualhostnametofoldermapping);
- turns scripts off;
- sizes its element to the shown page's full height at the width it's given, and again when the width or the page changes, so a description shows at [full height](task-list.md#one-list-in-rank-order-with-one-task-open-at-a-time) with no WebView2-specific part in the Formatted view or the Task list. It measures the page's `html` box through the DevTools protocol's [DOM.getBoxModel](https://chromedevtools.github.io/devtools-protocol/tot/DOM/#method-getBoxModel), so no script runs in the page. That box isn't raised to the web view's height, as the browser's layout metrics are, so a shorter page shrinks the element again. Only the shown page counts: once a newer page replaces a page, whether that page loads, or can be measured, is ignored;
- cancels every navigation the user starts, including a link that would open a new window, and reports its address instead.

Proposed, each a decision to confirm:

- The clone's address is `https://clone.memoria-todo.example/`. The `.example` name can never be a real site ([RFC 2606](https://www.rfc-editor.org/rfc/rfc2606.html)).
- WebView2 keeps its browser data in `%LocalAppData%\Memoria-ToDo\WebView2\`, outside the folder the installer replaces on update.
- A web view serves the clone folder set when it was made. After Configure picks another folder, images load from it once View Tasks opens again.
- When WebView2 refuses the shown page, such as a page over the 2 MB that [NavigateToString](https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.core.corewebview2.navigatetostring) takes, or the shown page doesn't finish loading or can't be measured, the web view fails, as when its browser can't start: every description shows as Markdown source until View Tasks opens again. For a page that didn't load, the reason is "The page did not load: " followed by WebView2's error status. The web view doesn't check a page's size first, since WebView2 sets the limit. Showing only that one description as source would need the Formatted view to recover page by page, for a failure that's rare.

## Public interface

The Task list uses these parts, and the composition root implements the two ports:

- **The formatted title**: shows a title's Markdown as WPF text, [named by that text](#a-titles-name-is-the-text-it-shows), opens web links, and reports other links.
- **The formatted description**: shows a description's Markdown in its web view, opens web links, and reports other links. If the web view fails, it shows the Markdown source with a message instead, from then on.
- **The description web view**, a port: the WPF element, the clone's address, showing a page, reporting a clicked link's address and a failure that stops it showing pages, and releasing its browser.
- **The default browser**, a port: opens an address.

Step 4 of the [desktop delivery backlog](README.md#delivery-backlog) builds it, in `Memoria.ToDo.Desktop.FormattedView`:

```csharp
namespace Memoria.ToDo.Desktop.FormattedView;

/// A task's title as a reader sees it: native text, with no Markdown characters.
public sealed class FormattedTitle : TextBlock
{
    public FormattedTitle(IReadOnlyList<PaletteColor> palette, ITheme theme, IDefaultBrowser browser);

    /// The title's Markdown. Setting it shows it formatted, and sets the title's
    /// AutomationProperties.Name to the text shown.
    public string Markdown { get; set; }

    /// Raised with the address of a clicked link that fails Rendering's browser check.
    public event EventHandler<string>? OtherLinkClicked;

    /// Raised when the default browser fails to open a web link.
    public event EventHandler<Exception>? OpenFailed;
}

/// A task's description as a reader sees it, in the web view it's given.
public sealed class FormattedDescription : ContentControl, IDisposable
{
    /// Takes ownership of webView: releasing the description releases it.
    public FormattedDescription(IDescriptionWebView webView, ITheme theme, IDefaultBrowser browser);

    /// The description's Markdown. Setting it shows it formatted.
    public string Markdown { get; set; }

    /// Raised with the address of a clicked link that fails Rendering's browser check.
    public event EventHandler<string>? OtherLinkClicked;

    /// Raised when the default browser fails to open a web link.
    public event EventHandler<Exception>? OpenFailed;

    /// Releases the web view and its browser.
    public void Dispose();
}

/// The web view a description shows in. Its members are used, and its events raised, on the UI thread.
public interface IDescriptionWebView : IDisposable
{
    /// The WPF element that shows the page, as tall as the shown page at the element's width.
    FrameworkElement Element { get; }

    /// The address the clone's files load from: absolute, http or https, and ending in "/".
    Uri CloneAddress { get; }

    /// Shows the page in place of the one shown, with scripts off. Before the browser has started,
    /// the last page waits, and shows once it has.
    void Show(string page);

    /// Raised with the address of a link the user clicked. The web view opens nothing itself.
    event EventHandler<string>? LinkClicked;

    /// Raised once, with the reason, when the web view can't show pages any more: its browser can't
    /// start, or it can't load or measure the shown page. It can be raised while Show runs.
    event EventHandler<string>? Failed;
}

/// The system's default browser.
public interface IDefaultBrowser
{
    /// Opens the address. The Formatted view passes only addresses that pass Rendering's browser check.
    /// Throws when the browser can't open it.
    void Open(string address);
}
```

## Failure modes

- **The web view fails**: its browser can't start, for example when the WebView2 Runtime is missing, or, as [proposed](#the-app-supplies-the-web-view-and-the-browser), it can't load or measure the shown page, so the page would show cut, with no height, or not at all. The description shows as Markdown source, with a message saying why, from then on. Titles don't need a web view, so they still show formatted.
- **A link fails the browser check**: it opens nothing, and the Formatted view reports its address.
- **The default browser can't open a web link**: the Formatted view reports the error, and its user shows it.

Proposed: the message reads "Descriptions can't show formatted: " followed by the web view's reason. This is a decision to confirm.

## Ruled out

The options below were considered and rejected, grouped by decision.

- Which module shows formatted Markdown:
  - **The Task editor**: the module that edits Markdown source would also show it formatted and open links, and the Task list would get its whole formatted view through the editor.
  - **The Task list, for itself only**: Add will later show a preview with the same view, so the user wants it common.
  - **The desktop platform**: it [never names a module](../modules.md#the-platform-serves-every-module-and-knows-none), and the view reads Rendering's spans.
  - **Rendering**: Rendering is in the shared library, which has no screens and serves both apps ([How the apps fit together](../overview.md#two-apps-share-one-c-library)), while the view is WPF.
- How a title's shown text reaches screen readers and the list:
  - **A new property for the shown text** on the formatted title: a new public member, with its own change notice, for text its name already carries; and the title's own name would still wait for WPF to lay it out.
  - **Rendering returns the shown text**, and the title and the Task list each ask it: a new member in the library both apps share, for a desktop need; and a row's name and the title's text would come from two calls that must agree.
- Who opens a web link:
  - **The web view itself**: it would both show pages and decide which links open, two jobs.
  - **A browser port in the desktop platform**: no module but the Formatted view opens links.
- How the web view is made:
  - **The Formatted view creates the WebView2 control itself**: a UX test would need a browser outside the test process, so only integration tests could check screens that show descriptions.
  - **The standard WebView2 control**: it draws on top of WPF content, so it can't scroll and clip with the list.
- How the desktop app gets the Windows API projection the web view's control needs:
  - **A projection package referenced by hand, with the plain Windows target**: a new version to pin, for what the .NET SDK supplies from the target.
  - **The target written in both project files**: one version in two places that must change together.
  - **A props file of the app's own, imported by both projects**: a new file and two imports, for one setting the root file can hold.
  - **The same target for every desktop project**: projects that call no Windows Runtime API would change too.
  - **A newer Windows version**: the app could call newer Windows APIs that no design needs.
