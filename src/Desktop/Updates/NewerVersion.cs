namespace Memoria.ToDo.Desktop.Updates;

/// <summary>
/// A newer version, with the notes of each release after the running one, newest first. <paramref name="OlderNotesPage"/> is
/// the page that holds every release's notes when the notes the check could read don't reach the running version, and null
/// when they do.
/// </summary>
public sealed record NewerVersion(string Version, IReadOnlyList<ReleaseNotes> Notes, Uri? OlderNotesPage = null);
