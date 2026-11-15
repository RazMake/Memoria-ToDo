using Xunit;
using static Memoria.ToDo.Testing.Analyzers.SnippetAnalysis;

namespace Memoria.ToDo.Testing.Analyzers;

[Unit]
[Collection(SnippetAnalysis.Collection)]
public sealed class A_test_class_with_no_kind
{
    [Fact]
    public async Task Gets_error_MTD0001_naming_the_class()
    {
        var diagnostics = await Analyze("public class OrdersTests { [Fact] public void Places_an_order() { } }");

        AssertOneError(diagnostics, "MTD0001", "OrdersTests");
    }

    [Fact]
    public async Task Is_reported_on_the_class_name()
    {
        var diagnostics = await Analyze("public class OrdersTests { [Fact] public void Places_an_order() { } }");

        var location = Assert.Single(diagnostics).Location;
        Assert.True(location.IsInSource);
        Assert.Equal("OrdersTests", location.SourceTree!.GetText(TestContext.Current.CancellationToken).ToString(location.SourceSpan));
    }

    [Fact]
    public async Task Gets_an_error_when_its_only_test_is_a_Theory()
    {
        var diagnostics = await Analyze("public class OrdersTests { [Theory][InlineData(1)] public void Places_an_order(int n) { } }");

        AssertOneError(diagnostics, "MTD0001", "OrdersTests");
    }

    [Fact]
    public async Task Gets_an_error_when_its_only_test_uses_an_attribute_derived_from_Fact_through_other_attributes()
    {
        var diagnostics = await Analyze("""
            public class BaseFactAttribute : FactAttribute { }
            public class DeeperFactAttribute : BaseFactAttribute { }
            public class OrdersTests { [DeeperFact] public void Places_an_order() { } }
            """);

        AssertOneError(diagnostics, "MTD0001", "OrdersTests");
    }

    [Fact]
    public async Task Gets_an_error_even_when_it_has_other_attributes()
    {
        var diagnostics = await Analyze("[System.Obsolete] public class OrdersTests { [Fact] public void Places_an_order() { } }");

        AssertOneError(diagnostics, "MTD0001", "OrdersTests");
    }

    [Fact]
    public async Task Is_reported_once_however_many_tests_it_has()
    {
        var diagnostics = await Analyze("""
            public class OrdersTests
            {
                [Fact] public void Places_an_order() { }
                [Fact] public void Cancels_an_order() { }
                [Theory][InlineData(1)] public void Ships_an_order(int n) { }
            }
            """);

        AssertOneError(diagnostics, "MTD0001", "OrdersTests");
    }

    [Fact]
    public async Task Reports_each_class_with_no_kind_and_no_other_class()
    {
        var diagnostics = await Analyze("""
            [Unit] public class OrdersTests { [Fact] public void Places_an_order() { } }
            public class BillingTests { [Fact] public void Sends_an_invoice() { } }
            public class ShippingTests { [Fact] public void Ships_a_parcel() { } }
            """);

        Assert.Equal(2, diagnostics.Count);
        Assert.Single(diagnostics, d => d.GetMessage().Contains("'BillingTests'"));
        Assert.Single(diagnostics, d => d.GetMessage().Contains("'ShippingTests'"));
    }
}

[Unit]
[Collection(SnippetAnalysis.Collection)]
public sealed class A_test_class_with_one_kind
{
    [Theory]
    [InlineData("Unit")]
    [InlineData("UX")]
    [InlineData("Integration")]
    public async Task Gets_no_diagnostic(string kind)
    {
        var diagnostics = await Analyze($"[{kind}] public class OrdersTests {{ [Fact] public void Places_an_order() {{ }} }}");

        Assert.Empty(diagnostics);
    }
}

[Unit]
[Collection(SnippetAnalysis.Collection)]
public sealed class A_test_class_with_two_or_more_kinds
{
    [Theory]
    [InlineData("[Unit][UX]")]
    [InlineData("[Unit, Integration]")]
    [InlineData("[UX][Integration]")]
    [InlineData("[Unit][UX][Integration]")]
    public async Task Gets_one_error_MTD0002_naming_the_class(string attributes)
    {
        var diagnostics = await Analyze($"{attributes} public class OrdersTests {{ [Fact] public void Places_an_order() {{ }} }}");

        AssertOneError(diagnostics, "MTD0002", "OrdersTests");
    }
}

