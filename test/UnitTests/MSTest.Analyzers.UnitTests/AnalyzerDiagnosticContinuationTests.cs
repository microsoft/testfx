// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class AnalyzerDiagnosticContinuationTests
{
    [TestMethod]
    [DataRow(ReportDiagnostic.Error, ReportDiagnostic.Error, DiagnosticSeverity.Error, true)]
    [DataRow(ReportDiagnostic.Error, ReportDiagnostic.Warn, DiagnosticSeverity.Error, true)]
    [DataRow(ReportDiagnostic.Error, ReportDiagnostic.Hidden, DiagnosticSeverity.Error, true)]
    [DataRow(ReportDiagnostic.Warn, ReportDiagnostic.Error, DiagnosticSeverity.Error, true)]
    [DataRow(ReportDiagnostic.Warn, ReportDiagnostic.Warn, DiagnosticSeverity.Warning, true)]
    [DataRow(ReportDiagnostic.Warn, ReportDiagnostic.Info, DiagnosticSeverity.Warning, true)]
    [DataRow(ReportDiagnostic.Info, ReportDiagnostic.Error, DiagnosticSeverity.Error, true)]
    [DataRow(ReportDiagnostic.Info, ReportDiagnostic.Warn, DiagnosticSeverity.Warning, true)]
    [DataRow(ReportDiagnostic.Info, ReportDiagnostic.Info, DiagnosticSeverity.Info, true)]
    [DataRow(ReportDiagnostic.Info, ReportDiagnostic.Hidden, DiagnosticSeverity.Info, true)]
    [DataRow(ReportDiagnostic.Hidden, ReportDiagnostic.Error, DiagnosticSeverity.Error, true)]
    [DataRow(ReportDiagnostic.Hidden, ReportDiagnostic.Warn, DiagnosticSeverity.Warning, true)]
    [DataRow(ReportDiagnostic.Hidden, ReportDiagnostic.Info, DiagnosticSeverity.Info, true)]
    [DataRow(ReportDiagnostic.Hidden, ReportDiagnostic.Hidden, DiagnosticSeverity.Hidden, true)]
    [DataRow(ReportDiagnostic.Default, ReportDiagnostic.Error, DiagnosticSeverity.Info, false)]
    [DataRow(ReportDiagnostic.Error, ReportDiagnostic.Default, DiagnosticSeverity.Error, true)]
    [DataRow(ReportDiagnostic.Default, ReportDiagnostic.Default, DiagnosticSeverity.Info, false)]
    [DataRow(ReportDiagnostic.Suppress, ReportDiagnostic.Error, DiagnosticSeverity.Info, false)]
    [DataRow(ReportDiagnostic.Error, ReportDiagnostic.Suppress, DiagnosticSeverity.Info, false)]
    public async Task NoLocationDiagnostic_PerTreeSeverity_UsesMostSevereUnlessSuppressed(
        ReportDiagnostic first,
        ReportDiagnostic second,
        DiagnosticSeverity expectedSeverity,
        bool expectedDiagnostic)
    {
        CSharpCompilationOptions options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            .WithSyntaxTreeOptionsProvider(new TreeSeverityOptions(first, second));
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, "public class First { }", options: options);
        document = document.Project.AddDocument("Second.cs", "public class Second { }").Project.GetDocument(document.Id)!;
        var analyzerOptions = new AnalyzerOptions([], new BuildOptionsProvider(adapterReferenced: true));

        ImmutableArray<Diagnostic> diagnostics = await AnalyzerContinuationTestHelpers.AnalyzeAsync(
            document, new UseParallelizeAttributeAnalyzer(), analyzerOptions, treeOptionsProvider: new TreeSeverityOptions(first, second));

        Assert.HasCount(expectedDiagnostic ? 1 : 0, diagnostics);
        if (expectedDiagnostic)
        {
            Diagnostic diagnostic = diagnostics[0];
            Assert.AreEqual("MSTEST0001", diagnostic.Id);
            Assert.AreEqual(expectedSeverity, diagnostic.Severity);
            Assert.AreEqual(Location.None, diagnostic.Location);
            Assert.IsEmpty(diagnostic.AdditionalLocations);
            Assert.AreEqual("Explicitly enable or disable tests parallelization", diagnostic.GetMessage(CultureInfo.InvariantCulture));
            Assert.AreEqual("https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0001", diagnostic.Descriptor.HelpLinkUri);
        }
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(true, true)]
    public async Task NoLocationDiagnostic_MissingAdapterOrAssemblyOptOut_DoesNotReport(bool adapterReferenced, bool explicitOptOut)
    {
        string source = explicitOptOut
            ? "[assembly: Microsoft.VisualStudio.TestTools.UnitTesting.DoNotParallelize] public class Example { }"
            : "public class Example { }";
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source);
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerContinuationTestHelpers.AnalyzeAsync(
            document,
            new UseParallelizeAttributeAnalyzer(),
            new AnalyzerOptions([], new BuildOptionsProvider(adapterReferenced)));
        Assert.IsEmpty(diagnostics);
    }

    [TestMethod]
    [DataRow("TestInitialize")]
    [DataRow("TestCleanup")]
    [DataRow("ClassInitialize")]
    [DataRow("AssemblyInitialize")]
    public async Task FixtureAnalyzers_MissingContracts_DoNotReport(string fixture)
    {
        DiagnosticAnalyzer analyzer = CreateFixtureAnalyzer(fixture);
        using var workspace = new AdhocWorkspace();
        Document noContracts = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(
            workspace, "public class NotATestProject { private void Setup() { } }", includeMSTest: false);
        Assert.IsEmpty(await AnalyzerContinuationTestHelpers.AnalyzeAsync(noContracts, analyzer));

        string source = $$"""
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                public class TestClassAttribute : Attribute { }
                public class {{fixture}}Attribute : Attribute { }
            }
            [TestClass]
            public class Tests
            {
                [{{fixture}}]
                public {{(fixture is "ClassInitialize" or "AssemblyInitialize" ? "static " : string.Empty)}}void Setup() { }
            }
            """;
        using var partialWorkspace = new AdhocWorkspace();
        Document partial = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(partialWorkspace, source, includeMSTest: false);
        Assert.IsEmpty(await AnalyzerContinuationTestHelpers.AnalyzeAsync(partial, CreateFixtureAnalyzer(fixture)));
    }

    [TestMethod]
    [DataRow("TestInitialize", "MSTEST0008", "TestInitialize method 'Broken' signature is invalid")]
    [DataRow("TestCleanup", "MSTEST0009", "TestCleanup method 'Broken' signature is invalid")]
    [DataRow("ClassInitialize", "MSTEST0010", "ClassInitialize method 'Broken' signature is invalid")]
    [DataRow("AssemblyInitialize", "MSTEST0012", "AssemblyInitialize method 'Broken' signature is invalid")]
    public async Task FixtureAnalyzers_InvalidAndValidSignatures_ReportOnlyInvalidMembers(string fixture, string id, string message)
    {
        bool isStatic = fixture is "ClassInitialize" or "AssemblyInitialize";
        string source = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            [TestClass]
            public class Tests
            {
                [{{fixture}}]
                private {{(isStatic ? "static " : string.Empty)}}void Broken({{(isStatic ? "TestContext context" : string.Empty)}}) { }
                [{{fixture}}]
                public {{(isStatic ? "static " : string.Empty)}}void Valid({{(isStatic ? "TestContext context" : string.Empty)}}) { }
            }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source);
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerContinuationTestHelpers.AnalyzeAsync(document, CreateFixtureAnalyzer(fixture));
        Assert.HasCount(1, diagnostics);
        AnalyzerContinuationTestHelpers.AssertDiagnostic(diagnostics[0], id, DiagnosticSeverity.Warning, source, "Broken", message);
        Assert.IsFalse(diagnostics[0].Properties.ContainsKey("CannotFix"));
    }

    [TestMethod]
    public async Task NullAssertions_MemberKinds_DistinguishValueFromNullableReferences()
    {
        const string source = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class Tests
            {
                private int _number;
                public int Number { get; set; }
                public int? Optional { get; set; }
                public event Action Changed;
                private int GetNumber() => 23;
                public void Check(int parameter)
                {
                    int local = 17;
                    Assert.IsNull(Number);
                    Assert.IsNull(_number);
                    Assert.IsNull(local);
                    Assert.IsNull(Optional);
                    Assert.IsNull(Changed);
                    Assert.IsNull(GetNumber);
                    Assert.IsNull(parameter);
                }
            }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source);
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerContinuationTestHelpers.AnalyzeAsync(document, new PreferAssertFailOverAlwaysFalseConditionsAnalyzer());
        CollectionAssert.AreEqual(
            new[] { "Assert.IsNull(Number)", "Assert.IsNull(_number)", "Assert.IsNull(local)" },
            diagnostics.Select(diagnostic => source.Substring(diagnostic.Location.SourceSpan.Start, diagnostic.Location.SourceSpan.Length)).ToArray());
        foreach (Diagnostic diagnostic in diagnostics)
        {
            Assert.AreEqual("MSTEST0025", diagnostic.Id);
            Assert.AreEqual(DiagnosticSeverity.Warning, diagnostic.Severity);
            Assert.AreEqual("Use 'Assert.Fail' instead of an always-failing 'Assert.IsNull' assert", diagnostic.GetMessage(CultureInfo.InvariantCulture));
        }
    }

    [TestMethod]
    public async Task OptionalRule_EnabledExplicitly_ReportsInheritedDisposeMethods()
    {
        const string source = """
            using System;
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class Base : IDisposable, IAsyncDisposable
            {
                public virtual void Dispose() { }
                public virtual ValueTask DisposeAsync() => default;
            }
            [TestClass]
            public class Tests : Base
            {
                public override void Dispose() { }
                public override ValueTask DisposeAsync() => default;
                public void Dispose(int unrelated) { }
                public Task DisposeAsync(int unrelated) => Task.CompletedTask;
            }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source);
        Assert.IsEmpty(await AnalyzerContinuationTestHelpers.AnalyzeAsync(document, new PreferTestCleanupOverDisposeAnalyzer()));
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerContinuationTestHelpers.AnalyzeAsync(
            document, new PreferTestCleanupOverDisposeAnalyzer(), enableDisabledRules: true);
        Assert.HasCount(2, diagnostics);
        CollectionAssert.AreEqual(
            new[] { "Dispose", "DisposeAsync" },
            diagnostics.Select(diagnostic => source.Substring(diagnostic.Location.SourceSpan.Start, diagnostic.Location.SourceSpan.Length)).ToArray());
        foreach (Diagnostic diagnostic in diagnostics)
        {
            Assert.AreEqual("MSTEST0022", diagnostic.Id);
            Assert.AreEqual(DiagnosticSeverity.Info, diagnostic.Severity);
            Assert.AreEqual("Prefer TestCleanup over 'Dispose' methods", diagnostic.GetMessage(CultureInfo.InvariantCulture));
        }
    }

    [TestMethod]
    public async Task AsyncAssertion_InterfaceImplementation_DoesNotChangeVoidContract()
    {
        const string source = """
            using System;
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public interface IContract { void Run(); }
            public class Implementation : IContract
            {
                [TestMethod]
                public void Run() => Assert.ThrowsExactly<Exception>(() => Task.CompletedTask.GetAwaiter().GetResult());
            }
            public class Independent
            {
                [TestMethod]
                public void Run() => Assert.ThrowsExactly<Exception>(() => Task.CompletedTask.GetAwaiter().GetResult());
            }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source);
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerContinuationTestHelpers.AnalyzeAsync(document, new PreferAsyncAssertionAnalyzer());
        Assert.HasCount(1, diagnostics);
        AnalyzerContinuationTestHelpers.AssertDiagnostic(diagnostics[0], "MSTEST0064", DiagnosticSeverity.Info, source,
            "Assert.ThrowsExactly<Exception>(() => Task.CompletedTask.GetAwaiter().GetResult())",
            "Use 'Assert.ThrowsExactlyAsync' instead of blocking an async call in 'Assert.ThrowsExactly'");
        Assert.IsTrue(diagnostics[0].Location.SourceSpan.Start > source.IndexOf("public class Independent", StringComparison.Ordinal));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Visibility_NestedPrivateAndInternalTypes_RespectDiscoveryOptIn(bool discoverInternals)
    {
        string source = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            {{(discoverInternals ? "[assembly: DiscoverInternals]" : string.Empty)}}
            public class Container
            {
                private class Hidden
                {
                    [TestMethod]
                    public void HiddenTest() { }
                }
            }
            internal class InternalTests
            {
                [TestMethod]
                public void InternalTest() { }
            }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source);
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerContinuationTestHelpers.AnalyzeAsync(document, new TestMethodShouldBeValidAnalyzer());
        CollectionAssert.AreEqual(
            discoverInternals ? new[] { "HiddenTest" } : ["HiddenTest", "InternalTest"],
            diagnostics.Select(diagnostic => source.Substring(diagnostic.Location.SourceSpan.Start, diagnostic.Location.SourceSpan.Length)).ToArray());
        foreach (Diagnostic diagnostic in diagnostics)
        {
            Assert.AreEqual("MSTEST0003", diagnostic.Id);
            Assert.AreEqual(DiagnosticSeverity.Warning, diagnostic.Severity);
        }
    }

    [TestMethod]
    public async Task Inheritance_ConstrainedQueryable_ReportsOnlyEnumerableContract()
    {
        const string source = """
            using System.Collections.Generic;
            using System.Linq;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class Tests
            {
                public void Check<T>(T query, List<int> cached) where T : IQueryable<int>
                {
                    Assert.HasCount(3, query);
                    Assert.IsEmpty(query);
                    Assert.HasCount(3, cached);
                    Assert.IsEmpty(cached);
                }
            }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source);
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerContinuationTestHelpers.AnalyzeAsync(document, new AvoidEnumerableAssertionsOnQueryableAnalyzer());
        Assert.HasCount(2, diagnostics);
        AnalyzerContinuationTestHelpers.AssertDiagnostic(diagnostics[0], "MSTEST0090", DiagnosticSeverity.Info, source,
            "Assert.HasCount(3, query)", "Use 'Queryable.Count' instead of 'Assert.HasCount' to avoid enumerating query results for a count or existence check");
        AnalyzerContinuationTestHelpers.AssertDiagnostic(diagnostics[1], "MSTEST0090", DiagnosticSeverity.Info, source,
            "Assert.IsEmpty(query)", "Use 'Queryable.Any' instead of 'Assert.IsEmpty' to avoid enumerating query results for a count or existence check");
    }

    [TestMethod]
    [DataRow("public", false, false)]
    [DataRow("internal", false, true)]
    [DataRow("internal", true, false)]
    public async Task TypeLookup_DuplicateAndInternalContracts_RespectVisibilityAndAmbiguity(string duplicateVisibility, bool grantAccess, bool expectedDiagnostic)
    {
        const string publicContracts = """
            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                public class TestClassAttribute : System.Attribute { }
                public class TestInitializeAttribute : System.Attribute { }
            }
            """;
        MetadataReference primary = await AnalyzerContinuationTestHelpers.EmitReferenceAsync(publicContracts, "PrimaryContract", "Primary");
        string duplicateContracts = $$"""
            {{(grantAccess ? "[assembly: System.Runtime.CompilerServices.InternalsVisibleTo(\"ContinuationFixture\")]" : string.Empty)}}
            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                {{duplicateVisibility}} class TestClassAttribute : System.Attribute { }
                {{duplicateVisibility}} class TestInitializeAttribute : System.Attribute { }
            }
            """;
        MetadataReference duplicate = await AnalyzerContinuationTestHelpers.EmitReferenceAsync(duplicateContracts, "DuplicateContract");
        const string source = """
            extern alias Primary;
            using TestClass = Primary::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
            using TestInitialize = Primary::Microsoft.VisualStudio.TestTools.UnitTesting.TestInitializeAttribute;
            [TestClass]
            public sealed class Tests
            {
                [TestInitialize]
                private void Setup() { }
            }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(
            workspace, source, includeMSTest: false, additionalReferences: [primary, duplicate]);
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerContinuationTestHelpers.AnalyzeAsync(document, new TestInitializeShouldBeValidAnalyzer());
        Assert.HasCount(expectedDiagnostic ? 1 : 0, diagnostics);
        if (expectedDiagnostic)
        {
            AnalyzerContinuationTestHelpers.AssertDiagnostic(diagnostics[0], "MSTEST0008", DiagnosticSeverity.Warning, source,
                "Setup", "TestInitialize method 'Setup' signature is invalid");
        }
    }

    private static DiagnosticAnalyzer CreateFixtureAnalyzer(string fixture) => fixture switch
    {
        "TestInitialize" => new TestInitializeShouldBeValidAnalyzer(),
        "TestCleanup" => new TestCleanupShouldBeValidAnalyzer(),
        "ClassInitialize" => new ClassInitializeShouldBeValidAnalyzer(),
        "AssemblyInitialize" => new AssemblyInitializeShouldBeValidAnalyzer(),
        _ => throw new ArgumentOutOfRangeException(nameof(fixture)),
    };

    private sealed class TreeSeverityOptions(ReportDiagnostic first, ReportDiagnostic second) : SyntaxTreeOptionsProvider
    {
        public override GeneratedKind IsGenerated(SyntaxTree tree, CancellationToken cancellationToken) => GeneratedKind.NotGenerated;

        public override bool TryGetDiagnosticValue(SyntaxTree tree, string diagnosticId, CancellationToken cancellationToken, out ReportDiagnostic severity)
        {
            severity = tree.FilePath.EndsWith("Second.cs", StringComparison.Ordinal) ? second : first;
            return diagnosticId == "MSTEST0001";
        }

        public override bool TryGetGlobalDiagnosticValue(string diagnosticId, CancellationToken cancellationToken, out ReportDiagnostic severity)
        {
            severity = default;
            return false;
        }
    }

    private sealed class BuildOptionsProvider(bool adapterReferenced) : AnalyzerConfigOptionsProvider
    {
        public override AnalyzerConfigOptions GlobalOptions { get; } = new BuildOptions(adapterReferenced);

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => GlobalOptions;

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => GlobalOptions;
    }

    private sealed class BuildOptions(bool adapterReferenced) : AnalyzerConfigOptions
    {
        public override bool TryGetValue(string key, out string value)
        {
            value = adapterReferenced ? "true" : "false";
            return key == "build_property.IsMSTestTestAdapterReferenced";
        }
    }

    [TestMethod]
    public async Task DisposeOverrides_UnresolvedBaseMethod_DoesNotAddAnalyzerNoise()
    {
        const string source = """
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            [TestClass]
            public class Tests
            {
                public override void Dispose() { }
                public override ValueTask DisposeAsync() => default;
            }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source);
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerContinuationTestHelpers.AnalyzeAsync(
            document, new PreferTestCleanupOverDisposeAnalyzer(), enableDisabledRules: true, expectedCompilerErrors: ["CS0115", "CS0115"]);
        Assert.IsEmpty(diagnostics);
    }

    [TestMethod]
    public async Task Inheritance_InterfaceUsedAsAttribute_DoesNotHideRealTestMethod()
    {
        const string source = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public interface IMarker { }
            public class Tests
            {
                [IMarker, TestMethod]
                public void Check()
                {
                    if (!OperatingSystem.IsLinux()) { return; }
                }
            }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source);
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerContinuationTestHelpers.AnalyzeAsync(
            document, new UseOSConditionAttributeInsteadOfRuntimeCheckAnalyzer(), expectedCompilerErrors: ["CS0653"]);
        Assert.HasCount(1, diagnostics);
        AnalyzerContinuationTestHelpers.AssertDiagnostic(diagnostics[0], "MSTEST0061", DiagnosticSeverity.Info, source,
            "if (!OperatingSystem.IsLinux()) { return; }",
            "Use '[OSCondition]' attribute instead of 'RuntimeInformation.IsOSPlatform' calls with early return or 'Assert.Inconclusive'");
    }
}
