// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Testing.Framework.SourceGeneration.UnitTests.Helpers;
using Microsoft.Testing.Framework.SourceGeneration.UnitTests.TestUtilities;

namespace Microsoft.Testing.Framework.SourceGeneration.UnitTests.Generators;

[TestClass]
public sealed class TestNodesGeneratorTests : TestBase
{
    public TestContext TestContext { get; set; }

    [DataRow("class", "public")]
    [DataRow("class", "internal")]
    [DataRow("record", "public")]
    [DataRow("record", "internal")]
    [DataRow("record class", "public")]
    [DataRow("record class", "internal")]
    [TestMethod]
    public async Task When_TypeIsMarkedWithTestClass_ItGeneratesAGraphWithAssemblyNamespaceTypeAndMethod(string typeKind, string accessibility)
    {
        GeneratorCompilationResult generatorResult = await GeneratorTester.TestGraph.CompileAndExecuteAsync(
            $$"""
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace MyNamespace
            {
                [TestClass]
                {{accessibility}} {{typeKind}} MyType
                {
                    [TestMethod]
                    public Task TestMethod()
                    {
                        return Task.CompletedTask;
                    }
                }
            }
            """, CancellationToken.None);

        generatorResult.AssertSuccessfulGeneration();
        generatorResult.GeneratedTrees.Should().HaveCount(3);

        SourceText myTypeSource = await generatorResult.RunResult.GeneratedTrees[0].GetTextAsync(TestContext.CancellationToken);
        myTypeSource.Should().ContainSourceCode("""
                    public static readonly MSTF::TestNode TestNode = new MSTF::TestNode
                    {
                        StableUid = "TestAssembly.MyNamespace.MyType",
                        DisplayName = "MyType",
                        Properties = new Msg::IProperty[1]
                        {
                            new Msg::TestFileLocationProperty(@"", new(new(6, -1), new(14, -1))),
                        },
                        Tests = new MSTF::TestNode[]
                        {
                            new MSTF::InternalUnsafeAsyncActionTestNode
                            {
                                StableUid = "TestAssembly.MyNamespace.MyType.TestMethod()",
                                DisplayName = "TestMethod",
                                Properties = new Msg::IProperty[2]
                                {
                                    new Msg::TestMethodIdentifierProperty(
                                        "TestAssembly, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null",
                                        "MyNamespace",
                                        "MyType",
                                        "TestMethod",
                                        0,
                                        Sys::Array.Empty<string>(),
                                        "System.Threading.Tasks.Task"),
                                    new Msg::TestFileLocationProperty(@"", new(new(9, -1), new(13, -1))),
                                },
                                Body = static async testExecutionContext =>
                                {
                                    var instance = new MyType();
                                    try
                                    {
                                        await instance.TestMethod();
                                    }
                                    catch (global::System.Exception ex)
                                    {
                                        testExecutionContext.ReportException(ex, null);
                                    }
                                },
                            },
                        },
                    };
            """);

        SourceText rootSource = await generatorResult.RunResult.GeneratedTrees[1].GetTextAsync(TestContext.CancellationToken);
        rootSource.Should().ContainSourceCode("""
                        MSTF::TestNode root = new MSTF::TestNode
                        {
                            StableUid = "TestAssembly",
                            DisplayName = "TestAssembly",
                            Properties = Sys::Array.Empty<Msg::IProperty>(),
                            Tests = new MSTF::TestNode[]
                            {
                                new MSTF::TestNode
                                {
                                    StableUid = "TestAssembly.MyNamespace",
                                    DisplayName = "MyNamespace",
                                    Properties = Sys::Array.Empty<Msg::IProperty>(),
                                    Tests = namespace1Tests.ToArray(),
                                },
                            },
                        };
            """);
    }

    [TestMethod]
    public async Task When_TypeInheritsABaseClassAndIsParameterless_ItGeneratesATestNode()
    {
        GeneratorCompilationResult generatorResult = await GeneratorTester.TestGraph.CompileAndExecuteAsync(
            $$"""
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace MyNamespace
            {
                public class MyBaseClass
                {
                    public MyBaseClass(string s) { }
                }

                [TestClass]
                public class MyType : MyBaseClass
                {
                    public MyType()
                        : base("hello")
                    {
                    }

                    [TestMethod]
                    public Task TestMethod()
                    {
                        return Task.CompletedTask;
                    }
                }
            }
            """, CancellationToken.None);

        generatorResult.AssertSuccessfulGeneration();
        generatorResult.GeneratedTrees.Should().HaveCount(3);
    }

