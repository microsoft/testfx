// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Testing.Framework.SourceGeneration.UnitTests.Helpers;
using Microsoft.Testing.Framework.SourceGeneration.UnitTests.TestUtilities;

namespace Microsoft.Testing.Framework.SourceGeneration.UnitTests.Generators;

[TestClass]
public sealed class StaticMethodGenerationTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public async Task StaticMethods_StaticMethodsWontGenerateTests()
    {
        GeneratorCompilationResult generatorResult = await GeneratorTester.TestGraph.CompileAndExecuteAsync(
            """
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace MyNamespace
            {
                [TestClass]
                public class TestClass
                {
                    [TestMethod]
                    public void TestMethod1() { }

                    [TestMethod]
                    public static void StaticTestMethod() { }

                    [TestMethod]
                    public static Task StaticTaskMethod() => Task.CompletedTask;

            
                    [TestMethod]
                    public static ValueTask StaticValueTaskMethod() => ValueTask.CompletedTask;
                }
            }
            """, CancellationToken.None);
        generatorResult.AssertSuccessfulGeneration();

        SyntaxTree? testClassTree = generatorResult.GeneratedTrees.FirstOrDefault(r => r.FilePath.EndsWith("TestClass.g.cs", StringComparison.OrdinalIgnoreCase));
        testClassTree.Should().NotBeNull();

        SourceText testClass = await testClassTree!.GetTextAsync(TestContext.CancellationTokenSource.Token);

        testClass.Should().ContainSourceCode("""StableUid = "TestAssembly.MyNamespace.TestClass.TestMethod1()",""");

        testClass.Should().NotContain("Static", "because none of the static methods should be output.");
    }
}
