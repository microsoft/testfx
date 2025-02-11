﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Testing.Framework.SourceGeneration.UnitTests.Helpers;
using Microsoft.Testing.Framework.SourceGeneration.UnitTests.TestUtilities;

namespace Microsoft.Testing.Framework.SourceGeneration.UnitTests.Generators;

[TestClass]
public sealed class IgnoreAttributeGenerationTests : TestBase
{
    [TestMethod]
    public async Task IgnoreAttribute_OnMethodExcludesTheMethodFromCompilation()
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
                    [Ignore]
                    public void IgnoredVoidMethod() { }

                    [TestMethod]
                    [Ignore]
                    public Task IgnoredTaskMethod() => Task.CompletedTask;

            
                    [TestMethod]
                    [Ignore]
                    public ValueTask IgnoredValueTaskMethod() => ValueTask.CompletedTask;

                    [TestMethod]
                    [Ignore("reason")]
                    public void IgnoredVoidMethodWithReason() { }
            
                    [TestMethod]
                    [Ignore("reason")]
                    public Task IgnoredTaskMethodWithReason() => Task.CompletedTask;
            
            
                    [TestMethod]
                    [Ignore("reason")]
                    public ValueTask IgnoredValueTaskMethodWithReason() => ValueTask.CompletedTask;
                }
            }
            """, CancellationToken.None);
        generatorResult.AssertSuccessfulGeneration();

        SyntaxTree? testClassTree = generatorResult.GeneratedTrees.FirstOrDefault(r => r.FilePath.EndsWith("TestClass.g.cs", StringComparison.OrdinalIgnoreCase));
        testClassTree.Should().NotBeNull();

        SourceText testClass = await testClassTree!.GetTextAsync();

        testClass.Should().ContainSourceCode("""StableUid = "TestAssembly.MyNamespace.TestClass.TestMethod1()",""");

        testClass.Should().NotContain("Ignored", "because none of the ignored methods should be output.");
    }
}
