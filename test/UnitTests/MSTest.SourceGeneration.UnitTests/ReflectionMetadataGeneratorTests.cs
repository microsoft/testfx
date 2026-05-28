// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration.Generators;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration.UnitTests;

[TestClass]
public sealed class ReflectionMetadataGeneratorTests
{
    private const string MinimalMSTestStub = """
        namespace Microsoft.VisualStudio.TestTools.UnitTesting
        {
            [System.AttributeUsage(System.AttributeTargets.Class)]
            public sealed class TestClassAttribute : System.Attribute {}

            [System.AttributeUsage(System.AttributeTargets.Method)]
            public class TestMethodAttribute : System.Attribute {}
        }
        """;

    private const string RuntimeHookStub = """
        namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration
        {
            public sealed class SourceGeneratedReflectionDataProvider
            {
                public System.Reflection.Assembly? Assembly { get; set; }
                public string? AssemblyName { get; set; }
                public System.Type[] Types { get; set; } = System.Array.Empty<System.Type>();
                public System.Collections.Generic.Dictionary<string, System.Type> TypesByName { get; set; } = new();
                public System.Collections.Generic.Dictionary<System.Type, System.Reflection.MethodInfo[]> TypeMethods { get; set; } = new();
            }

            public static class ReflectionMetadataHook
            {
                public static void SetMetadata(SourceGeneratedReflectionDataProvider data) { }
            }
        }
        """;

    [TestMethod]
    public void Generator_DiscoversTestClassesAndMethods_AndEmitsModuleInitializer()
    {
        const string userCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Sample
            {
                [TestClass]
                public class MyTests
                {
                    [TestMethod]
                    public void Test1() {}

                    [TestMethod]
                    public void Test2() {}

                    public void NotATest() {}
                }
            }
            """;

        GeneratorRunResult result = RunGenerator(MinimalMSTestStub, userCode);

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSources.Should().HaveCount(1);

        string generated = result.GeneratedSources[0].SourceText.ToString();

        generated.Should().Contain("[ModuleInitializer]");
        generated.Should().Contain("typeof(global::Sample.MyTests)");
        generated.Should().Contain("ResolveMethod(typeof(global::Sample.MyTests), \"Test1\", Type.EmptyTypes)");
        generated.Should().Contain("ResolveMethod(typeof(global::Sample.MyTests), \"Test2\", Type.EmptyTypes)");
        generated.Should().NotContain("\"NotATest\"");
        generated.Should().Contain("ReflectionMetadataHook.SetMetadata");

        // TypesByName key uses typeof(X).FullName! at runtime to match Type.FullName for nested/generic types.
        generated.Should().Contain("[typeof(global::Sample.MyTests).FullName!] = typeof(global::Sample.MyTests)");
        generated.Should().NotContain("[\"global::Sample.MyTests\"]");
    }

    [TestMethod]
    public void Generator_EmitsOverloadAwareMethodResolution()
    {
        const string userCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Sample
            {
                [TestClass]
                public class OverloadTests
                {
                    [TestMethod]
                    public void Run() {}

                    [TestMethod]
                    public void Run(int x) {}

                    [TestMethod]
                    public void Run(string s, int x) {}
                }
            }
            """;

        GeneratorRunResult result = RunGenerator(MinimalMSTestStub, userCode);

        result.Diagnostics.Should().BeEmpty();
        string generated = result.GeneratedSources[0].SourceText.ToString();

        generated.Should().Contain("ResolveMethod(typeof(global::Sample.OverloadTests), \"Run\", Type.EmptyTypes)");
        generated.Should().Contain("ResolveMethod(typeof(global::Sample.OverloadTests), \"Run\", new Type[] { typeof(global::System.Int32) })");
        generated.Should().Contain("ResolveMethod(typeof(global::Sample.OverloadTests), \"Run\", new Type[] { typeof(global::System.String), typeof(global::System.Int32) })");
    }