    [TestMethod]
    public async Task When_TypeInheritsAnAbstractBaseClassAndIsParameterless_ItGeneratesATestNode()
    {
        GeneratorCompilationResult generatorResult = await GeneratorTester.TestGraph.CompileAndExecuteAsync(
            $$"""
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace MyNamespace
            {
                public abstract class MyBaseClass
                {
                    public MyBaseClass(string s) { }
                }

                [TestClass]
                public class MyType : MyBaseClass
                {
                    public MyType()
                        : base("hello")
                    {
                    }

                    [TestMethod]
                    public Task TestMethod()
                    {
                        return Task.CompletedTask;
                    }
                }
            }
            """, CancellationToken.None);

        generatorResult.AssertSuccessfulGeneration();
        generatorResult.GeneratedTrees.Should().HaveCount(3);
    }

    [DataRow(false)]
    [DataRow(true)]
    [TestMethod]
    public async Task When_TypeInheritsABaseClassWithSomeTestMethodsButBaseIsNotTestClass_OnlyOneTestNodeTypeIsGenerated(bool isBaseClassAbstract)
    {
        string classModifier = isBaseClassAbstract ? "abstract " : string.Empty;
        GeneratorCompilationResult generatorResult = await GeneratorTester.TestGraph.CompileAndExecuteAsync(
            $$"""
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace MyNamespace
            {
                public {{classModifier}}class MyBaseClass
                {
                    public MyBaseClass(string s) { }

                    [TestMethod]
                    public void MyTestMethod() { }
                }

                [TestClass]
                public class MyType : MyBaseClass
                {
                    public MyType()
                        : base("hello")
                    {
                    }

                    [TestMethod]
                    public Task TestMethod()
                    {
                        return Task.CompletedTask;
                    }
                }
            }
            """, CancellationToken.None);

        generatorResult.AssertSuccessfulGeneration();
        generatorResult.GeneratedTrees.Should().HaveCount(3);

        SourceText myTypeSource = await generatorResult.RunResult.GeneratedTrees[0].GetTextAsync(TestContext.CancellationToken);
        myTypeSource.Should().ContainSourceCode("""
                    public static readonly MSTF::TestNode TestNode = new MSTF::TestNode
                    {
                        StableUid = "TestAssembly.MyNamespace.MyType",
                        DisplayName = "MyType",
                        Properties = new Msg::IProperty[1]
                        {
                            new Msg::TestFileLocationProperty(@"", new(new(14, -1), new(27, -1))),
                        },
                        Tests = new MSTF::TestNode[]
                        {
                            new MSTF::InternalUnsafeAsyncActionTestNode
                            {
                                StableUid = "TestAssembly.MyNamespace.MyType.TestMethod()",
                                DisplayName = "TestMethod",
                                Properties = new Msg::IProperty[2]
                                {
                                    new Msg::TestMethodIdentifierProperty(
                                        "TestAssembly, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null",
                                        "MyNamespace",
                                        "MyType",
                                        "TestMethod",
                                        0,
                                        Sys::Array.Empty<string>(),
                                        "System.Threading.Tasks.Task"),
                                    new Msg::TestFileLocationProperty(@"", new(new(22, -1), new(26, -1))),
                                },
                                Body = static async testExecutionContext =>
                                {
                                    var instance = new MyType();
                                    try
                                    {
                                        await instance.TestMethod();
                                    }
                                    catch (global::System.Exception ex)
                                    {
                                        testExecutionContext.ReportException(ex, null);
                                    }
                                },
                            },
                            new MSTF::InternalUnsafeActionTestNode
                            {
                                StableUid = "TestAssembly.MyNamespace.MyType.MyTestMethod()",
                                DisplayName = "MyTestMethod",
                                Properties = new Msg::IProperty[2]
                                {
                                    new Msg::TestMethodIdentifierProperty(
                                        "TestAssembly, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null",
                                        "MyNamespace",
                                        "MyBaseClass",
                                        "MyTestMethod",
                                        0,
                                        Sys::Array.Empty<string>(),
                                        "System.Void"),
                                    new Msg::TestFileLocationProperty(@"", new(new(10, -1), new(11, -1))),
                                },
                                Body = static testExecutionContext =>
                                {
                                    var instance = new MyType();
                                    try
                                    {
                                        instance.MyTestMethod();
                                    }
                                    catch (global::System.Exception ex)
                                    {
                                        testExecutionContext.ReportException(ex, null);
                                    }
                                },
                            },
                        },
                    };
            """);
    }

