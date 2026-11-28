using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ICSharpCode.AvalonEdit.Document;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>
/// A plain WPF text box as a text field the pickers can use. Its <see cref="Document"/> mirrors the box's text, so places in
/// the text follow edits, and an edit made to the document is made to the box too.
/// </summary>
internal sealed class TextBoxField : ITextField, IPopupPlace
{
    private readonly TextBox _box;
    private bool _syncing;

    public TextBoxField(TextBox box)
    {
        _box = box;
        Document = new TextDocument(box.Text);
        box.TextChanged += OnBoxTextChanged;
        box.SelectionChanged += (_, _) =>
        {
            if (!_syncing && Document.Text == box.Text)
            {
                Changed?.Invoke();
            }
        };
        box.LostKeyboardFocus += (_, _) => FocusLost?.Invoke();
        box.AddHandler(TextCompositionManager.TextInputEvent, new TextCompositionEventHandler((_, e) => TextEntered?.Invoke(e.Text)), handledEventsToo: true);
        box.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler((_, _) => Scrolled?.Invoke()));
        Document.Changed += OnDocumentChanged;
    }

    public TextDocument Document { get; }

    public int SelectionStart => _box.SelectionStart;

    public int SelectionLength => _box.SelectionLength;

    public int CaretOffset => _box.CaretIndex;

    public UIElement Target => _box;

    public event Action? Changed;

    public event Action<string>? TextEntered;

    public event Action? FocusLost;

    public event Action? Scrolled;

    public void Select(int start, int length) => _box.Select(start, length);

    public void FocusField() => KeyboardFocus.MoveTo(_box);

    public Rect? AnchorOf(int offset)
    {
        var box = Rect.Union(_box.GetRectFromCharacterIndex(offset), _box.GetRectFromCharacterIndex(offset, trailingEdge: true));
        return PopupAnchor.At(box, new Rect(0, 0, _box.ActualWidth, _box.ActualHeight));
    }

    // The user's edit, which the document takes as it was made, so its anchors follow it.
    private void OnBoxTextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_syncing && Document.Text != _box.Text)
        {
            var (start, oldLength, inserted) = e.Changes.Count == 1
                ? Single(e.Changes.First())
                : TextEditorCommands.ChangedPart(Document.Text, _box.Text);
            _syncing = true;
            try
            {
                Document.Replace(start, oldLength, inserted);
            }
            finally
            {
                _syncing = false;
            }
        }

        Changed?.Invoke();
    }

    private (int Start, int OldLength, string New) Single(TextChange change) =>
        (change.Offset, change.RemovedLength, _box.Text.Substring(change.Offset, change.AddedLength));

    // An edit made to the document, as the pickers make one, which the box takes too.
    private void OnDocumentChanged(object? sender, DocumentChangeEventArgs e)
    {
        if (_syncing)
        {
            return;
        }

        _syncing = true;
        try
        {
            _box.Select(e.Offset, e.RemovalLength);
            _box.SelectedText = e.InsertedText.Text;
        }
        finally
        {
            _syncing = false;
        }
    }
}
