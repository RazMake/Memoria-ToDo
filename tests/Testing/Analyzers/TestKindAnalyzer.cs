using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Memoria.ToDo.Testing.Analyzers;

/// <summary>Enforces test kinds and their real-input boundary.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TestKindAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor MissingKind = new(
        "MTD0001",
        "Test class has no kind attribute",
        "Test class '{0}' needs one kind attribute: [Unit], [UX], or [Integration]",
        "Testing",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor SeveralKinds = new(
        "MTD0002",
        "Test class has more than one kind attribute",
        "Test class '{0}' has more than one kind attribute; keep exactly one of [Unit], [UX], [Integration]",
        "Testing",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(MissingKind, SeveralKinds)
            .AddRange(RealInputBoundary.SupportedDiagnostics)
            .AddRange(UxScreenCheck.SupportedDiagnostics);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeType, SymbolKind.NamedType);
        RealInputBoundary.Register(context);
        UxScreenCheck.Register(context);
    }

    private static void AnalyzeType(SymbolAnalysisContext context)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        if (type.TypeKind != TypeKind.Class || type.IsAbstract || type.Locations.Length == 0 || !TestMethods.Any(type))
        {
            return;
        }

        var kinds = TestKindLookup.KindsOf(type);
        if (kinds.Count == 1)
        {
            return;
        }

        var descriptor = kinds.Count == 0 ? MissingKind : SeveralKinds;
        context.ReportDiagnostic(Diagnostic.Create(descriptor, type.Locations[0], type.Name));
    }

}
