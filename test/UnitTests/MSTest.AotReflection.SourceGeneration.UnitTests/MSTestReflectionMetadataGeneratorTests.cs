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

            [System.AttributeUsage(System.AttributeTargets.Method)]
            public class TestMethodAttribute : System.Attribute
            {
                public TestMethodAttribute() { }
                public TestMethodAttribute(string displayName) { DisplayName = displayName; }
                public string? DisplayName { get; set; }
            }

            [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Method, AllowMultiple = true)]
            public class TestCategoryAttribute : System.Attribute
            {
                public TestCategoryAttribute(string category) { Category = category; }
                public string Category { get; }
            }

            [System.AttributeUsage(System.AttributeTargets.Property)]
            public class TestContextAttribute : System.Attribute { }
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
        registry.Should().Contain("Invoke = static (instance, args) => { ((global::Sample.MyTests)instance!).Test1(); return null; },");
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

        result.Diagnostics.Should().BeEmpty();
        string registry = GetRegistry(result);
        // Static classes are excluded by the predicate in the generator (cannot be instantiated).
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

        result.Diagnostics.Should().BeEmpty();
        string registry = GetRegistry(result);
        // Open-generic test classes are out of scope for this PoC.
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
        registry.Should().Contain("Set = static (instance, value) => ((global::Sample.PropTests)instance!).Context = (global::Sample.TestContext?)value!,");
    }

    [TestMethod]
    public void Generator_EmittedSource_CompilesCleanly()
    {
        // NOTE: Scenario intentionally avoids nullable reference type annotations on
        // property/parameter types: the current PoC emits `typeof(T?)` verbatim, which
        // is invalid C#. That bug is tracked separately for a follow-up PR; this test
        // is here to prevent the emitted source from ever introducing OTHER compile
        // errors.
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
                    public TestContext Context { get; set; } = new();

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