[Unit]
[Collection(SnippetAnalysis.Collection)]
public sealed class A_class_that_is_not_a_test_class
{
    [Fact]
    public async Task Gets_no_diagnostic_when_it_has_no_test_methods()
    {
        var diagnostics = await Analyze("public class OrderFormatter { public string Format(int n) => n.ToString(); }");

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Gets_no_diagnostic_when_its_methods_carry_only_other_attributes()
    {
        var diagnostics = await Analyze("public class OrderFormatter { [System.Obsolete] public string Format(int n) => n.ToString(); }");

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Gets_no_diagnostic_when_it_is_abstract_even_with_tests_and_no_kind()
    {
        var diagnostics = await Analyze("public abstract class OrdersTestsBase { [Fact] public void Places_an_order() { } }");

        Assert.Empty(diagnostics);
    }
}

[Unit]
[Collection(SnippetAnalysis.Collection)]
public sealed class A_test_class_that_inherits_tests
{
    [Fact]
    public async Task Gets_an_error_naming_the_derived_class_when_neither_class_has_a_kind()
    {
        var diagnostics = await Analyze("""
            public abstract class OrdersTestsBase { [Fact] public void Places_an_order() { } }
            public class DesktopOrdersTests : OrdersTestsBase { }
            """);

        AssertOneError(diagnostics, "MTD0001", "DesktopOrdersTests");
    }

    [Fact]
    public async Task Gets_an_error_when_the_tests_come_from_a_grandparent()
    {
        var diagnostics = await Analyze("""
            public abstract class OrdersTestsBase { [Fact] public void Places_an_order() { } }
            public abstract class MoreOrdersTestsBase : OrdersTestsBase { }
            public class DesktopOrdersTests : MoreOrdersTestsBase { }
            """);

        AssertOneError(diagnostics, "MTD0001", "DesktopOrdersTests");
    }

    [Fact]
    public async Task Gets_no_diagnostic_when_it_has_a_kind_of_its_own()
    {
        var diagnostics = await Analyze("""
            public abstract class OrdersTestsBase { [Fact] public void Places_an_order() { } }
            [UX] public class DesktopOrdersTests : OrdersTestsBase { }
            """);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Gets_an_error_for_the_base_class_too_when_the_base_is_not_abstract_and_has_no_kind()
    {
        var diagnostics = await Analyze("""
            public class OrdersTests { [Fact] public void Places_an_order() { } }
            public class DesktopOrdersTests : OrdersTests { }
            """);

        Assert.Equal(2, diagnostics.Count);
        Assert.Single(diagnostics, d => d.GetMessage().Contains("'DesktopOrdersTests'"));
        Assert.Single(diagnostics, d => d.GetMessage().Contains("'OrdersTests'"));
    }

    // Decision to confirm: the kind attributes keep the default Inherited=true, and xUnit.net reads
    // class traits from base classes too, so a kind on a base class counts for its derived classes.
    [Fact]
    public async Task Counts_a_kind_inherited_from_an_abstract_base_class()
    {
        var diagnostics = await Analyze("""
            [Unit] public abstract class OrdersTestsBase { [Fact] public void Places_an_order() { } }
            public class DesktopOrdersTests : OrdersTestsBase { }
            """);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Counts_the_same_kind_on_the_base_and_the_derived_class_once()
    {
        var diagnostics = await Analyze("""
            [Unit] public abstract class OrdersTestsBase { [Fact] public void Places_an_order() { } }
            [Unit] public class DesktopOrdersTests : OrdersTestsBase { }
            """);

        Assert.Empty(diagnostics);
    }

    // Decision to confirm: xUnit.net would give the derived class's tests both kinds' traits.
    [Fact]
    public async Task Gets_an_error_when_it_adds_a_second_kind_to_an_inherited_one()
    {
        var diagnostics = await Analyze("""
            [Unit] public abstract class OrdersTestsBase { [Fact] public void Places_an_order() { } }
            [UX] public class DesktopOrdersTests : OrdersTestsBase { }
            """);

        AssertOneError(diagnostics, "MTD0002", "DesktopOrdersTests");
    }
}

[Unit]
[Collection(SnippetAnalysis.Collection)]
public sealed class A_nested_test_class
{
    [Fact]
    public async Task Gets_no_kind_from_the_outer_class()
    {
        var diagnostics = await Analyze("""
            [Unit]
            public class OuterOrdersTests
            {
                [Fact] public void Places_an_order() { }
                public class InnerOrdersTests { [Fact] public void Cancels_an_order() { } }
            }
            """);

        AssertOneError(diagnostics, "MTD0001", "InnerOrdersTests");
    }

    [Fact]
    public async Task Gets_no_diagnostic_when_it_has_its_own_kind()
    {
        var diagnostics = await Analyze("""
            public class OuterGroup
            {
                [Unit] public class InnerOrdersTests { [Fact] public void Places_an_order() { } }
            }
            """);

        Assert.Empty(diagnostics);
    }
}

[Unit]
[Collection(SnippetAnalysis.Collection)]
public sealed class A_partial_test_class
{
    [Fact]
    public async Task Gets_one_error_when_neither_part_has_a_kind_and_both_have_tests()
    {
        var diagnostics = await Analyze("""
            public partial class OrdersTests { [Fact] public void Places_an_order() { } }
            public partial class OrdersTests { [Fact] public void Cancels_an_order() { } }
            """);

        AssertOneError(diagnostics, "MTD0001", "OrdersTests");
    }

    [Fact]
    public async Task Gets_no_diagnostic_when_the_kind_is_on_one_part_and_the_tests_on_the_other()
    {
        var diagnostics = await Analyze("""
            [Unit] public partial class OrdersTests { }
            public partial class OrdersTests { [Fact] public void Places_an_order() { } }
            """);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Gets_one_error_when_its_parts_carry_different_kinds()
    {
        var diagnostics = await Analyze("""
            [Unit] public partial class OrdersTests { [Fact] public void Places_an_order() { } }
            [UX] public partial class OrdersTests { [Fact] public void Cancels_an_order() { } }
            """);

        AssertOneError(diagnostics, "MTD0002", "OrdersTests");
    }
}
