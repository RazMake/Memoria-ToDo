using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace Memoria.ToDo.Testing.Analyzers;

/// <summary>Compiles a C# snippet in memory and runs our analyzer on it.</summary>
internal static class SnippetAnalysis
{
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

    /// <summary>Asserts exactly one error, naming <paramref name="className"/>, and returns it.</summary>
    public static Diagnostic AssertOneErrorNaming(IReadOnlyList<Diagnostic> diagnostics, string className)
    {
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains(className, diagnostic.GetMessage());
        return diagnostic;
    }

    // The assemblies this test process already loaded, read from memory, so no file is opened.
    private static unsafe IReadOnlyList<MetadataReference> LoadedAssemblyReferences()
    {
        // Make sure the assemblies the snippets use are loaded.
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
