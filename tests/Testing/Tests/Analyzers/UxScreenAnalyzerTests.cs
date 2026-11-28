using Microsoft.CodeAnalysis;
using Xunit;
using Xunit.v3;
using static Memoria.ToDo.Testing.Analyzers.SnippetAnalysis;

namespace Memoria.ToDo.Testing.Analyzers;

/// <summary>Shared snippet parts for the screen tests. Each screen folder's namespace ends with the screen's name.</summary>
internal static class ScreenSnippets
{
    public const string TagControlNamespace = "Memoria.ToDo.Tasks.TaskList.Tests.TagControl";
    public const string TitleNamespace = "Memoria.ToDo.Tasks.TaskList.Tests.Title";

    /// <summary>A file with one [UX] class in <paramref name="ns"/> holding <paramref name="tests"/> facts.</summary>
    public static string File(string ns, string screen, string className, int tests, string baseList = "") =>
        $$"""
        namespace {{ns}};
        [UX("{{screen}}")]
        public class {{className}}{{baseList}}
        {
            {{Facts(tests)}}
        }
        """;

    public static string Facts(int count) =>
        string.Join("\n    ", Enumerable.Range(1, count).Select(i => $"[Fact] public void Path_{i}() {{ }}"));

    public static IEnumerable<Diagnostic> WithId(IEnumerable<Diagnostic> diagnostics, string id) =>
        diagnostics.Where(d => d.Id == id);

    public static string SourceTextAt(Location location) =>
        location.SourceTree!.GetText(TestContext.Current.CancellationToken).ToString(location.SourceSpan);
}

