using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Memoria.ToDo.Testing.Analyzers;

internal static class RealInputBoundary
{
    private static readonly DiagnosticDescriptor UnmarkedUse = new(
        "MTD0003",
        "Real input is used outside its boundary",
        "'{0}' sends real Windows input; use it only from a [RealInput] helper or an [Integration] test, never from a [Unit] or [UX] class",
        "Testing",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor UnmarkedImport = new(
        "MTD0004",
        "Native input import is not marked",
        "Native input import '{0}' needs the [RealInput] marker",
        "Testing",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(UnmarkedUse, UnmarkedImport);

    public static void Register(AnalysisContext context)
    {
        context.RegisterOperationAction(
            AnalyzeUse,
            OperationKind.Invocation,
            OperationKind.ObjectCreation,
            OperationKind.MethodReference,
            OperationKind.PropertyReference,
            OperationKind.FieldReference,
            OperationKind.EventReference);
        context.RegisterSymbolAction(AnalyzeImport, SymbolKind.Method);
    }

    private static void AnalyzeUse(OperationAnalysisContext context)
    {
        ISymbol? used = context.Operation switch
        {
            IInvocationOperation invocation => invocation.TargetMethod,
            IObjectCreationOperation creation => creation.Constructor,
            IMemberReferenceOperation reference => reference.Member,
            _ => null,
        };

        if (used is null
            || !(IsRealInput(used) || UsesRealInputAccessor(context.Operation))
            || IsAllowedIn(context.ContainingSymbol))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(UnmarkedUse, context.Operation.Syntax.GetLocation(), used.Name));
    }

    private static bool UsesRealInputAccessor(IOperation operation) => (operation switch
    {
        IPropertyReferenceOperation reference =>
            (IsRead(reference) && IsRealInputAccessor(reference.Property.GetMethod))
            || (IsWritten(reference) && IsRealInputAccessor(reference.Property.SetMethod ?? reference.Property.GetMethod)),
        IEventReferenceOperation { Parent: IEventAssignmentOperation assignment } reference
            when assignment.EventReference == reference =>
            IsRealInputAccessor(assignment.Adds ? reference.Event.AddMethod : reference.Event.RemoveMethod),
        _ => false,
    }) && !IsWithinNameOf(operation);

    private static bool IsWithinNameOf(IOperation operation)
    {
        for (var parent = operation.Parent; parent is not null; parent = parent.Parent)
        {
            if (parent is INameOfOperation)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsRealInputAccessor(IMethodSymbol? accessor) => accessor is not null && IsRealInput(accessor);

    private static bool IsWritten(IOperation operation)
    {
        var target = AssignmentTarget(operation);
        return (target.Parent is IAssignmentOperation assignment && assignment.Target == target)
            || target.Parent is IIncrementOrDecrementOperation;
    }

    private static bool IsRead(IOperation operation)
    {
        var target = AssignmentTarget(operation);
        return target.Parent is IAssignmentOperation assignment && assignment.Target == target
            ? assignment is ICompoundAssignmentOperation or ICoalesceAssignmentOperation
            : true;
    }

    // A deconstruction assigns to the tuple containing the property.
    private static IOperation AssignmentTarget(IOperation operation)
    {
        while (operation.Parent is ITupleOperation tuple)
        {
            operation = tuple;
        }

        return operation;
    }

    private static void AnalyzeImport(SymbolAnalysisContext context)
    {
        var method = (IMethodSymbol)context.Symbol;
        if (method.Locations.Length == 0
            || method.PartialDefinitionPart is not null
            || !NativeInputImports.IsNativeInput(method)
            || IsRealInput(method))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(UnmarkedImport, method.Locations[0], method.Name));
    }

    private static bool IsRealInput(ISymbol symbol)
    {
        for (ISymbol? current = symbol.OriginalDefinition; current is not null and not INamespaceSymbol; current = current.ContainingSymbol)
        {
            if (HasMarker(current))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsAllowedIn(ISymbol context)
    {
        var marked = false;
        for (ISymbol? current = context; current is not null and not INamespaceSymbol; current = current.ContainingSymbol)
        {
            marked |= HasMarker(current);
            if (current is INamedTypeSymbol type)
            {
                var kinds = TestKindLookup.KindsOf(type);
                if (kinds.Count > 0)
                {
                    return kinds.Count == 1 && kinds.Contains(TestKinds.IntegrationAttributeTypeName);
                }
            }
        }

        return marked;
    }

    private static bool HasMarker(ISymbol symbol)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString() == TestKinds.RealInputAttributeTypeName)
            {
                return true;
            }
        }

        return false;
    }
}
