using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Memoria.ToDo.Testing.Analyzers;

internal static class TestKindLookup
{
    private static readonly ImmutableHashSet<string> KindTypeNames = ImmutableHashSet.Create(
        TestKinds.UnitAttributeTypeName,
        TestKinds.UxAttributeTypeName,
        TestKinds.IntegrationAttributeTypeName);

    /// <summary>Whether the type or a base class carries the kind attribute with this full type name.</summary>
    public static bool HasKind(INamedTypeSymbol type, string kindAttributeTypeName) =>
        FindKind(type, kindAttributeTypeName, out _) is not null;

    /// <summary>The nearest kind attribute with this full type name, on the type or a base class, and the class that carries it.</summary>
    public static AttributeData? FindKind(INamedTypeSymbol type, string kindAttributeTypeName, out INamedTypeSymbol? declaringType)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            foreach (var attribute in current.GetAttributes())
            {
                if (attribute.AttributeClass?.ToDisplayString() == kindAttributeTypeName)
                {
                    declaringType = current;
                    return attribute;
                }
            }
        }

        declaringType = null;
        return null;
    }

    public static HashSet<string> KindsOf(INamedTypeSymbol type)
    {
        var kinds = new HashSet<string>();
        for (var current = type; current is not null; current = current.BaseType)
        {
            foreach (var attribute in current.GetAttributes())
            {
                var name = attribute.AttributeClass?.ToDisplayString();
                if (name is not null && KindTypeNames.Contains(name))
                {
                    kinds.Add(name);
                }
            }
        }

        return kinds;
    }
}
