using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Memoria.ToDo.Testing.Analyzers;

/// <summary>
/// Counts each screen's UX tests (a screen is a namespace) and enforces the limit per screen.
/// Design: docs/architecture/test-selection.md, "Our analyzer counts each screen's UX tests".
/// </summary>
internal static class UxScreenCheck
{
    /// <summary>The most UX tests one screen may have; its only home in code.</summary>
    private const int MaxTestsPerScreen = 10;

    private const string TestsSegment = "Tests";

    /// <summary>How the MTD0008 message shows a <c>[UX(null)]</c> screen name.</summary>
    private const string NoScreenName = "(none)";

    private static readonly DiagnosticDescriptor TooManyTests = new(
        "MTD0005",
        "Screen has too many UX tests",
        "Screen '{0}' has {1} UX tests; the limit is {2}",
        "Testing",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    private static readonly DiagnosticDescriptor TheoryInUxClass = new(
        "MTD0006",
        "UX class holds a theory",
        "UX test method '{0}' is a theory; a UX path is one [Fact], since a theory's rows would multiply the screen's count unseen",
        "Testing",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor ClassAboveScreenNamespace = new(
        "MTD0007",
        "UX class sits above screen namespaces",
        "UX class '{0}' is in a namespace that has UX classes in a child namespace; move it into its screen's folder",
        "Testing",
        DiagnosticSeverity.Error,
                isEnabledByDefault: true,
                customTags: WellKnownDiagnosticTags.CompilationEnd);

    private static readonly DiagnosticDescriptor ScreenNameMismatch = new(
        "MTD0008",
        "UX screen name differs from the namespace's screen",
        "[UX(\"{0}\")] names a different screen than the class's namespace, which is screen '{1}'",
        "Testing",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(TooManyTests, TheoryInUxClass, ClassAboveScreenNamespace, ScreenNameMismatch);

    public static void Register(AnalysisContext context) =>
        context.RegisterCompilationStartAction(start =>
        {
            var classes = new ConcurrentBag<UxClass>();
            start.RegisterSymbolAction(symbol => AnalyzeType(symbol, classes), SymbolKind.NamedType);
            start.RegisterCompilationEndAction(end => AnalyzeScreens(end, classes));
        });

    /// <summary>The screen a namespace stands for: its last segment, or the one before a final <c>Tests</c>.</summary>
    private static string ScreenOf(string ns)
    {
        var segments = ns.Split('.');
        var last = segments[segments.Length - 1];
        return last == TestsSegment && segments.Length > 1 ? segments[segments.Length - 2] : last;
    }

    private static void AnalyzeType(SymbolAnalysisContext context, ConcurrentBag<UxClass> classes)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        if (type.TypeKind != TypeKind.Class || type.Locations.Length == 0)
        {
            return;
        }

        var uxAttribute = TestKindLookup.FindKind(type, TestKinds.UxAttributeTypeName, out var uxDeclaringType);
        if (uxAttribute is null)
        {
            return;
        }

        CheckTheories(context, type);

        var ns = type.ContainingNamespace.ToDisplayString();
        CheckScreenName(context, type, ns, uxAttribute, uxDeclaringType!);

        if (!type.IsAbstract && TestMethods.Any(type))
        {
            classes.Add(new UxClass(ns, type.Name, type.Locations[0], TestMethods.Count(type)));
        }
    }

    /// <summary>
    /// Reports each theory the class declares on the theory, and each one it inherits on the class's name,
    /// unless the declaring base is itself a UX class, which reports its own.
    /// </summary>
    private static void CheckTheories(SymbolAnalysisContext context, INamedTypeSymbol type)
    {
        var inherited = false;
        for (var current = type; current is not null; current = current.BaseType)
        {
            var reportedByDeclaringClass = inherited && TestKindLookup.HasKind(current, TestKinds.UxAttributeTypeName);
            foreach (var member in current.GetMembers())
            {
                if (member is IMethodSymbol method && TestMethods.IsTheory(method) && !reportedByDeclaringClass)
                {
                    var location = inherited ? type.Locations[0] : method.Locations[0];
                    context.ReportDiagnostic(Diagnostic.Create(TheoryInUxClass, location, method.Name));
                }
            }

            inherited = true;
        }
    }

    /// <summary>
    /// Checks the class's effective screen name, inherited or not, against its namespace's screen.
    /// An inherited name has no attribute in this class to point at, so the error lands on the class's name.
    /// </summary>
    private static void CheckScreenName(
        SymbolAnalysisContext context, INamedTypeSymbol type, string ns, AttributeData attribute, INamedTypeSymbol declaringType)
    {
        if (attribute.ConstructorArguments.Length != 1)
        {
            return;
        }

        var named = attribute.ConstructorArguments[0].Value as string;
        var expected = ScreenOf(ns);
        if (named == expected)
        {
            return;
        }

        Location? location = null;
        if (SymbolEqualityComparer.Default.Equals(declaringType, type))
        {
            if (attribute.ApplicationSyntaxReference is { } syntax)
            {
                location = Location.Create(syntax.SyntaxTree, syntax.Span);
            }
        }
        else
        {
            location = type.Locations[0];
        }

        if (location is not null)
        {
            context.ReportDiagnostic(Diagnostic.Create(ScreenNameMismatch, location, named ?? NoScreenName, expected));
        }
    }

    private static void AnalyzeScreens(CompilationAnalysisContext context, ConcurrentBag<UxClass> classes)
    {
        var testsByNamespace = new Dictionary<string, int>();
        var parentNamespaces = new HashSet<string>();
        foreach (var uxClass in classes)
        {
            testsByNamespace.TryGetValue(uxClass.Namespace, out var count);
            testsByNamespace[uxClass.Namespace] = count + uxClass.Tests;

            // Every namespace above this class's, so a class sitting in one of them is found without comparing pairs.
            for (var dot = uxClass.Namespace.LastIndexOf('.'); dot > 0; dot = uxClass.Namespace.LastIndexOf('.', dot - 1))
            {
                if (!parentNamespaces.Add(uxClass.Namespace.Substring(0, dot)))
                {
                    break;
                }
            }
        }

        foreach (var uxClass in classes)
        {
            var total = testsByNamespace[uxClass.Namespace];
            if (total > MaxTestsPerScreen)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    TooManyTests, uxClass.Location, ScreenOf(uxClass.Namespace), total, MaxTestsPerScreen));
            }

            if (parentNamespaces.Contains(uxClass.Namespace))
            {
                context.ReportDiagnostic(Diagnostic.Create(ClassAboveScreenNamespace, uxClass.Location, uxClass.Name));
            }
        }
    }

    private sealed class UxClass(string ns, string name, Location location, int tests)
    {
        public string Namespace { get; } = ns;
        public string Name { get; } = name;
        public Location Location { get; } = location;
        public int Tests { get; } = tests;
    }
}
