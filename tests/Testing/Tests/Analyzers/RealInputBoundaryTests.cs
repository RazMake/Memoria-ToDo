using Microsoft.CodeAnalysis;
using Xunit;

namespace Memoria.ToDo.Testing.Analyzers;

[Unit]
[Collection(SnippetAnalysis.Collection)]
public sealed class RealInputBoundaryTests
{
    private const string Marker = """
        namespace Memoria.ToDo.Testing
        {
            [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Method |
                System.AttributeTargets.Constructor)]
            public sealed class RealInputAttribute : System.Attribute { }
        }
        """;

    private const string Input = """
        public static class Input
        {
            [RealInput] public static void Move() { }
            public static void Move(int ordinaryArgument) { }
        }
        """;

    [Theory]
    [InlineData("Unit")]
    [InlineData("UX")]
    public async Task A_test_that_uses_real_input_must_be_an_integration_test(string kind)
    {
        var diagnostics = await Analyze(Input + $$"""
            [{{kind}}] public class Screen
            {
                [Fact] public void Exercise() { Input.Move(); }
            }
            """);

        AssertError(diagnostics, "MTD0003");
    }

    [Theory]
    [InlineData("Input.Move();")]
    [InlineData("System.Action move = Input.Move;")]
    [InlineData("System.Action move = () => Input.Move();")]
    [InlineData("void MoveLater() { Input.Move(); } MoveLater();")]
    public async Task An_unmarked_helper_cannot_hide_real_input(string body)
    {
        var diagnostics = await Analyze(Input + $$"""
            public class Helper { public void Exercise() { {{body}} } }
            """);

        AssertError(diagnostics, "MTD0003");
    }

    [Theory]
    [InlineData("[RealInput] public class Screen", "[Fact] public void Exercise()", "Input.Move();")]
    [InlineData("public class Screen", "[Fact, RealInput] public void Exercise()", "Input.Move();")]
    [InlineData("public class Screen", "[RealInput] public void Exercise()", "Input.Move();")]
    public async Task Marking_a_UX_class_or_its_methods_cannot_bypass_the_boundary(
        string declaration, string method, string body)
    {
        var diagnostics = await Analyze(Input + $$"""
            [UX] {{declaration}} { {{method}} { {{body}} } }
            """);

        AssertError(diagnostics, "MTD0003");
    }

    [Fact]
    public async Task A_marked_nested_helper_cannot_bypass_its_unit_tests_boundary()
    {
        var diagnostics = await Analyze(Input + """
            [Unit] public class Screen
            {
                [Fact] public void Exercise() { }
                [RealInput] public class Helper { public void Move() { Input.Move(); } }
            }
            """);

        AssertError(diagnostics, "MTD0003");
    }

    [Theory]
    [InlineData("public class Native { [RealInput] public Native() { } }", "new Native();")]
    [InlineData("[RealInput] public class Native { public void Move() { } }", "Native.Move();")]
    public async Task Marked_constructors_and_types_are_real_input(string declaration, string body)
    {
        var diagnostics = await Analyze(declaration + $$"""
            public class Helper
            {
                private Native Native = null!;
                public void Exercise() { {{body}} }
            }
            """);

        AssertError(diagnostics, "MTD0003");
    }

    [Fact]
    public async Task Aliases_and_inherited_members_do_not_hide_real_input()
    {
        var diagnostics = await Analyze("""
            using Alias = Derived;
            public class Native { [RealInput] public static void Move() { } }
            public class Derived : Native { }
            public class Helper { public void Exercise() { Alias.Move(); } }
            """);

        AssertError(diagnostics, "MTD0003");
    }

