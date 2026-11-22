using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;
using Memoria.ToDo.Desktop.ContactList;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Shared.Contacts;
using Memoria.ToDo.Shared.Rendering;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>
/// A text field that shows its Markdown styled in place. Typed text shows at once; its styling follows on the dispatcher,
/// once for any number of edits queued before it, and again when the theme changes.
/// </summary>
internal sealed class StyledTextEditor : TextEditor, ITextField, IPopupPlace
{
    private const string BorderName = "ContentBorder";

    private static readonly DependencyProperty IsInvalidProperty =
        DependencyProperty.Register("IsInvalid", typeof(bool), typeof(StyledTextEditor));

    private readonly MarkdownField _field;
    private readonly IContacts _contacts;
    private readonly ITheme _theme;
    private readonly MarkdownStyler _styler;
    private bool _stylingQueued;

    public StyledTextEditor(string automationId, MarkdownField field, ITheme theme, IContacts contacts, IPointerPosition pointer, Serilog.ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(contacts);
        ArgumentNullException.ThrowIfNull(pointer);
        ArgumentNullException.ThrowIfNull(logger);
        _contacts = contacts;
        _theme = theme;
        _field = field;
        _styler = new MarkdownStyler(theme);
        Chips = new MentionChipEditing(this, field, contacts, theme);
        AutomationProperties.SetAutomationId(this, automationId);

        // Look like the theme's text box, from the theme's own resources, so the look follows the theme.
        SetResourceReference(BackgroundProperty, "TextControlBackground");
        SetResourceReference(ForegroundProperty, "TextControlForeground");
        SetResourceReference(BorderThicknessProperty, "TextControlBorderThemeThickness");
        SetResourceReference(PaddingProperty, "TextControlThemePadding");
        SetResourceReference(MinHeightProperty, "TextControlThemeMinHeight");
        SetResourceReference(Border.CornerRadiusProperty, "ControlCornerRadius");
        ShowInvalid(false);
        Template = TextBoxFrame();

        // The frame's border shows the focus, so no outline is drawn on the editor or on the text area that takes the focus.
        FocusVisualStyle = null;
        TextArea.FocusVisualStyle = null;

        // The text is Markdown source, so a link is only styled, never made clickable.
        Options.EnableHyperlinks = false;
        Options.EnableEmailHyperlinks = false;
        TextArea.TextView.LineTransformers.Add(_styler);

        TextChanged += (_, _) =>
        {
            QueueStyling();
            Changed?.Invoke();
        };
        TextArea.Caret.PositionChanged += (_, _) => Changed?.Invoke();
        TextArea.TextEntered += (_, e) => TextEntered?.Invoke(e.Text);
        TextArea.LostKeyboardFocus += (_, _) => FocusLost?.Invoke();
        TextArea.TextView.ScrollOffsetChanged += (_, _) => Scrolled?.Invoke();
        _ = new ThemeFollower(this, theme, QueueStyling);
        _ = new HoverCard(this, PartAt, pointer, logger);
    }

    /// <summary>The field's mentions, shown as chips.</summary>
    public MentionChipEditing Chips { get; }

    public event Action? Changed;

    public event Action<string>? TextEntered;

    public event Action? FocusLost;

    public event Action? Scrolled;

    public UIElement Target => TextArea.TextView;

    /// <summary>The box of the character at the offset, in the text view's coordinates, or null when it is out of the view.</summary>
    public Rect? AnchorOf(int offset)
    {
        var view = TextArea.TextView;
        view.EnsureVisualLines();
        var rect = BoxOf(offset, 1);
        return rect is { } box ? PopupAnchor.At(box, new Rect(0, 0, view.ActualWidth, view.ActualHeight)) : null;
    }

    // The part of the text under the point, in the editor's coordinates: the mention chip there, boxed, with its contact's card.
    internal Task<HoverPart?> PartAt(Point inEditor)
    {
        HoverPart? part = null;
        var view = TextArea.TextView;
        var inView = TranslatePoint(inEditor, view);
        if (view.GetPositionFloor(inView + view.ScrollOffset) is { } position
            && Chips.At(Document.GetOffset(position.Location)) is { } mention
            && BoxOf(mention.Start, mention.Length) is { } box
            && ContactCard.For(_contacts, _theme, mention.Address) is { } card)
        {
            part = new HoverPart(view.TransformToAncestor(this).TransformBounds(box), card);
        }

        return Task.FromResult(part);
    }

