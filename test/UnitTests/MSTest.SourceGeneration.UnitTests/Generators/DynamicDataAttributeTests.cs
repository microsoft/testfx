// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Testing.Framework.SourceGeneration.UnitTests.Helpers;
using Microsoft.Testing.Framework.SourceGeneration.UnitTests.TestUtilities;

namespace Microsoft.Testing.Framework.SourceGeneration.UnitTests.Generators;

[TestClass]
public sealed class DynamicDataAttributeGenerationTests : TestBase
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public async Task DynamicDataAttribute_TakesDataFromProperty()
    {
        GeneratorCompilationResult generatorResult = await GeneratorTester.TestGraph.CompileAndExecuteAsync(
            """
            using System.Threading.Tasks;
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using Microsoft.Testing.Framework;

            namespace MyNamespace
            {
                [TestClass]
                public class TestClass
                {
                    [DynamicData(nameof(Data))]        
                    [TestMethod]
                    public void TestMethod(int expected, int actualPlus1)
                        => Assert.AreEqual(expected, actualPlus1 - 1);

                    public static IEnumerable<object[]> Data => new[]
                        {
                            new object[] { 1, 2 },
                            new object[] { 2, 3 },
                        };
                }
            
            }
            """, CancellationToken.None);
        generatorResult.AssertSuccessfulGeneration();

        SyntaxTree? testClassTree = generatorResult.GeneratedTrees.FirstOrDefault(r => r.FilePath.EndsWith("TestClass.g.cs", StringComparison.OrdinalIgnoreCase));
        testClassTree.Should().NotBeNull();

        SourceText testClass = await testClassTree!.GetTextAsync(TestContext.CancellationTokenSource.Token);

        testClass.Should().ContainSourceCode("""
                                GetArguments = static () => {
                                    var data = MyNamespace.TestClass.Data;
                                    var dataCollection = new ColGen.List<MSTF::InternalUnsafeTestArgumentsEntry<(int expected, int actualPlus1)>>();
                                    var index = 0;
                                    foreach (var item in data)
                                    {
                                        string uidFragment = MSTF::DynamicDataNameProvider.GetUidFragment(new string[] {"expected", "actualPlus1"}, item, index);
                                        index++;
                                        dataCollection.Add(new(((int) item[0], (int) item[1]), uidFragment));
                                    }
                                    return dataCollection;
                                }
            """);
    }

    [TestMethod]
    public async Task DynamicDataAttribute_TakesDataFromMethod()
    {
        GeneratorCompilationResult generatorResult = await GeneratorTester.TestGraph.CompileAndExecuteAsync(
            """
            using System.Threading.Tasks;
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using Microsoft.Testing.Framework;

            namespace MyNamespace
            {
                [TestClass]
                public class TestClass
                {
                    [DynamicData(nameof(Data), DynamicDataSourceType.Method)]        
                    [TestMethod]
                    public void TestMethod(int expected, int actualPlus1)
                        => Assert.AreEqual(expected, actualPlus1 - 1);

                    public static IEnumerable<object[]> Data() => new[]
                        {
                            new object[] { 1, 2 },
                            new object[] { 2, 3 },
                        };
                }
            
            }
            """, CancellationToken.None);
        generatorResult.AssertSuccessfulGeneration();

        SyntaxTree? testClassTree = generatorResult.GeneratedTrees.FirstOrDefault(r => r.FilePath.EndsWith("TestClass.g.cs", StringComparison.OrdinalIgnoreCase));
        testClassTree.Should().NotBeNull();

        SourceText testClass = await testClassTree!.GetTextAsync(TestContext.CancellationTokenSource.Token);

        testClass.Should().ContainSourceCode("""
                                GetArguments = static () => {
                                    var data = MyNamespace.TestClass.Data();
                                    var dataCollection = new ColGen.List<MSTF::InternalUnsafeTestArgumentsEntry<(int expected, int actualPlus1)>>();
                                    var index = 0;
                                    foreach (var item in data)
                                    {
                                        string uidFragment = MSTF::DynamicDataNameProvider.GetUidFragment(new string[] {"expected", "actualPlus1"}, item, index);
                                        index++;
                                        dataCollection.Add(new(((int) item[0], (int) item[1]), uidFragment));
                                    }
                                    return dataCollection;
                                }
            """);
    }
}
