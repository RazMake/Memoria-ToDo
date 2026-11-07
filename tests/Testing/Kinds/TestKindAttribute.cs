using Xunit.v3;

namespace Memoria.ToDo.Testing;

/// <summary>Marks a test class's kind: gives every test in it the trait <c>Category=&lt;kind&gt;</c>.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public abstract class TestKindAttribute(string kind) : Attribute, ITraitAttribute
{
    /// <inheritdoc />
    public IReadOnlyCollection<KeyValuePair<string, string>> GetTraits() =>
        [new(TestKinds.TraitName, kind)];
}

/// <summary>The class's tests are unit tests: no real I/O, no application or service started.</summary>
public sealed class UnitAttribute() : TestKindAttribute(TestKinds.UnitKind);

/// <summary>The class's tests check one screen in-process, with fake services.</summary>
public sealed class UXAttribute() : TestKindAttribute(TestKinds.UxKind);

/// <summary>The class's tests check real boundaries and wiring.</summary>
public sealed class IntegrationAttribute() : TestKindAttribute(TestKinds.IntegrationKind);
