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
