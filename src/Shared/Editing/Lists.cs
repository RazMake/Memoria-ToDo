namespace Memoria.ToDo.Shared.Editing;

/// <summary>The list keys. Each returns null when the cursor is not in a list, so the key keeps its usual meaning.</summary>
public static class Lists
{
    public static TextAndSelection? Enter(TextAndSelection field) => throw new NotImplementedException();

    public static TextAndSelection? Indent(TextAndSelection field) => throw new NotImplementedException();

    public static TextAndSelection? Outdent(TextAndSelection field) => throw new NotImplementedException();
}