    [TestMethod]
    public async Task When_TypeInheritsABaseTestClassMarkedAsTestClassWithSomeTestMethods_TwoTestNodeTypesAreGenerated()
    {
        GeneratorCompilationResult generatorResult = await GeneratorTester.TestGraph.CompileAndExecuteAsync(
            $$"""
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace MyNamespace
            {
                [TestClass]
                public class MyBaseClass
                {
                    [TestMethod]
                    public void MyTestMethod() { }
                }

                [TestClass]
                public class MyType : MyBaseClass
                {
                    [TestMethod]
                    public Task TestMethod()
                    {
                        return Task.CompletedTask;
                    }
                }
            }
            """, CancellationToken.None);

        generatorResult.AssertSuccessfulGeneration();
        generatorResult.GeneratedTrees.Should().HaveCount(4);

        SourceText myBaseClassSource = await generatorResult.GeneratedTrees[0].GetTextAsync(TestContext.CancellationToken);
        myBaseClassSource.Should().ContainSourceCode("""
                    public static readonly MSTF::TestNode TestNode = new MSTF::TestNode
                    {
                        StableUid = "TestAssembly.MyNamespace.MyBaseClass",
                        DisplayName = "MyBaseClass",
                        Properties = new Msg::IProperty[1]
                        {
                            new Msg::TestFileLocationProperty(@"", new(new(6, -1), new(11, -1))),
                        },
                        Tests = new MSTF::TestNode[]
                        {
                            new MSTF::InternalUnsafeActionTestNode
                            {
                                StableUid = "TestAssembly.MyNamespace.MyBaseClass.MyTestMethod()",
                                DisplayName = "MyTestMethod",
                                Properties = new Msg::IProperty[2]
                                {
                                    new Msg::TestMethodIdentifierProperty(
                                        "TestAssembly, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null",
                                        "MyNamespace",
                                        "MyBaseClass",
                                        "MyTestMethod",
                                        0,
                                        Sys::Array.Empty<string>(),
                                        "System.Void"),
                                    new Msg::TestFileLocationProperty(@"", new(new(9, -1), new(10, -1))),
                                },
                                Body = static testExecutionContext =>
                                {
                                    var instance = new MyBaseClass();
                                    try
                                    {
                                        instance.MyTestMethod();
                                    }
                                    catch (global::System.Exception ex)
                                    {
                                        testExecutionContext.ReportException(ex, null);
                                    }
                                },
                            },
                        },
                    };
            """);

        SourceText myTypeSource = await generatorResult.GeneratedTrees[1].GetTextAsync(TestContext.CancellationToken);
        myTypeSource.Should().ContainSourceCode("""
                    public static readonly MSTF::TestNode TestNode = new MSTF::TestNode
                    {
                        StableUid = "TestAssembly.MyNamespace.MyType",
                        DisplayName = "MyType",
                        Properties = new Msg::IProperty[1]
                        {
                            new Msg::TestFileLocationProperty(@"", new(new(13, -1), new(21, -1))),
                        },
                        Tests = new MSTF::TestNode[]
                        {
                            new MSTF::InternalUnsafeAsyncActionTestNode
                            {
                                StableUid = "TestAssembly.MyNamespace.MyType.TestMethod()",
                                DisplayName = "TestMethod",
                                Properties = new Msg::IProperty[2]
                                {
                                    new Msg::TestMethodIdentifierProperty(
                                        "TestAssembly, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null",
                                        "MyNamespace",
                                        "MyType",
                                        "TestMethod",
                                        0,
                                        Sys::Array.Empty<string>(),
                                        "System.Threading.Tasks.Task"),
                                    new Msg::TestFileLocationProperty(@"", new(new(16, -1), new(20, -1))),
                                },
                                Body = static async testExecutionContext =>
                                {
                                    var instance = new MyType();
                                    try
                                    {
                                        await instance.TestMethod();
                                    }
                                    catch (global::System.Exception ex)
                                    {
                                        testExecutionContext.ReportException(ex, null);
                                    }
                                },
                            },
                            new MSTF::InternalUnsafeActionTestNode
                            {
                                StableUid = "TestAssembly.MyNamespace.MyType.MyTestMethod()",
                                DisplayName = "MyTestMethod",
                                Properties = new Msg::IProperty[2]
                                {
                                    new Msg::TestMethodIdentifierProperty(
                                        "TestAssembly, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null",
                                        "MyNamespace",
                                        "MyBaseClass",
                                        "MyTestMethod",
                                        0,
                                        Sys::Array.Empty<string>(),
                                        "System.Void"),
                                    new Msg::TestFileLocationProperty(@"", new(new(9, -1), new(10, -1))),
                                },
                                Body = static testExecutionContext =>
                                {
                                    var instance = new MyType();
                                    try
                                    {
                                        instance.MyTestMethod();
                                    }
                                    catch (global::System.Exception ex)
                                    {
                                        testExecutionContext.ReportException(ex, null);
                                    }
                                },
                            },
                        },
                    };
            """);
    }

