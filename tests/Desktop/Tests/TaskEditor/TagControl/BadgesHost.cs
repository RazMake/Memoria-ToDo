using System.Windows;
using Memoria.ToDo.Desktop.Fakes;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests.TagControl;

/// <summary>Hosts a <see cref="TagBadges"/> in a showing window over the fakes, with the tags shown. Shared test setup.</summary>
internal sealed class BadgesHost : IDisposable
{
    private readonly Window window;

    public BadgesHost(params string[] tags)
        : this(false, tags)
    {
    }

    public BadgesHost(bool dark, params string[] tags)
    {
        Services = new EditorServices();
        Services.Theme.IsDark = dark;
        Badges = Services.CreateTagBadges();
        Badges.Tags = tags;
        window = new Window { Content = Badges, Width = 480, Height = 200 };
        window.Show();
    }

    public EditorServices Services { get; }

    public TagBadges Badges { get; }

    /// <summary>Takes the badges out of the window and waits for them to unload.</summary>
    public void Remove() => LoadSync.Remove(window, Badges);

    /// <summary>Puts the badges back in the window and waits for them to load.</summary>
    public void Put() => LoadSync.Put(window, Badges);

    public void Dispose() => window.Close();
}
