// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using MSTest.AotReflection.SourceGeneration.Generators;

namespace MSTest.AotReflection.SourceGeneration.UnitTests;

/// <summary>
/// Behavior tests for <see cref="MSTestReflectionMetadataGenerator" />.
/// These pin the current PoC output so the upcoming follow-up PRs (#1837) can extend it safely.
/// </summary>
[TestClass]
public sealed class MSTestReflectionMetadataGeneratorTests
{
    /// <summary>
    /// Minimal MSTest attribute stubs so the generator can locate <c>[TestClass]</c> /
    /// <c>[TestMethod]</c> in test fixtures without dragging the real TestFramework
    /// assemblies into the Roslyn compilation.
    /// </summary>
    private const string MinimalMSTestStub = """
        namespace Microsoft.VisualStudio.TestTools.UnitTesting
        {
            [System.AttributeUsage(System.AttributeTargets.Class)]
            public class TestClassAttribute : System.Attribute { }

            [System.AttributeUsage(System.AttributeTargets.Method, Inherited = false)]
            public class TestMethodAttribute : System.Attribute
            {
                public TestMethodAttribute() { }
                public TestMethodAttribute(string displayName) { DisplayName = displayName; }
                public string? DisplayName { get; set; }
            }

            [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
            public class TestCategoryAttribute : System.Attribute
            {
                public TestCategoryAttribute(string category) { Category = category; }
                public string Category { get; }
            }

            [System.AttributeUsage(System.AttributeTargets.Property)]
            public class TestContextAttribute : System.Attribute { }

            [System.AttributeUsage(System.AttributeTargets.Method)]
            public class TestInitializeAttribute : System.Attribute { }

            [System.AttributeUsage(System.AttributeTargets.Method)]
            public class TestCleanupAttribute : System.Attribute { }

            [System.AttributeUsage(System.AttributeTargets.Assembly, AllowMultiple = true)]
            public class ParallelizeAttribute : System.Attribute
            {
                public int Workers { get; set; }
                public string? Scope { get; set; }
            }

            [System.AttributeUsage(System.AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
            public class DataRowAttribute : System.Attribute
            {
                public DataRowAttribute(object? data1) { }
                public DataRowAttribute(object? data1, params object?[] moreData) { }
            }
        }
        """;

    [TestMethod]
    public void Generator_EmitsSupportTypes_OnAnyCompilation()
    {
        const string userCode = """
            // Intentionally empty — no [TestClass] in the consumer.
            """;

        GeneratorRunResult result = RunGenerator(MinimalMSTestStub, userCode);

        result.Diagnostics.Should().BeEmpty();
        // Support types are emitted via RegisterPostInitializationOutput → always present.
        string support = result.GeneratedSources
            .Single(s => s.HintName == "MSTestReflectionMetadata.SupportTypes.g.cs")
            .SourceText.ToString();

        support.Should().Contain("namespace MSTest.SourceGenerated");
        support.Should().Contain("internal sealed class TestClassReflectionInfo");
        support.Should().Contain("internal sealed class TestMethodReflectionInfo");
        support.Should().Contain("internal sealed class TestPropertyReflectionInfo");
        support.Should().Contain("internal sealed class TestConstructorReflectionInfo");
    }

    [TestMethod]
    public void Generator_EmitsRegistry_WithDiscoveredTestClass()
    {
        const string userCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Sample
            {
                [TestClass]
                public class MyTests
                {
                    [TestMethod]
                    public void Test1() { }
                }
            }
            """;

        GeneratorRunResult result = RunGenerator(MinimalMSTestStub, userCode);

        result.Diagnostics.Should().BeEmpty();
        string registry = GetRegistry(result);

        registry.Should().Contain("internal static class MSTestReflectionMetadata");
        registry.Should().Contain("public const string AssemblyName = \"TestSample\";");
        registry.Should().Contain("Type = typeof(global::Sample.MyTests)");
        registry.Should().Contain("Name = \"Test1\"");
        registry.Should().Contain("Invoke = static (instance, args) => { ((global::Sample.MyTests)instance!).Test1(); return Task.CompletedTask; },");
    }

    [TestMethod]
    public void Generator_EmitsEmptyRegistry_WhenNoTestClasses()
    {
        const string userCode = """
            namespace Sample
            {
                // No [TestClass] anywhere.
                public class NotATest { public void Foo() { } }
            }
            """;

        GeneratorRunResult result = RunGenerator(MinimalMSTestStub, userCode);

        result.Diagnostics.Should().BeEmpty();
        string registry = GetRegistry(result);
        registry.Should().Contain("public static IReadOnlyList<TestClassReflectionInfo> TestClasses { get; } = new TestClassReflectionInfo[]");
        // No concrete TestClassReflectionInfo instance is emitted (note the open paren).
        registry.Should().NotContain("new TestClassReflectionInfo(");
    }

    [TestMethod]
    public void Generator_SkipsStaticTestClass()
    {
        const string userCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Sample
            {
                [TestClass]
                public static class StaticTests
                {
                    [TestMethod]
                    public static void Test1() { }
                }
            }
            """;

        GeneratorRunResult result = RunGenerator(MinimalMSTestStub, userCode);

        result.Diagnostics.Should().ContainSingle(d => d.Id == "AOTSG0001");
        string registry = GetRegistry(result);
        // Static classes are excluded from the registry (cannot be instantiated) and reported via AOTSG0001.
        registry.Should().NotContain("StaticTests");
    }