    [TestMethod]
    public async Task When_TestClassHasAliasForTestNode_ItWontConflictWithIdentifier()
    {
        // When class has alias for TestNode we should not see conflict in the compiled code. When this is not working correctly
        // e.g. when in the class definition you use just TestNode TestNode for the test node property you will see
        // "Namespace 'Microsoft.Testing.Framework' contains a definition conflicting with alias 'TestNode', but found False."
        // compilation error.
        GeneratorCompilationResult generatorResult = await GeneratorTester.TestGraph.CompileAndExecuteAsync(
            """
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using static System.ConsoleColor;
            using TestNode = A.TestNode;

            namespace A
            {
                public class TestNode
                {
                }
            }

            namespace MyNamespace
            {
                [TestClass]
                public class TestClass
                {
                    [TestMethod]
                    public Task TestMethod()
                    {
                        return Task.CompletedTask;
                    }
                }
            }
            """, CancellationToken.None);

        generatorResult.AssertSuccessfulGeneration();
        generatorResult.RunResult.GeneratedTrees.Should().HaveCount(3);

        SourceText testClass = await generatorResult.GeneratedTrees[0].GetTextAsync(TestContext.CancellationToken);

        testClass.Should().ContainSourceCode(
            "public static readonly MSTF::TestNode TestNode = new MSTF::TestNode",
            "because using short name for TestNode type would conflict with the type alias");
    }

    [TestMethod]
    public async Task When_TestClassIsNested_ItGeneratesNodesForIt()
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
                    [TestClass]
                    public class TestSubClass
                    {
                        [TestMethod]
                        public Task TestMethod1()
                        {
                            return Task.CompletedTask;
                        }
                    }

