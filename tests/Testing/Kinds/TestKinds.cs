// Compiled by the Kinds project and, through a linked Compile item, by the analyzer project
// (.NET Standard 2.0). Keep it to plain constants. Directory.Build.props reads TraitName and
// UnitKind from this file with a regular expression, so keep the `Name = "value"` layout.
namespace Memoria.ToDo.Testing
{
    internal static class TestKinds
    {
        public const string TraitName = "Category";

        public const string UnitKind = "Unit";
        public const string UxKind = "UX";
        public const string IntegrationKind = "Integration";

        public const string UnitAttributeTypeName = "Memoria.ToDo.Testing.UnitAttribute";
        public const string UxAttributeTypeName = "Memoria.ToDo.Testing.UXAttribute";
        public const string IntegrationAttributeTypeName = "Memoria.ToDo.Testing.IntegrationAttribute";
    }
}