    private Rect? BoxOf(int offset, int length)
    {
        var rects = BackgroundGeometryBuilder.GetRectsForSegment(TextArea.TextView, new TextSegment { StartOffset = offset, Length = length });
        Rect? box = null;
        foreach (var rect in rects)
        {
            box = box is { } known ? Rect.Union(known, rect) : rect;
        }

        return box;
    }

    public void FocusField() => KeyboardFocus.MoveTo(this);

    /// <summary>Puts the cursor at the end of the text, in view, and gives the field the focus.</summary>
    public void FocusEnd()
    {
        FocusField();
        Select(Text.Length, 0);
        TextArea.Caret.BringCaretToView();
    }

    /// <summary>Paints the border in the theme's error color while <paramref name="invalid"/>, else in the theme's text box border again.</summary>
    public void ShowInvalid(bool invalid)
    {
        SetValue(IsInvalidProperty, invalid);
        SetResourceReference(BorderBrushProperty, invalid ? "SystemFillColorCriticalBrush" : "TextControlElevationBorderBrush");
    }

    // The theme styles only WPF's own text box, so this frame draws what the theme's text box template draws:
    // a border, which the pointer and the focus restyle, around the editor's scroll viewer. The error color stays while invalid.
    private static ControlTemplate TextBoxFrame()
    {
        var scroller = new FrameworkElementFactory(typeof(ScrollViewer), "PART_ScrollViewer");
        scroller.SetValue(FocusableProperty, false);
        scroller.SetValue(ScrollViewer.CanContentScrollProperty, true);
        scroller.SetValue(ContentControl.ContentProperty, new Binding(nameof(TextArea)) { RelativeSource = RelativeSource.TemplatedParent });
        scroller.SetValue(Control.PaddingProperty, new TemplateBindingExtension(PaddingProperty));
        scroller.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, new TemplateBindingExtension(HorizontalScrollBarVisibilityProperty));
        scroller.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, new TemplateBindingExtension(VerticalScrollBarVisibilityProperty));

        var frame = new FrameworkElementFactory(typeof(Border), BorderName);
        frame.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(BackgroundProperty));
        frame.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(BorderBrushProperty));
        frame.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(BorderThicknessProperty));
        frame.SetValue(Border.CornerRadiusProperty, new TemplateBindingExtension(Border.CornerRadiusProperty));
        frame.AppendChild(scroller);

        var template = new ControlTemplate(typeof(TextEditor)) { VisualTree = frame };
        template.Triggers.Add(new Trigger { Property = IsMouseOverProperty, Value = true, Setters = { Themed("TextControlBackgroundPointerOver", BackgroundProperty) } });
        template.Triggers.Add(NotInvalid(IsMouseOverProperty, "TextControlBorderBrushPointerOver"));
        template.Triggers.Add(new Trigger
        {
            Property = IsKeyboardFocusWithinProperty,
            Value = true,
            Setters =
            {
                Themed("TextControlBackgroundFocused", BackgroundProperty),
                Themed("TextControlBorderThemeThicknessFocused", BorderThicknessProperty),
            },
        });
        template.Triggers.Add(NotInvalid(IsKeyboardFocusWithinProperty, "TextControlBorderBrushFocused"));
        return template;
    }

    private static Setter Themed(string resource, DependencyProperty property) =>
        new(property, new DynamicResourceExtension(resource), BorderName);

    private static MultiTrigger NotInvalid(DependencyProperty state, string borderBrush) => new()
    {
        Conditions = { new System.Windows.Condition(state, true), new System.Windows.Condition(IsInvalidProperty, false) },
        Setters = { new Setter(BorderBrushProperty, new DynamicResourceExtension(borderBrush), BorderName) },
    };

    private void QueueStyling()
    {
        if (_stylingQueued)
        {
            return;
        }

        _stylingQueued = true;
        Dispatcher.InvokeAsync(Restyle, DispatcherPriority.Background);
    }

    private void Restyle()
    {
        _stylingQueued = false;
        _styler.Update(StyledSpans.Of(Text, _field));
        TextArea.TextView.Redraw();
    }
}
