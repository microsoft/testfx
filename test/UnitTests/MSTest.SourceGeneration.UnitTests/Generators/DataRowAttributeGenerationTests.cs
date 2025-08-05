// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Testing.Framework.SourceGeneration.UnitTests.Helpers;
using Microsoft.Testing.Framework.SourceGeneration.UnitTests.TestUtilities;

namespace Microsoft.Testing.Framework.SourceGeneration.UnitTests.Generators;

[TestClass]
public sealed class DataRowAttributeGenerationTests : TestBase
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public async Task DataRowAttribute_HandlesPrimitiveTypes()
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
                    [DataRow(true)]
                    public Task MethodWithBool(bool b)
                        => Task.CompletedTask;

                    [TestMethod]
                    [DataRow(1)]
                    public Task MethodWithByte(byte b)
                        => Task.CompletedTask;

                    [TestMethod]
                    [DataRow(1)]
                    public Task MethodWithSbyte(sbyte b)
                        => Task.CompletedTask;

                    [TestMethod]
                    [DataRow('a')]
                    public Task MethodWithChar(char c)
                        => Task.CompletedTask;

                    [TestMethod]
                    [DataRow(1)]
                    public Task MethodWithDecimal(decimal d)
                        => Task.CompletedTask;

                    [TestMethod]
                    [DataRow(1)]
                    public Task MethodWithDouble(double d)
                        => Task.CompletedTask;

                    [TestMethod]
                    [DataRow(1)]
                    public Task MethodWithFloat(float f)
                        => Task.CompletedTask;

                    [TestMethod]
                    [DataRow(1)]
                    public Task MethodWithInt(int i)
                        => Task.CompletedTask;

                    [TestMethod]
                    [DataRow(1)]
                    public Task MethodWithUInt(uint i)
                        => Task.CompletedTask;

                    [TestMethod]
                    [DataRow(1)]
                    public Task MethodWithLong(long l)
                        => Task.CompletedTask;

                    [TestMethod]
                    [DataRow(1)]
                    public Task MethodWithULong(ulong l)
                        => Task.CompletedTask;

                    [TestMethod]
                    [DataRow(1)]
                    public Task MethodWithShort(short s)
                        => Task.CompletedTask;

                    [TestMethod]
                    [DataRow(1)]
                    public Task MethodWithUShort(ushort s)
                        => Task.CompletedTask;
                }
            }
            """, CancellationToken.None);
        generatorResult.AssertSuccessfulGeneration();

        SyntaxTree? testClassTree = generatorResult.GeneratedTrees.FirstOrDefault(r => r.FilePath.EndsWith("TestClass.g.cs", StringComparison.OrdinalIgnoreCase));
        testClassTree.Should().NotBeNull();

        SourceText testClass = await testClassTree!.GetTextAsync(TestContext.CancellationTokenSource.Token);

        testClass.Should().ContainSourceCode("""
                                GetArguments = static () => new MSTF::InternalUnsafeTestArgumentsEntry<bool>[]
                                {
                                    new MSTF::InternalUnsafeTestArgumentsEntry<bool>(true, "b: true"),
                                },
            """);
        testClass.Should().ContainSourceCode("""
                                GetArguments = static () => new MSTF::InternalUnsafeTestArgumentsEntry<byte>[]
                                {
                                    new MSTF::InternalUnsafeTestArgumentsEntry<byte>(1, "b: 1"),
                                },
            """);
        testClass.Should().ContainSourceCode("""
                                GetArguments = static () => new MSTF::InternalUnsafeTestArgumentsEntry<sbyte>[]
                                {
                                    new MSTF::InternalUnsafeTestArgumentsEntry<sbyte>(1, "b: 1"),
                                },
            """);
        testClass.Should().ContainSourceCode("""
                                GetArguments = static () => new MSTF::InternalUnsafeTestArgumentsEntry<char>[]
                                {
                                    new MSTF::InternalUnsafeTestArgumentsEntry<char>('a', "c: 'a'"),
                                },
            """);
        testClass.Should().ContainSourceCode("""
                                GetArguments = static () => new MSTF::InternalUnsafeTestArgumentsEntry<decimal>[]
                                {
                                    new MSTF::InternalUnsafeTestArgumentsEntry<decimal>(1, "d: 1"),
                                },
            """);
        testClass.Should().ContainSourceCode("""
                                GetArguments = static () => new MSTF::InternalUnsafeTestArgumentsEntry<double>[]
                                {
                                    new MSTF::InternalUnsafeTestArgumentsEntry<double>(1, "d: 1"),
                                },
            """);
        testClass.Should().ContainSourceCode("""
                                GetArguments = static () => new MSTF::InternalUnsafeTestArgumentsEntry<float>[]
                                {
                                    new MSTF::InternalUnsafeTestArgumentsEntry<float>(1, "f: 1"),
                                },
            """);
        testClass.Should().ContainSourceCode("""
                                GetArguments = static () => new MSTF::InternalUnsafeTestArgumentsEntry<int>[]
                                {
                                    new MSTF::InternalUnsafeTestArgumentsEntry<int>(1, "i: 1"),
                                },
            """);
        testClass.Should().ContainSourceCode("""
                                GetArguments = static () => new MSTF::InternalUnsafeTestArgumentsEntry<uint>[]
                                {
                                    new MSTF::InternalUnsafeTestArgumentsEntry<uint>(1, "i: 1"),
                                },
            """);
        testClass.Should().ContainSourceCode("""
                                GetArguments = static () => new MSTF::InternalUnsafeTestArgumentsEntry<long>[]
                                {
                                    new MSTF::InternalUnsafeTestArgumentsEntry<long>(1, "l: 1"),
                                },
            """);
        testClass.Should().ContainSourceCode("""
                                GetArguments = static () => new MSTF::InternalUnsafeTestArgumentsEntry<ulong>[]
                                {
                                    new MSTF::InternalUnsafeTestArgumentsEntry<ulong>(1, "l: 1"),
                                },
            """);
        testClass.Should().ContainSourceCode("""
                                GetArguments = static () => new MSTF::InternalUnsafeTestArgumentsEntry<short>[]
                                {
                                    new MSTF::InternalUnsafeTestArgumentsEntry<short>(1, "s: 1"),
                                },
            """);
        testClass.Should().ContainSourceCode("""
                                GetArguments = static () => new MSTF::InternalUnsafeTestArgumentsEntry<ushort>[]
                                {
                                    new MSTF::InternalUnsafeTestArgumentsEntry<ushort>(1, "s: 1"),
                                },
            """);
    }

    [TestMethod]
    public async Task DataRowAttribute_WhenGivenMultipleValues_GeneratesTupleData()
    {
        GeneratorCompilationResult generatorResult = await GeneratorTester.TestGraph.CompileAndExecuteAsync(
            """
            using System;
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace MyNamespace
            {
                [TestClass]
                public class TestClass
                {
                    [TestMethod, DataRow("a", 1)]
                    public Task TestMethod(string s, int i)
                    {
                        return Task.CompletedTask;
                    }

                    [TestMethod, DataRow("a", 1, true, 1.0)]
                    public Task TestMethod(string s, int i, bool b, double d)
                    {
                        return Task.CompletedTask;
                    }
                }
            }
            """, CancellationToken.None);
        generatorResult.AssertSuccessfulGeneration();

        SyntaxTree? testClassTree = generatorResult.GeneratedTrees.FirstOrDefault(r => r.FilePath.EndsWith("TestClass.g.cs", StringComparison.OrdinalIgnoreCase));
        testClassTree.Should().NotBeNull();

        SourceText testClass = await testClassTree!.GetTextAsync(TestContext.CancellationTokenSource.Token);

        testClass.Should().ContainSourceCode("""
                                GetArguments = static () => new MSTF::InternalUnsafeTestArgumentsEntry<(string s, int i)>[]
                                {
                                    new MSTF::InternalUnsafeTestArgumentsEntry<(string s, int i)>(("a", 1), "s: \"a\", i: 1"),
                                },
            """);
        testClass.Should().ContainSourceCode("""
                                GetArguments = static () => new MSTF::InternalUnsafeTestArgumentsEntry<(string s, int i, bool b, double d)>[]
                                {
                                    new MSTF::InternalUnsafeTestArgumentsEntry<(string s, int i, bool b, double d)>(("a", 1, true, 1), "s: \"a\", i: 1, b: true, d: 1"),
                                },
            """);
    }

    [TestMethod]
    public async Task DataRowAttribute_HandlesEscapedStrings()
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
                    [DataRow("\"abc\"")]
                    [DataRow(@"a\b\c")]
                    public Task TestMethod(string a)
                    {
                        return Task.CompletedTask;
                    }
                }
            }
            """, CancellationToken.None);
        generatorResult.AssertSuccessfulGeneration();

        SyntaxTree? testClassTree = generatorResult.GeneratedTrees.FirstOrDefault(r => r.FilePath.EndsWith("TestClass.g.cs", StringComparison.OrdinalIgnoreCase));
        testClassTree.Should().NotBeNull();

        SourceText testClass = await testClassTree!.GetTextAsync(TestContext.CancellationTokenSource.Token);

        testClass.Should().ContainSourceCode("""
                                GetArguments = static () => new MSTF::InternalUnsafeTestArgumentsEntry<string>[]
                                {
                                    new MSTF::InternalUnsafeTestArgumentsEntry<string>("\"abc\"", "a: \"\"abc\"\""),
                                    new MSTF::InternalUnsafeTestArgumentsEntry<string>("a\\b\\c", "a: \"a\\b\\c\""),
                                },
            """);
    }

    [TestMethod]
    public async Task DataRowAttribute_WhenGivenTypeofOtherType_GeneratesDataWithFullType()
    {
        GeneratorCompilationResult generatorResult = await GeneratorTester.TestGraph.CompileAndExecuteAsync(
            """
            using System;
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace MyNamespace
            {
                internal class MyClass
                {
                
                }

                [TestClass]
                public class TestClass
                {
                    [TestMethod]
                    [DataRow(typeof(MyClass))]
                    public Task TestMethod(Type a)
                    {
                        return Task.CompletedTask;
                    }
                }
            }
            """, CancellationToken.None);

        generatorResult.AssertSuccessfulGeneration();
        SyntaxTree? testClassTree = generatorResult.GeneratedTrees.FirstOrDefault(r => r.FilePath.EndsWith("TestClass.g.cs", StringComparison.OrdinalIgnoreCase));
        testClassTree.Should().NotBeNull();

        SourceText testClass = await testClassTree!.GetTextAsync(TestContext.CancellationTokenSource.Token);

        testClass.Should().ContainSourceCode("""
                                GetArguments = static () => new MSTF::InternalUnsafeTestArgumentsEntry<global::System.Type>[]
                                {
                                    new MSTF::InternalUnsafeTestArgumentsEntry<global::System.Type>(typeof(MyNamespace.MyClass), "a: typeof(MyNamespace.MyClass)"),
                                },
            """);
    }

    [TestMethod]
    public async Task DataRowAttribute_WithEnumAsChildFromParentClass_GeneratesDataWithFullType()
    {
        GeneratorCompilationResult generatorResult = await GeneratorTester.TestGraph.CompileAndExecuteAsync(
            """
            using System;
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace MyNamespace
            {
                [TestClass]
                public class ParentClass
                {

                    public enum MyEnum
                    {
                        One,
                    }

                    [TestClass]
                    public class TestClass
                    {
                        [TestMethod, DataRow(MyEnum.One)]
                        public Task TestMethod(MyEnum a)
                        {
                            return Task.CompletedTask;
                        }
                    }
                }
            }
            """, CancellationToken.None);

        generatorResult.AssertSuccessfulGeneration();
        SyntaxTree? testClassTree = generatorResult.GeneratedTrees.FirstOrDefault(r => r.FilePath.EndsWith("TestClass.g.cs", StringComparison.OrdinalIgnoreCase));
        testClassTree.Should().NotBeNull();

        SourceText testClass = await testClassTree!.GetTextAsync(TestContext.CancellationTokenSource.Token);

        testClass.Should().ContainSourceCode("""
                                GetArguments = static () => new MSTF::InternalUnsafeTestArgumentsEntry<global::MyNamespace.ParentClass.MyEnum>[]
                                {
                                    new MSTF::InternalUnsafeTestArgumentsEntry<global::MyNamespace.ParentClass.MyEnum>(global::MyNamespace.ParentClass.MyEnum.One, "a: global::MyNamespace.ParentClass.MyEnum.One"),
                                },
            """);
    }

    [TestMethod]
    public async Task DataRowAttribute_WithEnumAsSubNamespaceDoesNotShadowTypeFromAnotherNamespace_GeneratesDataWithFullGlobalType()
    {
        GeneratorCompilationResult generatorResult = await GeneratorTester.TestGraph.CompileAndExecuteAsync(
            """
            using System;
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            // This gives us a way to reference to the MyEnum (1)
            // in the DataRow.
            using MyEnum = global::ConflictingNamespace.MyEnum;

            namespace ConflictingNamespace {
                public enum MyEnum // 1
                {
                    One,
                }
            }

            namespace MyNamespace
            {
                namespace ConflictingNamespace {
                    public enum MyEnum // 2
                    {
                        Two,
                    }
                }

                [TestClass]
                public class TestClass
                {
                    // If the generated code from here emits just
                    // ConflictingNamespace.MyEnum.One, we will get an error
                    // saying that MyEnum does not have definition for One,
                    // because we are resolving the type by the relative namespace,
                    // and so we find MyNamespace.ConflictingNamespace.MyEnum, which is MyEnum (2).
                    [TestMethod, DataRow(MyEnum.One)]
                    public Task TestMethod(MyEnum a)
                    {
                        return Task.CompletedTask;
                    }
                }
            }
            """, CancellationToken.None);

        generatorResult.AssertSuccessfulGeneration();
        SyntaxTree? testClassTree = generatorResult.GeneratedTrees.FirstOrDefault(r => r.FilePath.EndsWith("TestClass.g.cs", StringComparison.OrdinalIgnoreCase));
        testClassTree.Should().NotBeNull();

        SourceText testClass = await testClassTree!.GetTextAsync(TestContext.CancellationTokenSource.Token);

        testClass.Should().ContainSourceCode("""
                                GetArguments = static () => new MSTF::InternalUnsafeTestArgumentsEntry<global::ConflictingNamespace.MyEnum>[]
                                {
                                    new MSTF::InternalUnsafeTestArgumentsEntry<global::ConflictingNamespace.MyEnum>(global::ConflictingNamespace.MyEnum.One, "a: global::ConflictingNamespace.MyEnum.One"),
                                },
            """);
    }

    [TestMethod]
    public async Task DataRowAttribute_WithEnumAsSubNamespaceDoesNotShadowTypeFromAnotherNamespaceAndUsesChildType_GeneratesDataWithFullGlobalType()
    {
        GeneratorCompilationResult generatorResult = await GeneratorTester.TestGraph.CompileAndExecuteAsync(
            """
            using System;
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace ConflictingNamespace {
                public enum MyEnum // 1
                {
                    One,
                }
            }

            namespace MyNamespace
            {
                
                namespace ConflictingNamespace {
                    public enum MyEnum // 2
                    {
                        Two,
                    }
                }

                [TestClass]
                public class TestClass
                {
                    // This refers to MyEnum (2), we should emit a full type
                    // with global:: into the code.
                    [TestMethod, DataRow(ConflictingNamespace.MyEnum.Two)]
                    public Task TestMethod(ConflictingNamespace.MyEnum a)
                    {
                        return Task.CompletedTask;
                    }
                }
            }
            """, CancellationToken.None);

        generatorResult.AssertSuccessfulGeneration();
        SyntaxTree? testClassTree = generatorResult.GeneratedTrees.FirstOrDefault(r => r.FilePath.EndsWith("TestClass.g.cs", StringComparison.OrdinalIgnoreCase));
        testClassTree.Should().NotBeNull();

        SourceText testClass = await testClassTree!.GetTextAsync(TestContext.CancellationTokenSource.Token);

        testClass.Should().ContainSourceCode("""
                                GetArguments = static () => new MSTF::InternalUnsafeTestArgumentsEntry<global::MyNamespace.ConflictingNamespace.MyEnum>[]
                                {
                                    new MSTF::InternalUnsafeTestArgumentsEntry<global::MyNamespace.ConflictingNamespace.MyEnum>(global::MyNamespace.ConflictingNamespace.MyEnum.Two, "a: global::MyNamespace.ConflictingNamespace.MyEnum.Two"),
                                },
            """);
    }

    [TestMethod]
    public async Task DataRowAttribute_GivenNullValues_GeneratesCorrectData()
    {
        GeneratorCompilationResult generatorResult = await GeneratorTester.TestGraph.CompileAndExecuteAsync(
            """
            using System;
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace MyNamespace
            {
                [TestClass]
                public class TestClass
                {
                    [TestMethod, DataRow(null)]
                    public Task TestMethod1(string a)
                    {
                        return Task.CompletedTask;
                    }

                    [TestMethod, DataRow(null)]
                    public Task TestMethod1(object a)
                    {
                        return Task.CompletedTask;
                    }

                    [TestMethod, DataRow(null, null)]
                    public Task TestMethod1(string s, object a)
                    {
                        return Task.CompletedTask;
                    }
                }
            }
            """, CancellationToken.None);

        generatorResult.AssertSuccessfulGeneration();
        SyntaxTree? testClassTree = generatorResult.GeneratedTrees.FirstOrDefault(r => r.FilePath.EndsWith("TestClass.g.cs", StringComparison.OrdinalIgnoreCase));
        testClassTree.Should().NotBeNull();

        SourceText testClass = await testClassTree!.GetTextAsync(TestContext.CancellationTokenSource.Token);

        testClass.Should().ContainSourceCode("""
                                GetArguments = static () => new MSTF::InternalUnsafeTestArgumentsEntry<string>[]
                                {
                                    new MSTF::InternalUnsafeTestArgumentsEntry<string>(null, "a: null"),
                                },
            """);
        testClass.Should().ContainSourceCode("""
                                GetArguments = static () => new MSTF::InternalUnsafeTestArgumentsEntry<object>[]
                                {
                                    new MSTF::InternalUnsafeTestArgumentsEntry<object>(null, "a: null"),
                                },
            """);
        testClass.Should().ContainSourceCode("""
                                GetArguments = static () => new MSTF::InternalUnsafeTestArgumentsEntry<(string s, object a)>[]
                                {
                                    new MSTF::InternalUnsafeTestArgumentsEntry<(string s, object a)>((null, null), "s: null, a: null"),
                                },
            """);
    }

    [TestMethod]
    public async Task DataRowAttribute_WhenMissingAttribute_OutputsCommentAboveTheTestNodeAndFailsToCompile()
    {
        GeneratorCompilationResult generatorResult = await GeneratorTester.TestGraph.CompileAndExecuteAsync(
            """
            using System;
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace MyNamespace
            {
                [TestClass]
                public class TestClass
                {
                    [TestMethod]
                    public Task TestMethod2(string a, string b)
                    {
                        return Task.CompletedTask;
                    }
                }
            }
            """, CancellationToken.None);

        generatorResult.AssertFailedGeneration(
            "*CS0308: The non-generic type 'InternalUnsafeAsyncActionTestNode' cannot be used with type arguments*",
            "*CS9035: Required member 'TestNode.StableUid' must be set in the object initializer or attribute constructor.*",
            "*CS9035: Required member 'TestNode.DisplayName' must be set in the object initializer or attribute constructor.*",
            "*CS9035: Required member 'InternalUnsafeAsyncActionTestNode.Body' must be set in the object initializer or attribute constructor.*");

        SyntaxTree? testClassTree = generatorResult.GeneratedTrees.FirstOrDefault(r => r.FilePath.EndsWith("TestClass.g.cs", StringComparison.OrdinalIgnoreCase));
        testClassTree.Should().NotBeNull();

        SourceText testClass = await testClassTree!.GetTextAsync(TestContext.CancellationTokenSource.Token);

        testClass.Should().ContainSourceCode("""
                            // The test method is parameterized but no argument was specified.
                            // This is most often caused by using an unsupported arguments input.
                            // Possible resolutions:
                            // - There is a mismatch between arguments from [DataRow] and the method parameters.
                            // - There is a mismatch between arguments from [DynamicData] and the method parameters.
                            // If nothing else worked, report the error and exclude this method by using [Ignore].
                            new MSTF::InternalUnsafeAsyncActionTestNode<(string a, string b)>
            """);
    }

    [TestMethod]
    public async Task Arguments_WithMisalignedDataTypes_ItOutputsTheCodeAndFailsToCompile()
    {
        GeneratorCompilationResult generatorResult = await GeneratorTester.TestGraph.CompileAndExecuteAsync(
            """
            using System;
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace MyNamespace
            {
                [TestClass]
                public class TestClass
                {
                    [TestMethod, DataRow("a", "b", "c")]
                    public Task TestWithTooMuchData(string a, string b)
                    {
                        return Task.CompletedTask;
                    }

                    [TestMethod, DataRow("a", "b")]
                    public Task TestWithNotEnoughData(string a, string b, string c)
                    {
                        return Task.CompletedTask;
                    }

                    [TestMethod, DataRow(1, 1)]
                    public Task TestWithMismatchedDataTypes(string a, string b)
                    {
                        return Task.CompletedTask;
                    }
                }
            }
            """, CancellationToken.None);

        generatorResult.AssertFailedGeneration(
            "*error CS1503: Argument 1: cannot convert from '(string, string, string)' to '(string a, string b)'*",
            "*error CS1503: Argument 1: cannot convert from '(string, string)' to '(string a, string b, string c)'*",
            "*error CS1503: Argument 1: cannot convert from '(int, int)' to '(string a, string b)'*");
    }

    [TestMethod]
    public async Task Arguments_GivenAnArrayOfObjects_GeneratesCorrectData()
    {
        GeneratorCompilationResult generatorResult = await GeneratorTester.TestGraph.CompileAndExecuteAsync(
            """
            using System;
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace MyNamespace
            {
                [TestClass]
                public class TestClass
                {
                    [TestMethod, DataRow(new object[] { 1, (object)"a" })]
                    public Task OneObjectArray(object[] args)
                    {
                        return Task.CompletedTask;
                    }
                }
            }
            """, CancellationToken.None);

        generatorResult.AssertSuccessfulGeneration();
        SyntaxTree? testClassTree = generatorResult.GeneratedTrees.FirstOrDefault(r => r.FilePath.EndsWith("TestClass.g.cs", StringComparison.OrdinalIgnoreCase));
        testClassTree.Should().NotBeNull();

        SourceText testClass = await testClassTree!.GetTextAsync(TestContext.CancellationTokenSource.Token);

        testClass.Should().ContainSourceCode("""
                                GetArguments = static () => new MSTF::InternalUnsafeTestArgumentsEntry<object[]>[]
                                {
                                    new MSTF::InternalUnsafeTestArgumentsEntry<object[]>(new object[] { 1, "a" }, "args: new object[] { 1, \"a\" }"),
                                },
            """);
    }

    [TestMethod]
    public async Task Arguments_GivenAnArrayOfInt_GeneratesCorrectData()
    {
        GeneratorCompilationResult generatorResult = await GeneratorTester.TestGraph.CompileAndExecuteAsync(
            """
            using System;
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace MyNamespace
            {
                [TestClass]
                public class TestClass
                {
                    [TestMethod, DataRow(new int[] { 1, 2 })]
                    public Task OneIntArray(int[] args)
                    {
                        return Task.CompletedTask;
                    }
                }
            }
            """, CancellationToken.None);

        generatorResult.AssertSuccessfulGeneration();
        SyntaxTree? testClassTree = generatorResult.GeneratedTrees.FirstOrDefault(r => r.FilePath.EndsWith("TestClass.g.cs", StringComparison.OrdinalIgnoreCase));
        testClassTree.Should().NotBeNull();

        SourceText testClass = await testClassTree!.GetTextAsync(TestContext.CancellationTokenSource.Token);

        testClass.Should().ContainSourceCode("""
                                GetArguments = static () => new MSTF::InternalUnsafeTestArgumentsEntry<int[]>[]
                                {
                                    new MSTF::InternalUnsafeTestArgumentsEntry<int[]>(new int[] { 1, 2 }, "args: new int[] { 1, 2 }"),
                                },
            """);
    }

    [TestMethod]
    public async Task Arguments_GivenAnArrayOfString_GeneratesCorrectData()
    {
        GeneratorCompilationResult generatorResult = await GeneratorTester.TestGraph.CompileAndExecuteAsync(
            """
            using System;
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace MyNamespace
            {
                [TestClass]
                public class TestClass
                {
                    [TestMethod, DataRow(new string[] { "a", "b" })]
                    public Task OneStringArray(string[] args)
                    {
                        return Task.CompletedTask;
                    }
                }
            }
            """, CancellationToken.None);

        generatorResult.AssertSuccessfulGeneration();
        SyntaxTree? testClassTree = generatorResult.GeneratedTrees.FirstOrDefault(r => r.FilePath.EndsWith("TestClass.g.cs", StringComparison.OrdinalIgnoreCase));
        testClassTree.Should().NotBeNull();

        SourceText testClass = await testClassTree!.GetTextAsync(TestContext.CancellationTokenSource.Token);

        testClass.Should().ContainSourceCode("""
                                GetArguments = static () => new MSTF::InternalUnsafeTestArgumentsEntry<string[]>[]
                                {
                                    new MSTF::InternalUnsafeTestArgumentsEntry<string[]>(new string[] { "a", "b" }, "args: new string[] { \"a\", \"b\" }"),
                                },
            """);
    }

    [TestMethod]
    public async Task Arguments_GivenMultipleArgumentsAndMethodAcceptsSingleObjectArray_GeneratesCorrectData()
    {
        GeneratorCompilationResult generatorResult = await GeneratorTester.TestGraph.CompileAndExecuteAsync(
            """
            using System;
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace MyNamespace
            {
                [TestClass]
                public class TestClass
                {
                    [TestMethod, DataRow(1, "a")]
                    public Task OneParamsObjectArray2(object[] args)
                    {
                        return Task.CompletedTask;
                    }
                }
            }
            """, CancellationToken.None);

        SyntaxTree? testClassTree = generatorResult.GeneratedTrees.FirstOrDefault(r => r.FilePath.EndsWith("TestClass.g.cs", StringComparison.OrdinalIgnoreCase));
        testClassTree.Should().NotBeNull();

        SourceText testClass = await testClassTree!.GetTextAsync(TestContext.CancellationTokenSource.Token);

        testClass.Should().ContainSourceCode("""
                                GetArguments = static () => new MSTF::InternalUnsafeTestArgumentsEntry<object[]>[]
                                {
                                    new MSTF::InternalUnsafeTestArgumentsEntry<object[]>(new object?[] { 1, "a" }, "args: new object?[] { 1, \"a\" }"),
                                },
            """);
    }

    [TestMethod]
    public async Task Arguments_GivenArrayOfIntWhenMethodExpectsArrayOfObjects_FailsCompilation()
    {
        GeneratorCompilationResult generatorResult = await GeneratorTester.TestGraph.CompileAndExecuteAsync(
            """
            using System;
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace MyNamespace
            {
                [TestClass]
                public class TestClass
                {
                    [TestMethod, DataRow(new int[] { 1, 2 })]
                    public Task OneIntArray(object[] args)
                    {
                        return Task.CompletedTask;
                    }
                }
            }
            """, CancellationToken.None);

        generatorResult.AssertFailedGeneration("*error CS1503: Argument 1: cannot convert from 'int[]' to 'object[]'*");

        SyntaxTree? testClassTree = generatorResult.GeneratedTrees.FirstOrDefault(r => r.FilePath.EndsWith("TestClass.g.cs", StringComparison.OrdinalIgnoreCase));
        testClassTree.Should().NotBeNull();

        SourceText testClass = await testClassTree!.GetTextAsync(TestContext.CancellationTokenSource.Token);

        testClass.Should().ContainSourceCode("""
                                GetArguments = static () => new MSTF::InternalUnsafeTestArgumentsEntry<object[]>[]
                                {
                                    new MSTF::InternalUnsafeTestArgumentsEntry<object[]>(new int[] { 1, 2 }, "args: new int[] { 1, 2 }"),
                                },
            """);
    }

    [TestMethod]
    public async Task Arguments_GivenMultipleArrays_GeneratesCorrectData()
    {
        GeneratorCompilationResult generatorResult = await GeneratorTester.TestGraph.CompileAndExecuteAsync(
            """
            using System;
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace MyNamespace
            {
                [TestClass]
                public class TestClass
                {
                    [TestMethod, DataRow(new object[] { 1, 2 }, new object[] { "a", 1 })]
                    public Task TwoObjectArrays(object[] args, object[] args2)
                    {
                        return Task.CompletedTask;
                    }
                }
            }
            """, CancellationToken.None);

        generatorResult.AssertSuccessfulGeneration();
        SyntaxTree? testClassTree = generatorResult.GeneratedTrees.FirstOrDefault(r => r.FilePath.EndsWith("TestClass.g.cs", StringComparison.OrdinalIgnoreCase));
        testClassTree.Should().NotBeNull();

        SourceText testClass = await testClassTree!.GetTextAsync(TestContext.CancellationTokenSource.Token);

        testClass.Should().ContainSourceCode("""
                                GetArguments = static () => new MSTF::InternalUnsafeTestArgumentsEntry<(object[] args, object[] args2)>[]
                                {
                                    new MSTF::InternalUnsafeTestArgumentsEntry<(object[] args, object[] args2)>((new object[] { 1, 2 }, new object[] { "a", 1 }), "args: new object[] { 1, 2 }, args2: new object[] { \"a\", 1 }"),
                                },
            """);
    }
}