    [TestMethod]
    public void Generator_EmittedSourceCompilesCleanly()
    {
        const string userCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Sample
            {
                [TestClass]
                public class MyTests
                {
                    [TestMethod]
                    public void Test1() {}

                    [TestMethod]
                    public void Test2(int x, string s) {}

                    [TestMethod]
                    internal void NonPublicTest() {}
                }
            }
            """;

        Compilation outputCompilation = RunGeneratorAndGetCompilation(MinimalMSTestStub, RuntimeHookStub, userCode);

        Diagnostic[] errors = outputCompilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToArray();

        errors.Should().BeEmpty("the generator output should compile without errors. Diagnostics: " + string.Join("\n", errors.Select(d => d.ToString())));
    }

    [TestMethod]
    public void Generator_SkipsStaticAndAbstractTestClasses()
    {
        const string userCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Sample
            {
                [TestClass]
                public static class StaticTests
                {
                    [TestMethod]
                    public static void Test1() {}
                }

                [TestClass]
                public abstract class AbstractTests
                {
                    [TestMethod]
                    public void Test1() {}
                }

                [TestClass]
                public class Concrete : AbstractTests
                {
                    [TestMethod]
                    public void Test2() {}
                }
            }
            """;

        GeneratorRunResult result = RunGenerator(MinimalMSTestStub, userCode);

        result.Diagnostics.Should().BeEmpty();
        string generated = result.GeneratedSources[0].SourceText.ToString();

        generated.Should().NotContain("typeof(global::Sample.StaticTests)");
        generated.Should().NotContain("typeof(global::Sample.AbstractTests)");
        generated.Should().Contain("typeof(global::Sample.Concrete)");

        // Concrete must surface BOTH its own Test2 AND the inherited Test1 from AbstractTests.
        generated.Should().Contain("ResolveMethod(typeof(global::Sample.Concrete), \"Test1\", Type.EmptyTypes)");
        generated.Should().Contain("ResolveMethod(typeof(global::Sample.Concrete), \"Test2\", Type.EmptyTypes)");
    }

    [TestMethod]
    public void Generator_EmitsTypesByName_UsesTypeofFullNameKey_ForNestedTypes()
    {
        const string userCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Sample
            {
                public class Outer
                {
                    [TestClass]
                    public class Nested
                    {
                        [TestMethod]
                        public void Test1() {}
                    }
                }
            }
            """;

        GeneratorRunResult result = RunGenerator(MinimalMSTestStub, userCode);

        result.Diagnostics.Should().BeEmpty();
        string generated = result.GeneratedSources[0].SourceText.ToString();

        // The key must be emitted as typeof(X).FullName! so the runtime dictionary lookup
        // matches Type.FullName, which uses '+' to separate the nesting type from the nested
        // one (Sample.Outer+Nested) and would not match a literal compile-time C# name like
        // "Sample.Outer.Nested".
        generated.Should().Contain("[typeof(global::Sample.Outer.Nested).FullName!] = typeof(global::Sample.Outer.Nested)");
        generated.Should().NotContain("[\"Sample.Outer.Nested\"]");
    }

    [TestMethod]
    public void Generator_HandlesEmptyAssembly()
    {
        const string userCode = """
            namespace Sample
            {
                public class NoTests {}
            }
            """;

        GeneratorRunResult result = RunGenerator(MinimalMSTestStub, userCode);

        result.Diagnostics.Should().BeEmpty();

        // When no [TestClass] types are discovered, the generator must not emit any source
        // (avoids polluting the compilation with an empty module initializer).
        result.GeneratedSources.Should().BeEmpty();
    }

    [TestMethod]
    public void Generator_SkipsOpenGenericTestClasses()
    {
        const string userCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Sample
            {
                [TestClass]
                public class Generic<T>
                {
                    [TestMethod]
                    public void Test1() {}
                }

                [TestClass]
                public class Concrete
                {
                    [TestMethod]
                    public void Test2() {}
                }
            }
            """;

        GeneratorRunResult result = RunGenerator(MinimalMSTestStub, userCode);

        result.Diagnostics.Should().BeEmpty();
        string generated = result.GeneratedSources[0].SourceText.ToString();

        // Open generic test classes cannot be emitted as `typeof(Generic<T>)` at module-initializer
        // scope, so they are silently skipped. The non-generic sibling must still be emitted.
        generated.Should().NotContain("typeof(global::Sample.Generic");
        generated.Should().Contain("typeof(global::Sample.Concrete)");
    }

