using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace Memoria.ToDo.Testing.Analyzers;

/// <summary>Compiles a C# snippet in memory and runs our analyzer on it.</summary>
internal static class SnippetAnalysis
{
    /// <summary>
    /// The xUnit collection of every analyzer test class. Its classes run one after another: Roslyn's cold start
    /// is then paid once, not by every class at the same time (measured: about 1.4 CPU-s more in parallel).
    /// </summary>
    public const string Collection = "Analyzer snippets";

    /// <summary>Puts xUnit.net's and our attributes in scope.</summary>
    public const string Usings = "using Xunit;\nusing Memoria.ToDo.Testing;\n";

    private static readonly Lazy<IReadOnlyList<MetadataReference>> References = new(LoadedAssemblyReferences);

    public static async Task<IReadOnlyList<Diagnostic>> Analyze(string source)
    {
        var compilation = CSharpCompilation.Create(
            "Snippet",
            [CSharpSyntaxTree.ParseText(Usings + source)],
            References.Value,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var snippetErrors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.True(snippetErrors.Count == 0, "The snippet doesn't compile: " + string.Join("; ", snippetErrors));

        var analyzed = compilation.WithAnalyzers([new TestKindAnalyzer()]);
        return await analyzed.GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Asserts exactly one error with the given id, naming <paramref name="className"/> in quotes, and returns it.</summary>
    public static Diagnostic AssertOneError(IReadOnlyList<Diagnostic> diagnostics, string id, string className)
    {
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal(id, diagnostic.Id);
        Assert.Contains($"'{className}'", diagnostic.GetMessage());
        return diagnostic;
    }

    private static unsafe IReadOnlyList<MetadataReference> LoadedAssemblyReferences()
    {
        _ = typeof(FactAttribute).Assembly;
        _ = typeof(UnitAttribute).Assembly;

        var references = new List<MetadataReference>();
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.IsDynamic || !System.Reflection.Metadata.AssemblyExtensions.TryGetRawMetadata(assembly, out var blob, out var length))
            {
                continue;
            }

            references.Add(AssemblyMetadata.Create(ModuleMetadata.CreateFromMetadata((IntPtr)blob, length)).GetReference());
        }

        return references;
    }
}