    [Fact]
    public async Task Integration_tests_can_use_real_input_from_their_nested_helpers()
    {
        var diagnostics = await Analyze(Input + """
            [Integration] public class Screen
            {
                [Fact] public void Exercise() { Input.Move(); }
                public class Helper
                {
                    public void Move() { System.Action action = Input.Move; action(); }
                }
            }
            """);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task A_nested_UX_test_cannot_borrow_an_outer_integration_boundary()
    {
        var diagnostics = await Analyze(Input + """
            [Integration] public class Outer
            {
                [UX] public class Screen
                {
                    [Fact] public void Exercise() { Input.Move(); }
                }
            }
            """);

        AssertError(diagnostics, "MTD0003");
    }

    [Fact]
    public async Task Marked_helpers_can_share_real_input_with_other_marked_helpers()
    {
        var diagnostics = await Analyze(Input + """
            [RealInput] public class Helper
            {
                public Helper() { Input.Move(); }
                public void Move() { System.Action action = Input.Move; action(); }
            }
            public class Caller { [RealInput] public void Exercise() { new Helper().Move(); } }
            """);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task An_unmarked_overload_and_an_unrelated_same_named_method_remain_usable()
    {
        var diagnostics = await Analyze(Input + """
            public static class Other { public static void Move() { } }
            [Unit] public class Screen
            {
                [Fact] public void Exercise() { Input.Move(1); Other.Move(); }
            }
            """);

        Assert.Empty(diagnostics);
    }

    [Theory]
    [InlineData("SetCursorPos")]
    [InlineData("SendInput")]
    [InlineData("mouse_event")]
    [InlineData("keybd_event")]
    [InlineData("SetForegroundWindow")]
    public async Task Native_input_imports_must_declare_the_boundary(string entryPoint)
    {
        var diagnostics = await Analyze($$"""
            public static class Native
            {
                [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "{{entryPoint}}")]
                public static extern int Renamed(int value);
            }
            """);

        AssertError(diagnostics, "MTD0004");
    }

    [Fact]
    public async Task An_imports_own_name_identifies_real_input_when_no_entry_point_is_given()
    {
        var diagnostics = await Analyze("""
            public static class Native
            {
                [System.Runtime.InteropServices.DllImport("USER32")]
                public static extern int SetCursorPos(int x, int y);
            }
            """);

        AssertError(diagnostics, "MTD0004");
    }

    [Fact]
    public async Task Library_import_entry_point_aliases_must_declare_the_boundary()
    {
        var diagnostics = await Analyze("""
            public static partial class Native
            {
                [System.Runtime.InteropServices.LibraryImport("user32.dll", EntryPoint = "SetCursorPos")]
                public static partial int Renamed(int x, int y);
                public static partial int Renamed(int x, int y) => 0;
            }
            """);

        AssertError(diagnostics, "MTD0004");
    }

    [Fact]
    public async Task Even_integration_tests_must_mark_their_native_input_imports()
    {
        var diagnostics = await Analyze("""
            [Integration] public class Screen
            {
                [Fact] public void Exercise() { }
                [System.Runtime.InteropServices.DllImport("user32.dll")]
                public static extern int SendInput(int count);
            }
            """);

        AssertError(diagnostics, "MTD0004");
    }

    [Fact]
    public async Task Marked_native_imports_are_allowed()
    {
        var diagnostics = await Analyze("""
            public static class Native
            {
                [RealInput, System.Runtime.InteropServices.DllImport("user32.dll")]
                public static extern int SetCursorPos(int x, int y);
            }
            """);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Unrelated_imports_are_not_mistaken_for_native_input()
    {
        var diagnostics = await Analyze("""
            public static class Native
            {
                [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "GetCursorPos")]
                public static extern int SetCursorPos(int value);
                [System.Runtime.InteropServices.DllImport("another-library")]
                public static extern int SendInput(int value);
            }
            """);

        Assert.Empty(diagnostics);
    }

    [Theory]
    [InlineData(true, false, "var value = Native.Value;")]
    [InlineData(false, true, "Native.Value = 1;")]
    [InlineData(true, false, "Native.Value += 1;")]
    [InlineData(false, true, "Native.Value += 1;")]
    [InlineData(true, false, "Native.Value++;")]
    [InlineData(false, true, "Native.Value++;")]
    [InlineData(true, false, "Native.Value ??= 1;")]
    [InlineData(false, true, "Native.Value ??= 1;")]
    [InlineData(false, true, "(Native.Value, _) = (1, 2);")]
    public async Task A_marked_property_accessor_that_the_code_invokes_is_real_input(
        bool getterMarked, bool setterMarked, string body)
    {
        var diagnostics = await Analyze(PropertyAccessors(getterMarked, setterMarked) + $$"""
            [UX] public class Helper { public void Exercise() { {{body}} } }
            """);

        AssertError(diagnostics, "MTD0003");
    }

    [Theory]
    [InlineData(false, true, "var value = Native.Value;")]
    [InlineData(true, false, "Native.Value = 1;")]
    public async Task The_unmarked_opposite_property_accessor_remains_usable(
        bool getterMarked, bool setterMarked, string body)
    {
        var diagnostics = await Analyze(PropertyAccessors(getterMarked, setterMarked) + $$"""
            public class Helper { public void Exercise() { {{body}} } }
            """);

        Assert.Empty(diagnostics);
    }

    [Theory]
    [InlineData("var value = Native.Value;")]
    [InlineData("Native.Value = 1;")]
    public async Task Reading_or_assigning_a_reference_property_invokes_its_marked_getter(string body)
    {
        var diagnostics = await Analyze($$"""
            public static class Native
            {
                private static int value;
                public static ref int Value { [RealInput] get => ref value; }
            }
            public class Helper { public void Exercise() { {{body}} } }
            """);

        AssertError(diagnostics, "MTD0003");
    }

    [Theory]
    [InlineData(true, false, "var value = new Native()[0];")]
    [InlineData(false, true, "new Native()[0] = 1;")]
    public async Task Indexer_access_invokes_its_marked_accessor(
        bool getterMarked, bool setterMarked, string body)
    {
        var diagnostics = await Analyze(PropertyAccessors(getterMarked, setterMarked, "int? this[int index]") + $$"""
            [UX] public class Helper { public void Exercise() { {{body}} } }
            """);

        AssertError(diagnostics, "MTD0003");
    }

    [Theory]
    [InlineData(true, false, "Native.Changed += () => { };")]
    [InlineData(false, true, "Native.Changed -= () => { };")]
    public async Task A_marked_event_accessor_that_the_code_invokes_is_real_input(
        bool addMarked, bool removeMarked, string body)
    {
        var diagnostics = await Analyze(EventAccessors(addMarked, removeMarked) + $$"""
            [Unit] public class Helper { public void Exercise() { {{body}} } }
            """);

        AssertError(diagnostics, "MTD0003");
    }

    [Theory]
    [InlineData(true, false, "Native.Changed -= () => { };")]
    [InlineData(false, true, "Native.Changed += () => { };")]
    public async Task The_unmarked_opposite_event_accessor_remains_usable(
        bool addMarked, bool removeMarked, string body)
    {
        var diagnostics = await Analyze(EventAccessors(addMarked, removeMarked) + $$"""
            public class Helper { public void Exercise() { {{body}} } }
            """);

        Assert.Empty(diagnostics);
    }

    [Theory]
    [InlineData("[Integration] public class Helper", "public void Exercise()")]
    [InlineData("public class Helper", "[RealInput] public void Exercise()")]
    public async Task Integration_tests_and_marked_helpers_can_invoke_marked_accessors(
        string declaration, string method)
    {
        var diagnostics = await Analyze(PropertyAccessors(true, true) + $$"""
            {{declaration}}
            {
                {{method}} { Native.Value += 1; }
            }
            """);

        Assert.Empty(diagnostics);
    }

    [Theory]
    [InlineData("nameof(Native.Value)")]
    [InlineData("nameof(Native.Value.Value)")]
    public async Task Naming_a_property_does_not_invoke_its_marked_getter(string expression)
    {
        var diagnostics = await Analyze(PropertyAccessors(true, false) + $$"""
            [Unit] public class Helper { public string Label() => {{expression}}; }
            """);

        Assert.Empty(diagnostics);
    }

    private static string Mark(bool marked) => marked ? "[RealInput] " : "";

    private static string PropertyAccessors(bool getterMarked, bool setterMarked, string member = "static int? Value") => $$"""
        public class Native
        {
            public {{member}} { {{Mark(getterMarked)}}get => null; {{Mark(setterMarked)}}set { } }
        }
        """;

    private static string EventAccessors(bool addMarked, bool removeMarked) => $$"""
        public static class Native
        {
            public static event System.Action? Changed { {{Mark(addMarked)}}add { } {{Mark(removeMarked)}}remove { } }
        }
        """;

    private static Task<IReadOnlyList<Diagnostic>> Analyze(string source) =>
        SnippetAnalysis.Analyze(source + Marker);

    private static void AssertError(IReadOnlyList<Diagnostic> diagnostics, string id)
    {
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(id, diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
    }
}