    [TestMethod]
    public void Generator_SkipsMethodsWithByRefParameters()
    {
        const string userCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Sample
            {
                [TestClass]
                public class ByRefTests
                {
                    [TestMethod]
                    public void RefTest(ref int x) {}

                    [TestMethod]
                    public void OutTest(out int x) { x = 0; }

                    [TestMethod]
                    public void InTest(in int x) {}

                    [TestMethod]
                    public void NormalTest(int x) {}
                }
            }
            """;

        GeneratorRunResult result = RunGenerator(MinimalMSTestStub, userCode);

        result.Diagnostics.Should().BeEmpty();
        string generated = result.GeneratedSources[0].SourceText.ToString();

        // By-ref signatures (ref/out/in) cannot round-trip through ResolveMethod's
        // typeof(T) == ParameterType check, so the generator omits these methods entirely.
        // The plain-parameter overload must still be emitted.
        generated.Should().NotContain("\"RefTest\"");
        generated.Should().NotContain("\"OutTest\"");
        generated.Should().NotContain("\"InTest\"");
        generated.Should().Contain("ResolveMethod(typeof(global::Sample.ByRefTests), \"NormalTest\", new Type[] { typeof(global::System.Int32) })");
    }

    [TestMethod]
    public void Generator_SkipsInaccessibleNestedTestClasses()
    {
        const string userCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Sample
            {
                public class Outer
                {
                    [TestClass]
                    private class PrivateNested
                    {
                        [TestMethod]
                        public void Test1() {}
                    }

                    [TestClass]
                    public class PublicNested
                    {
                        [TestMethod]
                        public void Test2() {}
                    }
                }
            }
            """;

        GeneratorRunResult result = RunGenerator(MinimalMSTestStub, userCode);

        result.Diagnostics.Should().BeEmpty();
        string generated = result.GeneratedSources[0].SourceText.ToString();

        // A private (or otherwise inaccessible) nested [TestClass] cannot be referenced from
        // the generated `internal` module initializer; it would compile as CS0122. The
        // generator skips such types but still emits siblings that are reachable.
        generated.Should().NotContain("PrivateNested");
        generated.Should().Contain("typeof(global::Sample.Outer.PublicNested)");
    }

    [TestMethod]
    public void Generator_SkipsFileLocalTestClasses()
    {
        const string userCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Sample
            {
                [TestClass]
                file class FileLocalTests
                {
                    [TestMethod]
                    public void HiddenTest() {}
                }

                [TestClass]
                public class VisibleTests
                {
                    [TestMethod]
                    public void OpenTest() {}
                }
            }
            """;

        GeneratorRunResult result = RunGenerator(MinimalMSTestStub, userCode);

        result.Diagnostics.Should().BeEmpty();
        string generated = result.GeneratedSources[0].SourceText.ToString();

        // `file`-scoped types are only addressable within their own source file. The generated
        // module initializer lives in a different file and would fail with CS9051. Skip them.
        generated.Should().NotContain("FileLocalTests");
        generated.Should().NotContain("\"HiddenTest\"");
        generated.Should().Contain("typeof(global::Sample.VisibleTests)");
    }

    [TestMethod]
    public void Generator_DedupesOverriddenMethodsAcrossInheritance()
    {
        const string userCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Sample
            {
                public abstract class BaseTests
                {
                    [TestMethod]
                    public virtual void TestA() {}
                }

                [TestClass]
                public class Derived : BaseTests
                {
                    [TestMethod]
                    public override void TestA() {}
                }
            }
            """;

        GeneratorRunResult result = RunGenerator(MinimalMSTestStub, userCode);

        result.Diagnostics.Should().BeEmpty();
        string generated = result.GeneratedSources[0].SourceText.ToString();

        // The base + override must collapse to a single emit. Counting occurrences of the
        // exact ResolveMethod call site catches double-emission regressions in the inheritance
        // walk's dedupe-by-signature logic.
        int count = CountOccurrences(generated, "ResolveMethod(typeof(global::Sample.Derived), \"TestA\", Type.EmptyTypes)");
        count.Should().Be(1);
    }

    private static int CountOccurrences(string source, string needle)
    {
        int count = 0;
        int index = 0;
        while ((index = source.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
    }

    private static GeneratorRunResult RunGenerator(params string[] sources)
    {
        IEnumerable<SyntaxTree> trees = sources.Select(s => CSharpSyntaxTree.ParseText(s));
        var references = new MetadataReference[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Runtime.CompilerServices.ModuleInitializerAttribute).Assembly.Location),
        };

        var compilation = CSharpCompilation.Create(
            "TestSample",
            trees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(new ReflectionMetadataGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);

        return driver.GetRunResult().Results[0];
    }

    private static Compilation RunGeneratorAndGetCompilation(params string[] sources)
    {
        IEnumerable<SyntaxTree> trees = sources.Select(s => CSharpSyntaxTree.ParseText(s));
        var references = new MetadataReference[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Runtime.CompilerServices.ModuleInitializerAttribute).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Reflection.Assembly).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Collections.Generic.Dictionary<,>).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Reflection.MethodInfo).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Reflection.BindingFlags).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.MissingMethodException).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Linq.Enumerable).Assembly.Location),
        };

        var compilation = CSharpCompilation.Create(
            "TestSample",
            trees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(new ReflectionMetadataGenerator());
        driver.RunGeneratorsAndUpdateCompilation(compilation, out Compilation outputCompilation, out _);

        return outputCompilation;
    }
}