[Unit]
[Collection(SnippetAnalysis.Collection)]
public sealed class A_screen_with_UX_tests_in_its_limit
{
    [Fact]
    public async Task Gets_no_diagnostic_with_exactly_10_UX_tests()
    {
        var diagnostics = await AnalyzeFiles(ScreenSnippets.File(ScreenSnippets.TagControlNamespace, "TagControl", "TagPathTests", 10));

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Gets_no_diagnostic_with_exactly_10_UX_tests_split_across_two_classes_and_files()
    {
        var diagnostics = await AnalyzeFiles(
            ScreenSnippets.File(ScreenSnippets.TagControlNamespace, "TagControl", "TagAddTests", 6),
            ScreenSnippets.File(ScreenSnippets.TagControlNamespace, "TagControl", "TagRemoveTests", 4));

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Does_not_count_the_tests_of_an_abstract_base_class_itself()
    {
        var diagnostics = await AnalyzeFiles(
            ScreenSnippets.File(ScreenSnippets.TagControlNamespace, "TagControl", "TagTestsBase", 6).Replace("public class", "public abstract class"),
            ScreenSnippets.File(ScreenSnippets.TagControlNamespace, "TagControl", "TagAddTests", 0, " : TagTestsBase"),
            ScreenSnippets.File(ScreenSnippets.TagControlNamespace, "TagControl", "TagRemoveTests", 4));

        // 6 inherited once + 4 own = 10; counting the abstract base too would make 16.
        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Counts_two_screens_apart()
    {
        var diagnostics = await AnalyzeFiles(
            ScreenSnippets.File(ScreenSnippets.TagControlNamespace, "TagControl", "TagPathTests", 6),
            ScreenSnippets.File(ScreenSnippets.TitleNamespace, "Title", "TitlePathTests", 6));

        Assert.Empty(diagnostics);
    }
}

[Unit]
[Collection(SnippetAnalysis.Collection)]
public sealed class A_screen_with_more_than_10_UX_tests
{
    [Fact]
    public async Task Gets_error_MTD0005_naming_the_screen_the_count_and_the_limit()
    {
        var diagnostics = await AnalyzeFiles(ScreenSnippets.File(ScreenSnippets.TagControlNamespace, "TagControl", "TagPathTests", 11));

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("MTD0005", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("TagControl", diagnostic.GetMessage());
        Assert.Contains("11", diagnostic.GetMessage());
        Assert.Contains("10", diagnostic.GetMessage());
        Assert.Equal("TagPathTests", ScreenSnippets.SourceTextAt(diagnostic.Location));
    }

    [Fact]
    public async Task Gets_the_error_on_each_UX_class_of_the_screen_when_11_tests_are_split_across_two_classes_and_files()
    {
        var diagnostics = await AnalyzeFiles(
            ScreenSnippets.File(ScreenSnippets.TagControlNamespace, "TagControl", "TagAddTests", 6),
            ScreenSnippets.File(ScreenSnippets.TagControlNamespace, "TagControl", "TagRemoveTests", 5));

        Assert.Equal(2, diagnostics.Count);
        Assert.All(diagnostics, d => Assert.Equal("MTD0005", d.Id));
        Assert.Equal(
            ["TagAddTests", "TagRemoveTests"],
            diagnostics.Select(d => ScreenSnippets.SourceTextAt(d.Location)).Order());
        Assert.NotEqual(diagnostics[0].Location.SourceTree, diagnostics[1].Location.SourceTree);
    }

    [Fact]
    public async Task Counts_the_tests_a_class_inherits()
    {
        var diagnostics = await AnalyzeFiles(
            ScreenSnippets.File(ScreenSnippets.TagControlNamespace, "TagControl", "TagTestsBase", 7).Replace("public class", "public abstract class"),
            ScreenSnippets.File(ScreenSnippets.TagControlNamespace, "TagControl", "TagAddTests", 4, " : TagTestsBase"));

        // 7 inherited + 4 declared = 11, shown on the one non-abstract class.
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("MTD0005", diagnostic.Id);
        Assert.Equal("TagAddTests", ScreenSnippets.SourceTextAt(diagnostic.Location));
    }

    [Fact]
    public async Task Reports_only_the_screen_over_its_limit_when_another_screen_is_within_it()
    {
        var diagnostics = await AnalyzeFiles(
            ScreenSnippets.File(ScreenSnippets.TagControlNamespace, "TagControl", "TagPathTests", 11),
            ScreenSnippets.File(ScreenSnippets.TitleNamespace, "Title", "TitlePathTests", 3));

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("MTD0005", diagnostic.Id);
        Assert.Equal("TagPathTests", ScreenSnippets.SourceTextAt(diagnostic.Location));
    }
}

[Unit]
[Collection(SnippetAnalysis.Collection)]
public sealed class A_UX_class_that_holds_a_theory
{
    [Fact]
    public async Task Gets_error_MTD0006_on_the_inheriting_class_when_the_theory_comes_from_an_unmarked_abstract_base()
    {
        // The theory's own location is in the base, which has no UX kind, so the error lands on the UX class's name.
        var diagnostics = await AnalyzeFiles("""
            namespace Memoria.ToDo.Tasks.TaskList.Tests.TagControl;
            public abstract class TagTestsBase
            {
                [Theory][InlineData(1)] public void Adds_tags(int count) { }
            }
            [UX("TagControl")]
            public class TagPathTests : TagTestsBase
            {
                [Fact] public void Adds_a_tag() { }
            }
            """);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("MTD0006", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("Adds_tags", diagnostic.GetMessage());
        Assert.Equal("TagPathTests", ScreenSnippets.SourceTextAt(diagnostic.Location));
    }

    [Fact]
    public async Task Gets_error_MTD0006_on_the_theory_method()
    {
        var diagnostics = await AnalyzeFiles("""
            namespace Memoria.ToDo.Tasks.TaskList.Tests.TagControl;
            [UX("TagControl")]
            public class TagPathTests
            {
                [Fact] public void Adds_a_tag() { }
                [Theory][InlineData(1)] public void Adds_tags(int count) { }
            }
            """);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("MTD0006", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal("Adds_tags", ScreenSnippets.SourceTextAt(diagnostic.Location));
    }

    [Fact]
    public async Task Gets_no_MTD0006_for_a_theory_in_a_unit_class()
    {
        var diagnostics = await AnalyzeFiles("""
            namespace Memoria.ToDo.Tasks.TaskList.Tests.TagControl;
            [Unit]
            public class TagTests
            {
                [Theory][InlineData(1)] public void Adds_tags(int count) { }
            }
            """);

        Assert.Empty(diagnostics);
    }
}

[Unit]
[Collection(SnippetAnalysis.Collection)]
public sealed class A_UX_class_beside_a_screen_namespace
{
    private const string ModuleNamespace = "Memoria.ToDo.Tasks.TaskList.Tests";

    [Fact]
    public async Task Gets_error_MTD0007_on_its_name()
    {
        var diagnostics = await AnalyzeFiles(
            ScreenSnippets.File(ModuleNamespace, "TaskList", "TaskListTests", 2),
            ScreenSnippets.File(ScreenSnippets.TagControlNamespace, "TagControl", "TagPathTests", 2));

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("MTD0007", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal("TaskListTests", ScreenSnippets.SourceTextAt(diagnostic.Location));
    }

    [Fact]
    public async Task Gets_no_diagnostic_when_the_module_namespace_has_no_UX_classes_in_a_child_namespace()
    {
        var diagnostics = await AnalyzeFiles(ScreenSnippets.File(ModuleNamespace, "TaskList", "TaskListTests", 2));

        Assert.Empty(diagnostics);
    }
}

[Unit]
[Collection(SnippetAnalysis.Collection)]
public sealed class A_UX_class_screen_name
{
    [Fact]
    public async Task Gets_no_diagnostic_when_it_matches_the_last_segment_of_a_screen_folder_namespace()
    {
        var diagnostics = await AnalyzeFiles(ScreenSnippets.File(ScreenSnippets.TagControlNamespace, "TagControl", "TagPathTests", 1));

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Gets_no_diagnostic_when_it_matches_the_module_name_of_a_module_with_one_screen()
    {
        var diagnostics = await AnalyzeFiles(ScreenSnippets.File("Memoria.ToDo.Tasks.TaskList.Tests", "TaskList", "TaskListTests", 1));

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Gets_error_MTD0008_on_the_attribute_naming_both_names_when_it_differs_from_the_screen_folder()
    {
        var diagnostics = await AnalyzeFiles(ScreenSnippets.File(ScreenSnippets.TagControlNamespace, "Tags", "TagPathTests", 1));

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("MTD0008", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("Tags", diagnostic.GetMessage());
        Assert.Contains("TagControl", diagnostic.GetMessage());
        Assert.Contains("UX", ScreenSnippets.SourceTextAt(diagnostic.Location));
        Assert.DoesNotContain("TagPathTests", ScreenSnippets.SourceTextAt(diagnostic.Location));
    }

    [Fact]
    public async Task Gets_error_MTD0008_on_the_class_name_when_the_screen_name_is_inherited_from_a_base_naming_another_screen()
    {
        // The class has no [UX] of its own to point at, so the error lands on its name.
        var diagnostics = await AnalyzeFiles(
            ScreenSnippets.File(ScreenSnippets.TagControlNamespace, "TagControl", "TagTestsBase", 0).Replace("public class", "public abstract class"),
            $$"""
            namespace {{ScreenSnippets.TitleNamespace}};
            public class TitlePathTests : Memoria.ToDo.Tasks.TaskList.Tests.TagControl.TagTestsBase
            {
                [Fact] public void Edits_the_title() { }
            }
            """);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("MTD0008", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("TagControl", diagnostic.GetMessage());
        Assert.Contains("Title", diagnostic.GetMessage());
        Assert.Equal("TitlePathTests", ScreenSnippets.SourceTextAt(diagnostic.Location));
    }

    [Fact]
    public async Task Gets_error_MTD0008_on_the_attribute_when_the_screen_name_is_null()
    {
        var diagnostics = await AnalyzeFiles("""
            namespace Memoria.ToDo.Tasks.TaskList.Tests.TagControl;
            [UX(null!)]
            public class TagPathTests
            {
                [Fact] public void Adds_a_tag() { }
            }
            """);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("MTD0008", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("TagControl", diagnostic.GetMessage());
        Assert.Contains("UX", ScreenSnippets.SourceTextAt(diagnostic.Location));
        Assert.DoesNotContain("TagPathTests", ScreenSnippets.SourceTextAt(diagnostic.Location));
    }

    [Fact]
    public async Task Gets_error_MTD0008_when_it_differs_from_the_module_name()
    {
        var diagnostics = await AnalyzeFiles(ScreenSnippets.File("Memoria.ToDo.Tasks.TaskList.Tests", "Tasks", "TaskListTests", 1));

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("MTD0008", diagnostic.Id);
        Assert.Contains("Tasks", diagnostic.GetMessage());
        Assert.Contains("TaskList", diagnostic.GetMessage());
    }
}

[Unit]
public sealed class The_UX_attribute
{
    [Fact]
    public void Gives_each_test_the_traits_Category_UX_and_Screen_with_the_screen_name()
    {
        // Built by reflection so this test compiles before and after the screen argument exists.
        var attribute = (ITraitAttribute)Activator.CreateInstance(typeof(UXAttribute), "TagControl")!;

        var traits = attribute.GetTraits();

        Assert.Equal(2, traits.Count);
        Assert.Contains(new KeyValuePair<string, string>("Category", "UX"), traits);
        Assert.Contains(new KeyValuePair<string, string>("Screen", "TagControl"), traits);
    }
}
