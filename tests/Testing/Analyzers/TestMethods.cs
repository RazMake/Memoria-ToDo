using Microsoft.CodeAnalysis;

namespace Memoria.ToDo.Testing.Analyzers;

/// <summary>Finds xUnit.net test methods by full type name, since the analyzer doesn't reference xUnit.net.</summary>
internal static class TestMethods
{
    private const string FactAttributeTypeName = "Xunit.FactAttribute";
    private const string TheoryAttributeTypeName = "Xunit.TheoryAttribute";

    /// <summary>A test method is marked <c>[Fact]</c>, <c>[Theory]</c>, or an attribute derived from them.</summary>
    public static bool IsTestMethod(IMethodSymbol method) => HasAttributeDerivedFrom(method, FactAttributeTypeName);

    public static bool IsTheory(IMethodSymbol method) => HasAttributeDerivedFrom(method, TheoryAttributeTypeName);

    /// <summary>Whether the type declares or inherits a test method.</summary>
    public static bool Any(INamedTypeSymbol type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            foreach (var member in current.GetMembers())
            {
                if (member is IMethodSymbol method && IsTestMethod(method))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>How many test methods the type declares or inherits; an override counts once, as its base method.</summary>
    public static int Count(INamedTypeSymbol type)
    {
        var count = 0;
        for (var current = type; current is not null; current = current.BaseType)
        {
            foreach (var member in current.GetMembers())
            {
                if (member is IMethodSymbol { IsOverride: false } method && IsTestMethod(method))
                {
                    count++;
                }
            }
        }

        return count;
    }

    private static bool HasAttributeDerivedFrom(IMethodSymbol method, string attributeTypeName)
    {
        foreach (var attribute in method.GetAttributes())
        {
            for (var current = attribute.AttributeClass; current is not null; current = current.BaseType)
            {
                if (current.ToDisplayString() == attributeTypeName)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
