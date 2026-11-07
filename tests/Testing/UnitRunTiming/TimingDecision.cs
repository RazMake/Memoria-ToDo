namespace Memoria.ToDo.Testing.UnitRunTiming;

/// <summary>What the workflow does with the timing issue.</summary>
public enum IssueAction { None, Open, Note, Close }

/// <summary>The decision and the issue's Markdown text. <see cref="Text"/> is null only when <see cref="Action"/> is <see cref="IssueAction.None"/>.</summary>
public sealed record TimingDecision(IssueAction Action, string? Text);
