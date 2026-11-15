# Formatted view: how the desktop app shows a task as a reader sees it

Status: Drafting

The Formatted view is the desktop module that shows a task's title and description formatted, with no Markdown characters, and opens their web links in the default browser. Task View uses it now, also for the Preview of a description it edits, [Updates](updates.md#what-is-new-shows-each-newer-releases-notes) uses it for What is new, and Add's preview will use it later.

## Behavior

### It shows what Rendering decides, never Markdown

The user decided that Task View shows titles and descriptions formatted, as a reader sees them, and that this part is common, so Add can later show a preview with it. [Rendering](../rendering.md) stays the one home of the formatting rules; the Formatted view only shows its output in WPF:

- **A title shows as native WPF text**, from Rendering's [styled spans](../rendering.md#titles-and-the-editor-get-styled-spans-not-html) without the spans marked as markers. Bold, italic, code, strikethrough, links, and colors show. Proposed: each stretch's color comes from Rendering's [text look](../rendering.md#headings-links-code-and-italic-text-have-one-look-in-each-theme) for the current theme, which gives a palette color, a link, code, or italic text its color. This is a decision to confirm. A title needs no web view.
- **A description shows in a web view**, as Rendering's [description page](../rendering.md#public-interface). The web view runs no scripts and loads images only from the clone's address, as [Rendering](../rendering.md#the-web-view-runs-no-scripts-and-loads-images-only-from-the-clones-address) requires.
- **A description shown as an edit's preview** shows Rendering's page with [resizable images](#a-resized-image-is-reported-not-saved) instead. Its user turns the preview on and off; the description starts as a plain one, whose images have no resize corner.
- **Both follow the theme**: when Windows switches between light and dark, a shown title and description switch too.

The [Task editor](task-editor.md#the-editor-styles-markdown-in-place-with-no-preview) doesn't use the Formatted view. It shows Markdown source, with its markers dimmed, which changes for different reasons than what a reader sees. Add's preview isn't built yet: it will reopen the editor's no-preview rule, and will then add a dependency of the Task editor on the Formatted view.

Proposed, each a decision to confirm:

- Code in a title shows as the text look's code does: in its font and color, on its faded background.
- A shown title and description stop following the theme while they're off screen, so a closed Task View holds no subscription. They follow it through the platform's [theme follower](README.md#the-platform-runs-the-app-and-the-composition-root-builds-it), which the Task editor and Configure use too.

### A title's name is the text it shows

The user decided that a title's name, which screen readers read and which elements around it can take, is the text it shows, without markers. The formatted title sets WPF's standard name, `AutomationProperties.Name`, in the same pass that shows its Markdown, as soon as the Markdown is set. So the name is right even before WPF lays the title out; a name taken from WPF's own text of the title isn't.

The title owns its name: a name set by its user is replaced the next time the title shows its Markdown.

### Web links open in the default browser, and other links are reported

Clicking a link in a title or a description applies Rendering's [browser check](../rendering.md#only-web-links-open-in-the-browser): an address that passes opens in the default browser, as the user confirmed. Any other address, such as a link to another task, opens nothing; the Formatted view reports it, so its user decides. The Task list selects a linked task, and ignores the rest.

The Formatted view knows no task formats: it doesn't tell a task link from other links; its user asks [Tasks](../tasks.md#a-time-ordered-id-names-each-task-its-file-and-links-to-it).

Proposed: in a preview, clicking a link does nothing: no link opens, and none is reported. A preview shows a draft, and following a task link would leave the edit. This is a decision to confirm.

Proposed: links in titles follow the same rule as links in descriptions, and clicking one doesn't also select the row around it, as [Task View](task-list.md#tasks-show-grouped-by-tag-beside-the-selected-tasks-description) does on a click on the title. This is a decision to confirm.

### One web view serves a window, and releases its browser

A web view costs memory and start-up time, so each window that shows descriptions uses one, and releases it, with its browser, when the window closes. The Formatted view's description takes a web view when it's made and releases it when it's released. The [Task list](task-list.md#tasks-show-grouped-by-tag-beside-the-selected-tasks-description) makes one when Task View opens.

A description shown before the browser has started waits, and shows once it has.

### The app supplies the web view and the browser

The Formatted view doesn't create its web view or open the browser itself. It publishes two small interfaces, the description web view and the default browser, and the desktop app implements both beside its composition root, as [Modules](../modules.md#the-platform-serves-every-module-and-knows-none) decides for an app's implementations of a module's interface. [UX tests](../technology-stack.md#wpf-screens-get-ux-tests-on-the-pc-and-android-screens-on-an-emulator) pass [fakes](../technology-stack.md#each-port-has-one-hand-written-fake-shared-by-every-test) instead, since WebView2's browser runs outside the test process and a test can't open a real browser. These are ports, which the [patterns](../overview.md#each-pattern-serves-one-requirement) allow where a test needs one.

The web view also gives the clone's address: Rendering's page needs the address the clone's images load from, and only the web view knows it, since it serves the images.

The desktop app's web view uses `WebView2CompositionControl`, a drop-in WebView2 control, stable since January 2025 ([SDK 1.0.2957.106](https://learn.microsoft.com/microsoft-edge/webview2/release-notes/sdk/1-0-2957-106)). The standard control is a separate window that always draws on top of WPF content, the airspace problem; this one scrolls and clips with the list. It copies the browser's output, which can lower its frame rate ([control spec](https://github.com/MicrosoftEdge/WebView2Feedback/blob/main/specs/WPF_WebView2CompositionControl.md)); a description doesn't animate, so that's acceptable. It needs [Microsoft.Web.WebView2](https://www.nuget.org/packages/Microsoft.Web.WebView2) 1.0.4314.35, the current stable release, a new dependency of the desktop app's composition root project only.

The control is built for a Windows 10 target and needs the Windows API projection, the assembly through which .NET code calls Windows Runtime APIs. A plain Windows target doesn't include it, so without it the control fails when it starts ([WebView2 issue 5436](https://github.com/MicrosoftEdge/WebView2Feedback/issues/5436)). The user decided that the composition root project and its test project target the Windows version the control is built for, and no other project does. Proposed, a decision to confirm: the composition root's tests now share the [desktop test project](../technology-stack.md#each-part-keeps-its-tests-in-one-test-project) with the desktop modules' tests, so that whole project targets this version. That target has one home, `MemoriaAppTargetFramework` in the root [`Directory.Build.props`](../../../Directory.Build.props), built from the default .NET target; the .NET SDK then supplies the projection. The version sets which Windows APIs the app can call. It doesn't promise which Windows versions the app runs on, and the projects keep .NET's oldest-supported-Windows setting at its default, which only drives build warnings.

The desktop app's web view:

- serves images at the clone's address, from the clone and from this device's waiting edit images, as [below](#the-clones-address-also-serves-an-open-edits-waiting-images);
- turns scripts off;
- sizes its element to the shown page's full height at the width it's given, and again when the width or the page changes, so a description shows at [full height](task-list.md#tasks-show-grouped-by-tag-beside-the-selected-tasks-description) with no WebView2-specific part in the Formatted view or the Task list. It measures the page's `html` box through the DevTools protocol's [DOM.getBoxModel](https://chromedevtools.github.io/devtools-protocol/tot/DOM/#method-getBoxModel), so no script runs in the page. That box isn't raised to the web view's height, as the browser's layout metrics are, so a shorter page shrinks the element again. Only the shown page counts: once a newer page replaces a page, whether that page loads, or can be measured, is ignored;
- cancels every navigation the user starts, including a link that would open a new window, and reports its address instead.

### The clone's address also serves an open edit's waiting images

The user decided that the web view also serves this device's images that wait for an open edit to be saved, read-only, so an edit's [Preview](task-list.md#edit-description-has-an-edit-tab-and-a-preview-tab) shows and resizes an image pasted or dropped before Save. [Tasks](../tasks.md#images-are-at-most-1-mb-and-live-beside-their-task) keeps those images in one folder of this device, laid out as the clone, and publishes where it is. The composition root gives each web view that folder beside the clone's.

A waiting image's link, such as `images/<task-id>/<image-id>.png`, is the one it keeps once saved, so it resolves to the same address before and after Save. Proposed, each a decision to confirm:

- **One address serves both folders.** For each request to the clone's address, the web view looks in the clone first, then in the waiting edit images. Rendering's page and its [content security policy](../rendering.md#the-web-view-runs-no-scripts-and-loads-images-only-from-the-clones-address) don't change, and neither do the Formatted view and its port.
- **The web view answers every request to the address itself**, through WebView2's [WebResourceRequested](https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.core.corewebview2.webresourcerequested) event, in place of the virtual host mapping, which can't fall back to a second folder. WebView2 doesn't raise that event for a mapped host ([custom network requests](https://learn.microsoft.com/en-us/microsoft-edge/webview2/how-to/webresourcerequested)), so the two can't be combined. A request it doesn't serve gets "404 Not Found", never the network.
- **It serves only an image, read-only**: a `GET` of a path that Tasks' [file rules](../../../src/Shared/Tasks/Synchronization/IRepositoryFileRules.cs) classify as an image, `images/<task-id>/<file>`, so the image layout keeps its one home in Tasks. The web view adds its own safety rule: the file name holds only ASCII letters, digits, and hyphens, and ends in `.png` or `.jpg`, the names Tasks gives images. Any other path, such as `tasks/<id>.json`, `..`, a backslash, or an escaped character, gets "404 Not Found". The answer carries the image's content type, `image/png` or `image/jpeg`, and `X-Content-Type-Options: nosniff`. So the address no longer serves the clone's other files, which no page loads.
- **A missing image is not found**, in either folder. Another read failure gets "500 Internal Server Error". Either way the page shows the image's alt text, as for any [missing image](../rendering.md#failure-modes).
- **Images are read whole, through the platform's [file system interface](../modules.md#the-file-system-interface-reads-and-writes-whole-files).** An image is at most 1 MB, so reading it on the UI thread, where WebView2 raises the event, doesn't hold the window up. The composition root gives each web view the file system and Tasks' file rules, so the web view's constructor takes both.

Not confirmed by a run: WebView2 raises WebResourceRequested for the images of a page shown with NavigateToString. The web view's integration tests establish it.

### A resized image is reported, not saved

The user decided that an image is resized only while its description is edited, by dragging its corner in the edit's Preview, as [Task View](task-list.md#an-image-resizes-by-dragging-its-corner-in-the-edits-preview) says, and that the web view reports the new width through an event on its port. Only a description shown as a preview has resize corners, and only a preview reports a resized image. The Formatted view doesn't change the Markdown it shows: it passes the report on to its user, which writes the width into the draft.

- **The report names the image by its number and gives its width**: the number the [description page](../rendering.md#an-image-keeps-the-width-the-user-gives-it) puts on the image's box, and the box's width in whole device-independent pixels, at least 1.
- **The desktop app's web view watches each image box's style** through the DevTools protocol's [DOM.inlineStyleInvalidated](https://chromedevtools.github.io/devtools-protocol/tot/DOM/#event-inlineStyleInvalidated), which a run showed is the event a drag raises at each step in `WebView2CompositionControl` (DOM.attributeModified isn't raised), so no script runs in the page, as it already measures the page through DevTools. It reports once a drag ends, not at each step of it. Only the shown page's boxes count.
- **A drag ends when the mouse button is up after a short pause**, since the browser raises no event when it ends. The user decided that once a box's style has been quiet for the pause, 400 ms, the web view reports the box's width only if the mouse button is up. While the button is still held, it reports nothing and checks again after each further pause, so a user who stops mid-drag without letting go gets one report, after letting go.
- **The web view reads the button from WPF**, on the UI thread where it already runs: `Mouse.LeftButton`, which WPF takes from Windows' state of the primary button, the one that drags, also when the user swaps the buttons. Not confirmed by a run: the web view's integration test establishes that it reads the button as released after a drag ends.
- **A drag on a page that's replaced is dropped.** When a new page shows, or the web view fails or is released, before the drag ends, nothing is reported.
- **A measurement that finishes late is dropped.** The report needs two DevTools round trips after the pause, and they can end after a new drag step, a new page, a failure, or a new press of the button. The web view counts the drag steps and the pages shown, and reports only if, once the round trips are done, the count is as it was at the pause, the page is the same, nothing has failed, and the button is still up. A late measurement that finds the button down again reports nothing and starts the pause again, as at the pause itself.
- **The pause and the button are a small seam, so tests can drive the window above.** Proposed, a decision to confirm: `IDragPause`, internal to the desktop app, with `Start`, `Stop`, a `Tick` event, and `IsButtonDown`, which holds the `Mouse.LeftButton` read of the bullet before. The web view takes it through an internal constructor, with the dispatcher-timer pause as the default of the public one, which the composition root uses. A test drives the pause with a fake and fires the tick itself, since a test that waited for the real pause couldn't hit the time between the tick and the report without a sleep. The width measurement, the DevTools round trips that read the dragged box's image number and width, is a second internal constructor dependency, `IImageMeasure`, which the web view awaits before its stale checks. A test wraps the real measurement and holds its result, so it can make a new drag step between the measurement and the report. These two are the only test seams; no switch or hook is added to the web view.

### The page never holds the keyboard focus

The user decided that every key must always reach the window, so the page never holds the keyboard focus. The desktop app's web view isn't focusable: a click on the page moves the focus to the nearest focusable ancestor, such as the right side's scrolling pane, and Tab skips the page. Without this, the browser keeps the function keys for itself: a run showed it raises no event for them, so the window never saw F5. With it, [Esc, and the keys with Ctrl or Alt held](task-list.md#esc-goes-back-one-step-then-closes-task-view), reach the window as they do from any control. The page keeps its mouse: dragging an image's corner and clicking a link still work, and so does the mouse wheel over it. Selecting text in the page, and so copying it with Ctrl+C, is the cost: the page has no focus to copy from. This is a decision to confirm.

Proposed, each a decision to confirm:

- The clone's address is `https://clone.memoria-todo.example/`. The `.example` name can never be a real site ([RFC 2606](https://www.rfc-editor.org/rfc/rfc2606.html)).
- WebView2 keeps its browser data in `%LocalAppData%\Memoria-ToDo\WebView2\`, outside the folder the installer replaces on update.
- A web view serves the clone folder set when it was made. After Configure picks another folder, images load from it once Task View opens again. The waiting edit images' folder never changes while the app runs.
- When WebView2 refuses the shown page, such as a page over the 2 MB that [NavigateToString](https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.core.corewebview2.navigatetostring) takes, or the shown page doesn't finish loading or can't be measured, the web view fails, as when its browser can't start: every description shows as Markdown source until Task View opens again. For a page that didn't load, the reason is "The page did not load: " followed by WebView2's error status. The web view doesn't check a page's size first, since WebView2 sets the limit. Showing only that one description as source would need the Formatted view to recover page by page, for a failure that's rare.

## Public interface

The Task list uses these parts, and the composition root implements the two ports:

- **The formatted title**: shows a title's Markdown as WPF text, [named by that text](#a-titles-name-is-the-text-it-shows), opens web links, and reports other links.
- **The formatted description**: shows a description's Markdown in its web view, opens web links, and reports other links. As an edit's preview, it shows images with resize corners, reports [resized images](#a-resized-image-is-reported-not-saved), and opens no link. If the web view fails, it shows the Markdown source with a message instead, from then on.
- **The description web view**, a port: the WPF element, the clone's address, showing a page, reporting a clicked link's address, a resized image, and a failure that stops it showing pages, and releasing its browser.
- **The default browser**, a port: opens an address.

Step 4 of the [desktop delivery backlog](README.md#delivery-backlog) builds it, in `Memoria.ToDo.Desktop.FormattedView`, and row 11 adds `ImageResize`, the two `ImageResized` events, and `FormattedDescription.IsPreview`. Proposed: `IsPreview`'s shape. This is a decision to confirm. The user decided that the formatted title no longer takes the palette, since the [text look](../rendering.md#public-interface) gives every color it shows.

```csharp
namespace Memoria.ToDo.Desktop.FormattedView;

/// A task's title as a reader sees it: native text, with no Markdown characters.
public sealed class FormattedTitle : TextBlock
{
    public FormattedTitle(ITheme theme, IDefaultBrowser browser);

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

    /// Whether the description shows as an edit's preview: its images have resize corners, a finished drag raises
    /// ImageResized, and a clicked link opens nothing and raises nothing. False at first. Changing it shows the
    /// Markdown again.
    public bool IsPreview { get; set; }

    /// Raised with the address of a clicked link that fails Rendering's browser check, unless IsPreview.
    public event EventHandler<string>? OtherLinkClicked;

    /// Raised when the default browser fails to open a web link.
    public event EventHandler<Exception>? OpenFailed;

    /// Raised, only while IsPreview, when the user resized one of the description's images; the Markdown doesn't change.
    public event EventHandler<ImageResize>? ImageResized;

    /// Releases the web view and its browser.
    public void Dispose();
}

/// An image the user resized: its number on the description page, counted from 0, and its new width
/// in device-independent pixels, at least 1.
public sealed record ImageResize(int Image, int Width);

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

    /// Raised once the user ends dragging an image box on the shown page to a new width: the box's style has
    /// been quiet for a pause and the mouse button is up. A drag on a page that's replaced raises nothing.
    event EventHandler<ImageResize>? ImageResized;

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
- Whether the formatted title takes the palette:
  - **Keep it, only for `:red[text]` colors**: the title would take colors from two places that could disagree.
- How a resized image reaches the Task list:
  - **The web view's context menu, with set sizes**: no new event on the port, but WebView2's [ContextMenuRequested](https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.core.corewebview2.contextmenurequested) would still need one, and the user can't pick any width.
  - **The user types the width in the Markdown only**: no change to the port, but the user asked to resize images.
- Which descriptions have resize corners:
  - **Every formatted description, with a drag saved at once**: the user chose to resize images only while editing, in the edit's Preview.
- How a preview gets its page:
  - **A second formatted description, with its own web view, for the preview**: a second browser for one window, against the [one web view a window uses](#one-web-view-serves-a-window-and-releases-its-browser).
- When a drag ends:
  - **At the first pause, whatever the button**: a user who stops mid-drag gets two reports, the first while still holding the corner.
  - **When WPF sees the button go up**: the browser may hold the mouse while it drags, so the release can come with no event to WPF; reading the button's state at each pause needs no event.
- How the web view serves an open edit's waiting images:
  - **A second virtual host mapping for the waiting images, at an address of its own**: the page would have to name that address for each waiting image, but Rendering can't tell which images wait. Tasks, the Task list, and Rendering's page would all change, and a link would change address at Save.
  - **The virtual host mapping for the clone, plus a WebResourceRequested handler for the waiting images on the same host**: WebView2 never raises the event for a mapped host.
  - **Serving every file in both folders, as the mapping did**: no page loads anything but images, so serving task files and settings only widens what a page could read.
