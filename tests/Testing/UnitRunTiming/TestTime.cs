namespace Memoria.ToDo.Testing.UnitRunTiming;

/// <summary>One test case's time from a JUnit report.</summary>
/// <param name="ClassName">The test's class, or empty when the report gives none.</param>
/// <param name="Name">The test's name without the class prefix.</param>
/// <param name="Milliseconds">The test's time, rounded to whole milliseconds.</param>
internal readonly record struct TestTime(string ClassName, string Name, long Milliseconds);
