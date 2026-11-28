using System.Windows;

namespace Memoria.ToDo.Desktop.FormattedView;

/// <summary>A mention in a description page: its number, counted from 0 in the page's order, and its box in the web view's coordinates.</summary>
public sealed record MentionAt(int Mention, Rect Box);
