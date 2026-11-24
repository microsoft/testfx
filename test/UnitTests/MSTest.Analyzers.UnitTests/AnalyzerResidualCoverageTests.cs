// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class AnalyzerResidualCoverageTests
{
    private const string ConditionContracts = """
        namespace Microsoft.VisualStudio.TestTools.UnitTesting
        {
            public class TestMethodAttribute : System.Attribute { }
            public abstract class ConditionBaseAttribute : System.Attribute { }
            public sealed class ArchitectureConditionAttribute : ConditionBaseAttribute { }
            public enum TestArchitectures { X64 = 2 }
            public sealed class CIConditionAttribute : ConditionBaseAttribute { }
            public sealed class ExecutableConditionAttribute : ConditionBaseAttribute { }
            public static class Assert { }
        }
        """;

    [TestMethod]
    [DataRow("ArgumentOrder")]
    [DataRow("ReferenceEquality")]
    [DataRow("ProperAssertions")]
    [DataRow("ThrowsStatements")]
    [DataRow("AlwaysTrue")]
    [DataRow("Negation")]
    public async Task AssertionAnalyzers_NonstandardSignatures_DoNotInventOperands(string scenario)
    {
        const string source = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                public static class Assert
                {
                    public static void AreEqual() { }
                    public static void AreEqual(object observation, object result) { }
                    public static void AreNotEqual(object expected, string message) { }
                    public static void AreSame(object expected) { }
                    public static void AreNotSame() { }
                    public static void HasCount(int expected) { }
                    public static void Throws() { }
                    public static void DoesNotThrow() { }
                    public static void IsTrue(bool success) { }
                }
            }
            public class Consumer
            {
                public void Check(object value)
                {
                    Assert.AreEqual();
                    Assert.AreEqual(value, value);
                    Assert.AreNotEqual(value, "explanation");
                    Assert.AreSame(value);
                    Assert.AreNotSame();
                    Assert.HasCount(0);
                    Assert.Throws();
                    Assert.DoesNotThrow();
                    Assert.IsTrue(!(value is null));
                }
            }
            """;
        DiagnosticAnalyzer analyzer = scenario switch
        {
            "ArgumentOrder" => new AssertionArgsShouldBePassedInCorrectOrderAnalyzer(),
            "ReferenceEquality" => new AvoidAssertAreSameWithValueTypesAnalyzer(),
            "ProperAssertions" => new UseProperAssertMethodsAnalyzer(),
            "ThrowsStatements" => new AssertThrowsShouldContainSingleStatementAnalyzer(),
            "AlwaysTrue" => new ReviewAlwaysTrueAssertConditionAnalyzer(),
            "Negation" => new DoNotNegateBooleanAssertionAnalyzer(),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario)),
        };
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source, includeMSTest: false);

        Assert.IsEmpty(await AnalyzerContinuationTestHelpers.AnalyzeAsync(document, analyzer));
    }

    [TestMethod]
    [DataRow("String")]
    [DataRow("Collection")]
    public async Task LegacyAssertionMigration_UnknownMethodsAndMissingOperands_DoNotOfferLossyMigration(string scenario)
    {
        const string source = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                public static class StringAssert
                {
                    public static void Contains(string value) { }
                    public static void Other(string value, string pattern) { }
                }
                public static class CollectionAssert
                {
                    public static void Contains(object collection) { }
                    public static void AreEquivalent(object collection) { }
                    public static void AllItemsAreInstancesOfType(object collection) { }
                    public static void Other(object collection, object item) { }
                }
            }
            public class Consumer
            {
                public void Check()
                {
                    StringAssert.Contains("value");
                    StringAssert.Other("value", "pattern");
                    CollectionAssert.Contains(new int[] { 1, 2 });
                    CollectionAssert.AreEquivalent(new int[] { 1, 2 });
                    CollectionAssert.AllItemsAreInstancesOfType(new int[] { 1, 2 });
                    CollectionAssert.Other(new int[] { 1, 2 }, 1);
                }
            }
            """;
        DiagnosticAnalyzer analyzer = scenario == "String" ? new StringAssertToAssertAnalyzer() : new CollectionAssertToAssertAnalyzer();
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source, includeMSTest: false);

        Assert.IsEmpty(await AnalyzerContinuationTestHelpers.AnalyzeAsync(document, analyzer));
    }

    [TestMethod]
    [DataRow("object", true, DisplayName = "OrdinaryComparerParameter")]
    [DataRow("Custom.IComparer", true, DisplayName = "NamesakeComparerIsNotBcl")]
    [DataRow("System.IComparable", true, DisplayName = "UnrelatedBclInterface")]
    [DataRow("System.Collections.IComparer", false, DisplayName = "OrderingComparerCannotBeMigrated")]
    [DataRow("System.Collections.Generic.IEqualityComparer<int>", false, DisplayName = "EqualityComparerCannotBeMigrated")]
    public async Task CollectionMigration_ComparerIdentityControlsWhetherMigrationIsSafe(string comparerType, bool reports)
    {
        string source = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            namespace Custom { public interface IComparer { } }
            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                public static class CollectionAssert
                {
                    public static void AreEqual(object expected, object actual, {{comparerType}} comparer) { }
                }
            }
            public class Consumer
            {
                public void Check({{comparerType}} comparer)
                {
                    CollectionAssert.AreEqual(new int[] { 1, 2 }, new int[] { 1, 2 }, comparer);
                }
            }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source, includeMSTest: false);
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerContinuationTestHelpers.AnalyzeAsync(document, new CollectionAssertToAssertAnalyzer());

        if (!reports)
        {
            Assert.IsEmpty(diagnostics);
            return;
        }

        AssertSingleDiagnostic(diagnostics, source, "MSTEST0068", DiagnosticSeverity.Info,
            "CollectionAssert.AreEqual(new int[] { 1, 2 }, new int[] { 1, 2 }, comparer)",
            "Use 'Assert.AreSequenceEqual' instead of 'CollectionAssert.AreEqual'",
            [("ProperAssertMethodNameKey", "AreSequenceEqual"), ("FixKindKey", "Simple")]);
    }

    [TestMethod]
    [DataRow("Any()", DisplayName = "AnyWithoutCollection")]
    [DataRow("Any(1, 2, 3)", DisplayName = "AnyWithExtraOperands")]
    [DataRow("Count()", DisplayName = "CountWithoutCollection")]
    [DataRow("Count(1, 2, 3)", DisplayName = "CountWithExtraOperands")]
    public async Task ProperAssertions_NonstandardLinqSignatures_AreNotTreatedAsCollectionPredicates(string call)
    {
        string assertion = call.StartsWith("Any", StringComparison.Ordinal)
            ? $"Assert.IsTrue(System.Linq.Enumerable.{call})"
            : $"Assert.AreEqual(1, System.Linq.Enumerable.{call})";
        string source = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            namespace System.Linq
            {
                public static class Enumerable
                {
                    public static bool Any() => true;
                    public static bool Any(int first, int second, int third) => true;
                    public static int Count() => 1;
                    public static int Count(int first, int second, int third) => 1;
                }
            }
            public class Consumer { public void Check() { {{assertion}}; } }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source);

        Assert.IsEmpty(await AnalyzerContinuationTestHelpers.AnalyzeAsync(document, new UseProperAssertMethodsAnalyzer()));
    }

    [TestMethod]
    [DataRow("Culture")]
    [DataRow("Directory")]
    [DataRow("File")]
    [DataRow("Process")]
    [DataRow("Blocking")]
    public async Task RuntimeDependentAnalyzers_EmptyReferenceGraph_CompleteWithoutDiagnostics(string scenario)
    {
        DiagnosticAnalyzer analyzer = CreateRuntimeAnalyzer(scenario);
        var compilation = CSharpCompilation.Create(
            "MissingRuntimeContracts",
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithSpecificDiagnosticOptions(analyzer.SupportedDiagnostics.ToDictionary(rule => rule.Id, _ => ReportDiagnostic.Info)));
        AnalyzerContinuationTestHelpers.AssertCompilerErrors(compilation);

        ImmutableArray<Diagnostic> diagnostics = await compilation.WithAnalyzers([analyzer]).GetAnalyzerDiagnosticsAsync();

        Assert.IsEmpty(diagnostics, string.Join(Environment.NewLine, diagnostics));
    }

    [TestMethod]
    [DataRow("Culture")]
    [DataRow("Directory")]
    [DataRow("File")]
    [DataRow("Process")]
    public async Task ParallelSafety_ForcedParallelizationWithoutTestContracts_DoesNotAnalyzeUnrelatedCode(string scenario)
    {
        const string source = "public class Unrelated { public static string Describe() => \"value\"; }";
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source, includeMSTest: false);
        Compilation compilation = (await document.Project.GetCompilationAsync())!;
        Assert.IsNull(compilation.GetTypeByMetadataName("Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute"));
        Assert.IsNotNull(compilation.GetTypeByMetadataName("System.Globalization.CultureInfo"));
        Assert.IsNotNull(compilation.GetTypeByMetadataName("System.Environment"));
        Assert.IsNotNull(compilation.GetTypeByMetadataName("System.IO.File"));

        Assert.IsEmpty(await AnalyzerContinuationTestHelpers.AnalyzeAsync(document, CreateRuntimeAnalyzer(scenario),
            new AnalyzerOptions([], new FixedOptionsProvider(("mstest_parallel_safety_mode", "always")))));
    }

    [TestMethod]
    [DataRow("Architecture")]
    [DataRow("CI")]
    [DataRow("Executable")]
    public async Task ConditionMigration_MissingRuntimeMember_DoesNotRegisterAnUnsafeRewrite(string scenario)
    {
        (DiagnosticAnalyzer Analyzer, string RuntimeContract) scenarioData = scenario switch
        {
            "Architecture" => (new UseArchitectureConditionAttributeInsteadOfRuntimeCheckAnalyzer(),
                "namespace System.Runtime.InteropServices { public static class RuntimeInformation { } }"),
            "CI" => (new UseCIConditionAttributeInsteadOfEnvironmentCheckAnalyzer(),
                "namespace System { public static class Environment { } }"),
            "Executable" => (new UseExecutableConditionAttributeInsteadOfProcessCheckAnalyzer(),
                "namespace System.IO { public static class File { public static bool Exists(int descriptor) => false; } }"),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario)),
        };
        (DiagnosticAnalyzer? analyzer, string? runtimeContract) = scenarioData;
        string source = ConditionContracts + Environment.NewLine + runtimeContract + Environment.NewLine
            + "public class Consumer { [Microsoft.VisualStudio.TestTools.UnitTesting.TestMethod] public void Check() { } }";
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source, includeMSTest: false);

        Assert.IsEmpty(await AnalyzerContinuationTestHelpers.AnalyzeAsync(document, analyzer));
    }

    [TestMethod]
    [DataRow("OS")]
    [DataRow("Blocking")]
    public async Task RuntimeAnalyzers_MinimalCoreLibrary_DoesNotInventMissingPlatformOrTaskAPIs(string scenario)
    {
        const string source = """
            namespace System
            {
                public class Object { }
                public abstract class ValueType { }
                public class Attribute { }
                public struct Void { }
                public struct Boolean { }
                public struct Int32 { }
                public sealed class String { }
            }
            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                public class TestMethodAttribute : System.Attribute { }
                public static class Assert { }
            }
            """;
        DiagnosticAnalyzer analyzer = scenario == "OS"
            ? new UseOSConditionAttributeInsteadOfRuntimeCheckAnalyzer()
            : new AvoidThreadSleepAndTaskWaitInTestsAnalyzer();
        var compilation = CSharpCompilation.Create(
            "ReferenceSurfaceWithoutPlatformApis",
            [CSharpSyntaxTree.ParseText(source)],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithSpecificDiagnosticOptions(analyzer.SupportedDiagnostics.ToDictionary(rule => rule.Id, _ => ReportDiagnostic.Info)));
        AnalyzerContinuationTestHelpers.AssertCompilerErrors(compilation);
        Assert.IsNotNull(compilation.GetTypeByMetadataName("Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute"));
        Assert.IsNull(compilation.GetTypeByMetadataName("System.OperatingSystem"));
        Assert.IsNull(compilation.GetTypeByMetadataName("System.Runtime.InteropServices.RuntimeInformation"));

        ImmutableArray<Diagnostic> diagnostics = await compilation.WithAnalyzers([analyzer]).GetAnalyzerDiagnosticsAsync();

        Assert.IsEmpty(diagnostics, string.Join(Environment.NewLine, diagnostics));
    }

    [TestMethod]
    [DataRow("ArchitectureVariable")]
    [DataRow("CITextComparison")]
    [DataRow("OSBooleanVariable")]
    [DataRow("OSPlatformVariable")]
    [DataRow("OSEmptyBody")]
    public async Task ConditionMigration_UnrecognizedGuardIsSilentWhileRecognizedGuardRetainsItsProperties(string scenario)
    {
        (DiagnosticAnalyzer Analyzer, string Rejected, string Accepted, string Id, string Message, (string, string?)[] Properties) scenarioData = scenario switch
        {
            "ArchitectureVariable" => (
                new UseArchitectureConditionAttributeInsteadOfRuntimeCheckAnalyzer(),
                "if (RuntimeInformation.ProcessArchitecture == expected) return;",
                "if (RuntimeInformation.ProcessArchitecture == Architecture.X64) return;",
                "MSTEST0079",
                "Use '[ArchitectureCondition]' attribute instead of 'RuntimeInformation.ProcessArchitecture' checks with early return or 'Assert.Inconclusive'",
                [("IsNegatedKey", "False"), ("ArchitectureKey", "X64")]),
            "CITextComparison" => (
                new UseCIConditionAttributeInsteadOfEnvironmentCheckAnalyzer(),
                "if (Environment.GetEnvironmentVariable(\"CI\") == \"true\") return;",
                "if (Environment.GetEnvironmentVariable(\"CI\") is null) return;",
                "MSTEST0080",
                "Use '[CICondition]' attribute instead of 'Environment.GetEnvironmentVariable' checks with early return or 'Assert.Inconclusive'",
                [("ConditionModeKey", "Include")]),
            _ => (
                new UseOSConditionAttributeInsteadOfRuntimeCheckAnalyzer(),
                scenario switch
                {
                    "OSBooleanVariable" => "if (ready) return;",
                    "OSPlatformVariable" => "if (RuntimeInformation.IsOSPlatform(platform)) return;",
                    "OSEmptyBody" => "if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) { }",
                    _ => throw new ArgumentOutOfRangeException(nameof(scenario)),
                },
                "if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;",
                "MSTEST0061",
                "Use '[OSCondition]' attribute instead of 'RuntimeInformation.IsOSPlatform' calls with early return or 'Assert.Inconclusive'",
                [("IsNegatedKey", "False"), ("OSPlatformKey", "Windows")]),
        };
        (DiagnosticAnalyzer? analyzer, string? rejected, string? accepted, string? id, string? message, (string, string?)[]? properties) = scenarioData;
        string source = $$"""
            using System;
            using System.Runtime.InteropServices;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            {{ConditionContracts}}
            public class Consumer
            {
                [TestMethod]
                public void Rejected(Architecture expected, OSPlatform platform, bool ready)
                {
                    {{rejected}}
                    GC.KeepAlive(42);
                }
                [TestMethod]
                public void Accepted()
                {
                    {{accepted}}
                    GC.KeepAlive(42);
                }
            }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source, includeMSTest: false);

        AssertSingleDiagnostic(
            await AnalyzerContinuationTestHelpers.AnalyzeAsync(document, analyzer),
            source, id, DiagnosticSeverity.Info, accepted, message, properties);
    }

    [TestMethod]
    public async Task BlockingCalls_LocalFunctionsAndLambdas_InheritOnlyTheirEnclosingTestContract()
    {
        const string source = """
            using System;
            using System.Threading;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                public class TestMethodAttribute : Attribute { }
            }
            public class Consumer
            {
                [TestMethod]
                public void Check()
                {
                    void TestLocal() { Thread.Sleep(1); }
                    Action callback = () => Thread.Sleep(2);
                    TestLocal();
                    callback();
                }
                public void Helper()
                {
                    void HelperLocal() { Thread.Sleep(3); }
                    HelperLocal();
                }
            }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source, includeMSTest: false);
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerContinuationTestHelpers.AnalyzeAsync(
            document, new AvoidThreadSleepAndTaskWaitInTestsAnalyzer(), enableDisabledRules: true);
        Assert.HasCount(2, diagnostics);
        for (int i = 0; i < diagnostics.Length; i++)
        {
            AnalyzerContinuationTestHelpers.AssertDiagnostic(diagnostics[i], "MSTEST0067", DiagnosticSeverity.Info, source,
                $"Thread.Sleep({i + 1})",
                "Avoid 'Thread.Sleep' in test code as it can cause test flakiness; consider an asynchronous alternative such as 'await Task.Delay' or 'await' the task");
            Assert.IsEmpty(diagnostics[i].Properties);
            Assert.IsEmpty(diagnostics[i].AdditionalLocations);
        }

        Compilation compilation = (await document.Project.GetCompilationAsync())!;
        SemanticModel model = (await document.GetSemanticModelAsync())!;
        SyntaxNode root = (await document.GetSyntaxRootAsync())!;
        INamedTypeSymbol testMethod = compilation.GetTypeByMetadataName("Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute")!;
        foreach (LocalFunctionStatementSyntax local in root.DescendantNodes().OfType<LocalFunctionStatementSyntax>())
        {
            Assert.AreEqual(local.Identifier.ValueText == "TestLocal", IsInsideTestCode(model.GetDeclaredSymbol(local)!));
        }

        var lambda = (IAnonymousFunctionOperation)model.GetOperation(root.DescendantNodes().OfType<LambdaExpressionSyntax>().Single())!;
        Assert.IsTrue(IsInsideTestCode(lambda.Symbol));

        bool IsInsideTestCode(ISymbol symbol)
            => (bool)ProductReflection.Call(typeof(AvoidThreadSleepAndTaskWaitInTestsAnalyzer), null, "IsInsideTestCode",
                [typeof(ISymbol), typeof(ImmutableHashSet<INamedTypeSymbol>), typeof(INamedTypeSymbol)],
                symbol, ImmutableHashSet<INamedTypeSymbol>.Empty, testMethod)!;
    }

    [TestMethod]
    [DataRow("Culture")]
    [DataRow("Directory")]
    [DataRow("File")]
    public async Task ParallelSafety_UnrelatedAssignmentsAndInstanceCalls_AreNotGlobalMutations(string scenario)
    {
        const string source = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            [assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]
            public class Service
            {
                public void SetCurrentDirectory(string value) { }
                public void WriteAllText(string path, string value) { }
            }
            [TestClass]
            public class Consumer
            {
                private int state;
                [TestMethod]
                public void Check(Service service)
                {
                    state = 17;
                    state += 3;
                    service.SetCurrentDirectory("not-a-process-directory");
                    service.WriteAllText("not-a-file", "data");
                }
            }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source);

        Assert.IsEmpty(await AnalyzerContinuationTestHelpers.AnalyzeAsync(document, CreateRuntimeAnalyzer(scenario)));
    }

    [TestMethod]
    [DataRow("Empty", "Two", false, DisplayName = "DifferentArgumentCounts")]
    [DataRow("One", "Two", false, DisplayName = "DifferentNonemptyCounts")]
    [DataRow("One", "Long", false, DisplayName = "DifferentPrimitiveTypes")]
    [DataRow("One", "Other", false, DisplayName = "DifferentPrimitiveValues")]
    [DataRow("NullNested", "Nested", false, DisplayName = "NullVersusNonNullArray")]
    [DataRow("Nested", "Reordered", false, DisplayName = "ArrayOrderMatters")]
    [DataRow("Nested", "SameNested", true, DisplayName = "EqualNestedArrays")]
    [DataRow("NullNested", "SameNullNested", true, DisplayName = "EqualNullArrays")]
    public async Task DataRowComparer_RealAttributeConstants_PreserveStructuralEquality(string left, string right, bool equal)
    {
        const string source = """
            using System;
            public sealed class ValuesAttribute : Attribute { public ValuesAttribute(params object[] entries) { } }
            [Values()] public class Empty { }
            [Values(1)] public class One { }
            [Values(1, 2)] public class Two { }
            [Values(1L)] public class Long { }
            [Values(2)] public class Other { }
            [Values((int[])null)] public class NullNested { }
            [Values((int[])null)] public class SameNullNested { }
            [Values(new int[] { 1, 2 })] public class Nested { }
            [Values(new int[] { 1, 2 })] public class SameNested { }
            [Values(new int[] { 2, 1 })] public class Reordered { }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source, includeMSTest: false);
        Compilation compilation = (await document.Project.GetCompilationAsync())!;
        AnalyzerContinuationTestHelpers.AssertCompilerErrors(compilation);
        Type comparerType = ProductReflection.Type(false, "MSTest.Analyzers.DuplicateDataRowAnalyzer+TypedConstantArrayComparer");
        var comparer = (IEqualityComparer<ImmutableArray<TypedConstant>>)comparerType.GetProperty("Instance", ProductReflection.AllMembers)!.GetValue(null)!;
        ImmutableArray<TypedConstant> leftValues = Values(left);
        ImmutableArray<TypedConstant> rightValues = Values(right);

        Assert.AreEqual(equal, comparer.Equals(leftValues, rightValues));
        Assert.AreEqual(equal, comparer.Equals(rightValues, leftValues));
        if (equal)
        {
            Assert.AreEqual(comparer.GetHashCode(leftValues), comparer.GetHashCode(rightValues));
        }

        ImmutableArray<TypedConstant> Values(string name)
            => compilation.GetTypeByMetadataName(name)!.GetAttributes().Single().ConstructorArguments.Single().Values;
    }

    [TestMethod]
    public async Task DictionaryContains_RealImplementations_DistinguishKeyLookupFromElementMembership()
    {
        const string source = """
            using System.Collections;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class ShadowDictionary : Hashtable
            {
                public new bool Contains(object item) => false;
            }
            public class Consumer
            {
                public void Check(ArrayList list, Hashtable dictionary, ShadowDictionary shadow)
                {
                    Assert.IsTrue(list.Contains(7));
                    Assert.IsTrue(dictionary.Contains(7));
                    Assert.IsTrue(shadow.Contains(7));
                }
            }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source);
        AssertSingleDiagnostic(
            await AnalyzerContinuationTestHelpers.AnalyzeAsync(document, new UseProperAssertMethodsAnalyzer()),
            source, "MSTEST0037", DiagnosticSeverity.Info, "Assert.IsTrue(list.Contains(7))",
            "Use 'Assert.Contains' instead of 'Assert.IsTrue'",
            [("ProperAssertMethodNameKey", "Contains"), ("CodeFixModeKey", "CodeFixModeAddArgument")],
            ["list.Contains(7)", "7", "list"]);

        Compilation compilation = (await document.Project.GetCompilationAsync())!;
        INamedTypeSymbol dictionaryType = compilation.GetTypeByMetadataName("System.Collections.IDictionary")!;
        IMethodSymbol dictionaryContains = Contains(dictionaryType);
        IMethodSymbol hashtableContains = Contains(compilation.GetTypeByMetadataName("System.Collections.Hashtable")!);
        IMethodSymbol arrayListContains = Contains(compilation.GetTypeByMetadataName("System.Collections.ArrayList")!);
        IMethodSymbol shadowContains = Contains(compilation.GetTypeByMetadataName("ShadowDictionary")!);

        Assert.IsTrue(IsDictionaryContains(dictionaryContains, dictionaryType));
        Assert.IsTrue(IsDictionaryContains(hashtableContains, dictionaryType));
        Assert.IsFalse(IsDictionaryContains(arrayListContains, dictionaryType));
        Assert.IsFalse(IsDictionaryContains(shadowContains, dictionaryType));
        Assert.IsFalse(IsDictionaryContains(hashtableContains, null));

        static IMethodSymbol Contains(INamedTypeSymbol type) => type.GetMembers("Contains").OfType<IMethodSymbol>().Single();

        static bool IsDictionaryContains(IMethodSymbol method, INamedTypeSymbol? contract)
            => (bool)ProductReflection.Call(typeof(UseProperAssertMethodsAnalyzer), null, "IsNonGenericDictionaryContains",
                [typeof(IMethodSymbol), typeof(INamedTypeSymbol)], method, contract)!;
    }

    [TestMethod]
    public async Task CollectionEquality_MissingOptionalEquatableSymbol_DoesNotInventAnEqualityContract()
    {
        const string source = """
            using System;
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class OwnEquality : List<int>, IEquatable<OwnEquality>
            {
                public bool Equals(OwnEquality other) => other != null && Count == other.Count;
            }
            public class Consumer
            {
                public void Check(OwnEquality left, OwnEquality right) { Assert.AreEqual(left, right); }
            }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source);
        Assert.IsEmpty(await AnalyzerContinuationTestHelpers.AnalyzeAsync(document, new AvoidAssertAreEqualOnCollectionsAnalyzer()));
        Compilation compilation = (await document.Project.GetCompilationAsync())!;
        INamedTypeSymbol collectionType = compilation.GetTypeByMetadataName("OwnEquality")!;
        INamedTypeSymbol equatableType = compilation.GetTypeByMetadataName("System.IEquatable`1")!;

        Assert.IsTrue(ImplementsSelfEquatable(equatableType));
        Assert.IsFalse(ImplementsSelfEquatable(null));

        bool ImplementsSelfEquatable(INamedTypeSymbol? contract)
            => (bool)ProductReflection.Call(typeof(AvoidAssertAreEqualOnCollectionsAnalyzer), null, "ImplementsSelfEquatable",
                [typeof(INamedTypeSymbol), typeof(ITypeSymbol), typeof(INamedTypeSymbol)],
                collectionType, collectionType, contract)!;
    }

    [TestMethod]
    public async Task CancellationFlow_ExpressionTreeAndDelegateWithIdenticalOverloads_OnlyDelegateIsActionable()
    {
        const string source = """
            using System;
            using System.Linq.Expressions;
            using System.Threading;
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public interface IService
            {
                Task Run(int count);
                Task Run(int count, CancellationToken token);
            }
            [TestClass]
            public class Consumer
            {
                public TestContext Context { get; set; }
                [TestMethod]
                public void Check(IService service)
                {
                    Expression<Func<Task>> expression = () => service.Run(7);
                    Func<Task> callback = () => service.Run(8);
                }
            }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source);

        AssertSingleDiagnostic(
            await AnalyzerContinuationTestHelpers.AnalyzeAsync(document, new FlowTestContextCancellationTokenAnalyzer()),
            source, "MSTEST0049", DiagnosticSeverity.Info, "service.Run(8)",
            "Consider using the overload that accepts a CancellationToken and pass 'TestContext.CancellationToken'",
            [("TestContextMemberNamePropertyKey", "Context")]);
    }

    [TestMethod]
    public async Task CancellationFlow_IncompatibleOverloadAndFieldInitializer_DoNotReceiveMethodScopedSuggestions()
    {
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public static class Service
            {
                public static Task Run(int count) => Task.CompletedTask;
                public static Task Run(string count, CancellationToken token) => Task.CompletedTask;
                public static Task Match(int count) => Task.CompletedTask;
                public static Task Match(int count, CancellationToken token) => Task.CompletedTask;
            }
            [TestClass]
            public class Consumer
            {
                private static readonly Task Pending = Service.Match(5);
                [TestMethod]
                public void Check()
                {
                    _ = Service.Run(3);
                    _ = Service.Match(3);
                }
            }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source);

        AssertSingleDiagnostic(
            await AnalyzerContinuationTestHelpers.AnalyzeAsync(document, new FlowTestContextCancellationTokenAnalyzer()),
            source, "MSTEST0049", DiagnosticSeverity.Info, "Service.Match(3)",
            "Consider using the overload that accepts a CancellationToken and pass 'TestContext.CancellationToken'",
            [("TestContextState", "CouldBeInScopeAsProperty")]);
    }

    [TestMethod]
    public async Task CancellationFlow_WithoutExpressionTreeReference_StillReportsOmittedOptionalToken()
    {
        const string source = """
            using System.Threading;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public static class Service { public static void Run(CancellationToken token = default) { } }
            public class Consumer
            {
                [TestMethod]
                public void Check() { Service.Run(); }
            }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source);
        Compilation original = (await document.Project.GetCompilationAsync())!;
        INamedTypeSymbol expression = original.GetTypeByMetadataName("System.Linq.Expressions.Expression`1")!;
        MetadataReference[] expressionReferences = document.Project.MetadataReferences
            .Where(reference => SymbolEqualityComparer.Default.Equals(original.GetAssemblyOrModuleSymbol(reference), expression.ContainingAssembly))
            .ToArray();
        Assert.HasCount(1, expressionReferences);
        document = document.Project.WithMetadataReferences(document.Project.MetadataReferences.Except(expressionReferences)).GetDocument(document.Id)!;
        Assert.IsNull((await document.Project.GetCompilationAsync())!.GetTypeByMetadataName("System.Linq.Expressions.Expression`1"));

        AssertSingleDiagnostic(
            await AnalyzerContinuationTestHelpers.AnalyzeAsync(document, new FlowTestContextCancellationTokenAnalyzer()),
            source, "MSTEST0049", DiagnosticSeverity.Info, "Service.Run()",
            "Consider using the overload that accepts a CancellationToken and pass 'TestContext.CancellationToken'",
            [("TestContextState", "CouldBeInScopeAsProperty")]);
    }

    [TestMethod]
    public async Task TestContextUsage_FieldInitializerAndUnrelatedPropertiesBag_AreNotFixturePropertyAccesses()
    {
        const string source = """
            using System.Collections;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class OtherContext
            {
                public IDictionary Properties { get; } = new Hashtable();
            }
            public class Consumer
            {
                private static TestContext context = null;
                private static readonly string Name = context.TestName;
                [ClassInitialize]
                public static void Initialize(TestContext testContext, OtherContext other)
                {
                    _ = other.Properties["TestName"];
                    _ = testContext.TestName;
                }
            }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source);

        AssertSingleDiagnostic(
            await AnalyzerContinuationTestHelpers.AnalyzeAsync(document, new TestContextPropertyUsageAnalyzer()),
            source, "MSTEST0048", DiagnosticSeverity.Warning, "testContext.TestName",
            "TestContext property 'TestName' cannot be accessed in 'ClassInitialize' method");
    }

    [TestMethod]
    [DataRow("other.DisplayName", true, DisplayName = "DisplayNameIsNotCallerPath")]
    [DataRow("other.DeclaringFilePath", false, DisplayName = "OriginalDeclaringSourcePath")]
    [DataRow("OtherSource.DeclaringFilePath", true, DisplayName = "UnrelatedNamesakeProperty")]
    public async Task TestMethodCreation_OnlyDeclaringSourcePropertyIsSafeToForwardAsCallerFilePath(string expression, bool reports)
    {
        string source = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public static class OtherSource { public static string DeclaringFilePath => "unrelated"; }
            public class Consumer
            {
                public void Check(TestMethodAttribute other) { _ = new TestMethodAttribute({{expression}}); }
            }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source);
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerContinuationTestHelpers.AnalyzeAsync(document, new TestMethodAttributeShouldSetDisplayNameCorrectlyAnalyzer());

        if (reports)
        {
            AssertSingleDiagnostic(diagnostics, source, "MSTEST0056", DiagnosticSeverity.Warning, expression,
                "Use the 'DisplayName' property instead of passing a string argument to TestMethodAttribute");
        }
        else
        {
            Assert.IsEmpty(diagnostics);
        }
    }

    [TestMethod]
    [DataRow("SupportedOSPlatform")]
    [DataRow("UnsupportedOSPlatform")]
    public async Task OSPlatformConsistency_NullPlatformName_ReportsWithoutInventingAnOSCondition(string attribute)
    {
        string source = $$"""
            using System.Runtime.Versioning;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class Consumer
            {
                [TestMethod, {{attribute}}(null)]
                public void Check() { }
            }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source);

        AssertSingleDiagnostic(
            await AnalyzerContinuationTestHelpers.AnalyzeAsync(document, new OSPlatformAttributesShouldBeConsistentAnalyzer()),
            source, "MSTEST0084", DiagnosticSeverity.Info, $"{attribute}(null)",
            "Platform compatibility attributes on test 'Check' are not represented by a compatible '[OSCondition]' attribute");
    }

    [TestMethod]
    [DataRow("ResourceLock(null)", "", DisplayName = "NullResourceKey")]
    [DataRow("ResourceLock()", "CS7036", DisplayName = "MissingResourceKey")]
    public async Task ResourceLock_InvalidConstructorArguments_DoNotProduceLiteralKeySuggestions(string attribute, string compilerError)
    {
        string source = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            [{{attribute}}]
            public class Consumer { }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source);

        Assert.IsEmpty(await AnalyzerContinuationTestHelpers.AnalyzeAsync(document, new PreferConstantForResourceLockAnalyzer(),
            expectedCompilerErrors: compilerError.Length == 0 ? [] : [compilerError]));
    }

    [TestMethod]
    public async Task RedundantIgnore_NonstandardConstructor_UsesItsDocumentedUnknownMessageFallback()
    {
        const string source = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                public sealed class IgnoreAttribute : System.Attribute
                {
                    public IgnoreAttribute(int reason, int detail) { }
                }
            }
            [TestClass, Ignore(1, 2)]
            public sealed class Consumer
            {
                [TestMethod, Ignore(3, 4)]
                public void Check() { }
            }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source);

        AssertSingleDiagnostic(
            await AnalyzerContinuationTestHelpers.AnalyzeAsync(document, new RedundantTestMethodAttributeAnalyzer()),
            source, "MSTEST0086", DiagnosticSeverity.Info, "Ignore(3, 4)",
            "[Ignore] on test method 'Check' is redundant because it does not change behavior established by its containing test class");
    }

    [TestMethod]
    public async Task AttributeDisplayName_MissingMetadataAndNamesWithoutSuffix_RetainTheirDistinctFallbacks()
    {
        const string source = """
            public sealed class Marker : System.Attribute { }
            public sealed class MarkerAttribute : System.Attribute { }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source, includeMSTest: false);
        Compilation compilation = (await document.Project.GetCompilationAsync())!;
        AnalyzerContinuationTestHelpers.AssertCompilerErrors(compilation);

        Assert.AreEqual("Attribute", Format(null));
        Assert.AreEqual("[Marker]", Format(compilation.GetTypeByMetadataName("Marker")));
        Assert.AreEqual("[Marker]", Format(compilation.GetTypeByMetadataName("MarkerAttribute")));

        static string Format(INamedTypeSymbol? symbol)
            => (string)ProductReflection.Call(typeof(RedundantTestMethodAttributeAnalyzer), null, "FormatAttributeName",
                [typeof(INamedTypeSymbol)], symbol)!;
    }

    [TestMethod]
    public async Task DependencyValidation_OlderFrameworkWithoutDiscoverInternals_StillReportsMissingPublicTarget()
    {
        const string source = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                public sealed class TestClassAttribute : System.Attribute { }
                public sealed class TestMethodAttribute : System.Attribute { }
                public sealed class DependsOnAttribute : System.Attribute
                {
                    public DependsOnAttribute(string methodName) { }
                }
            }
            [TestClass]
            public class Consumer
            {
                [TestMethod, DependsOn("Missing")]
                public void Check() { }
            }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source, includeMSTest: false);

        AssertSingleDiagnostic(
            await AnalyzerContinuationTestHelpers.AnalyzeAsync(document, new DependsOnShouldBeValidAnalyzer()),
            source, "MSTEST0078", DiagnosticSeverity.Warning, "DependsOn(\"Missing\")",
            "'[DependsOn]' references method 'Missing', which does not exist on type 'Consumer'");
    }

    [TestMethod]
    [DataRow(510, true, DisplayName = "CycleWithinWalkBudget")]
    [DataRow(511, false, DisplayName = "CycleAtWalkBudget")]
    [DataRow(512, false, DisplayName = "HighFanOutExceedsBudget")]
    public async Task DependencyCycles_FanOutBudget_DoesNotReportACycleBeyondTheBoundedWalk(int leafCount, bool reports)
    {
        // The first edge expands the leaves before the second edge can close the two-node cycle.
        // The two-node cycle fits at 510 leaves; visiting Branch plus 511 leaves exhausts the 512-node budget.
        string leaves = string.Join(
            Environment.NewLine,
            Enumerable.Range(0, leafCount).Select(index => $"[TestMethod] public void Leaf{index:D3}() {{ }}"));
        string source = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            [TestClass]
            public class Root
            {
                [TestMethod, DependsOn(typeof(Branch), nameof(Branch.Run))]
                public void Run() { }
            }
            [TestClass]
            public class Branch
            {
                [TestMethod, DependsOn(typeof(Leaves)), DependsOn(typeof(Root), nameof(Root.Run))]
                public void Run() { }
            }
            [TestClass]
            public class Leaves
            {
                {{leaves}}
            }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source);
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerContinuationTestHelpers.AnalyzeAsync(document, new DependsOnShouldBeValidAnalyzer());

        if (!reports)
        {
            Assert.IsEmpty(diagnostics);
            return;
        }

        Assert.HasCount(2, diagnostics);
        AnalyzerContinuationTestHelpers.AssertDiagnostic(diagnostics[0], "MSTEST0078", DiagnosticSeverity.Warning, source,
            "DependsOn(typeof(Branch), nameof(Branch.Run))",
            "'[DependsOn]' declares the dependency cycle 'Root.Run > Branch.Run > Root.Run', which fails every test in the cycle at run time");
        AnalyzerContinuationTestHelpers.AssertDiagnostic(diagnostics[1], "MSTEST0078", DiagnosticSeverity.Warning, source,
            "DependsOn(typeof(Root), nameof(Root.Run))",
            "'[DependsOn]' declares the dependency cycle 'Branch.Run > Root.Run > Branch.Run', which fails every test in the cycle at run time");
        foreach (Diagnostic diagnostic in diagnostics)
        {
            Assert.IsEmpty(diagnostic.Properties);
            Assert.IsEmpty(diagnostic.AdditionalLocations);
        }
    }

    [TestMethod]
    public async Task DependencyNodes_ObjectEqualityAndHashing_UseTheFullyQualifiedClassAndMethodKey()
    {
        const string source = """
            namespace First { public class Tests { public void Run() { } } }
            namespace Second { public class Tests { public void Run() { } } }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source, includeMSTest: false);
        Compilation compilation = (await document.Project.GetCompilationAsync())!;
        AnalyzerContinuationTestHelpers.AssertCompilerErrors(compilation);
        Type nodeType = ProductReflection.Type(false, "MSTest.Analyzers.DependsOnShouldBeValidAnalyzer+TestNode");
        object first = Node("First.Tests", "Run");
        object same = Node("First.Tests", "Run");
        object otherClass = Node("Second.Tests", "Run");
        object otherMethod = Node("First.Tests", "Other");

        Assert.IsTrue(first.Equals(same));
        Assert.IsFalse(first.Equals(otherClass));
        Assert.IsFalse(first.Equals(otherMethod));
        Assert.IsFalse(first.Equals(null));
        Assert.IsFalse(first.Equals("global::First.Tests.Run"));
        Assert.AreEqual(StringComparer.Ordinal.GetHashCode("global::First.Tests.Run"), first.GetHashCode());
        var nodes = new HashSet<object> { first };
        Assert.IsTrue(nodes.Contains(same));
        Assert.IsFalse(nodes.Contains(otherClass));
        Assert.IsFalse(nodes.Contains(otherMethod));

        object Node(string type, string method)
            => ProductReflection.Create(nodeType, [typeof(INamedTypeSymbol), typeof(string)],
                compilation.GetTypeByMetadataName(type), method);
    }

    [TestMethod]
    public async Task DependencyTarget_UnresolvedType_DoesNotStackAnalyzerDiagnosticsOnTheErrorType()
    {
        const string source = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            [TestClass]
            public class Consumer
            {
                [TestMethod, DependsOn(typeof(Missing))]
                public void Check() { }
            }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source);
        Assert.IsEmpty(await AnalyzerContinuationTestHelpers.AnalyzeAsync(document, new DependsOnShouldBeValidAnalyzer(),
            expectedCompilerErrors: ["CS0246"]));
        Compilation compilation = (await document.Project.GetCompilationAsync())!;
        AttributeData attribute = compilation.GetTypeByMetadataName("Consumer")!.GetMembers("Check").Single().GetAttributes()
            .Single(candidate => candidate.AttributeClass!.Name == "DependsOnAttribute");
        TypedConstant unresolved = attribute.ConstructorArguments.Single();

        Assert.AreEqual(TypedConstantKind.Type, unresolved.Kind);
        Assert.IsFalse(unresolved.IsNull);
        IErrorTypeSymbol errorType = Assert.IsInstanceOfType<IErrorTypeSymbol>(unresolved.Value);
        Assert.AreEqual("Missing", errorType.Name);
        object result = ProductReflection.Call(typeof(DependsOnShouldBeValidAnalyzer), null, "ReadTarget",
            [typeof(AttributeData)], attribute)!;
        (ITypeSymbol? targetClass, string? targetMethod, bool malformed) = ((ITypeSymbol?, string?, bool))result;
        Assert.IsTrue(SymbolEqualityComparer.Default.Equals(errorType, targetClass));
        Assert.IsNull(targetMethod);
        Assert.IsFalse(malformed);
    }

    [TestMethod]
    public async Task FilterProvider_UnresolvedGenericArgument_DoesNotStackAnAnalyzerDiagnosticOnTheCompilerError()
    {
        const string source = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            [assembly: TestFilterProvider<Missing>]
            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                public sealed class TestFilterProviderAttribute<T> : System.Attribute { }
            }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source);

        Assert.IsEmpty(await AnalyzerContinuationTestHelpers.AnalyzeAsync(document, new TestFilterProviderShouldBeValidAnalyzer(),
            expectedCompilerErrors: ["CS0246"]));
    }

    [TestMethod]
    [DataRow("query.AsEnumerable()", DisplayName = "ParenthesizedExtensionCall")]
    [DataRow("Enumerable.AsEnumerable(query)", DisplayName = "ParenthesizedStaticCall")]
    public async Task QueryableCollectionType_RealParenthesizedAsEnumerableOperation_RecoversQueryInsteadOfWrapperType(string expression)
    {
        string source = $$"""
            using System.Linq;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class Consumer
            {
                public void Check(IQueryable<int> query) { Assert.IsEmpty({{expression}}); }
            }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source);
        AssertSingleDiagnostic(
            await AnalyzerContinuationTestHelpers.AnalyzeAsync(document, new AvoidEnumerableAssertionsOnQueryableAnalyzer()),
            source, "MSTEST0090", DiagnosticSeverity.Info, $"Assert.IsEmpty({expression})",
            "Use 'Queryable.Any' instead of 'Assert.IsEmpty' to avoid enumerating query results for a count or existence check");
        Compilation compilation = (await document.Project.GetCompilationAsync())!;
        SemanticModel model = (await document.GetSemanticModelAsync())!;
        SyntaxNode root = (await document.GetSyntaxRootAsync())!;
        InvocationExpressionSyntax syntax = root.DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Single(invocation => invocation.ToString() == expression);
        var operand = (IInvocationOperation)model.GetOperation(syntax)!;
        IParenthesizedOperation parenthesized = ProductReflection.Parenthesize(operand, model);
        INamedTypeSymbol enumerable = compilation.GetTypeByMetadataName("System.Linq.Enumerable")!;
        INamedTypeSymbol expected = compilation.GetTypeByMetadataName("System.Linq.IQueryable`1")!
            .Construct(compilation.GetSpecialType(SpecialType.System_Int32));
        Assert.AreEqual("System.Collections.Generic.IEnumerable<int>", parenthesized.Type!.ToDisplayString());

        var actual = (ITypeSymbol)ProductReflection.Call(typeof(AvoidEnumerableAssertionsOnQueryableAnalyzer), null, "GetCollectionType",
            [typeof(IOperation), typeof(INamedTypeSymbol)], parenthesized, enumerable)!;

        Assert.IsTrue(SymbolEqualityComparer.Default.Equals(expected, actual));
        Assert.AreEqual("System.Linq.IQueryable<int>", actual.ToDisplayString());
    }

    [TestMethod]
    [DataRow("DataRowAttribute")]
    [DataRow("TestDataRow")]
    public async Task ConflictingRowNames_OneAvailableRowContract_IsSufficientToReportBothNames(string contract)
    {
        string declaration = contract == "DataRowAttribute"
            ? "public sealed class DataRowAttribute : System.Attribute"
            : "public sealed class TestDataRow<T>";
        string creation = contract == "DataRowAttribute"
            ? "new DataRowAttribute { DisplayName = \"full name\", ArgumentsDisplayName = \"arguments\" }"
            : "new TestDataRow<int> { DisplayName = \"full name\", ArgumentsDisplayName = \"arguments\" }";
        string source = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                {{declaration}}
                {
                    public string DisplayName { get; set; }
                    public string ArgumentsDisplayName { get; set; }
                }
            }
            public class Consumer
            {
                public void Check() { _ = {{creation}}; }
            }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source, includeMSTest: false);

        AssertSingleDiagnostic(
            await AnalyzerContinuationTestHelpers.AnalyzeAsync(document, new ConflictingDataRowDisplayNamesAnalyzer()),
            source, "MSTEST0089", DiagnosticSeverity.Warning, creation,
            "'ArgumentsDisplayName' is ignored because 'DisplayName' overrides the entire test case name");
    }

    [TestMethod]
    [DataRow("AssertFailedException")]
    [DataRow("AssertInconclusiveException")]
    public async Task CatchAssertions_OneAvailableExceptionContract_StillIdentifiesTheExplicitOutcome(string exception)
    {
        string source = $$"""
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                public class {{exception}} : Exception { }
            }
            public class Consumer
            {
                public void Check()
                {
                    try { throw new Exception(); }
                    catch (Exception) { throw new {{exception}}(); }
                }
            }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source, includeMSTest: false);

        AssertSingleDiagnostic(
            await AnalyzerContinuationTestHelpers.AnalyzeAsync(document, new AvoidAssertsInCatchBlocksAnalyzer()),
            source, "MSTEST0058", DiagnosticSeverity.Info, $"throw new {exception}();",
            "Do not use asserts in catch blocks because they may not fail the test if no exception is thrown");
    }

    [TestMethod]
    public async Task Shadowing_DifferentPropertyTypes_AreNotCollapsedIntoTheSameMember()
    {
        const string source = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class Base
            {
                public string Name { get; set; }
                public int Same { get; set; }
            }
            [TestClass]
            public class Consumer : Base
            {
                public new int Name { get; set; }
                public new int Same { get; set; }
            }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source);
        Diagnostic diagnostic = Assert.ContainsSingle(await AnalyzerContinuationTestHelpers.AnalyzeAsync(document, new DoNotUseShadowingAnalyzer()));

        AnalyzerContinuationTestHelpers.AssertDiagnostic(diagnostic, "MSTEST0036", DiagnosticSeverity.Warning, source, "Same",
            "Member 'Same' already exists in the base class");
        Assert.AreEqual(new TextSpan(source.LastIndexOf("Same", StringComparison.Ordinal), 4), diagnostic.Location.SourceSpan);
        Assert.IsEmpty(diagnostic.Properties);
        Assert.IsEmpty(diagnostic.AdditionalLocations);
    }

    [TestMethod]
    public async Task ExecutableMigration_ParameterlessStart_DoesNotSupplyAnExecutableFromAnUnrelatedGuard()
    {
        string source = $$"""
            using System.IO;
            using System.Diagnostics;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            {{ConditionContracts}}
            namespace System.Diagnostics
            {
                public sealed class Process
                {
                    public static Process Start() => new Process();
                    public static Process Start(string fileName) => new Process();
                }
            }
            public class Consumer
            {
                [TestMethod]
                public void Rejected()
                {
                    if (!File.Exists("tool-one")) return;
                    Process.Start();
                }
                [TestMethod]
                public void Accepted()
                {
                    if (!File.Exists("tool-two")) return;
                    Process.Start("tool-two");
                }
            }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source, includeMSTest: false);

        AssertSingleDiagnostic(
            await AnalyzerContinuationTestHelpers.AnalyzeAsync(document, new UseExecutableConditionAttributeInsteadOfProcessCheckAnalyzer()),
            source, "MSTEST0083", DiagnosticSeverity.Info, "if (!File.Exists(\"tool-two\")) return;",
            "Use '[ExecutableCondition]' attribute instead of checking for executable 'tool-two' before starting it",
            [("ExecutableKey", "tool-two")]);
    }

    [TestMethod]
    public async Task HostInjection_NonNamedServiceParameterTypes_AreNotMistakenForDerivedTestContext()
    {
        MetadataReference host = await AnalyzerContinuationTestHelpers.EmitReferenceAsync(
            """
            [assembly: System.Reflection.AssemblyMetadata("MSTestHostTestClassInjectionEnabled", "true")]
            public class HostMarker { }
            """, "HostInjectionMetadata");
        const string source = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            [TestClass]
            public class Consumer<T>
            {
                public Consumer(T service, int[] values) { }
            }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source, additionalReferences: [host]);

        Assert.IsEmpty(await AnalyzerContinuationTestHelpers.AnalyzeAsync(document, new TestClassConstructorShouldBeValidAnalyzer()));
    }

    [TestMethod]
    [DataRow("ArgumentOrder")]
    [DataRow("ReferenceEquality")]
    [DataRow("ProperAssertions")]
    [DataRow("ThrowsStatements")]
    [DataRow("Negation")]
    [DataRow("AlwaysFalse")]
    [DataRow("AlwaysTrue")]
    public async Task AssertionAnalyzers_VarargOperandsWithoutParameters_AreNotMistakenForNamedAssertionArguments(string scenario)
    {
        const string source = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                public static class Assert
                {
                    public static void AreEqual(object observation, __arglist) { }
                    public static void AreNotEqual(object expected, __arglist) { }
                    public static void AreSame(object expected, __arglist) { }
                    public static void HasCount(int expected, __arglist) { }
                    public static void Throws(__arglist) { }
                    public static void IsTrue(bool success, __arglist) { }
                    public static void IsExactInstanceOfType(object value, __arglist) { }
                }
            }
            public class Consumer
            {
                public void Check(object value)
                {
                    Assert.AreEqual(value, __arglist(1));
                    Assert.AreNotEqual(value, __arglist(2));
                    Assert.AreSame(value, __arglist(3));
                    Assert.HasCount(0, __arglist("not-a-collection"));
                    Assert.Throws(__arglist(4));
                    Assert.IsTrue(!(value is null), __arglist(5));
                    Assert.IsExactInstanceOfType(value, __arglist(6));
                }
            }
            """;
        DiagnosticAnalyzer analyzer = scenario switch
        {
            "ArgumentOrder" => new AssertionArgsShouldBePassedInCorrectOrderAnalyzer(),
            "ReferenceEquality" => new AvoidAssertAreSameWithValueTypesAnalyzer(),
            "ProperAssertions" => new UseProperAssertMethodsAnalyzer(),
            "ThrowsStatements" => new AssertThrowsShouldContainSingleStatementAnalyzer(),
            "Negation" => new DoNotNegateBooleanAssertionAnalyzer(),
            "AlwaysFalse" => new PreferAssertFailOverAlwaysFalseConditionsAnalyzer(),
            "AlwaysTrue" => new ReviewAlwaysTrueAssertConditionAnalyzer(),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario)),
        };
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source, includeMSTest: false);
        SemanticModel model = (await document.GetSemanticModelAsync())!;
        SyntaxNode root = (await document.GetSyntaxRootAsync())!;
        InvocationExpressionSyntax syntax = root.DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Single(invocation => invocation.Expression.ToString() == "Assert.AreNotEqual");
        var operation = (IInvocationOperation)model.GetOperation(syntax)!;
        Assert.IsTrue(operation.TargetMethod.IsVararg);
        Assert.HasCount(1, operation.TargetMethod.Parameters);
        Assert.HasCount(2, operation.Arguments);
        Assert.IsNull(operation.Arguments[1].Parameter);

        Assert.IsEmpty(await AnalyzerContinuationTestHelpers.AnalyzeAsync(document, analyzer));
    }

    [TestMethod]
    public async Task OSConditionMigration_VarargPlatformCall_IsNotAOneOperandPlatformCheck()
    {
        string source = $$"""
            using System.Runtime.InteropServices;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            {{ConditionContracts}}
            namespace System.Runtime.InteropServices
            {
                public static class RuntimeInformation
                {
                    public static bool IsOSPlatform(OSPlatform platform, __arglist) => false;
                }
            }
            public class Consumer
            {
                [TestMethod]
                public void Check()
                {
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows, __arglist(17))) return;
                }
            }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source, includeMSTest: false);
        SemanticModel model = (await document.GetSemanticModelAsync())!;
        SyntaxNode root = (await document.GetSyntaxRootAsync())!;
        InvocationExpressionSyntax syntax = root.DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Single(invocation => invocation.Expression.ToString() == "RuntimeInformation.IsOSPlatform");
        var operation = (IInvocationOperation)model.GetOperation(syntax)!;
        Assert.HasCount(1, operation.TargetMethod.Parameters);
        Assert.HasCount(2, operation.Arguments);
        Assert.IsNull(operation.Arguments[1].Parameter);

        Assert.IsEmpty(await AnalyzerContinuationTestHelpers.AnalyzeAsync(document, new UseOSConditionAttributeInsteadOfRuntimeCheckAnalyzer()));
    }

    [TestMethod]
    public async Task OSSkipBody_MissingAndEmptyBodies_AreNotEquivalentToAnEarlyReturn()
    {
        const string source = """
            public class Consumer
            {
                public void Check(bool ready)
                {
                    if (ready) { }
                    if (ready) return;
                }
            }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source);
        Compilation compilation = (await document.Project.GetCompilationAsync())!;
        AnalyzerContinuationTestHelpers.AssertCompilerErrors(compilation);
        SemanticModel model = (await document.GetSemanticModelAsync())!;
        SyntaxNode root = (await document.GetSyntaxRootAsync())!;
        IOperation[] bodies = root.DescendantNodes().OfType<IfStatementSyntax>()
            .Select(statement => ((IConditionalOperation)model.GetOperation(statement)!).WhenTrue).ToArray();
        INamedTypeSymbol assertType = compilation.GetTypeByMetadataName("Microsoft.VisualStudio.TestTools.UnitTesting.Assert")!;

        Assert.IsFalse(IsSkip(null));
        Assert.IsFalse(IsSkip(bodies[0]));
        Assert.IsTrue(IsSkip(bodies[1]));

        bool IsSkip(IOperation? body)
            => (bool)ProductReflection.Call(typeof(UseOSConditionAttributeInsteadOfRuntimeCheckAnalyzer), null,
                "IsEarlyReturnOrAssertInconclusive", [typeof(IOperation), typeof(INamedTypeSymbol)], body, assertType)!;
    }

    [TestMethod]
    [DataRow("\"true\"", true, DisplayName = "ExactMetadataOptIn")]
    [DataRow("\"TRUE\"", false, DisplayName = "CaseSensitiveMetadataOptIn")]
    [DataRow("null", false, DisplayName = "NullMetadataDoesNotOptIn")]
    public async Task HostInjection_MetadataValue_MustBeTheExactOptInAndNullUnsupportedModeIsIgnored(string value, bool enabled)
    {
        MetadataReference host = await AnalyzerContinuationTestHelpers.EmitReferenceAsync(
            $$"""
            [assembly: System.Reflection.AssemblyMetadata("MSTestHostTestClassInjectionEnabled", {{value}})]
            public class HostMarker { }
            """, "HostMetadataValue");
        const string source = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            [assembly: System.Reflection.AssemblyMetadata("MSTestHostTestClassInjectionUnsupportedMode", null)]
            [TestClass]
            public class Consumer
            {
                public Consumer(int service) { }
            }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source, additionalReferences: [host]);
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerContinuationTestHelpers.AnalyzeAsync(document, new TestClassConstructorShouldBeValidAnalyzer());

        if (enabled)
        {
            Assert.IsEmpty(diagnostics);
        }
        else
        {
            AssertSingleDiagnostic(diagnostics, source, "MSTEST0063", DiagnosticSeverity.Warning, "Consumer",
                "Test class 'Consumer' does not have a valid constructor. Valid constructors are 'public' and either parameterless or with one parameter of type 'TestContext'.");
        }
    }

    [TestMethod]
    public async Task DependencySelfReference_AfterAnUnrelatedSameNamedTarget_IsReportedOnlyOnce()
    {
        const string source = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            [TestClass]
            public class Other
            {
                [TestMethod] public void Run() { }
            }
            [TestClass]
            public class Consumer
            {
                [TestMethod, DependsOn(typeof(Other), nameof(Other.Run)), DependsOn(nameof(Run))]
                public void Run() { }
            }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source);

        AssertSingleDiagnostic(
            await AnalyzerContinuationTestHelpers.AnalyzeAsync(document, new DependsOnShouldBeValidAnalyzer()),
            source, "MSTEST0078", DiagnosticSeverity.Warning, "DependsOn(nameof(Run))",
            "'[DependsOn]' makes 'Run' depend on itself, which is a dependency cycle and fails the test at run time");
    }

    [TestMethod]
    [DataRow("Span", false)]
    [DataRow("Span", true)]
    [DataRow("ReadOnlySpan", false)]
    [DataRow("ReadOnlySpan", true)]
    [DataRow("Memory", false)]
    [DataRow("Memory", true)]
    [DataRow("ReadOnlyMemory", false)]
    [DataRow("ReadOnlyMemory", true)]
    public async Task SpanCountAssertion_OnlyAnAvailableMatchingHasCountOverloadCanBeSuggested(string typeName, bool matchingOverload)
    {
        string matchingDeclaration = matchingOverload
            ? $"public static void HasCount<T>(int expected, System.{typeName}<T> collection) {{ }}"
            : string.Empty;
        string source = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                public static class Assert
                {
                    public static void AreEqual(int expected, int actual) { }
                    public static void HasCount(int expected) { }
                    public static void HasCount(int expected, int[] collection) { }
                    {{matchingDeclaration}}
                }
            }
            public class Consumer
            {
                public void Check(System.{{typeName}}<int> values) { Assert.AreEqual(0, values.Length); }
            }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source, includeMSTest: false);
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerContinuationTestHelpers.AnalyzeAsync(document, new UseProperAssertMethodsAnalyzer());

        if (matchingOverload)
        {
            AssertSingleDiagnostic(diagnostics, source, "MSTEST0037", DiagnosticSeverity.Info, "Assert.AreEqual(0, values.Length)",
                "Use 'Assert.HasCount' instead of 'Assert.AreEqual'",
                [("ProperAssertMethodNameKey", "HasCount"), ("CodeFixModeKey", "CodeFixModeSimple")],
                ["values.Length", "values"]);
        }
        else
        {
            Assert.IsEmpty(diagnostics);
        }
    }

    [TestMethod]
    [DataRow("Memory", DisplayName = "NullVersusMemoryLength")]
    [DataRow("List", DisplayName = "NullVersusCollectionCount")]
    [DataRow("Enumerable", DisplayName = "NullVersusLinqCount")]
    public async Task NullExpectedCount_IsANullAssertionRatherThanAnIntCountSuggestion(string scenario)
    {
        (string type, string count) = scenario switch
        {
            "Memory" => ("System.Memory<int>", "values.Length"),
            "List" => ("System.Collections.Generic.List<int>", "values.Count"),
            "Enumerable" => ("System.Collections.Generic.IEnumerable<int>", "values.Count()"),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario)),
        };
        string source = $$"""
            using System.Linq;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                public static class Assert
                {
                    public static void AreEqual<T>(T expected, T actual) { }
                    public static void HasCount<T>(int expected, System.Memory<T> collection) { }
                }
            }
            public class Consumer
            {
                public void Check({{type}} values) { Assert.AreEqual<object>(null, {{count}}); }
            }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source, includeMSTest: false);

        AssertSingleDiagnostic(
            await AnalyzerContinuationTestHelpers.AnalyzeAsync(document, new UseProperAssertMethodsAnalyzer()),
            source, "MSTEST0037", DiagnosticSeverity.Info, $"Assert.AreEqual<object>(null, {count})",
            "Use 'Assert.IsNull' instead of 'Assert.AreEqual'",
            [("ProperAssertMethodNameKey", "IsNull"), ("CodeFixModeKey", "CodeFixModeRemoveArgument")],
            ["null"]);
    }

    [TestMethod]
    [DataRow("object", false, DisplayName = "WidenedObjectWithoutNamedOperandsIsNotGuessed")]
    [DataRow("IEnumerable<int>", true, DisplayName = "SelectedEnumerableTypeStillReports")]
    public async Task CollectionEquality_NonstandardOperandNames_DoNotInventCallSiteTypes(string comparedType, bool reports)
    {
        string source = $$"""
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                public static class Assert { public static void AreEqual<T>(T observation, T result) { } }
            }
            public class Consumer
            {
                public void Check(IEnumerable<int> left, IEnumerable<int> right) { Assert.AreEqual<{{comparedType}}>(left, right); }
            }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source, includeMSTest: false);
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerContinuationTestHelpers.AnalyzeAsync(document, new AvoidAssertAreEqualOnCollectionsAnalyzer());

        if (reports)
        {
            AssertSingleDiagnostic(diagnostics, source, "MSTEST0065", DiagnosticSeverity.Warning, "Assert.AreEqual<IEnumerable<int>>(left, right)",
                "`Assert.AreEqual` on IEnumerable<int> (which implements IEnumerable<T>) does not perform element-wise sequence comparison. Use `Assert.AreSequenceEqual` for ordered element-wise comparison, `Assert.AreSequenceEqual(..., SequenceOrder.InAnyOrder)` for unordered, or `Assert.AreEquivalent` for deep structural comparison.");
        }
        else
        {
            Assert.IsEmpty(diagnostics);
        }
    }

    [TestMethod]
    public async Task QueryableAssertions_NonstandardCollectionParameterName_DoesNotGuessAnOperandByPosition()
    {
        const string source = """
            using System.Linq;
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                public static class Assert
                {
                    public static void HasCount(int expected, IEnumerable<int> values, __arglist) { }
                    public static void IsEmpty(IEnumerable<int> values) { }
                }
            }
            public class Consumer
            {
                public void Check(IQueryable<int> query)
                {
                    Assert.HasCount(3, query, __arglist("not-an-operand"));
                    Assert.IsEmpty(query);
                }
            }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source, includeMSTest: false);

        Assert.IsEmpty(await AnalyzerContinuationTestHelpers.AnalyzeAsync(document, new AvoidEnumerableAssertionsOnQueryableAnalyzer()));
    }

    [TestMethod]
    public async Task CancellationFlow_LocalFunction_UsesOwningContextWithoutTreatingItsBlockAsAnExpressionTree()
    {
        const string source = """
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class Consumer
            {
                public TestContext Context { get; set; }
                [TestMethod]
                public async Task Check()
                {
                    async Task Local() { await Task.Delay(17); }
                    await Local();
                }
            }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source);

        AssertSingleDiagnostic(
            await AnalyzerContinuationTestHelpers.AnalyzeAsync(document, new FlowTestContextCancellationTokenAnalyzer()),
            source, "MSTEST0049", DiagnosticSeverity.Info, "Task.Delay(17)",
            "Consider using the overload that accepts a CancellationToken and pass 'TestContext.CancellationToken'",
            [("TestContextMemberNamePropertyKey", "Context")]);
    }

    [TestMethod]
    [DataRow("Culture")]
    [DataRow("Directory")]
    [DataRow("File")]
    [DataRow("Process")]
    [DataRow("Blocking")]
    public async Task RuntimeSafety_FixtureOnlyFramework_StillReportsWithoutTestMethodOrResourceLockContracts(string scenario)
    {
        (string statement, string id, string message) = scenario switch
        {
            "Culture" => (
                "CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture",
                "MSTEST0076",
                "'CultureInfo.DefaultThreadCurrentCulture' sets the default culture for the whole process, so every concurrently running test observes the change, which can corrupt formatting and parsing in tests running in parallel; avoid mutating it in a parallelized test suite, or declare '[DoNotParallelize]'"),
            "Directory" => (
                "Directory.SetCurrentDirectory(\"fixture\")",
                "MSTEST0075",
                "'Directory.SetCurrentDirectory' changes the process-global current directory, which has no per-thread equivalent and is observed by every concurrently running test; avoid mutating it in a parallelized test suite (declare '[ResourceLock(WellKnownResources.CurrentDirectory)]' to at least coordinate, or '[DoNotParallelize]')"),
            "File" => (
                "File.WriteAllText(\"shared.txt\", \"value\")",
                "MSTEST0077",
                "The path 'shared.txt' is a fixed location shared by every test in the assembly (and, when relative, resolves against the process-global current directory), so parallel tests that write to it can collide; use a unique per-test location such as 'TestContext.TestTempDirectory'"),
            "Process" => (
                "Environment.SetEnvironmentVariable(\"fixture\", \"value\")",
                "MSTEST0074",
                "'Environment.SetEnvironmentVariable' mutates process-global state and is unsafe when tests run in parallel; declare '[ResourceLock(WellKnownResources.EnvironmentVariables)]' on the test (or '[DoNotParallelize]')"),
            "Blocking" => (
                "Thread.Sleep(1)",
                "MSTEST0067",
                "Avoid 'Thread.Sleep' in test code as it can cause test flakiness; consider an asynchronous alternative such as 'await Task.Delay' or 'await' the task"),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario)),
        };
        string source = $$"""
            using System;
            using System.Globalization;
            using System.IO;
            using System.Threading;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                public sealed class TestInitializeAttribute : Attribute { }
            }
            public class Consumer
            {
                [TestInitialize]
                public void Setup() { {{statement}}; }
            }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source, includeMSTest: false);
        Compilation compilation = (await document.Project.GetCompilationAsync())!;
        Assert.IsNull(compilation.GetTypeByMetadataName("Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute"));
        Assert.IsNull(compilation.GetTypeByMetadataName("Microsoft.VisualStudio.TestTools.UnitTesting.ResourceLockAttribute"));

        ImmutableArray<Diagnostic> diagnostics = await AnalyzerContinuationTestHelpers.AnalyzeAsync(document, CreateRuntimeAnalyzer(scenario),
            new AnalyzerOptions([], new FixedOptionsProvider(("mstest_parallel_safety_mode", "always"))), enableDisabledRules: true);

        AssertSingleDiagnostic(diagnostics, source, id, DiagnosticSeverity.Info, statement, message);
    }

    [TestMethod]
    [DataRow("UnmappedAction")]
    [DataRow("GetAwaiterArgument")]
    [DataRow("GetResultArgument")]
    [DataRow("StaticGetResult")]
    [DataRow("StaticGetAwaiter")]
    public async Task AsyncAssertion_NonstandardAwaiterAndActionContracts_DoNotHideTheRealBlockedTask(string scenario)
    {
        string rejected = scenario switch
        {
            "UnmappedAction" => "Assert.Throws(() => Task.CompletedTask.GetAwaiter().GetResult(), __arglist(17))",
            "GetAwaiterArgument" => "Assert.ThrowsExactly(() => new TaskLike().GetAwaiter(17).GetResult())",
            "GetResultArgument" => "Assert.ThrowsExactly(() => new TaskLike().GetAwaiter().GetResult(17))",
            "StaticGetResult" => "Assert.ThrowsExactly(() => StaticResult.GetResult())",
            "StaticGetAwaiter" => "Assert.ThrowsExactly(() => StaticAwaitable.GetAwaiter().GetResult())",
            _ => throw new ArgumentOutOfRangeException(nameof(scenario)),
        };
        string source = $$"""
            using System;
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                public class TestMethodAttribute : Attribute { }
                public static class Assert
                {
                    public static void Throws(Action callback, __arglist) { }
                    public static void ThrowsExactly(Action action) { }
                }
            }
            public class Awaiter
            {
                public void GetResult() { }
                public void GetResult(int value) { }
            }
            public class TaskLike : Task
            {
                public TaskLike() : base(() => { }) { }
                public new Awaiter GetAwaiter() => new Awaiter();
                public Awaiter GetAwaiter(int value) => new Awaiter();
            }
            public static class StaticResult { public static void GetResult() { } }
            public static class StaticAwaitable { public static Awaiter GetAwaiter() => new Awaiter(); }
            public class Consumer
            {
                [TestMethod]
                public void Check()
                {
                    {{rejected}};
                    Assert.ThrowsExactly(() => Task.CompletedTask.GetAwaiter().GetResult());
                }
            }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source, includeMSTest: false);

        AssertSingleDiagnostic(
            await AnalyzerContinuationTestHelpers.AnalyzeAsync(document, new PreferAsyncAssertionAnalyzer()),
            source, "MSTEST0064", DiagnosticSeverity.Info, "Assert.ThrowsExactly(() => Task.CompletedTask.GetAwaiter().GetResult())",
            "Use 'Assert.ThrowsExactlyAsync' instead of blocking an async call in 'Assert.ThrowsExactly'");
    }

    [TestMethod]
    [DataRow("public TestContext Context { private get; set; }", DisplayName = "PrivateContextGetter")]
    [DataRow("public static TestContext Context { get; set; }", DisplayName = "StaticContextGetter")]
    [DataRow("public TestContext Context { set { } }", DisplayName = "MissingContextGetter")]
    public async Task DependencyTarget_InvalidContextGetter_PreventsAClassFromBeingARunnableDependency(string property)
    {
        string source = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            [TestClass]
            public class InvalidTarget
            {
                {{property}}
                [TestMethod] public void Setup() { }
            }
            [TestClass]
            public class Consumer
            {
                [TestMethod, DependsOn(typeof(InvalidTarget), nameof(InvalidTarget.Setup))]
                public void Check() { }
            }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source);

        AssertSingleDiagnostic(
            await AnalyzerContinuationTestHelpers.AnalyzeAsync(document, new DependsOnShouldBeValidAnalyzer()),
            source, "MSTEST0078", DiagnosticSeverity.Warning, "DependsOn(typeof(InvalidTarget), nameof(InvalidTarget.Setup))",
            "'[DependsOn]' references type 'InvalidTarget', which is not a test class, so the dependency is ignored at run time");
    }

    [TestMethod]
    public async Task StringReturnValue_SameNamedMethodOnAnotherType_IsNotAStringPredicate()
    {
        const string source = """
            public class StringLike
            {
                public bool Contains(string value) => false;
            }
            public class Consumer
            {
                public void Check(string text, StringLike other)
                {
                    text.Contains("piece");
                    other.Contains("piece");
                }
            }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source, includeMSTest: false);

        AssertSingleDiagnostic(
            await AnalyzerContinuationTestHelpers.AnalyzeAsync(document, new IgnoreStringMethodReturnValueAnalyzer()),
            source, "MSTEST0055", DiagnosticSeverity.Warning, "text.Contains(\"piece\")",
            "The return value of 'Contains' should not be ignored");
    }

    private static DiagnosticAnalyzer CreateRuntimeAnalyzer(string scenario)
        => scenario switch
        {
            "Culture" => new CultureMutationUnderParallelizationAnalyzer(),
            "Directory" => new CurrentDirectoryMutationUnderParallelizationAnalyzer(),
            "File" => new SharedFileSystemPathInTestAnalyzer(),
            "Process" => new UndeclaredProcessGlobalStateMutationAnalyzer(),
            "Blocking" => new AvoidThreadSleepAndTaskWaitInTestsAnalyzer(),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario)),
        };

    private static Diagnostic AssertSingleDiagnostic(
        ImmutableArray<Diagnostic> diagnostics,
        string source,
        string id,
        DiagnosticSeverity severity,
        string span,
        string message,
        (string Key, string? Value)[]? properties = null,
        string[]? additionalSpans = null)
    {
        Diagnostic diagnostic = Assert.ContainsSingle(diagnostics);
        AnalyzerContinuationTestHelpers.AssertDiagnostic(diagnostic, id, severity, source, span, message);
        Assert.AreEqual(new TextSpan(source.IndexOf(span, StringComparison.Ordinal), span.Length), diagnostic.Location.SourceSpan);
        Assert.HasCount(properties?.Length ?? 0, diagnostic.Properties);
        foreach ((string key, string? value) in properties ?? [])
        {
            Assert.IsTrue(diagnostic.Properties.ContainsKey(key), key);
            Assert.AreEqual(value, diagnostic.Properties[key], key);
        }

        Assert.AreSequenceEqual(
            additionalSpans ?? [],
            diagnostic.AdditionalLocations.Select(location => source.Substring(location.SourceSpan.Start, location.SourceSpan.Length)).ToArray());
        return diagnostic;
    }

    private sealed class FixedOptionsProvider(params (string Key, string Value)[] entries) : AnalyzerConfigOptionsProvider
    {
        public override AnalyzerConfigOptions GlobalOptions { get; } = new FixedOptions(entries);

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => new FixedOptions([]);

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => new FixedOptions([]);

        private sealed class FixedOptions((string Key, string Value)[] entries) : AnalyzerConfigOptions
        {
            public override bool TryGetValue(string key, out string value)
            {
                foreach ((string name, string configuredValue) in entries)
                {
                    if (name == key)
                    {
                        value = configuredValue;
                        return true;
                    }
                }

                value = string.Empty;
                return false;
            }
        }
    }
}
