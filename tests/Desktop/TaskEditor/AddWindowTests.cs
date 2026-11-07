using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

[UX]
public class AddWindowTests
{
    [WpfFact]
    public void Create_opens_no_window()
    {
        using var windows = new OpenWindows();

        AddWindow.Create();

        Assert.Empty(windows.Visible());
    }

    [WpfFact]
    public void Show_makes_the_window_visible_and_active()
    {
        using var windows = new OpenWindows();
        var add = AddWindow.Create();

        add.Show();

        var window = Assert.Single(windows.Visible());
        Assert.True(window.IsActive);
    }

    [WpfFact]
    public void Esc_hides_the_window()
    {
        using var windows = new OpenWindows();
        var add = AddWindow.Create();
        add.Show();
        var window = Assert.Single(windows.Visible());

        OpenWindows.PressEscape(window);

        Assert.False(window.IsVisible);
        Assert.Empty(windows.Visible());
    }

    [WpfFact]
    public void The_close_button_hides_the_window_instead_of_closing_it()
    {
        using var windows = new OpenWindows();
        var add = AddWindow.Create();
        add.Show();
        var window = Assert.Single(windows.Visible());

        window.Close();

        Assert.False(window.IsVisible);
        Assert.Empty(windows.Visible());
    }

    [WpfFact]
    public void Show_after_Esc_reopens_the_same_window()
    {
        using var windows = new OpenWindows();
        var add = AddWindow.Create();
        add.Show();
        var first = Assert.Single(windows.Visible());
        OpenWindows.PressEscape(first);

        add.Show();

        Assert.Same(first, Assert.Single(windows.Visible()));
        Assert.True(first.IsActive);
    }

    [WpfFact]
    public void Show_after_the_close_button_reopens_the_same_window()
    {
        using var windows = new OpenWindows();
        var add = AddWindow.Create();
        add.Show();
        var first = Assert.Single(windows.Visible());
        first.Close();

        add.Show();

        Assert.Same(first, Assert.Single(windows.Visible()));
    }

    [WpfFact]
    public void Show_while_open_keeps_one_window()
    {
        using var windows = new OpenWindows();
        var add = AddWindow.Create();
        add.Show();
        var first = Assert.Single(windows.Visible());

        add.Show();

        Assert.Same(first, Assert.Single(windows.Visible()));
    }

    [WpfFact]
    public void Holds_a_one_line_notice_of_what_is_coming()
    {
        using var windows = new OpenWindows();
        AddWindow.Create().Show();
        var window = Assert.Single(windows.Visible());

        var texts = OpenWindows.TextsIn(window);

        Assert.NotEmpty(texts);
        Assert.All(texts, text => Assert.DoesNotContain('\n', text));
    }

    [WpfFact]
    public void Has_no_input_fields_and_no_Save()
    {
        using var windows = new OpenWindows();
        AddWindow.Create().Show();
        var window = Assert.Single(windows.Visible());

        var parts = OpenWindows.Descendants(window).ToList();

        Assert.DoesNotContain(parts, part => part is TextBoxBase);
        Assert.DoesNotContain(parts, part => part is ButtonBase { Content: string text } && text.Contains("Save", StringComparison.OrdinalIgnoreCase));
    }
}