    [TestMethod]
    public void Generator_SkipsAbstractTestClass()
    {
        const string userCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Sample
            {
                [TestClass]
                public abstract class AbstractTests
                {
                    [TestMethod]
                    public void Test1() { }
                }

                [TestClass]
                public class ConcreteTests
                {
                    [TestMethod]
                    public void Test2() { }
                }
            }
            """;

        GeneratorRunResult result = RunGenerator(MinimalMSTestStub, userCode);

        result.Diagnostics.Should().BeEmpty();
        string registry = GetRegistry(result);
        // Abstract classes are filtered in BuildModel — they cannot be instantiated.
        registry.Should().NotContain("AbstractTests");
        registry.Should().Contain("typeof(global::Sample.ConcreteTests)");
    }

    [TestMethod]
    public void Generator_SkipsGenericTestClass()
    {
        const string userCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Sample
            {
                [TestClass]
                public class GenericTests<T>
                {
                    [TestMethod]
                    public void Test1() { }
                }
            }
            """;

        GeneratorRunResult result = RunGenerator(MinimalMSTestStub, userCode);

        result.Diagnostics.Should().ContainSingle(d => d.Id == "AOTSG0002");
        string registry = GetRegistry(result);
        // Open-generic test classes are out of scope for this PoC and reported via AOTSG0002.
        registry.Should().NotContain("GenericTests");
    }

    [TestMethod]
    public void Generator_EmitsConstructorInvoker()
    {
        const string userCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Sample
            {
                [TestClass]
                public class CtorTests
                {
                    public CtorTests() { }

                    [TestMethod]
                    public void Test1() { }
                }
            }
            """;

        GeneratorRunResult result = RunGenerator(MinimalMSTestStub, userCode);

        result.Diagnostics.Should().BeEmpty();
        string registry = GetRegistry(result);
        registry.Should().Contain("Constructors = new TestConstructorReflectionInfo[]");
        registry.Should().Contain("Invoke = static args => new global::Sample.CtorTests(),");
    }

    [TestMethod]
    public void Generator_EmitsParameterTypes_ForMethodWithParameters()
    {
        const string userCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Sample
            {
                [TestClass]
                public class ParamTests
                {
                    [TestMethod]
                    public void Test1(int x, string y) { }
                }
            }
            """;

        GeneratorRunResult result = RunGenerator(MinimalMSTestStub, userCode);

        result.Diagnostics.Should().BeEmpty();
        string registry = GetRegistry(result);
        registry.Should().Contain("ParameterTypes = new Type[] { typeof(int), typeof(string) }");
        registry.Should().Contain("ParameterNames = new string[] { \"x\", \"y\" }");
    }

    [TestMethod]
    public void Generator_FlagsAsyncReturnTypes()
    {
        const string userCode = """
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Sample
            {
                [TestClass]
                public class AsyncTests
                {
                    [TestMethod]
                    public Task Test1() => Task.CompletedTask;

                    [TestMethod]
                    public ValueTask Test2() => default;
                }
            }
            """;

        GeneratorRunResult result = RunGenerator(MinimalMSTestStub, userCode);

        result.Diagnostics.Should().BeEmpty();
        string registry = GetRegistry(result);
        registry.Should().Contain("Name = \"Test1\"");
        registry.Should().Contain("ReturnsTask = true");
        registry.Should().Contain("Name = \"Test2\"");
        registry.Should().Contain("ReturnsValueTask = true");
    }

    [TestMethod]
    public void Generator_CapturesClassLevelAttributes()
    {
        const string userCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Sample
            {
                [TestClass]
                [TestCategory("Smoke")]
                public class TaggedTests
                {
                    [TestMethod]
                    public void Test1() { }
                }
            }
            """;

        GeneratorRunResult result = RunGenerator(MinimalMSTestStub, userCode);

        result.Diagnostics.Should().BeEmpty();
        string registry = GetRegistry(result);
        registry.Should().Contain("global::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute");
        registry.Should().Contain("global::Microsoft.VisualStudio.TestTools.UnitTesting.TestCategoryAttribute");
        registry.Should().Contain("\"Smoke\"");
    }

    [TestMethod]
    public void Generator_EmitsPropertyGetterAndSetter()
    {
        const string userCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Sample
            {
                public class TestContext { }

                [TestClass]
                public class PropTests
                {
                    [TestContext]
                    public TestContext? Context { get; set; }

                    [TestMethod]
                    public void Test1() { }
                }
            }
            """;

        GeneratorRunResult result = RunGenerator(MinimalMSTestStub, userCode);

        result.Diagnostics.Should().BeEmpty();
        string registry = GetRegistry(result);
        registry.Should().Contain("Name = \"Context\"");
        registry.Should().Contain("HasPublicSetter = true");
        registry.Should().Contain("Get = static instance => instance is null ? null : (object?)((global::Sample.PropTests)instance).Context,");
        registry.Should().Contain("Set = static (instance, value) => ((global::Sample.PropTests)instance!).Context = (global::Sample.TestContext)value!,");
    }

    [TestMethod]
    public void Generator_EmittedSource_CompilesCleanly()
    {
        const string userCode = """
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Sample
            {
                public class TestContext { }

                [TestClass]
                [TestCategory("Smoke")]
                public class FullShape
                {
                    [TestContext]
                    public TestContext? Context { get; set; }

                    public FullShape() { }

                    [TestMethod("alias")]
                    public void Sync(int x) { }

                    [TestMethod]
                    public Task Asynchronous() => Task.CompletedTask;
                }
            }
            """;

        Compilation outputCompilation = RunGeneratorAndGetCompilation(MinimalMSTestStub, userCode);

        IEnumerable<Diagnostic> diagnostics = outputCompilation
            .GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error);

