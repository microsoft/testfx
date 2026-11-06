// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class AnalyzerResidualMetadataTests
{
    private const string AttributeContracts = """
        namespace Microsoft.VisualStudio.TestTools.UnitTesting
        {
            public sealed class TestClassAttribute : System.Attribute { }
            public class TestMethodAttribute : System.Attribute
            {
                public string DisplayName { get; set; }
            }
            [System.AttributeUsage(System.AttributeTargets.Method, AllowMultiple = true)]
            public sealed class DataRowAttribute : System.Attribute
            {
                public DataRowAttribute(params object[] data) { }
                public string DisplayName { get; set; }
            }
            public sealed class IgnoreAttribute : System.Attribute
            {
                public IgnoreAttribute(string message = "") { }
            }
            public enum DynamicDataSourceType { Property, Method, AutoDetect, Field }
            public sealed class DynamicDataAttribute : System.Attribute
            {
                public DynamicDataAttribute(string memberName) { }
                public DynamicDataAttribute(string memberName, DynamicDataSourceType sourceType) { }
            }
            public sealed class MemberConditionAttribute : System.Attribute
            {
                public MemberConditionAttribute(System.Type conditionType, string memberName) { }
            }
            public sealed class ResourceLockAttribute : System.Attribute
            {
                public ResourceLockAttribute(string resource) { }
            }
            public sealed class DependsOnAttribute : System.Attribute
            {
                public DependsOnAttribute(string methodName) { }
            }
            public sealed class TimeoutAttribute : System.Attribute
            {
                public TimeoutAttribute(int milliseconds) { }
                public bool CooperativeCancellation { get; set; }
            }
            public sealed class OwnerAttribute : System.Attribute
            {
                public OwnerAttribute(string owner) { }
            }
            public sealed class TestCleanupAttribute : System.Attribute { }
        }
        """;

    [TestMethod]
    [DataRow("RedundantDisplayName")]
    [DataRow("DuplicateRowDisplayName")]
    [DataRow("IgnoreMethod")]
    [DataRow("IgnoreClass")]
    public async Task MetadataAttributes_DiagnosticsWithoutAttributeSyntax_FallBackToNoLocation(string scenario)
    {
        const string declarations = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            [TestClass, Ignore]
            public class IgnoredClass { }
            public class Fixture
            {
                [TestMethod(DisplayName = "Check")]
                public void Check() { }
                [TestMethod, DataRow(1, DisplayName = "same"), DataRow(2, DisplayName = "same")]
                public void Rows(int value) { }
                [TestMethod, Ignore]
                public void Ignored() { }
            }
            """;
        MetadataReference reference = await AnalyzerContinuationTestHelpers.EmitReferenceAsync(
            declarations + Environment.NewLine + AttributeContracts, "MetadataAttributeContracts");
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(
            workspace,
            "public class Consumer { }", includeMSTest: false, additionalReferences: [reference]);
        Compilation compilation = (await document.Project.GetCompilationAsync())!;
        AnalyzerContinuationTestHelpers.AssertCompilerErrors(compilation);
        INamedTypeSymbol testMethod = Contract("TestMethodAttribute");
        INamedTypeSymbol testClass = Contract("TestClassAttribute");
        INamedTypeSymbol dataRow = Contract("DataRowAttribute");
        INamedTypeSymbol ignore = Contract("IgnoreAttribute");
        INamedTypeSymbol fixture = compilation.GetTypeByMetadataName("Fixture")!;
        ISymbol target = scenario switch
        {
            "RedundantDisplayName" => fixture.GetMembers("Check").Single(),
            "DuplicateRowDisplayName" => fixture.GetMembers("Rows").Single(),
            "IgnoreMethod" => fixture.GetMembers("Ignored").Single(),
            "IgnoreClass" => compilation.GetTypeByMetadataName("IgnoredClass")!,
            _ => throw new ArgumentOutOfRangeException(nameof(scenario)),
        };
        Assert.IsEmpty(target.DeclaringSyntaxReferences);
        Assert.IsTrue(target.GetAttributes().All(attribute => attribute.ApplicationSyntaxReference is null));
        var diagnostics = new List<Diagnostic>();
        SymbolAnalysisContext context = SymbolContext(target, compilation, diagnostics);
        string id;
        string message;
        DiagnosticSeverity severity;
        switch (scenario)
        {
            case "RedundantDisplayName":
                ProductReflection.Call(typeof(RedundantTestMethodDisplayNameAnalyzer), null, "AnalyzeSymbol",
                    [typeof(SymbolAnalysisContext), typeof(INamedTypeSymbol)], context, testMethod);
                id = "MSTEST0071";
                severity = DiagnosticSeverity.Info;
                message = "The 'DisplayName' is the same as the test method name 'Check' and can be removed";
                break;
            case "DuplicateRowDisplayName":
                ProductReflection.Call(typeof(DuplicateDataRowDisplayNameAnalyzer), null, "AnalyzeSymbol",
                    [typeof(SymbolAnalysisContext), typeof(INamedTypeSymbol), typeof(INamedTypeSymbol)], context, testMethod, dataRow);
                id = "MSTEST0087";
                severity = DiagnosticSeverity.Warning;
                message = "Do not duplicate the 'DataRow' display name 'same'. Each 'DataRow' on a test method should use a unique display name.";
                break;
            default:
                ProductReflection.Call(typeof(IgnoreShouldHaveJustificationAnalyzer), null,
                    scenario == "IgnoreClass" ? "AnalyzeType" : "AnalyzeMethod",
                    [typeof(SymbolAnalysisContext), typeof(INamedTypeSymbol), typeof(INamedTypeSymbol)],
                    context, ignore, scenario == "IgnoreClass" ? testClass : testMethod);
                id = "MSTEST0066";
                severity = DiagnosticSeverity.Info;
                message = $"'[Ignore]' should specify a message explaining why '{target.Name}' is ignored";
                break;
        }

        AssertNoLocationDiagnostic(diagnostics, id, severity, message);

        INamedTypeSymbol Contract(string name)
            => compilation.GetTypeByMetadataName("Microsoft.VisualStudio.TestTools.UnitTesting." + name)!;
    }

    [TestMethod]
    [DataRow("DataRow")]
    [DataRow("DynamicData")]
    [DataRow("MemberCondition")]
    [DataRow("ResourceLock")]
    [DataRow("DependsOn")]
    [DataRow("ExplicitDynamicData")]
    [DataRow("Timeout")]
    [DataRow("UseAttribute")]
    [DataRow("DependencyNoEffect")]
    public async Task MetadataAttributes_SyntaxRequiredValidation_DoesNotInventAnEditableDeclaration(string scenario)
    {
        string attributeText = scenario switch
        {
            "DataRow" => "DataRow(1)",
            "DynamicData" => "DynamicData(\"Missing\")",
            "MemberCondition" => "MemberCondition(typeof(Fixture), \"Missing\")",
            "ResourceLock" => "ResourceLock(\"database\")",
            "DependsOn" => "DependsOn(\"Missing\")",
            "ExplicitDynamicData" => "DynamicData(\"Missing\", DynamicDataSourceType.Method)",
            "Timeout" => "Timeout(100, CooperativeCancellation = false)",
            "UseAttribute" => "Owner(\"owner\")",
            "DependencyNoEffect" => "DependsOn(\"Missing\")",
            _ => throw new ArgumentOutOfRangeException(nameof(scenario)),
        };
        string declarations = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            [TestClass]
            public class Fixture
            {
                [{{(scenario is "UseAttribute" or "DependencyNoEffect" ? string.Empty : "TestMethod, ")}}{{attributeText}}]
                public void Check(string value) { }
            }
            """;
        MetadataReference reference = await AnalyzerContinuationTestHelpers.EmitReferenceAsync(
            declarations + Environment.NewLine + AttributeContracts, "SyntaxRequiredAttributeContracts");
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(
            workspace,
            "public class Consumer { }", includeMSTest: false, additionalReferences: [reference]);
        Compilation compilation = (await document.Project.GetCompilationAsync())!;
        AnalyzerContinuationTestHelpers.AssertCompilerErrors(compilation);
        INamedTypeSymbol fixture = compilation.GetTypeByMetadataName("Fixture")!;
        var method = (IMethodSymbol)fixture.GetMembers("Check").Single();
        string attributeName = scenario switch
        {
            "ExplicitDynamicData" => "DynamicDataAttribute",
            "UseAttribute" => "OwnerAttribute",
            "DependencyNoEffect" => "DependsOnAttribute",
            _ => scenario + "Attribute",
        };
        AttributeData attribute = method.GetAttributes().Single(candidate => candidate.AttributeClass!.Name == attributeName);
        Assert.IsNull(attribute.ApplicationSyntaxReference);
        Assert.IsFalse(attribute.ConstructorArguments.IsEmpty);
        Assert.IsEmpty(method.DeclaringSyntaxReferences);
        var diagnostics = new List<Diagnostic>();
        SymbolAnalysisContext context = SymbolContext(method, compilation, diagnostics);

        switch (scenario)
        {
            case "DataRow":
                ProductReflection.Call(typeof(DataRowShouldBeValidAnalyzer), null, "AnalyzeAttribute",
                    [typeof(SymbolAnalysisContext), typeof(AttributeData), typeof(IMethodSymbol)], context, attribute, method);
                break;
            case "DynamicData":
                ProductReflection.Call(typeof(DynamicDataShouldBeValidAnalyzer), null, "AnalyzeAttribute",
                    [typeof(SymbolAnalysisContext), typeof(AttributeData), typeof(IMethodSymbol), typeof(INamedTypeSymbol), typeof(INamedTypeSymbol)],
                    context, attribute, method, Contract("DynamicDataSourceType"), compilation.GetTypeByMetadataName("System.Reflection.MethodInfo"));
                break;
            case "MemberCondition":
                ProductReflection.Call(typeof(MemberConditionShouldBeValidAnalyzer), null, "AnalyzeAttribute",
                    [typeof(SymbolAnalysisContext), typeof(AttributeData)], context, attribute);
                break;
            case "ResourceLock":
                ProductReflection.Call(typeof(PreferConstantForResourceLockAnalyzer), null, "AnalyzeAttribute",
                    [typeof(SymbolAnalysisContext), typeof(AttributeData)], context, attribute);
                break;
            case "DependsOn":
            case "DependencyNoEffect":
                Type symbolsType = ProductReflection.Type(false, "MSTest.Analyzers.DependsOnShouldBeValidAnalyzer+AnalysisSymbols");
                object symbols = ProductReflection.Create(
                    symbolsType,
                    [typeof(INamedTypeSymbol), typeof(INamedTypeSymbol), typeof(INamedTypeSymbol), typeof(bool)],
                    Contract("DependsOnAttribute"), Contract("TestMethodAttribute"), Contract("TestClassAttribute"), false);
                if (scenario == "DependencyNoEffect")
                {
                    ProductReflection.Call(typeof(DependsOnShouldBeValidAnalyzer), null, "AnalyzeMethod",
                        [typeof(SymbolAnalysisContext), symbolsType], context, symbols);
                }
                else
                {
                    ProductReflection.Call(typeof(DependsOnShouldBeValidAnalyzer), null, "AnalyzeTarget",
                        [typeof(SymbolAnalysisContext), typeof(AttributeData), symbolsType, typeof(INamedTypeSymbol), typeof(IMethodSymbol)],
                        context, attribute, symbols, fixture, method);
                }

                break;
            case "ExplicitDynamicData":
                ProductReflection.Call(typeof(AvoidExplicitDynamicDataSourceTypeAnalyzer), null, "AnalyzeAttribute",
                    [typeof(SymbolAnalysisContext), typeof(AttributeData), typeof(INamedTypeSymbol)],
                    context, attribute, Contract("DynamicDataSourceType"));
                break;
            case "Timeout":
                ProductReflection.Call(typeof(UseCooperativeCancellationForTimeoutAnalyzer), null, "AnalyzeSymbol",
                    [typeof(SymbolAnalysisContext), typeof(INamedTypeSymbol)], context, Contract("TimeoutAttribute"));
                break;
            case "UseAttribute":
                IEnumerable<(INamedTypeSymbol, DiagnosticDescriptor)> rules = [(Contract("OwnerAttribute"), UseAttributeOnTestMethodAnalyzer.OwnerRule)];
                ProductReflection.Call(typeof(UseAttributeOnTestMethodAnalyzer), null, "AnalyzeSymbol",
                    [typeof(SymbolAnalysisContext), typeof(INamedTypeSymbol), typeof(IEnumerable<(INamedTypeSymbol, DiagnosticDescriptor)>)],
                    context, Contract("TestMethodAttribute"), rules);
                break;
        }

        Assert.IsEmpty(diagnostics);

        INamedTypeSymbol Contract(string name)
            => compilation.GetTypeByMetadataName("Microsoft.VisualStudio.TestTools.UnitTesting." + name)!;
    }

    [TestMethod]
    [DataRow("AotFixtureProvider")]
    [DataRow("InvalidFilterProvider")]
    public async Task AssemblyAttributes_FromLinkedNetmodule_AreValidatedWithoutSourceLocations(string scenario)
    {
        string marker = scenario == "AotFixtureProvider" ? "AssemblyFixtureProvider" : "TestFilterProvider(typeof(BadFilter))";
        string moduleSource = $$"""
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            [assembly: {{marker}}]
            [assembly: System.Reflection.AssemblyMetadata("Unrelated", "value")]
            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                [AttributeUsage(AttributeTargets.Assembly)]
                public sealed class AssemblyFixtureProviderAttribute : Attribute { }
                public interface ITestFilter { }
                [AttributeUsage(AttributeTargets.Assembly)]
                public sealed class TestFilterProviderAttribute : Attribute
                {
                    public TestFilterProviderAttribute(Type filterType) { }
                }
            }
            public class BadFilter { }
            """;
        MetadataReference module = await EmitModuleAsync(moduleSource);
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(
            workspace,
            "public class Consumer { }", includeMSTest: false, additionalReferences: [module]);
        Compilation compilation = (await document.Project.GetCompilationAsync())!;
        AnalyzerContinuationTestHelpers.AssertCompilerErrors(compilation);
        AttributeData markerAttribute = compilation.Assembly.GetAttributes().Single(attribute =>
            attribute.AttributeClass!.Name == (scenario == "AotFixtureProvider" ? "AssemblyFixtureProviderAttribute" : "TestFilterProviderAttribute"));
        Assert.IsNull(markerAttribute.ApplicationSyntaxReference);
        Assert.HasCount(2, compilation.Assembly.Modules);
        DiagnosticAnalyzer analyzer = scenario == "AotFixtureProvider"
            ? new AssemblyFixtureProviderNotSupportedWithNativeAotAnalyzer()
            : new TestFilterProviderShouldBeValidAnalyzer();
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerContinuationTestHelpers.AnalyzeAsync(document, analyzer,
            new AnalyzerOptions([], new AotOptionsProvider()));

        AssertNoLocationDiagnostic(
            diagnostics,
            scenario == "AotFixtureProvider" ? "MSTEST0072" : "MSTEST0081",
            DiagnosticSeverity.Warning,
            scenario == "AotFixtureProvider"
                ? "'[AssemblyFixtureProvider]' is not supported with ahead-of-time compilation (such as Native AOT or Blazor WebAssembly AOT) and will be ignored at run time"
                : "'[TestFilterProvider]' references type 'BadFilter', which does not implement 'ITestFilter'");
    }

    [TestMethod]
    [DataRow("", true, false, DisplayName = "OmittedInheritedDefaultsToTrue")]
    [DataRow(", Inherited = true", true, false, DisplayName = "ExplicitInheritedTrue")]
    [DataRow(", Inherited = false", false, false, DisplayName = "ExplicitInheritedFalse")]
    [DataRow("", true, true, DisplayName = "CustomAttributeWithoutAttributeSuffix")]
    public async Task LegacyCustomAttribute_AttributeUsageDefault_DecidesWhetherAnOverrideStillLosesItsTest(string inheritedSetting, bool reports, bool omitSuffix)
    {
        string legacySource = $$"""
            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                [System.AttributeUsage(System.AttributeTargets.Method, Inherited = false)]
                public class DataTestMethodAttribute : System.Attribute { }
            }
            namespace Fixture
            {
                [System.AttributeUsage(System.AttributeTargets.Method{{inheritedSetting}})]
                public sealed class {{(omitSuffix ? "DefaultInherited" : "DefaultInheritedAttribute")}} : Microsoft.VisualStudio.TestTools.UnitTesting.DataTestMethodAttribute { }
                public abstract class Base
                {
                    [DefaultInherited]
                    public virtual void Run() { }
                }
            }
            """;
        MetadataReference legacy = await AnalyzerContinuationTestHelpers.EmitReferenceAsync(
            legacySource,
            "Microsoft.VisualStudio.TestPlatform.TestFramework", "Legacy");
        const string source = """
            extern alias Legacy;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            [TestClass]
            public class Consumer : Legacy::Fixture.Base
            {
                public override void Run() { }
            }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source, additionalReferences: [legacy]);
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerContinuationTestHelpers.AnalyzeAsync(document, new InheritedMemberFromDifferentMSTestVersionAnalyzer());

        if (reports)
        {
            AssertLegacyDiagnostic(diagnostics, source, "DefaultInherited");
        }
        else
        {
            Assert.IsEmpty(diagnostics);
        }
    }

    [TestMethod]
    [DataRow("public new void Run(int value, int other) { }", DisplayName = "DifferentParameterCount")]
    [DataRow("public new void Run<T>(T value) { }", DisplayName = "DifferentMethodArity")]
    [DataRow("public new System.Threading.Tasks.Task Run(int value) => System.Threading.Tasks.Task.CompletedTask;", DisplayName = "DifferentReturnType")]
    public async Task LegacyTest_ShadowingRequiresTheWholeDiscoverySignatureToMatch(string hidingDeclaration)
    {
        const string legacySource = """
            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                public sealed class TestMethodAttribute : System.Attribute { }
            }
            namespace Fixture
            {
                public abstract class Base
                {
                    [Microsoft.VisualStudio.TestTools.UnitTesting.TestMethod]
                    public void Run(int value) { }
                }
            }
            """;
        MetadataReference legacy = await AnalyzerContinuationTestHelpers.EmitReferenceAsync(
            legacySource,
            "Microsoft.VisualStudio.TestPlatform.TestFramework", "Legacy");
        string source = $$"""
            extern alias Legacy;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            [TestClass]
            public class Consumer : Legacy::Fixture.Base
            {
                [TestMethod]
                {{hidingDeclaration}}
            }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source, additionalReferences: [legacy]);

        AssertLegacyDiagnostic(
            await AnalyzerContinuationTestHelpers.AnalyzeAsync(document, new InheritedMemberFromDifferentMSTestVersionAnalyzer()),
            source, "TestMethod");
    }

    [TestMethod]
    [DataRow(true, DisplayName = "VoidCleanupCanUseDispose")]
    [DataRow(false, DisplayName = "TaskCleanupNeedsAsyncDisposable")]
    public async Task CleanupMigration_WithoutAsyncDisposableContract_OnlyVoidCleanupCanBeReplaced(bool returnsVoid)
    {
        string declaration = returnsVoid
            ? "public void Check() { }"
            : "public System.Threading.Tasks.Task Check() => System.Threading.Tasks.Task.CompletedTask;";
        string source = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class Fixture
            {
                [TestCleanup]
                {{declaration}}
            }
            {{AttributeContracts}}
            """;
        MetadataReference reference = await AnalyzerContinuationTestHelpers.EmitReferenceAsync(source, "CleanupWithoutAsyncDisposableContract");
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, "public class Consumer { }",
            includeMSTest: false, additionalReferences: [reference]);
        Compilation compilation = (await document.Project.GetCompilationAsync())!;
        AnalyzerContinuationTestHelpers.AssertCompilerErrors(compilation);
        ISymbol method = compilation.GetTypeByMetadataName("Fixture")!.GetMembers("Check").Single();
        INamedTypeSymbol cleanup = compilation.GetTypeByMetadataName("Microsoft.VisualStudio.TestTools.UnitTesting.TestCleanupAttribute")!;
        var diagnostics = new List<Diagnostic>();

        ProductReflection.Call(typeof(PreferDisposeOverTestCleanupAnalyzer), null, "AnalyzeSymbol",
            [typeof(SymbolAnalysisContext), typeof(INamedTypeSymbol), typeof(INamedTypeSymbol)],
            SymbolContext(method, compilation, diagnostics), cleanup, null);

        if (returnsVoid)
        {
            AssertNoLocationDiagnostic(diagnostics, "MSTEST0021", DiagnosticSeverity.Info, "Prefer 'Dispose' over TestCleanup methods");
        }
        else
        {
            Assert.IsEmpty(diagnostics);
        }
    }

    [TestMethod]
    public async Task DependencyCycle_InheritedMetadataEdgeWithoutSyntax_DoesNotInventAnEditableDiagnosticLocation()
    {
        string source = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class Base
            {
                [TestMethod, DependsOn("Run")]
                public void Run() { }
            }
            """ + Environment.NewLine + AttributeContracts;
        MetadataReference reference = await AnalyzerContinuationTestHelpers.EmitReferenceAsync(source, "InheritedDependencyContracts");
        const string consumer = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            [TestClass]
            public class Consumer : Base { }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, consumer,
            includeMSTest: false, additionalReferences: [reference]);
        Compilation compilation = (await document.Project.GetCompilationAsync())!;
        AttributeData edge = compilation.GetTypeByMetadataName("Base")!.GetMembers("Run").Single().GetAttributes()
            .Single(attribute => attribute.AttributeClass!.Name == "DependsOnAttribute");
        Assert.IsNull(edge.ApplicationSyntaxReference);
        Assert.AreEqual("Run", edge.ConstructorArguments.Single().Value);

        Assert.IsEmpty(await AnalyzerContinuationTestHelpers.AnalyzeAsync(document, new DependsOnShouldBeValidAnalyzer()));
    }

    [TestMethod]
    [DataRow(true, DisplayName = "ArrayThenBeforeEachDerivedClass")]
    [DataRow(false, DisplayName = "ArrayThenNoInheritance")]
    public async Task LegacyClassFixture_MetadataArrayArgument_DoesNotHideTheFollowingInheritanceBehavior(bool inherits)
    {
        string legacySource = $$"""
            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                public class TestContext { }
                public enum InheritanceBehavior { None, BeforeEachDerivedClass }
                public class ClassInitializeAttribute : System.Attribute
                {
                    public ClassInitializeAttribute(InheritanceBehavior behavior) { }
                }
            }
            namespace Fixture
            {
                using Microsoft.VisualStudio.TestTools.UnitTesting;
                public sealed class TaggedInitializeAttribute : ClassInitializeAttribute
                {
                    public TaggedInitializeAttribute(int[] tags, InheritanceBehavior behavior) : base(behavior) { }
                }
                public abstract class Base
                {
                    [TaggedInitialize(new int[] { 17, 29 }, InheritanceBehavior.{{(inherits ? "BeforeEachDerivedClass" : "None")}})]
                    public static void Run(TestContext context) { }
                }
            }
            """;
        MetadataReference legacy = await AnalyzerContinuationTestHelpers.EmitReferenceAsync(
            legacySource,
            "Microsoft.VisualStudio.TestPlatform.TestFramework", "Legacy");
        const string source = """
            extern alias Legacy;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            [TestClass]
            public class Consumer : Legacy::Fixture.Base { }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source, additionalReferences: [legacy]);
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerContinuationTestHelpers.AnalyzeAsync(document, new InheritedMemberFromDifferentMSTestVersionAnalyzer());

        if (inherits)
        {
            AssertLegacyDiagnostic(diagnostics, source, "TaggedInitialize");
        }
        else
        {
            Assert.IsEmpty(diagnostics);
        }
    }

    [TestMethod]
    [DataRow(false, DisplayName = "OnlyAliasedTestClassFramework")]
    [DataRow(true, DisplayName = "GloballyVisibleFrameworkWins")]
    public async Task LegacyInheritance_AliasedTestClassFallback_IsUsedOnlyWithoutAGloballyVisibleFramework(bool globallyVisibleLegacyTestClass)
    {
        MetadataReference current = await AnalyzerContinuationTestHelpers.EmitReferenceAsync(
            """
            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                public sealed class TestClassAttribute : System.Attribute { }
            }
            """, "MSTest.TestFramework", "Current");
        string legacySource = $$"""
            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                public sealed class TestMethodAttribute : System.Attribute { }
                {{(globallyVisibleLegacyTestClass ? "public sealed class TestClassAttribute : System.Attribute { }" : string.Empty)}}
            }
            namespace Fixture
            {
                public abstract class Base
                {
                    [Microsoft.VisualStudio.TestTools.UnitTesting.TestMethod]
                    public void Run() { }
                }
            }
            """;
        MetadataReference legacy = await AnalyzerContinuationTestHelpers.EmitReferenceAsync(
            legacySource,
            "Microsoft.VisualStudio.TestPlatform.TestFramework");
        const string source = """
            extern alias Current;
            [Current::Microsoft.VisualStudio.TestTools.UnitTesting.TestClass]
            public class Consumer : Fixture.Base { }
            """;
        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, source,
            includeMSTest: false, additionalReferences: [current, legacy]);
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerContinuationTestHelpers.AnalyzeAsync(document, new InheritedMemberFromDifferentMSTestVersionAnalyzer());

        if (globallyVisibleLegacyTestClass)
        {
            Assert.IsEmpty(diagnostics);
        }
        else
        {
            AssertLegacyDiagnostic(diagnostics, source, "TestMethod");
        }
    }

    private static SymbolAnalysisContext SymbolContext(ISymbol symbol, Compilation compilation, List<Diagnostic> diagnostics)
    {
        // The metadata fallback is a private callback contract: the normal driver only schedules source symbols.
        // Use the real Roslyn context and real emitted symbols, not an ISymbol/AttributeData implementation.
        Action<Diagnostic> report = diagnostics.Add;
        Func<Diagnostic, bool> supported = diagnostic => diagnostic.Id.StartsWith("MSTEST", StringComparison.Ordinal);
        return (SymbolAnalysisContext)ProductReflection.Create(
            typeof(SymbolAnalysisContext),
            [typeof(ISymbol), typeof(Compilation), typeof(AnalyzerOptions), typeof(Action<Diagnostic>), typeof(Func<Diagnostic, bool>), typeof(CancellationToken)],
            symbol, compilation, new AnalyzerOptions([]), report, supported, CancellationToken.None);
    }

    private static async Task<MetadataReference> EmitModuleAsync(string source)
    {
        ImmutableArray<MetadataReference> references = await AnalyzerTestReferenceAssemblies.Default.ResolveAsync(LanguageNames.CSharp, CancellationToken.None);
        var compilation = CSharpCompilation.Create(
            "MetadataAttributeModule",
            [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview))],
            references, new CSharpCompilationOptions(OutputKind.NetModule));
        AnalyzerContinuationTestHelpers.AssertCompilerErrors(compilation);
        using var stream = new MemoryStream();
        Microsoft.CodeAnalysis.Emit.EmitResult result = compilation.Emit(stream);
        Assert.IsTrue(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        return MetadataReference.CreateFromImage(stream.ToArray(), MetadataReferenceProperties.Module);
    }

    private static void AssertNoLocationDiagnostic(IEnumerable<Diagnostic> diagnostics, string id, DiagnosticSeverity severity, string message)
    {
        Diagnostic diagnostic = Assert.ContainsSingle(diagnostics);
        Assert.AreEqual(id, diagnostic.Id);
        Assert.AreEqual(severity, diagnostic.Severity);
        Assert.AreEqual(message, diagnostic.GetMessage(CultureInfo.InvariantCulture));
        Assert.AreEqual(Location.None, diagnostic.Location);
        Assert.IsEmpty(diagnostic.AdditionalLocations);
        Assert.IsEmpty(diagnostic.Properties);
    }

    private static void AssertLegacyDiagnostic(ImmutableArray<Diagnostic> diagnostics, string source, string attributeName)
    {
        Diagnostic diagnostic = Assert.ContainsSingle(diagnostics);
        AnalyzerContinuationTestHelpers.AssertDiagnostic(diagnostic, "MSTEST0082", DiagnosticSeverity.Warning, source, "Consumer",
            $"The inherited member 'Base.Run' is annotated with a '[{attributeName}]' attribute that resolves to a canonical MSTest attribute in 'Microsoft.VisualStudio.TestPlatform.TestFramework', a different MSTest framework assembly than this test project uses; MSTest matches these attributes by exact type identity, so the inherited '[{attributeName}]' is silently ignored at run time. Recompile the assembly that declares 'Base' and, for custom attributes, the assembly that defines '[{attributeName}]', against the same MSTest version as the test project.");
        Assert.IsEmpty(diagnostic.AdditionalLocations);
        Assert.IsEmpty(diagnostic.Properties);
    }

    private sealed class AotOptionsProvider : AnalyzerConfigOptionsProvider
    {
        public override AnalyzerConfigOptions GlobalOptions { get; } = new AotOptions();

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => GlobalOptions;

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => GlobalOptions;

        private sealed class AotOptions : AnalyzerConfigOptions
        {
            public override bool TryGetValue(string key, out string value)
            {
                value = key == "build_property.PublishAot" ? "true" : string.Empty;
                return key == "build_property.PublishAot";
            }
        }
    }
}
