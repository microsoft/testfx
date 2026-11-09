// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class AnalyzerRoslynHelperTests
{
    [TestMethod]
    public async Task SymbolHelpers_VisibilityAndMemberTypes_FollowContainingDeclarations()
    {
        const string source = """
            using Alias = Example.Host;
            namespace Example
            {
                public class Host
                {
                    public event System.Action Changed;
                    public string Field;
                    public bool Property { get; set; }
                    public int Method<T>(int number) { int local = number; return local; }
                    private void Private() { }
                    internal class Nested { public void Public() { } }
                }
                public class Use { public Alias Value; }
            }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source, includeMSTest: false);
        Compilation compilation = (await document.Project.GetCompilationAsync())!;
        AnalyzerContinuationTestHelpers.AssertCompilerErrors(compilation);
        SyntaxNode root = (await document.GetSyntaxRootAsync())!;
        SemanticModel model = (await document.GetSemanticModelAsync())!;
        INamedTypeSymbol host = compilation.GetTypeByMetadataName("Example.Host")!;
        IMethodSymbol method = host.GetMembers("Method").OfType<IMethodSymbol>().Single();
        Type extensions = ProductReflection.Type(false, "Analyzer.Utilities.Extensions.ISymbolExtensions");
        string Visibility(ISymbol symbol) => ProductReflection.Call(extensions, null, "GetResultantVisibility", [typeof(ISymbol)], symbol)!.ToString()!;
        Assert.AreEqual("Public", Visibility(host));
        Assert.AreEqual("Public", Visibility(method.Parameters[0]));
        Assert.AreEqual("Private", Visibility(method.TypeParameters[0]));
        Assert.AreEqual("Private", Visibility(host.GetMembers("Private").Single()));
        Assert.AreEqual("Internal", Visibility(host.GetTypeMembers("Nested").Single().GetMembers("Public").Single()));
        IAliasSymbol alias = model.GetAliasInfo(root.DescendantNodes().OfType<IdentifierNameSyntax>().Single(node => node.Identifier.ValueText == "Alias" && node.Parent is not NameEqualsSyntax))!;
        Assert.AreEqual("Private", Visibility(alias));
        foreach ((string name, SpecialType expected) in new[] { ("Field", SpecialType.System_String), ("Property", SpecialType.System_Boolean), ("Method", SpecialType.System_Int32) })
        {
            var actual = (ITypeSymbol)ProductReflection.Call(extensions, null, "GetMemberType", [typeof(ISymbol)], host.GetMembers(name).Single())!;
            Assert.AreEqual(expected, actual.SpecialType);
        }

        var eventType = (ITypeSymbol)ProductReflection.Call(extensions, null, "GetMemberType", [typeof(ISymbol)], host.GetMembers("Changed").Single())!;
        Assert.AreEqual("System.Action", eventType.ToDisplayString());
        Assert.IsNull(ProductReflection.Call(extensions, null, "GetMemberType", [typeof(ISymbol)], host));
        Type methodExtensions = ProductReflection.Type(false, "Analyzer.Utilities.Extensions.IMethodSymbolExtensions");
        Assert.IsFalse((bool)ProductReflection.Call(methodExtensions, null, "IsImplementationOfAnyInterfaceMember", [typeof(ISymbol)], compilation.GlobalNamespace)!);
        Type analyzerExtensions = ProductReflection.Type(false, "MSTest.Analyzers.Helpers.ISymbolExtensions");
        Assert.AreEqual("System.Action", ((ITypeSymbol)ProductReflection.Call(analyzerExtensions, null, "GetReferencedMemberOrLocalOrParameter", [typeof(ISymbol)], host.GetMembers("Changed").Single())!).ToDisplayString());
        Assert.AreEqual(SpecialType.System_Int32, ((ITypeSymbol)ProductReflection.Call(analyzerExtensions, null, "GetReferencedMemberOrLocalOrParameter", [typeof(ISymbol)], method)!).SpecialType);
        Type fixtures = ProductReflection.Type(false, "MSTest.Analyzers.Helpers.FixtureUtils");
        foreach (bool discoverInternals in new[] { false, true })
        {
            Assert.IsFalse((bool)ProductReflection.Call(fixtures, null, "HasValidTestMethodSignature",
                [typeof(IMethodSymbol), typeof(INamedTypeSymbol), typeof(INamedTypeSymbol), typeof(bool)],
                host.GetMembers("Private").Single(), compilation.GetTypeByMetadataName("System.Threading.Tasks.Task"),
                compilation.GetTypeByMetadataName("System.Threading.Tasks.ValueTask"), discoverInternals)!);
        }
    }

    [TestMethod]
    public async Task TypeHelpers_InheritanceConversionsAndNullableTypes_RespectActualRelationships()
    {
        const string source = """
            public interface IItem<T> { }
            public class Base { }
            public class Derived : Base, IItem<int> { }
            public class Holder<T> where T : Derived
            {
                public bool? Flag;
                public int? Number;
            }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source, includeMSTest: false);
        Compilation compilation = (await document.Project.GetCompilationAsync())!;
        AnalyzerContinuationTestHelpers.AssertCompilerErrors(compilation);
        INamedTypeSymbol baseType = compilation.GetTypeByMetadataName("Base")!;
        INamedTypeSymbol derived = compilation.GetTypeByMetadataName("Derived")!;
        INamedTypeSymbol iface = compilation.GetTypeByMetadataName("IItem`1")!;
        INamedTypeSymbol holder = compilation.GetTypeByMetadataName("Holder`1")!;
        ITypeSymbol integer = compilation.GetSpecialType(SpecialType.System_Int32);
        Type extensions = ProductReflection.Type(false, "Analyzer.Utilities.Extensions.ITypeSymbolExtensions");
        bool Inherits(ITypeSymbol? type, ITypeSymbol? candidate)
            => (bool)ProductReflection.Call(extensions, null, "Inherits", [typeof(ITypeSymbol), typeof(ITypeSymbol)], type, candidate)!;
        Assert.IsTrue(Inherits(derived, baseType));
        Assert.IsTrue(Inherits(derived, iface));
        Assert.IsFalse(Inherits(iface, baseType));
        Assert.IsFalse(Inherits(derived, integer));
        Assert.IsFalse(Inherits(null, baseType));
        Assert.IsFalse(Inherits(derived, null));
        Assert.IsTrue((bool)ProductReflection.Call(extensions, null, "IsAssignableTo", [typeof(ITypeSymbol), typeof(ITypeSymbol), typeof(Compilation)], derived, baseType, compilation)!);
        Assert.IsFalse((bool)ProductReflection.Call(extensions, null, "IsAssignableTo", [typeof(ITypeSymbol), typeof(ITypeSymbol), typeof(Compilation)], baseType, derived, compilation)!);
        Assert.IsFalse((bool)ProductReflection.Call(extensions, null, "IsAssignableTo", [typeof(ITypeSymbol), typeof(ITypeSymbol), typeof(Compilation)], null, baseType, compilation)!);
        Assert.IsFalse((bool)ProductReflection.Call(extensions, null, "DerivesFrom", [typeof(ITypeSymbol), typeof(ITypeSymbol), typeof(bool), typeof(bool)], derived, null, false, true)!);
        Assert.IsTrue((bool)ProductReflection.Call(extensions, null, "DerivesFrom", [typeof(ITypeSymbol), typeof(ITypeSymbol), typeof(bool), typeof(bool)], holder.TypeParameters[0], baseType, false, true)!);
        Assert.IsFalse((bool)ProductReflection.Call(extensions, null, "DerivesFrom", [typeof(ITypeSymbol), typeof(ITypeSymbol), typeof(bool), typeof(bool)], holder.TypeParameters[0], baseType, false, false)!);
        ITypeSymbol flag = ((IFieldSymbol)holder.GetMembers("Flag").Single()).Type;
        ITypeSymbol number = ((IFieldSymbol)holder.GetMembers("Number").Single()).Type;
        Assert.IsTrue((bool)ProductReflection.Call(extensions, null, "IsNullableOfBoolean", [typeof(ITypeSymbol)], flag)!);
        Assert.IsFalse((bool)ProductReflection.Call(extensions, null, "IsNullableOfBoolean", [typeof(ITypeSymbol)], number)!);
        Assert.IsFalse((bool)ProductReflection.Call(extensions, null, "IsNullableValueType", [typeof(ITypeSymbol)], [null])!);
    }

    [TestMethod]
    public void ReportDiagnostic_SeverityOrderingAndInvalidValue_AreExplicit()
    {
        Type type = ProductReflection.Type(false, "Microsoft.CodeAnalysis.ReportDiagnosticExtensions");
        ReportDiagnostic[] ordered = [ReportDiagnostic.Default, ReportDiagnostic.Suppress, ReportDiagnostic.Hidden, ReportDiagnostic.Info, ReportDiagnostic.Warn, ReportDiagnostic.Error];
        DiagnosticSeverity?[] expectedSeverities = [null, null, DiagnosticSeverity.Hidden, DiagnosticSeverity.Info, DiagnosticSeverity.Warning, DiagnosticSeverity.Error];
        for (int current = 0; current < ordered.Length; current++)
        {
            Assert.AreEqual(expectedSeverities[current], ProductReflection.Call(type, null, "ToDiagnosticSeverity", [typeof(ReportDiagnostic)], ordered[current]));
            for (int other = 0; other < ordered.Length; other++)
            {
                bool expected = ordered[current] == ReportDiagnostic.Suppress || (current >= 2 && current < other);
                Assert.AreEqual(expected, ProductReflection.Call(type, null, "IsLessSevereThan", [typeof(ReportDiagnostic), typeof(ReportDiagnostic)], ordered[current], ordered[other]),
                    $"{ordered[current]} versus {ordered[other]}");
            }
        }

        Assert.ThrowsExactly<NotImplementedException>(() => ProductReflection.Call(type, null, "ToDiagnosticSeverity", [typeof(ReportDiagnostic)], (ReportDiagnostic)731));
    }

    [TestMethod]
    public async Task NoLocationDiagnostic_WithoutTreeOptions_UsesTheDeclaredSeverity()
    {
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, "public class C { }", includeMSTest: false);
        Compilation compilation = (await document.Project.GetCompilationAsync())!;
        compilation = compilation.WithOptions(compilation.Options.WithSyntaxTreeOptionsProvider(null));
        var rule = new DiagnosticDescriptor("TEST001", "Title", "Observed {0}", "Contract", DiagnosticSeverity.Info, true);
        List<Diagnostic> reported = [];
        Type extensions = ProductReflection.Type(false, "Analyzer.Utilities.Extensions.DiagnosticExtensions");
        ProductReflection.Call(extensions, null, "ReportNoLocationDiagnostic",
            [typeof(Compilation), typeof(DiagnosticDescriptor), typeof(Action<Diagnostic>), typeof(ImmutableDictionary<string, string?>), typeof(object[])],
            compilation, rule, (Action<Diagnostic>)reported.Add, ImmutableDictionary<string, string?>.Empty.Add("key", "value"), new object[] { "payload" });
        Diagnostic diagnostic = Assert.ContainsSingle(reported);
        Assert.AreEqual("TEST001", diagnostic.Id);
        Assert.AreEqual(DiagnosticSeverity.Info, diagnostic.Severity);
        Assert.AreEqual("Observed payload", diagnostic.GetMessage(CultureInfo.InvariantCulture));
        Assert.AreEqual(Location.None, diagnostic.Location);
        Assert.AreEqual("value", diagnostic.Properties["key"]);
    }

    [TestMethod]
    [DataRow(false, false, false)]
    [DataRow(true, false, false)]
    [DataRow(false, true, false)]
    [DataRow(false, false, true)]
    [DataRow(true, true, true)]
    public void DescriptorHelper_IndependentTags_PreserveCustomTagsAndRuleMetadata(bool compilationEnd, bool escalate, bool disable)
    {
        Type type = ProductReflection.Type(false, "MSTest.Analyzers.Helpers.DiagnosticDescriptorHelper");
        Type category = ProductReflection.Type(false, "MSTest.Analyzers.Helpers.Category");
        var descriptor = (DiagnosticDescriptor)ProductReflection.Call(
            type, null, "Create",
            [typeof(string), typeof(LocalizableString), typeof(LocalizableString), typeof(LocalizableString), category, typeof(DiagnosticSeverity), typeof(bool), typeof(bool), typeof(bool), typeof(bool), typeof(string[])],
            "MSTEST9999", (LocalizableString)"Title", (LocalizableString)"Message {0}", (LocalizableString)"Description",
            Enum.Parse(category, "Usage"), DiagnosticSeverity.Info, false, compilationEnd, escalate, disable, new[] { "UserTag" })!;
        List<string> expectedTags = ["UserTag"];
        if (compilationEnd)
        {
            expectedTags.Add(WellKnownDiagnosticTags.CompilationEnd);
        }

        if (escalate)
        {
            expectedTags.Add("EscalateToErrorInRecommended");
        }

        if (disable)
        {
            expectedTags.Add("DisableInAllMode");
        }

        CollectionAssert.AreEqual(expectedTags.ToArray(), descriptor.CustomTags.ToArray());
        Assert.AreEqual("MSTEST9999", descriptor.Id);
        Assert.AreEqual("Usage", descriptor.Category);
        Assert.AreEqual(DiagnosticSeverity.Info, descriptor.DefaultSeverity);
        Assert.IsFalse(descriptor.IsEnabledByDefault);
        Assert.AreEqual("https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest9999", descriptor.HelpLinkUri);
        Assert.AreEqual("Message payload", Diagnostic.Create(descriptor, Location.None, "payload").GetMessage(CultureInfo.InvariantCulture));
    }

    [TestMethod]
    public async Task DiagnosticHelpers_Overloads_PreserveSourceLocationsAndMetadata()
    {
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, "public class C { public int Get() => 17; }", includeMSTest: false);
        SyntaxNode root = (await document.GetSyntaxRootAsync())!;
        SemanticModel model = (await document.GetSemanticModelAsync())!;
        LiteralExpressionSyntax literal = root.DescendantNodes().OfType<LiteralExpressionSyntax>().Single();
        IOperation operation = model.GetOperation(literal)!;
        ISymbol symbol = model.GetDeclaredSymbol(root.DescendantNodes().OfType<MethodDeclarationSyntax>().Single())!;
        Type extensions = ProductReflection.Type(false, "Analyzer.Utilities.Extensions.DiagnosticExtensions");
        var rule = new DiagnosticDescriptor("TEST001", "Title", "Observed {0}", "Contract", DiagnosticSeverity.Warning, true);
        ImmutableDictionary<string, string?> properties = ImmutableDictionary<string, string?>.Empty.Add("key", "value");
        ImmutableArray<Location> extraLocations = [symbol.Locations.Single()];
        foreach ((Type firstType, object first) in new (Type, object)[] { (typeof(IOperation), operation), (typeof(SyntaxToken), literal.Token), (typeof(ISymbol), symbol), (typeof(SyntaxReference), literal.GetReference()) })
        {
            Type[] signature = firstType == typeof(SyntaxReference)
                ? [firstType, typeof(DiagnosticDescriptor), typeof(CancellationToken), typeof(object[])]
                : [firstType, typeof(DiagnosticDescriptor), typeof(object[])];
            object?[] arguments = firstType == typeof(SyntaxReference) ? [first, rule, CancellationToken.None, new object[] { "payload" }] : [first, rule, new object[] { "payload" }];
            var diagnostic = (Diagnostic)ProductReflection.Call(extensions, null, "CreateDiagnostic", signature, arguments)!;
            Assert.AreEqual("TEST001", diagnostic.Id);
            Assert.AreEqual("Observed payload", diagnostic.GetMessage(CultureInfo.InvariantCulture));
            Assert.AreEqual(firstType == typeof(ISymbol) ? "Get" : "17", SourceText(diagnostic.Location));
            Assert.AreEqual(DiagnosticSeverity.Warning, diagnostic.Severity);
        }

        var withProperties = (Diagnostic)ProductReflection.Call(extensions, null, "CreateDiagnostic",
            [typeof(IOperation), typeof(DiagnosticDescriptor), typeof(ImmutableDictionary<string, string?>), typeof(object[])],
            operation, rule, properties, new object[] { "property" })!;
        Assert.AreEqual("value", withProperties.Properties["key"]);
        var withLocations = (Diagnostic)ProductReflection.Call(extensions, null, "CreateDiagnostic",
            [typeof(IOperation), typeof(DiagnosticDescriptor), typeof(ImmutableArray<Location>), typeof(ImmutableDictionary<string, string?>), typeof(object[])],
            operation, rule, extraLocations, properties, new object[] { "extra" })!;
        Assert.AreEqual("Get", SourceText(Assert.ContainsSingle(withLocations.AdditionalLocations)));
        var noSource = (Diagnostic)ProductReflection.Call(extensions, null, "CreateDiagnostic",
            [typeof(IEnumerable<Location>), typeof(DiagnosticDescriptor), typeof(object[])], new[] { Location.None }, rule, new object[] { "none" })!;
        Assert.AreEqual(Location.None, noSource.Location);
        Assert.AreEqual("Observed none", noSource.GetMessage(CultureInfo.InvariantCulture));
        var metadata = (Diagnostic)ProductReflection.Call(extensions, null, "CreateDiagnostic",
            [typeof(Location), typeof(DiagnosticDescriptor), typeof(object[])],
            Location.Create("metadata.dll", new TextSpan(0, 1), new LinePositionSpan(new LinePosition(0, 0), new LinePosition(0, 1))), rule, new object[] { "metadata" })!;
        Assert.AreEqual(Location.None, metadata.Location);
        Assert.AreEqual("Observed metadata", metadata.GetMessage(CultureInfo.InvariantCulture));

        static string SourceText(Location location) => location.SourceTree!.GetText().ToString(location.SourceSpan);
    }

    [TestMethod]
    [DataRow("A9.Z_name.Thing`1", "A9", "Z_name", DisplayName = "AsciiGenericType")]
    [DataRow("Ā.a\u0301.Thing", "Ā", "a\u0301", DisplayName = "UnicodeLetterAndCombiningMark")]
    [DataRow("A.\u200cB.Thing", "A", "\u200cB", DisplayName = "UnicodeFormatCharacter")]
    [DataRow("A.B.Outer+Inner", "A", "B", DisplayName = "NestedType")]
    public void MetadataNameParsing_UnicodeAndNestedNames_ReturnOnlyNamespaceSegments(string metadataName, string first, string second)
    {
        Type type = ProductReflection.Type(false, "Analyzer.Utilities.WellKnownTypeProvider");
        var result = (ImmutableArray<string>)ProductReflection.Call(type, null, "GetNamespaceNamesFromFullTypeName", [typeof(string)], metadataName)!;
        CollectionAssert.AreEqual(new[] { first, second }, result.ToArray());
    }

    [TestMethod]
    [DataRow('@', false, DisplayName = "BeforeUppercase")]
    [DataRow('A', true, DisplayName = "FirstUppercase")]
    [DataRow('Z', true, DisplayName = "LastUppercase")]
    [DataRow('[', false, DisplayName = "AfterUppercase")]
    [DataRow('_', true, DisplayName = "Underscore")]
    [DataRow('`', false, DisplayName = "BeforeLowercase")]
    [DataRow('a', true, DisplayName = "FirstLowercase")]
    [DataRow('z', true, DisplayName = "LastLowercase")]
    [DataRow('{', false, DisplayName = "AfterLowercase")]
    [DataRow('/', false, DisplayName = "BeforeDigits")]
    [DataRow('0', true, DisplayName = "FirstDigit")]
    [DataRow('9', true, DisplayName = "LastDigit")]
    [DataRow(':', false, DisplayName = "AfterDigits")]
    [DataRow('\u007f', false, DisplayName = "AsciiDel")]
    [DataRow('\u00a0', false, DisplayName = "UnicodeSpace")]
    [DataRow('\u00e9', true, DisplayName = "UnicodeLetter")]
    [DataRow('\u0100', true, DisplayName = "UnicodeUppercaseLetter")]
    [DataRow('\u01c5', true, DisplayName = "UnicodeTitlecaseLetter")]
    [DataRow('\u02b0', true, DisplayName = "UnicodeModifierLetter")]
    [DataRow('\u4e00', true, DisplayName = "UnicodeOtherLetter")]
    [DataRow('\u2160', true, DisplayName = "UnicodeLetterNumber")]
    [DataRow('\u0660', true, DisplayName = "UnicodeDecimalDigit")]
    [DataRow('\u203f', true, DisplayName = "UnicodeConnectorPunctuation")]
    [DataRow('\u0301', true, DisplayName = "CombiningMark")]
    [DataRow('\u0903', true, DisplayName = "UnicodeSpacingCombiningMark")]
    [DataRow('\u200c', true, DisplayName = "FormatCharacter")]
    [DataRow('\u20dd', false, DisplayName = "UnicodeEnclosingMark")]
    [DataRow('\u00b2', false, DisplayName = "UnicodeOtherNumber")]
    [DataRow('\u0085', false, DisplayName = "UnicodeControl")]
    [DataRow('\u0378', false, DisplayName = "UnicodeUnassigned")]
    [DataRow('\ue000', false, DisplayName = "UnicodePrivateUse")]
    [DataRow('\ud800', false, DisplayName = "UnicodeIsolatedSurrogate")]
    [DataRow('\u00d7', false, DisplayName = "UnicodeMathSymbol")]
    [DataRow('\u00a3', false, DisplayName = "UnicodeCurrencySymbol")]
    [DataRow('\u00a8', false, DisplayName = "UnicodeModifierSymbol")]
    [DataRow('\u2603', false, DisplayName = "UnicodeOtherSymbol")]
    [DataRow('\u2028', false, DisplayName = "UnicodeLineSeparator")]
    [DataRow('\u2029', false, DisplayName = "UnicodeParagraphSeparator")]
    [DataRow('\u2010', false, DisplayName = "UnicodeDashPunctuation")]
    [DataRow('\u3008', false, DisplayName = "UnicodeOpeningPunctuation")]
    [DataRow('\u3009', false, DisplayName = "UnicodeClosingPunctuation")]
    [DataRow('\u2018', false, DisplayName = "UnicodeInitialQuote")]
    [DataRow('\u2019', false, DisplayName = "UnicodeFinalQuote")]
    [DataRow('\u00a1', false, DisplayName = "UnicodeOtherPunctuation")]
    public void IdentifierCharacter_BoundariesAndUnicode_AreClassifiedPrecisely(char value, bool expected)
    {
        Type type = ProductReflection.Type(false, "Analyzer.Utilities.WellKnownTypeProvider");
        Assert.AreEqual(expected, ProductReflection.Call(type, null, "IsIdentifierPartCharacter", [typeof(char)], value));
    }

    [TestMethod]
    public async Task WellKnownTypes_VisibleDuplicatesAndTaskArguments_RespectAmbiguityAndPredicate()
    {
        MetadataReference first = await AnalyzerContinuationTestHelpers.EmitReferenceAsync("namespace Duplicate { public class Target { } }", "First");
        MetadataReference second = await AnalyzerContinuationTestHelpers.EmitReferenceAsync("namespace Duplicate { public class Target { } }", "Second");
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, "public class Consumer { }",
            includeMSTest: false, additionalReferences: [first, second]);
        Compilation compilation = (await document.Project.GetCompilationAsync())!;
        Type type = ProductReflection.Type(false, "Analyzer.Utilities.WellKnownTypeProvider");
        object provider = ProductReflection.Call(type, null, "GetOrCreate", [typeof(Compilation)], compilation)!;
        Assert.IsNull(ProductReflection.Call(type, provider, "GetOrCreateTypeByMetadataName", [typeof(string)], "Duplicate.Target"));
        Assert.IsNull(ProductReflection.Call(type, provider, "GetOrCreateTypeByMetadataName", [typeof(string)], "Duplicate.Target"));
        ITypeSymbol integer = compilation.GetSpecialType(SpecialType.System_Int32);
        INamedTypeSymbol task = compilation.GetTypeByMetadataName("System.Threading.Tasks.Task`1")!.Construct(integer);
        Func<ITypeSymbol, bool> isInteger = candidate => candidate.SpecialType == SpecialType.System_Int32;
        Assert.IsTrue((bool)ProductReflection.Call(type, provider, "IsTaskOfType", [typeof(ITypeSymbol), typeof(Func<ITypeSymbol, bool>)], task, isInteger)!);
        Assert.IsFalse((bool)ProductReflection.Call(type, provider, "IsTaskOfType", [typeof(ITypeSymbol), typeof(Func<ITypeSymbol, bool>)], task, (Func<ITypeSymbol, bool>)(_ => false))!);
        Assert.IsFalse((bool)ProductReflection.Call(type, provider, "IsTaskOfType", [typeof(ITypeSymbol), typeof(Func<ITypeSymbol, bool>)], integer, isInteger)!);
        Assert.IsFalse((bool)ProductReflection.Call(type, provider, "IsTaskOfType", [typeof(ITypeSymbol), typeof(Func<ITypeSymbol, bool>)], null, isInteger)!);
    }

    [TestMethod]
    public async Task DataRowTypeMapping_MapsEveryScalarAndRejectsUnsupportedTypes()
    {
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, "public class Unsupported { } public enum ByteEnum : byte { Value = 17 }", includeMSTest: false);
        Compilation compilation = (await document.Project.GetCompilationAsync())!;
        foreach ((SpecialType specialType, Type expected) in new[]
        {
            (SpecialType.System_Boolean, typeof(bool)),
            (SpecialType.System_Byte, typeof(byte)),
            (SpecialType.System_Char, typeof(char)),
            (SpecialType.System_Decimal, typeof(decimal)),
            (SpecialType.System_Double, typeof(double)),
            (SpecialType.System_Int16, typeof(short)),
            (SpecialType.System_Int32, typeof(int)),
            (SpecialType.System_Int64, typeof(long)),
            (SpecialType.System_IntPtr, typeof(nint)),
            (SpecialType.System_Object, typeof(object)),
            (SpecialType.System_SByte, typeof(sbyte)),
            (SpecialType.System_Single, typeof(float)),
            (SpecialType.System_String, typeof(string)),
            (SpecialType.System_UInt16, typeof(ushort)),
            (SpecialType.System_UInt32, typeof(uint)),
            (SpecialType.System_UInt64, typeof(ulong)),
            (SpecialType.System_UIntPtr, typeof(nuint)),
        })
        {
            Assert.AreEqual(expected, ProductReflection.Call(typeof(DataRowShouldBeValidAnalyzer), null, "GetSystemType",
                [typeof(ITypeSymbol)], compilation.GetSpecialType(specialType)));
        }

        Assert.AreEqual(typeof(Type), ProductReflection.Call(typeof(DataRowShouldBeValidAnalyzer), null, "GetSystemType",
            [typeof(ITypeSymbol)], compilation.GetTypeByMetadataName("System.Type")));
        Assert.AreEqual(typeof(byte), ProductReflection.Call(typeof(DataRowShouldBeValidAnalyzer), null, "GetSystemType",
            [typeof(ITypeSymbol)], compilation.GetTypeByMetadataName("ByteEnum")));
        ArgumentException error = Assert.ThrowsExactly<ArgumentException>(() => ProductReflection.Call(
            typeof(DataRowShouldBeValidAnalyzer), null, "GetSystemType",
            [typeof(ITypeSymbol)], compilation.GetTypeByMetadataName("Unsupported")));
        Assert.AreEqual("Unexpected SpecialType 'None'.", error.Message);
    }

    [TestMethod]
    public async Task OperationHelpers_BuiltInConversions_RetainReferenceIdentity()
    {
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(
            workspace,
            "public class C { public object Check(int value) => (object)value; }", includeMSTest: false);
        SyntaxNode root = (await document.GetSyntaxRootAsync())!;
        SemanticModel model = (await document.GetSemanticModelAsync())!;
        IOperation conversion = model.GetOperation(root.DescendantNodes().OfType<CastExpressionSyntax>().Single())!;
        IOperation parameter = ((IConversionOperation)conversion).Operand;
        Type extensions = ProductReflection.Type(false, "MSTest.Analyzers.RoslynAnalyzerHelpers.IOperationExtensions");
        var referenced = (ISymbol)ProductReflection.Call(extensions, null, "GetReferencedMemberOrLocalOrParameter", [typeof(IOperation)], conversion)!;
        Assert.AreEqual(SymbolKind.Parameter, referenced.Kind);
        Assert.AreEqual("value", referenced.Name);
        Assert.IsTrue((bool)ProductReflection.Call(extensions, null, "IsEquivalentReferenceTo", [typeof(IOperation), typeof(IOperation)], conversion, parameter)!);
        Assert.IsFalse((bool)ProductReflection.Call(extensions, null, "IsEquivalentReferenceTo", [typeof(IOperation), typeof(IOperation)], conversion, null)!);
        Assert.AreSame(parameter, ProductReflection.Call(extensions, null, "WalkDownBuiltInConversion", [typeof(IOperation)], conversion));
    }

    [TestMethod]
    public void PerformanceAttribute_ConstructorPolicyAndUsage_ArePreserved()
    {
        Type type = ProductReflection.Type(false, "Roslyn.Utilities.PerformanceSensitiveAttribute");
        object attribute = ProductReflection.Create(type, [typeof(string)], "https://example.invalid/performance");
        Assert.AreEqual("https://example.invalid/performance", ProductReflection.Get<string>(attribute, "Uri"));
        Assert.IsNull(ProductReflection.Get<string?>(attribute, "Constraint"));
        type.GetProperty("Constraint")!.SetValue(attribute, "No allocations on the hot path");
        Assert.AreEqual("No allocations on the hot path", ProductReflection.Get<string>(attribute, "Constraint"));
        string[] flags = ["AllowCaptures", "AllowImplicitBoxing", "AllowGenericEnumeration", "AllowLocks", "OftenCompletesSynchronously", "IsParallelEntry"];
        foreach (string flag in flags)
        {
            Assert.IsFalse(ProductReflection.Get<bool>(attribute, flag), flag);
        }

        for (int selected = 0; selected < flags.Length; selected++)
        {
            type.GetProperty(flags[selected])!.SetValue(attribute, true);
            CollectionAssert.AreEqual(
                Enumerable.Range(0, flags.Length).Select(index => index == selected).ToArray(),
                flags.Select(flag => ProductReflection.Get<bool>(attribute, flag)).ToArray());
            type.GetProperty(flags[selected])!.SetValue(attribute, false);
        }

        AttributeUsageAttribute usage = type.GetCustomAttribute<AttributeUsageAttribute>()!;
        Assert.AreEqual(AttributeTargets.Constructor | AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Field, usage.ValidOn);
        Assert.IsTrue(usage.AllowMultiple);
        Assert.IsFalse(usage.Inherited);
        Assert.AreEqual("EMIT_CODE_ANALYSIS_ATTRIBUTES", type.GetCustomAttribute<ConditionalAttribute>()!.ConditionString);
    }

    [TestMethod]
    [DataRow("typeof(string)", "typeof(string)", true, DisplayName = "SameTypeConstant")]
    [DataRow("typeof(string)", "typeof(int)", false, DisplayName = "DifferentTypeConstants")]
    [DataRow("new int[] { 17, 29 }", "new int[] { 17, 29 }", true, DisplayName = "SameArrayValues")]
    [DataRow("new int[] { 17, 29 }", "new int[] { 17 }", false, DisplayName = "DifferentArrayLengths")]
    [DataRow("new int[] { 17, 29 }", "new int[] { 29, 17 }", false, DisplayName = "DifferentArrayOrder")]
    [DataRow("new int[] { }", "new int[] { }", true, DisplayName = "EmptyArrays")]
    [DataRow("new object[] { new int[] { 17 }, typeof(string) }", "new object[] { new int[] { 17 }, typeof(string) }", true, DisplayName = "NestedArrayAndType")]
    [DataRow("17, Label = \"one\"", "17, Label = \"two\"", false, DisplayName = "DifferentNamedValues")]
    [DataRow("17, Label = \"one\"", "17, Other = \"one\"", false, DisplayName = "DifferentNamedKeys")]
    public async Task AttributeConstantEquivalence_RecursesThroughTypesArraysAndNamedArguments(string left, string right, bool expected)
    {
        string source = $$"""
            using System;
            public class ContractAttribute : Attribute
            {
                public ContractAttribute(object value) { }
                public string Label { get; set; }
                public string Other { get; set; }
            }
            [Contract({{left}})] public class Left { }
            [Contract({{right}})] public class Right { }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source, includeMSTest: false);
        Compilation compilation = (await document.Project.GetCompilationAsync())!;
        AnalyzerContinuationTestHelpers.AssertCompilerErrors(compilation);
        AttributeData first = compilation.GetTypeByMetadataName("Left")!.GetAttributes().Single();
        AttributeData second = compilation.GetTypeByMetadataName("Right")!.GetAttributes().Single();
        Assert.AreEqual(expected, ProductReflection.Call(typeof(RedundantTestMethodAttributeAnalyzer), null, "AreArgumentsEquivalent",
            [typeof(AttributeData), typeof(AttributeData)], first, second));
    }
}