        diagnostics.Should().BeEmpty(
            "the generated source MUST compile cleanly when consumed in the same compilation as the user code");
    }

    [TestMethod]
    public void Generator_SkipsProtectedMembers()
    {
        const string userCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Sample
            {
                public class TestContext { }

                [TestClass]
                public class ProtectedShapes
                {
                    [TestContext]
                    protected TestContext? Context { get; set; }

                    [TestMethod]
                    protected void ProtectedTest() { }

                    [TestMethod]
                    private protected void PrivateProtectedTest() { }

                    [TestMethod]
                    protected internal void ProtectedInternalTest() { }
                }
            }
            """;

        Compilation outputCompilation = RunGeneratorAndGetCompilation(MinimalMSTestStub, userCode);
        string registry = outputCompilation
            .SyntaxTrees
            .Single(t => t.FilePath.EndsWith("MSTestReflectionMetadata.Registry.g.cs", System.StringComparison.Ordinal))
            .ToString();

        registry.Should().NotContain("ProtectedTest");
        registry.Should().NotContain("PrivateProtectedTest");
        registry.Should().NotContain("Context");
        registry.Should().Contain("ProtectedInternalTest");

        IEnumerable<Diagnostic> errors = outputCompilation
            .GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error);
        errors.Should().BeEmpty("the registry can only call members accessible from a non-derived type in the same assembly");
    }

    [TestMethod]
    public void Generator_StripsNullableAnnotation_FromTypeofExpressions()
    {
        const string userCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Sample
            {
                public class TestContext { }

                [TestClass]
                public class NullableShapes
                {
                    [TestContext]
                    public TestContext? Context { get; set; }

                    [TestMethod]
                    public void TakesNullableRef(string? value) { }

                    [TestMethod]
                    public void TakesNullableValueType(int? n) { }
                }
            }
            """;

        Compilation outputCompilation = RunGeneratorAndGetCompilation(MinimalMSTestStub, userCode);
        string registry = outputCompilation
            .SyntaxTrees
            .Single(t => t.FilePath.EndsWith("MSTestReflectionMetadata.Registry.g.cs", System.StringComparison.Ordinal))
            .ToString();

        // typeof(...) MUST NOT carry nullable reference type annotation (CS8639).
        registry.Should().NotContain("typeof(global::Sample.TestContext?)");
        registry.Should().NotContain("typeof(string?)");
        // Reference types in typeof drop the annotation entirely.
        registry.Should().Contain("typeof(global::Sample.TestContext)");
        registry.Should().Contain("typeof(string)");
        // Nullable value types are still distinct from their underlying type and must be preserved as Nullable<T>.
        registry.Should().Contain("typeof(int?)");

        // The whole compilation must be free of CS errors.
        IEnumerable<Diagnostic> errors = outputCompilation
            .GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error);
        errors.Should().BeEmpty("typeof(T?) on a reference type is invalid C# (CS8639)");
    }

    [TestMethod]
    public void Generator_IsIncremental_SupportTypesAreCached_WhenInputUnchanged()
    {
        const string userCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Sample
            {
                [TestClass]
                public class IncTests
                {
                    [TestMethod]
                    public void Test1() { }
                }
            }
            """;

        CSharpCompilation compilation = CreateCompilation(MinimalMSTestStub, userCode);
        GeneratorDriver driver = CSharpGeneratorDriver
            .Create(new MSTestReflectionMetadataGenerator())
            .WithUpdatedParseOptions((CSharpParseOptions)compilation.SyntaxTrees.First().Options);

        // Track step output cache reasons.
        driver = driver.RunGenerators(compilation);
        driver = driver.RunGenerators(compilation);

        GeneratorDriverRunResult result = driver.GetRunResult();
        result.Diagnostics.Should().BeEmpty();
        result.Results.Should().ContainSingle();
        // Two passes against the same compilation must produce identical sources.
        result.Results[0].GeneratedSources.Should().HaveCount(2);
    }

    [TestMethod]
    public void Generator_IncludesMethodsFromBaseType()
    {
        const string userCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Sample
            {
                public class BaseTests
                {
                    [TestInitialize]
                    public void Setup() { }

                    [TestMethod]
                    public void InheritedTest() { }
                }

                [TestClass]
                public class DerivedTests : BaseTests
                {
                    [TestMethod]
                    public void DerivedTest() { }
                }
            }
            """;

        string registry = GetRegistry(RunGenerator(MinimalMSTestStub, userCode));

        registry.Should().Contain("Name = \"InheritedTest\"");
        registry.Should().Contain("Name = \"Setup\"");
        registry.Should().Contain("Name = \"DerivedTest\"");
        // The TestInitialize attribute applied on the base method must propagate too.
        registry.Should().Contain("global::Microsoft.VisualStudio.TestTools.UnitTesting.TestInitializeAttribute");
    }

    [TestMethod]
    public void Generator_IncludesMethodsFromMultiLevelInheritance()
    {
        const string userCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Sample
            {
                public class GrandparentTests
                {
                    [TestMethod]
                    public void GrandparentTest() { }
                }

                public class ParentTests : GrandparentTests
                {
                    [TestMethod]
                    public void ParentTest() { }
                }

                [TestClass]
                public class LeafTests : ParentTests
                {
                    [TestMethod]
                    public void LeafTest() { }
                }
            }
            """;

        string registry = GetRegistry(RunGenerator(MinimalMSTestStub, userCode));

        registry.Should().Contain("Name = \"GrandparentTest\"");
        registry.Should().Contain("Name = \"ParentTest\"");
        registry.Should().Contain("Name = \"LeafTest\"");
    }

    [TestMethod]
    public void Generator_OverriddenVirtualMethod_KeepsOnlyDerivedImplementation()
    {
        const string userCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Sample
            {
                public class BaseTests
                {
                    [TestMethod]
                    public virtual void Run() { }
                }

                [TestClass]
                public class DerivedTests : BaseTests
                {
                    public override void Run() { }
                }
            }
            """;

        string registry = GetRegistry(RunGenerator(MinimalMSTestStub, userCode));

        // Only one entry for Run should be emitted, and the invoker must dispatch on the derived type.
        int runEntries = registry.Split(["Name = \"Run\""], System.StringSplitOptions.None).Length - 1;
        runEntries.Should().Be(1, "the derived override must replace the base entry (not duplicate it)");
        registry.Should().Contain("((global::Sample.DerivedTests)instance!).Run();");
        registry.Should().NotContain("((global::Sample.BaseTests)instance!).Run();");

        // TestMethodAttribute is not inherited, so the override should not pick up the base attribute.
        registry.Should().NotContain("global::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute");
    }

    [TestMethod]
    public void Generator_OverriddenVirtualMethod_HonorsInheritedAttributeUsage()
    {
        const string userCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Sample
            {
                public class BaseTests
                {
                    [TestMethod]
                    [TestCategory("Base")]
                    [DataRow(1)]
                    public virtual void Run(int value) { }
                }

                [TestClass]
                public class DerivedTests : BaseTests
                {
                    [TestMethod]
                    [TestCategory("Derived")]
                    public override void Run(int value) { }
                }
            }
            """;

        string registry = GetRegistry(RunGenerator(MinimalMSTestStub, userCode));

        registry.Should().Contain("\"Base\"");
        registry.Should().Contain("\"Derived\"");
        registry.Should().Contain("DataRows = Array.Empty<object?[]>()");
        registry.Should().NotContain("new object?[] { 1 }");
    }

    [TestMethod]
    public void Generator_NewKeywordHiddenMethod_DedupsBySignature()
    {
        const string userCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Sample
            {
                public class BaseTests
                {
                    [TestMethod]
                    public void Hidden() { }
                }

                [TestClass]
                public class DerivedTests : BaseTests
                {
                    [TestMethod]
                    public new void Hidden() { }
                }
            }
            """;

        string registry = GetRegistry(RunGenerator(MinimalMSTestStub, userCode));

        int hiddenEntries = registry.Split(["Name = \"Hidden\""], System.StringSplitOptions.None).Length - 1;
        hiddenEntries.Should().Be(1, "members with the same name and signature must be de-duplicated; derived wins");
        registry.Should().Contain("((global::Sample.DerivedTests)instance!).Hidden();");
    }

    [TestMethod]
    public void Generator_OverloadsWithDifferentSignatures_AreAllPreserved()
    {
        const string userCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Sample
            {
                public class BaseTests
                {
                    [TestMethod]
                    public void Op(int x) { }
                }

                [TestClass]
                public class DerivedTests : BaseTests
                {
                    [TestMethod]
                    public void Op(string x) { }
                }
            }
            """;

        string registry = GetRegistry(RunGenerator(MinimalMSTestStub, userCode));

        // Both overloads survive — they have different signatures.
        int opEntries = registry.Split(["Name = \"Op\""], System.StringSplitOptions.None).Length - 1;
        opEntries.Should().Be(2);
        registry.Should().Contain("typeof(int)");
        registry.Should().Contain("typeof(string)");
    }

    [TestMethod]
    public void Generator_IncludesPropertiesFromBaseType()
    {
        const string userCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Sample
            {
                public class TestContext { }

                public class BaseTests
                {
                    [TestContext]
                    public virtual TestContext Context { get; set; } = new();
                }

                [TestClass]
                public class DerivedTests : BaseTests
                {
                    [TestMethod]
                    public void Test() { }
                }
            }
            """;

        string registry = GetRegistry(RunGenerator(MinimalMSTestStub, userCode));

        registry.Should().Contain("Name = \"Context\"");
        registry.Should().Contain("global::Microsoft.VisualStudio.TestTools.UnitTesting.TestContextAttribute");
    }

    [TestMethod]
    public void Generator_DoesNotInheritConstructors()
    {
        const string userCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Sample
            {
                public class BaseTests
                {
                    public BaseTests(int x) { }
                }

                [TestClass]
                public class DerivedTests : BaseTests
                {
                    public DerivedTests() : base(1) { }

                    [TestMethod]
                    public void Test() { }
                }
            }
            """;

        string registry = GetRegistry(RunGenerator(MinimalMSTestStub, userCode));

        // Only the derived ctor (parameterless) should be emitted — base ctor is never inherited.
        registry.Should().Contain("Invoke = static args => new global::Sample.DerivedTests(),");
        registry.Should().NotContain("Invoke = static args => new global::Sample.BaseTests(");
        // No int parameter from the base constructor leaks into the constructor list.
        registry.Should().NotContain("ParameterTypes = new Type[] { typeof(int) },");
    }

    [TestMethod]
    public void Generator_AbstractBaseWithConcreteDerived_FoldsBaseMembers()
    {
        const string userCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Sample
            {
                public class TestContext { }

                public abstract class AbstractBase
                {
                    [TestInitialize]
                    public void Setup() { }

                    [TestContext]
                    public TestContext Ctx { get; set; } = new();
                }

                [TestClass]
                public class Concrete : AbstractBase
                {
                    [TestMethod]
                    public void Test() { }
                }
            }
            """;

        string registry = GetRegistry(RunGenerator(MinimalMSTestStub, userCode));

        registry.Should().Contain("Name = \"Setup\"");
        registry.Should().Contain("Name = \"Ctx\"");
        registry.Should().Contain("Name = \"Test\"");
        // The base class was abstract but the concrete derived type is the one emitted.
        registry.Should().Contain("Type = typeof(global::Sample.Concrete)");
    }

    [TestMethod]
    public void Generator_DoesNotWalkPastSystemObject()
    {
        const string userCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Sample
            {
                [TestClass]
                public class SimpleTests
                {
                    [TestMethod]
                    public void Test() { }
                }
            }
            """;

        string registry = GetRegistry(RunGenerator(MinimalMSTestStub, userCode));

        // Members of System.Object (ToString, Equals, GetHashCode, GetType) must NOT be emitted.
        registry.Should().NotContain("Name = \"ToString\"");
        registry.Should().NotContain("Name = \"Equals\"");
        registry.Should().NotContain("Name = \"GetHashCode\"");
        registry.Should().NotContain("Name = \"GetType\"");
    }

    [TestMethod]
    public void Generator_CapturesAssemblyLevelAttribute()
    {
        const string userCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: Parallelize(Workers = 4, Scope = "Method")]

            namespace Sample
            {
                [TestClass]
                public class Tests
                {
                    [TestMethod]
                    public void Test() { }
                }
            }
            """;

        GeneratorRunResult result = RunGenerator(MinimalMSTestStub, userCode);

        result.Diagnostics.Should().BeEmpty();
        string registry = GetRegistry(result);
        registry.Should().Contain("public static IReadOnlyList<Attribute> AssemblyAttributes { get; } = new Attribute[]");
        registry.Should().Contain("new global::Microsoft.VisualStudio.TestTools.UnitTesting.ParallelizeAttribute()");
        registry.Should().Contain("Workers = 4");
        registry.Should().Contain("Scope = \"Method\"");
    }

    [TestMethod]
    public void Generator_AssemblyAttributes_IsEmptyArray_WhenNoneApplied()
    {
        const string userCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Sample
            {
                [TestClass]
                public class Tests
                {
                    [TestMethod]
                    public void Test() { }
                }
            }
            """;

        GeneratorRunResult result = RunGenerator(MinimalMSTestStub, userCode);

        result.Diagnostics.Should().BeEmpty();
        string registry = GetRegistry(result);
        registry.Should().Contain("public static IReadOnlyList<Attribute> AssemblyAttributes { get; } = Array.Empty<Attribute>();");
        registry.Should().NotContain("public static IReadOnlyList<Attribute> AssemblyAttributes { get; } = new Attribute[]");
    }

    [TestMethod]
    public void Generator_CapturesMultipleAssemblyAttributes_InDeclarationOrder()
    {
        const string userCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: Parallelize(Workers = 1)]
            [assembly: Parallelize(Workers = 2)]
            [assembly: Parallelize(Workers = 3)]

            namespace Sample
            {
                [TestClass]
                public class Tests
                {
                    [TestMethod]
                    public void Test() { }
                }
            }
            """;

        GeneratorRunResult result = RunGenerator(MinimalMSTestStub, userCode);

        result.Diagnostics.Should().BeEmpty();
        string registry = GetRegistry(result);

        int idx1 = registry.IndexOf("Workers = 1", StringComparison.Ordinal);
        int idx2 = registry.IndexOf("Workers = 2", StringComparison.Ordinal);
        int idx3 = registry.IndexOf("Workers = 3", StringComparison.Ordinal);

        idx1.Should().BeGreaterThan(-1);
        idx2.Should().BeGreaterThan(idx1);
        idx3.Should().BeGreaterThan(idx2);
    }

    [TestMethod]
    public void Generator_AssemblyAttributes_AreEmittedEvenWithNoTestClasses()
    {
        const string userCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: Parallelize(Workers = 8)]

            namespace Sample
            {
                public class NotATest { }
            }
            """;

        GeneratorRunResult result = RunGenerator(MinimalMSTestStub, userCode);

        result.Diagnostics.Should().BeEmpty();
        string registry = GetRegistry(result);
        registry.Should().Contain("new global::Microsoft.VisualStudio.TestTools.UnitTesting.ParallelizeAttribute()");
        registry.Should().Contain("Workers = 8");
        registry.Should().NotContain("new TestClassReflectionInfo(");
    }

    [TestMethod]
    public void Generator_EmitsEmptyDataRows_WhenMethodHasNoDataRow()
    {
        const string userCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Sample
            {
                [TestClass]
                public class Tests
                {
                    [TestMethod]
                    public void NoData() { }
                }
            }
            """;

        GeneratorRunResult result = RunGenerator(MinimalMSTestStub, userCode);

        result.Diagnostics.Should().BeEmpty();
        string registry = GetRegistry(result);
        registry.Should().Contain("DataRows = Array.Empty<object?[]>()");
        registry.Should().NotContain("DataRows = new object?[][]");
    }

    [TestMethod]
    public void Generator_CapturesSingleDataRow_WithScalarArgs()
    {
        const string userCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Sample
            {
                [TestClass]
                public class Tests
                {
                    [TestMethod]
                    [DataRow(1, "x")]
                    public void Test(int a, string b) { }
                }
            }
            """;

        GeneratorRunResult result = RunGenerator(MinimalMSTestStub, userCode);

        result.Diagnostics.Should().BeEmpty();
        string registry = GetRegistry(result);
        registry.Should().Contain("DataRows = new object?[][]");
        registry.Should().Contain("new object?[] { 1, \"x\" }");
    }

    [TestMethod]
    public void Generator_CapturesMultipleDataRows_InDeclarationOrder()
    {
        const string userCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Sample
            {
                [TestClass]
                public class Tests
                {
                    [TestMethod]
                    [DataRow(1, "a")]
                    [DataRow(2, "b")]
                    [DataRow(3, "c")]
                    public void Test(int a, string b) { }
                }
            }
            """;

        GeneratorRunResult result = RunGenerator(MinimalMSTestStub, userCode);

        result.Diagnostics.Should().BeEmpty();
        string registry = GetRegistry(result);
        registry.Should().Contain("DataRows = new object?[][]");

        int idx1 = registry.IndexOf("new object?[] { 1, \"a\" }", StringComparison.Ordinal);
        int idx2 = registry.IndexOf("new object?[] { 2, \"b\" }", StringComparison.Ordinal);
        int idx3 = registry.IndexOf("new object?[] { 3, \"c\" }", StringComparison.Ordinal);

        idx1.Should().BeGreaterThan(-1);
        idx2.Should().BeGreaterThan(idx1);
        idx3.Should().BeGreaterThan(idx2);
    }

    [TestMethod]
    public void Generator_FlattensParamsArrayInDataRow()
    {
        const string userCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Sample
            {
                [TestClass]
                public class Tests
                {
                    [TestMethod]
                    [DataRow(1, 2, 3, 4)]
                    public void Test(int a, int b, int c, int d) { }
                }
            }
            """;

        GeneratorRunResult result = RunGenerator(MinimalMSTestStub, userCode);

        result.Diagnostics.Should().BeEmpty();
        string registry = GetRegistry(result);
        // The variadic `params object?[] moreData` tail must be flattened into a single flat row
        // within the DataRows block — the row contains all four values inline, not nested.
        registry.Should().Contain("new object?[] { 1, 2, 3, 4 }");
    }

    [TestMethod]
    public void Generator_HandlesNullValueInDataRow()
    {
        const string userCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Sample
            {
                [TestClass]
                public class Tests
                {
                    [TestMethod]
                    [DataRow(null)]
                    public void Test(string? value) { }
                }
            }
            """;

        GeneratorRunResult result = RunGenerator(MinimalMSTestStub, userCode);

        result.Diagnostics.Should().BeEmpty();
        string registry = GetRegistry(result);
        registry.Should().Contain("DataRows = new object?[][]");
        // The single-arg DataRowAttribute(object? data1) overload binds null to object,
        // which surfaces as `(object)null!` from BuildConstantExpression (C# keyword form
        // produced by FullyQualifiedFormat for System.Object).
        registry.Should().Contain("new object?[] { (object)null! }");
    }

    [TestMethod]
    public void Generator_SupportType_DeclaresInvokeAsTaskReturning()
    {
        const string userCode = """
            // Empty consumer — we only care about the post-init support types.
            """;

        GeneratorRunResult result = RunGenerator(MinimalMSTestStub, userCode);

        result.Diagnostics.Should().BeEmpty();
        string support = result.GeneratedSources
            .Single(s => s.HintName == "MSTestReflectionMetadata.SupportTypes.g.cs")
            .SourceText.ToString();

        support.Should().Contain("using System.Threading.Tasks;");
        // Invoke must be Task-returning so consumers can await without type-testing the result.
        support.Should().Contain("public Func<object?, object?[]?, Task> Invoke { get; init; } = static (_, _) => Task.CompletedTask;");
    }

    [TestMethod]
    public void Generator_InvokerForVoidMethod_ReturnsCompletedTask()
    {
        const string userCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Sample
            {
                [TestClass]
                public class Tests
                {
                    [TestMethod]
                    public void SyncVoid() { }
                }
            }
            """;

        string registry = GetRegistry(RunGenerator(MinimalMSTestStub, userCode));

        registry.Should().Contain("Invoke = static (instance, args) => { ((global::Sample.Tests)instance!).SyncVoid(); return Task.CompletedTask; },");
    }

    [TestMethod]
    public void Generator_InvokerForTaskMethod_ForwardsTask()
    {
        const string userCode = """
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Sample
            {
                [TestClass]
                public class Tests
                {
                    [TestMethod]
                    public Task AsyncTask() => Task.CompletedTask;
                }
            }
            """;

        string registry = GetRegistry(RunGenerator(MinimalMSTestStub, userCode));

        // Task and Task<T> both forward via the same `Task? __t = …` path; null is tolerated so
        // the invoker contract (non-null Task) holds even for a misbehaving test method.
        registry.Should().Contain("Invoke = static (instance, args) => { Task? __t = ((global::Sample.Tests)instance!).AsyncTask(); return __t ?? Task.CompletedTask; },");
    }

    [TestMethod]
    public void Generator_InvokerForTaskOfTMethod_ForwardsTask()
    {
        const string userCode = """
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Sample
            {
                [TestClass]
                public class Tests
                {
                    [TestMethod]
                    public Task<int> AsyncTaskOfInt() => Task.FromResult(42);
                }
            }
            """;

        string registry = GetRegistry(RunGenerator(MinimalMSTestStub, userCode));

        registry.Should().Contain("Invoke = static (instance, args) => { Task? __t = ((global::Sample.Tests)instance!).AsyncTaskOfInt(); return __t ?? Task.CompletedTask; },");
    }

    [TestMethod]
    public void Generator_InvokerForValueTaskMethod_UnwrapsViaAsTask()
    {
        const string userCode = """
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Sample
            {
                [TestClass]
                public class Tests
                {
                    [TestMethod]
                    public ValueTask AsyncValueTask() => default;
                }
            }
            """;

        string registry = GetRegistry(RunGenerator(MinimalMSTestStub, userCode));

        // ValueTask unwrap uses IsCompletedSuccessfully so the synchronous-completion fast path
        // skips the Task allocation; only when the operation actually went async do we pay AsTask().
        registry.Should().Contain("Invoke = static (instance, args) => { var __vt = ((global::Sample.Tests)instance!).AsyncValueTask(); return __vt.IsCompletedSuccessfully ? Task.CompletedTask : __vt.AsTask(); },");
    }

    [TestMethod]
    public void Generator_InvokerForValueTaskOfTMethod_UnwrapsViaAsTask()
    {
        const string userCode = """
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Sample
            {
                [TestClass]
                public class Tests
                {
                    [TestMethod]
                    public ValueTask<string> AsyncValueTaskOfString() => new ValueTask<string>("ok");
                }
            }
            """;

        string registry = GetRegistry(RunGenerator(MinimalMSTestStub, userCode));

        registry.Should().Contain("Invoke = static (instance, args) => { var __vt = ((global::Sample.Tests)instance!).AsyncValueTaskOfString(); return __vt.IsCompletedSuccessfully ? Task.CompletedTask : __vt.AsTask(); },");
    }

    [TestMethod]
    public void Generator_InvokerForNonVoidSyncMethod_DiscardsResultAndReturnsCompletedTask()
    {
        const string userCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Sample
            {
                [TestClass]
                public class Tests
                {
                    [TestMethod]
                    public int SyncInt() => 42;
                }
            }
            """;

        string registry = GetRegistry(RunGenerator(MinimalMSTestStub, userCode));

        // For a sync non-void test the returned value is discarded but the call must still execute
        // (its side-effects ARE the test). We surface that with a `_ = call;` pattern.
        registry.Should().Contain("Invoke = static (instance, args) => { _ = ((global::Sample.Tests)instance!).SyncInt(); return Task.CompletedTask; },");
    }

    [TestMethod]
    public void Generator_EmittedRegistry_ImportsSystemThreadingTasks()
    {
        const string userCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Sample
            {
                [TestClass]
                public class Tests
                {
                    [TestMethod]
                    public void Test() { }
                }
            }
            """;

        string registry = GetRegistry(RunGenerator(MinimalMSTestStub, userCode));

        // The registry file references Task.CompletedTask directly in every invoker, so it must
        // bring System.Threading.Tasks into scope.
        registry.Should().Contain("using System.Threading.Tasks;");
    }

    [TestMethod]
    public void Diagnostic_AOTSG0002_ReportedForNestedClassInsideGenericOuter()
    {
        const string userCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Sample
            {
                public class Outer<T>
                {
                    [TestClass]
                    public class InnerTests
                    {
                        [TestMethod]
                        public void Test1() { }
                    }
                }
            }
            """;

        GeneratorRunResult result = RunGenerator(MinimalMSTestStub, userCode);

        result.Diagnostics.Should().ContainSingle(d => d.Id == "AOTSG0002");
        string registry = GetRegistry(result);
        registry.Should().NotContain("InnerTests");
    }

    [TestMethod]
    public void Diagnostic_AOTSG0003_ReportedForFileLocalClass()
    {
        const string userCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Sample;

            [TestClass]
            file class FileLocalTests
            {
                [TestMethod]
                public void Test1() { }
            }
            """;

        GeneratorRunResult result = RunGenerator(MinimalMSTestStub, userCode);

        result.Diagnostics.Should().ContainSingle(d => d.Id == "AOTSG0003");
        string registry = GetRegistry(result);
        registry.Should().NotContain("FileLocalTests");
    }

    [TestMethod]
    public void Diagnostic_AOTSG0003_ReportedForPrivateNestedClass()
    {
        const string userCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Sample
            {
                public class Outer
                {
                    [TestClass]
                    private class HiddenTests
                    {
                        [TestMethod]
                        public void Test1() { }
                    }
                }
            }
            """;

        GeneratorRunResult result = RunGenerator(MinimalMSTestStub, userCode);

        result.Diagnostics.Should().ContainSingle(d => d.Id == "AOTSG0003");
        string registry = GetRegistry(result);
        registry.Should().NotContain("HiddenTests");
    }

    [TestMethod]
    public void Diagnostic_AOTSG0003_NotReportedForInternalNestedInPublicOuter()
    {
        const string userCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Sample
            {
                public class Outer
                {
                    [TestClass]
                    internal class VisibleTests
                    {
                        [TestMethod]
                        public void Test1() { }
                    }
                }
            }
            """;

        GeneratorRunResult result = RunGenerator(MinimalMSTestStub, userCode);

        result.Diagnostics.Should().BeEmpty();
        string registry = GetRegistry(result);
        registry.Should().Contain("typeof(global::Sample.Outer.VisibleTests)");
    }

    [TestMethod]
    public void Diagnostic_AOTSG0003_ReportedWhenOuterIsPrivateNested()
    {
        const string userCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Sample
            {
                public class Outer
                {
                    private class HiddenOuter
                    {
                        [TestClass]
                        public class Tests
                        {
                            [TestMethod]
                            public void Test1() { }
                        }
                    }
                }
            }
            """;

        GeneratorRunResult result = RunGenerator(MinimalMSTestStub, userCode);

        result.Diagnostics.Should().ContainSingle(d => d.Id == "AOTSG0003");
        string registry = GetRegistry(result);
        registry.Should().NotContain("typeof(global::Sample.Outer.HiddenOuter.Tests)");
    }

    [TestMethod]
    public void Diagnostic_AOTSG0004_ReportedForGenericTestMethod_OtherMethodsStillEmitted()
    {
        const string userCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Sample
            {
                [TestClass]
                public class Tests
                {
                    [TestMethod]
                    public void GenericMethod<T>() { }

                    [TestMethod]
                    public void NormalMethod() { }
                }
            }
            """;

        GeneratorRunResult result = RunGenerator(MinimalMSTestStub, userCode);

        result.Diagnostics.Should().ContainSingle(d => d.Id == "AOTSG0004");
        string registry = GetRegistry(result);
        // The class itself is still emitted because at least one supported member remains.
        registry.Should().Contain("typeof(global::Sample.Tests)");
        // The generic method is excluded from the registry; the normal one is present.
        registry.Should().NotContain("\"GenericMethod\"");
        registry.Should().Contain("\"NormalMethod\"");
    }

    [TestMethod]
    public void Diagnostic_AOTSG0005_ReportedForByRefParameter()
    {
        const string userCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Sample
            {
                [TestClass]
                public class Tests
                {
                    [TestMethod]
                    public void RefParam(ref int x) { }

                    [TestMethod]
                    public void NormalMethod() { }
                }
            }
            """;

        GeneratorRunResult result = RunGenerator(MinimalMSTestStub, userCode);

        result.Diagnostics.Should().ContainSingle(d => d.Id == "AOTSG0005");
        string registry = GetRegistry(result);
        registry.Should().Contain("typeof(global::Sample.Tests)");
        registry.Should().NotContain("\"RefParam\"");
        registry.Should().Contain("\"NormalMethod\"");
    }

    [TestMethod]
    public void Diagnostic_AOTSG0005_ReportedForOutAndInParameters()
    {
        const string userCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Sample
            {
                [TestClass]
                public class Tests
                {
                    [TestMethod]
                    public void OutParam(out int x) { x = 0; }

                    [TestMethod]
                    public void InParam(in int x) { }
                }
            }
            """;

        GeneratorRunResult result = RunGenerator(MinimalMSTestStub, userCode);

        result.Diagnostics.Where(d => d.Id == "AOTSG0005").Should().HaveCount(2);
        string registry = GetRegistry(result);
        registry.Should().NotContain("\"OutParam\"");
        registry.Should().NotContain("\"InParam\"");
    }

    [TestMethod]
    public void Diagnostic_AOTSG0005_ReportedForByRefConstructorParameter()
    {
        const string userCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Sample
            {
                [TestClass]
                public class Tests
                {
                    public Tests() { }

                    public Tests(ref int x) { }

                    [TestMethod]
                    public void Test1() { }
                }
            }
            """;

        GeneratorRunResult result = RunGenerator(MinimalMSTestStub, userCode);

        result.Diagnostics.Should().ContainSingle(d => d.Id == "AOTSG0005");
        string registry = GetRegistry(result);
        // The valid parameterless constructor is still emitted.
        registry.Should().Contain("typeof(global::Sample.Tests)");
    }

    [TestMethod]
    public void Diagnostic_NoneReportedForWellFormedTestClass()
    {
        const string userCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Sample
            {
                [TestClass]
                public class Tests
                {
                    [TestMethod]
                    public void Test1() { }

                    [TestMethod]
                    public void Test2(int x, string y) { }
                }
            }
            """;

        GeneratorRunResult result = RunGenerator(MinimalMSTestStub, userCode);

        result.Diagnostics.Should().BeEmpty();
    }

    private static string GetRegistry(GeneratorRunResult result)
        => result.GeneratedSources
            .Single(s => s.HintName == "MSTestReflectionMetadata.Registry.g.cs")
            .SourceText.ToString()
            .Replace("\r\n", "\n");

    private static GeneratorRunResult RunGenerator(params string[] sources)
    {
        CSharpCompilation compilation = CreateCompilation(sources);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new MSTestReflectionMetadataGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);
        return driver.GetRunResult().Results[0];
    }

    private static Compilation RunGeneratorAndGetCompilation(params string[] sources)
    {
        CSharpCompilation compilation = CreateCompilation(sources);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new MSTestReflectionMetadataGenerator());
        driver.RunGeneratorsAndUpdateCompilation(compilation, out Compilation outputCompilation, out _);
        return outputCompilation;
    }

    private static CSharpCompilation CreateCompilation(params string[] sources)
    {
        IEnumerable<SyntaxTree> trees = sources.Select(s => CSharpSyntaxTree.ParseText(s));
        MetadataReference[] references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Runtime.CompilerServices.ModuleInitializerAttribute).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Reflection.Assembly).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Collections.Generic.Dictionary<,>).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Reflection.MethodInfo).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Linq.Enumerable).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Threading.Tasks.Task).Assembly.Location),
        };

        return CSharpCompilation.Create(
            "TestSample",
            trees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }
}