                    [TestMethod]
                    public Task TestMethod2()
                    {
                        return Task.CompletedTask;
                    }
                }
            }
            """, CancellationToken.None);

        generatorResult.AssertSuccessfulGeneration();
        generatorResult.RunResult.GeneratedTrees.Should().HaveCount(4);

        SyntaxTree? testClassTree = generatorResult.GeneratedTrees.FirstOrDefault(r => r.FilePath.EndsWith("TestSubClass.g.cs", StringComparison.OrdinalIgnoreCase));
        testClassTree.Should().NotBeNull();

        SourceText testClass = await testClassTree!.GetTextAsync(TestContext.CancellationToken);
        testClass.Should().ContainSourceCode("""
                            new MSTF::InternalUnsafeAsyncActionTestNode
                            {
                                StableUid = "TestAssembly.MyNamespace.TestClass.TestSubClass.TestMethod1()",
                                DisplayName = "TestMethod1",
                                Properties = new Msg::IProperty[2]
                                {
                                    new Msg::TestMethodIdentifierProperty(
                                        "TestAssembly, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null",
                                        "MyNamespace",
                                        "TestClass.TestSubClass",
                                        "TestMethod1",
                                        0,
                                        Sys::Array.Empty<string>(),
                                        "System.Threading.Tasks.Task"),
                                    new Msg::TestFileLocationProperty(@"", new(new(12, -1), new(16, -1))),
                                },
                                Body = static async testExecutionContext =>
                                {
                                    var instance = new TestClass.TestSubClass();
                                    try
                                    {
                                        await instance.TestMethod1();
                                    }
                                    catch (global::System.Exception ex)
                                    {
                                        testExecutionContext.ReportException(ex, null);
                                    }
                                },
                            },
            """);
    }

    [TestMethod]
    public async Task When_TypeIsInGlobalNamespace_ItGeneratesATestNode()
    {
        GeneratorCompilationResult generatorResult = await GeneratorTester.TestGraph.CompileAndExecuteAsync(
            $$"""
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyType
            {
                [TestMethod]
                public Task TestMethod()
                {
                    return Task.CompletedTask;
                }
            }
            """, CancellationToken.None);

        generatorResult.AssertSuccessfulGeneration();
        generatorResult.RunResult.GeneratedTrees.Should().HaveCount(3);

        SourceText myTypeSource = await generatorResult.GeneratedTrees[0].GetTextAsync(TestContext.CancellationToken);
        myTypeSource.Should().ContainSourceCode("""
                public static readonly MSTF::TestNode TestNode = new MSTF::TestNode
                {
                    StableUid = "TestAssembly.MyType",
                    DisplayName = "MyType",
                    Properties = new Msg::IProperty[1]
                    {
                        new Msg::TestFileLocationProperty(@"", new(new(4, -1), new(12, -1))),
                    },
            """);

        SourceText rootSource = await generatorResult.GeneratedTrees[1].GetTextAsync(TestContext.CancellationToken);
        rootSource.Should().ContainSourceCode("""
                    MSTF::TestNode root = new MSTF::TestNode
                    {
                        StableUid = "TestAssembly",
                        DisplayName = "TestAssembly",
                        Properties = Sys::Array.Empty<Msg::IProperty>(),
                        Tests = new MSTF::TestNode[]
                        {
                            new MSTF::TestNode
                            {
                                StableUid = "TestAssembly.<global namespace>",
                                DisplayName = "<global namespace>",
                                Properties = Sys::Array.Empty<Msg::IProperty>(),
                                Tests = namespace1Tests.ToArray(),
                            },
                        },
                    };
            """);
    }

    [TestMethod]
    public async Task When_MultipleClassesFromSameNamespace_ItGeneratesASingleNamespaceTestNode()
    {
        GeneratorCompilationResult generatorResult = await GeneratorTester.TestGraph.CompileAndExecuteAsync(
        [
            $$"""
              using System.Threading.Tasks;
              using Microsoft.VisualStudio.TestTools.UnitTesting;

              namespace MyNamespace
              {
                  [TestClass]
                  public class MyType1
                  {
                      [TestMethod]
                      public Task TestMethod()
                      {
                          return Task.CompletedTask;
                      }
                  }
              }
              """,
                $$"""
                using System.Threading.Tasks;
                using Microsoft.VisualStudio.TestTools.UnitTesting;

                namespace MyNamespace
                {
                    [TestClass]
                    public class MyType2
                    {
                        [TestMethod]
                        public Task TestMethod()
                        {
                            return Task.CompletedTask;
                        }
                    }
                }
                """
        ], CancellationToken.None);

        generatorResult.AssertSuccessfulGeneration();
        generatorResult.GeneratedTrees.Should().HaveCount(4);

        SourceText rootSource = await generatorResult.RunResult.GeneratedTrees[2].GetTextAsync(TestContext.CancellationToken);
        rootSource.Should().ContainSourceCode("""
                        ColGen::List<MSTF::TestNode> namespace1Tests = new();
                        namespace1Tests.Add(MyNamespace_MyType1.TestNode);
                        namespace1Tests.Add(MyNamespace_MyType2.TestNode);

                        MSTF::TestNode root = new MSTF::TestNode
                        {
                            StableUid = "TestAssembly",
                            DisplayName = "TestAssembly",
                            Properties = Sys::Array.Empty<Msg::IProperty>(),
                            Tests = new MSTF::TestNode[]
                            {
                                new MSTF::TestNode
                                {
                                    StableUid = "TestAssembly.MyNamespace",
                                    DisplayName = "MyNamespace",
                                    Properties = Sys::Array.Empty<Msg::IProperty>(),
                                    Tests = namespace1Tests.ToArray(),
                                },
                            },
                        };
            """);
    }

    [DataRow("class")]
    [DataRow("struct")]
    [DataRow("record")]
    [DataRow("record struct")]
    [DataRow("record class")]
    [TestMethod]
    [Ignore("Initialize is not supported yet.")]
    public async Task When_TypeIsIAsyncInitializable_GeneratedTestNodeIsAsExpected(string typeKind)
    {
        GeneratorCompilationResult generatorResult = await GeneratorTester.TestGraph.CompileAndExecuteAsync(
            $$"""
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace MyNamespace
            {
                [TestClass]
                public {{typeKind}} MyType : IAsyncInitializable
                {
                    [TestMethod]
                    public Task TestMethod()
                    {
                        return Task.CompletedTask;
                    }

                    [TestInitialize]
                    public Task InitializeAsync(InitializationContext context)
                    {
                        return Task.CompletedTask;
                    }
                }
            }
            """, CancellationToken.None);

        generatorResult.AssertSuccessfulGeneration();
        generatorResult.GeneratedTrees.Should().HaveCount(2);

        SourceText myTypeSource = await generatorResult.RunResult.GeneratedTrees[0].GetTextAsync(TestContext.CancellationToken);

        // The test node for the type should not have a test node for the InitializeAsync method.
        myTypeSource.Should().NotContain("StableUid = \"TestAssembly.MyNamespace.MyType.InitializeAsync\"");

        // The body of the test node for the method should call InitializeAsync before calling the test method.
        myTypeSource.Should().ContainSourceCode("""
                                Body = static async testExecutionContext =>
                                {
                                    var instance = new MyType();
                                    try
                                    {
                                        await instance.InitializeAsync(new MSTF::InitializationContext(testExecutionContext.CancellationToken));
                                        await instance.TestMethod();
                                    }
                                    catch (global::System.Exception ex)
                                    {
                                        testExecutionContext.ReportException(ex, null);
                                    }
                                },
            """);
    }

    [DataRow("class")]
    [DataRow("struct")]
    [DataRow("record")]
    [DataRow("record struct")]
    [DataRow("record class")]
    [TestMethod]
    [Ignore("Initialize is not supported yet.")]
    public async Task When_TypeIsIAsyncCleanable_GeneratedTestNodeIsAsExpected(string typeKind)
    {
        GeneratorCompilationResult generatorResult = await GeneratorTester.TestGraph.CompileAndExecuteAsync(
            $$"""
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace MyNamespace
            {
                [TestClass]
                public {{typeKind}} MyType : IAsyncCleanable
                {
                    public Task TestMethod()
                    {
                        return Task.CompletedTask;
                    }

                    public Task CleanupAsync(CleanupContext context)
                    {
                        return Task.CompletedTask;
                    }
                }
            }
            """, CancellationToken.None);

        generatorResult.AssertSuccessfulGeneration();
        generatorResult.GeneratedTrees.Should().HaveCount(2);

        SourceText myTypeSource = await generatorResult.RunResult.GeneratedTrees[0].GetTextAsync(TestContext.CancellationToken);

        // The test node for the type should not have a test node for the CleanupAsync method.
        myTypeSource.Should().NotContain("StableUid = \"TestAssembly.MyNamespace.MyType.CleanupAsync\"");

        // The body of the test node for the method should call CleanupAsync after calling the test method.
        myTypeSource.Should().ContainSourceCode("""
                                Body = static async testExecutionContext =>
                                {
                                    var instance = new MyType();
                                    try
                                    {
                                        await instance.TestMethod();
                                    }
                                    catch (global::System.Exception ex)
                                    {
                                        testExecutionContext.ReportException(ex, null);
                                    }
                                    try
                                    {
                                        await instance.CleanupAsync(new MSTF::CleanupContext(testExecutionContext.CancellationToken));
                                    }
                                    catch (global::System.Exception ex)
                                    {
                                        testExecutionContext.ReportException(ex, null);
                                    }
                                },
            """);
    }

    [DataRow("class")]
    [DataRow("struct")]
    [DataRow("record")]
    [DataRow("record struct")]
    [DataRow("record class")]
    [TestMethod]
    [Ignore("Initialize is not supported yet.")]
    public async Task When_TypeIsIDisposable_GeneratedTestNodeIsAsExpected(string typeKind)
    {
        GeneratorCompilationResult generatorResult = await GeneratorTester.TestGraph.CompileAndExecuteAsync(
            $$"""
            using System;
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace MyNamespace
            {
                [TestClass]
                public {{typeKind}} MyType : IDisposable
                {
                    public Task TestMethod()
                    {
                        return Task.CompletedTask;
                    }

                    public void Dispose()
                    {
                    }
                }
            }
            """, CancellationToken.None);

        generatorResult.AssertSuccessfulGeneration();
        generatorResult.GeneratedTrees.Should().HaveCount(2);

        SourceText myTypeSource = await generatorResult.RunResult.GeneratedTrees[0].GetTextAsync(TestContext.CancellationToken);

        // The test node for the type should not have a test node for the Dispose method.
        myTypeSource.Should().NotContain("StableUid = \"TestAssembly.MyNamespace.MyType.Dispose\"");

        // The body of the test node for the method should call Dispose after calling the test method.
        myTypeSource.Should().ContainSourceCode("""
                                Body = static async testExecutionContext =>
                                {
                                    using var instance = new MyType();
                                    try
                                    {
                                        await instance.TestMethod();
                                    }
                                    catch (global::System.Exception ex)
                                    {
                                        testExecutionContext.ReportException(ex, null);
                                    }
                                },
            """);
    }

    [DataRow("class")]
    [DataRow("struct")]
    [DataRow("record")]
    [DataRow("record struct")]
    [DataRow("record class")]
    [TestMethod]
    [Ignore("Initialize is not supported yet.")]
    public async Task When_TypeIsIAsyncDisposable_GeneratedTestNodeIsAsExpected(string typeKind)
    {
        GeneratorCompilationResult generatorResult = await GeneratorTester.TestGraph.CompileAndExecuteAsync(
            $$"""
            using System;
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace MyNamespace
            {
                [TestClass]
                public {{typeKind}} MyType : IAsyncDisposable
                {
                    public Task TestMethod()
                    {
                        return Task.CompletedTask;
                    }

                    public ValueTask DisposeAsync()
                    {
                        return ValueTask.CompletedTask;
                    }
                }
            }
            """, CancellationToken.None);

        generatorResult.AssertSuccessfulGeneration();
        generatorResult.GeneratedTrees.Should().HaveCount(2);

        SourceText myTypeSource = await generatorResult.RunResult.GeneratedTrees[0].GetTextAsync(TestContext.CancellationToken);

        // The test node for the type should not have a test node for the DisposeAsync method.
        myTypeSource.Should().NotContain("StableUid = \"TestAssembly.MyNamespace.MyType.DisposeAsync\"");

        // The body of the test node for the method should call DisposeAsync after calling the test method.
        myTypeSource.Should().ContainSourceCode("""
                                Body = static async testExecutionContext =>
                                {
                                    await using var instance = new MyType();
                                    try
                                    {
                                        await instance.TestMethod();
                                    }
                                    catch (global::System.Exception ex)
                                    {
                                        testExecutionContext.ReportException(ex, null);
                                    }
                                },
            """);
    }

    [DataRow("class")]
    [DataRow("struct")]
    [DataRow("record")]
    [DataRow("record struct")]
    [DataRow("record class")]
    [TestMethod]
    [Ignore("Initialize is not supported yet.")]
    public async Task When_TypeIsIAsyncDisposableAndIDisposable_GeneratedTestNodeIsAsExpected(string typeKind)
    {
        GeneratorCompilationResult generatorResult = await GeneratorTester.TestGraph.CompileAndExecuteAsync(
            $$"""
            using System;
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace MyNamespace
            {
                [TestClass]
                public {{typeKind}} MyType : IAsyncDisposable, IDisposable
                {
                    public Task TestMethod()
                    {
                        return Task.CompletedTask;
                    }

                    public ValueTask DisposeAsync()
                    {
                        return ValueTask.CompletedTask;
                    }

                    public void Dispose()
                    {
                    }
                }
            }
            """, CancellationToken.None);

        generatorResult.AssertSuccessfulGeneration();
        generatorResult.GeneratedTrees.Should().HaveCount(2);

        SourceText myTypeSource = await generatorResult.RunResult.GeneratedTrees[0].GetTextAsync(TestContext.CancellationToken);

        // The body of the test node for the method should call only DisposeAsync after calling the test method.
        myTypeSource.Should().ContainSourceCode("""
                                Body = static async testExecutionContext =>
                                {
                                    await using var instance = new MyType();
                                    try
                                    {
                                        await instance.TestMethod();
                                    }
                                    catch (global::System.Exception ex)
                                    {
                                        testExecutionContext.ReportException(ex, null);
                                    }
                                },
            """);
    }

    [TestMethod]
    [Ignore("Initialize is not supported yet.")]
    public async Task When_MethodIsNotAsyncButTypeUsesAsync_GeneratedTestNodeIsAsync()
    {
        GeneratorCompilationResult generatorResult = await GeneratorTester.TestGraph.CompileAndExecuteAsync(
            $$"""
            using System;
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace MyNamespace
            {
                [TestClass]
                public class MyClass1 : IAsyncDisposable
                {
                    public void TestMethod()
                    {
                    }

                    public ValueTask DisposeAsync()
                    {
                        return ValueTask.CompletedTask;
                    }
                }

                [TestClass]
                public class MyClass2 : IAsyncInitializable
                {
                    public void TestMethod()
                    {
                    }

                    public Task InitializeAsync(InitializationContext context)
                    {
                        return Task.CompletedTask;
                    }
                }

                [TestClass]
                public class MyClass3 : IAsyncCleanable
                {
                    public void TestMethod()
                    {
                    }

                    public Task CleanupAsync(CleanupContext context)
                    {
                        return Task.CompletedTask;
                    }
                }
            }
            """, CancellationToken.None);

        generatorResult.AssertSuccessfulGeneration();
        generatorResult.GeneratedTrees.Should().HaveCount(4);

        SourceText myClass1Source = await generatorResult.RunResult.GeneratedTrees[0].GetTextAsync(TestContext.CancellationToken);
        myClass1Source.Should().ContainSourceCode("""
                            new MSTF::InternalUnsafeAsyncActionTestNode
                            {
                                StableUid = "TestAssembly.MyNamespace.MyClass1.TestMethod()",
            """);

        SourceText myClass2Source = await generatorResult.RunResult.GeneratedTrees[1].GetTextAsync(TestContext.CancellationToken);
        myClass2Source.Should().ContainSourceCode("""
                            new MSTF::InternalUnsafeAsyncActionTestNode
                            {
                                StableUid = "TestAssembly.MyNamespace.MyClass2.TestMethod()",
            """);

        SourceText myClass3Source = await generatorResult.RunResult.GeneratedTrees[2].GetTextAsync(TestContext.CancellationToken);
        myClass3Source.Should().ContainSourceCode("""
                            new MSTF::InternalUnsafeAsyncActionTestNode
                            {
                                StableUid = "TestAssembly.MyNamespace.MyClass3.TestMethod()",
            """);
    }

    [TestMethod]
    public async Task When_MethodIsObsolete_WrapMethodCallWithPragma()
    {
        GeneratorCompilationResult generatorResult = await GeneratorTester.TestGraph.CompileAndExecuteAsync(
            $$"""
            using System;
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace MyNamespace
            {
                [TestClass]
                public class MyType
                {
                    [Obsolete]
                    [TestMethod]
                    public Task TestMethod1()
                    {
                        return Task.CompletedTask;
                    }

                    [Obsolete("This is obsolete with message")]
                    [TestMethod]
                    public Task TestMethod2()
                    {
                        return Task.CompletedTask;
                    }

                    [Obsolete("This is obsolete with message", false)]
                    [TestMethod]
                    public Task TestMethod3()
                    {
                        return Task.CompletedTask;
                    }

                    [Obsolete("This is obsolete with message", true)]
                    [TestMethod]
                    public Task TestMethod4()
                    {
                        return Task.CompletedTask;
                    }
                }
            }
            """, CancellationToken.None);

        generatorResult.AssertFailedGeneration("*error CS0619: 'MyType.TestMethod4()' is obsolete*");
        generatorResult.GeneratedTrees.Should().HaveCount(3);

        SourceText myTypeSource = await generatorResult.RunResult.GeneratedTrees[0].GetTextAsync(TestContext.CancellationToken);
        myTypeSource.Should().ContainSourceCode("""
            #pragma warning disable CS0612 // Type or member is obsolete
                                        await instance.TestMethod1();
            #pragma warning restore CS0612 // Type or member is obsolete
            """);

        myTypeSource.Should().ContainSourceCode("""
            #pragma warning disable CS0618 // Type or member is obsolete
                                        await instance.TestMethod2();
            #pragma warning restore CS0618 // Type or member is obsolete
            """);

        myTypeSource.Should().ContainSourceCode("""
            #pragma warning disable CS0618 // Type or member is obsolete
                                        await instance.TestMethod3();
            #pragma warning restore CS0618 // Type or member is obsolete
            """);
    }

    [DataRow("1ab", "_1ab")]
    [DataRow("a-b", "a_b")]
    [DataRow("a.", "a_")]
    [DataRow("!@#$%^&*()_+-=", "______________")]
    [TestMethod]

    // Disabled for potential bug in templating (where escaping code is copied from)
    // https://github.com/dotnet/templating/issues/7200
    // [DataRow("a..b", "a..b")]
    // [DataRow("a...b", "a...b")]
    public void GenerateValidNamespaceName_WithGivenAssemblyName_ReturnsExpectedNamespaceName(string assemblyName, string expectedNamespaceName)
        => Assert.AreEqual(expectedNamespaceName, TestNodesGenerator.ToSafeNamespace(assemblyName));

    [TestMethod]
    public async Task When_APartialTypeIsMarkedWithTestClass_ItGeneratesAGraphWithAssemblyNamespaceTypeAndMethods()
    {
        GeneratorCompilationResult generatorResult = await GeneratorTester.TestGraph.CompileAndExecuteAsync(
        [
            $$"""
              using System.Threading.Tasks;
              using Microsoft.VisualStudio.TestTools.UnitTesting;

              namespace MyNamespace
              {
                  [TestClass]
                  public partial class MyType
                  {
                      public MyType(int a) { }

                      [TestMethod]
                      public Task TestMethod1()
                      {
                          return Task.CompletedTask;
                      }
                  }
              }
              """,
                $$"""
                using System.Threading.Tasks;
                using Microsoft.VisualStudio.TestTools.UnitTesting;

                namespace MyNamespace
                {
                    // Defining [TestClass] twice would fail
                    // the source gen with Duplicate source MyNamespace.MyType.g.cs
                    // but if we fix that problem it will subsequently fail with
                    // duplicate attribute [TestClass].
                    public partial class MyType
                    {
                        public MyType() {}

                        [TestMethod]
                        public Task TestMethod2()
                        {
                            return Task.CompletedTask;
                        }
                    }
                }
                """
        ], CancellationToken.None);

        generatorResult.AssertSuccessfulGeneration();
        generatorResult.GeneratedTrees.Should().HaveCount(3);

        SourceText myTypeSource = await generatorResult.RunResult.GeneratedTrees[0].GetTextAsync(TestContext.CancellationToken);
        myTypeSource.Should().ContainSourceCode("""
                public static class MyNamespace_MyType
                {
                    public static readonly MSTF::TestNode TestNode = new MSTF::TestNode
                    {
                        StableUid = "TestAssembly.MyNamespace.MyType",
                        DisplayName = "MyType",
                        Properties = new Msg::IProperty[2]
                        {
                            new Msg::TestFileLocationProperty(@"", new(new(6, -1), new(16, -1))),
                            new Msg::TestFileLocationProperty(@"", new(new(10, -1), new(19, -1))),
                        },
                        Tests = new MSTF::TestNode[]
                        {
            """);

        myTypeSource.Should().ContainSourceCode("""
                        StableUid = "TestAssembly.MyNamespace.MyType.TestMethod1()",
            """);

        myTypeSource.Should().ContainSourceCode("""
                        StableUid = "TestAssembly.MyNamespace.MyType.TestMethod2()",
            """);
    }
}
