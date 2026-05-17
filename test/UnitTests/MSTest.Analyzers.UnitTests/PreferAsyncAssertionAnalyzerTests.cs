// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.PreferAsyncAssertionAnalyzer,
    MSTest.Analyzers.PreferAsyncAssertionFixer>;

using VerifyVB = MSTest.Analyzers.Test.VisualBasicCodeFixVerifier<
    MSTest.Analyzers.PreferAsyncAssertionAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class PreferAsyncAssertionAnalyzerTests
{
    [TestMethod]
    public async Task WhenAssertThrowsExactlyBlocksOnTask_CodeFixUsesAsyncAssertionAndUpdatesSignature()
    {
        string code = """
            using System;
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    [|Assert.ThrowsExactly<InvalidOperationException>(() => BarAsync().GetAwaiter().GetResult())|];
                }

                private Task BarAsync() => Task.CompletedTask;
            }
            """;

        string fixedCode = """
            using System;
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public async Task MyTestMethod()
                {
                    await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => BarAsync());
                }

                private Task BarAsync() => Task.CompletedTask;
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenVoidTestMethodDoesNotHaveTaskInScope_CodeFixUsesFullyQualifiedTaskReturnType()
    {
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    [|Assert.ThrowsExactly<InvalidOperationException>(() => BarAsync().GetAwaiter().GetResult())|];
                }

                private System.Threading.Tasks.Task BarAsync() => System.Threading.Tasks.Task.CompletedTask;
            }
            """;

        string fixedCode = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public async System.Threading.Tasks.Task MyTestMethod()
                {
                    await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => BarAsync());
                }

                private System.Threading.Tasks.Task BarAsync() => System.Threading.Tasks.Task.CompletedTask;
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertThrowsBlocksOnGenericTask_CodeFixUsesAsyncAssertion()
    {
        string code = """
            using System;
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public async Task MyTestMethod()
                {
                    Exception exception = [|Assert.Throws<InvalidOperationException>(() => BarAsync().GetAwaiter().GetResult())|];
                    Assert.IsNotNull(exception);
                    await Task.CompletedTask;
                }

                private Task<int> BarAsync() => Task.FromResult(42);
            }
            """;

        string fixedCode = """
            using System;
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public async Task MyTestMethod()
                {
                    Exception exception = await Assert.ThrowsAsync<InvalidOperationException>(() => BarAsync());
                    Assert.IsNotNull(exception);
                    await Task.CompletedTask;
                }

                private Task<int> BarAsync() => Task.FromResult(42);
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertThrowsBlocksOnGenericTaskInBlockLambda_CodeFixRemovesBlockingReturnStatement()
    {
        string code = """
            using System;
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public async Task MyTestMethod()
                {
                    Exception exception = [|Assert.Throws<InvalidOperationException>(() => { return BarAsync().GetAwaiter().GetResult(); })|];
                    Assert.IsNotNull(exception);
                    await Task.CompletedTask;
                }

                private Task<int> BarAsync() => Task.FromResult(42);
            }
            """;

        string fixedCode = """
            using System;
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public async Task MyTestMethod()
                {
                    Exception exception = await Assert.ThrowsAsync<InvalidOperationException>(() => BarAsync());
                    Assert.IsNotNull(exception);
                    await Task.CompletedTask;
                }

                private Task<int> BarAsync() => Task.FromResult(42);
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenStaticallyImportedAssertionBlocksOnTask_CodeFixUpdatesMethodName()
    {
        string code = """
            using System;
            using System.Threading.Tasks;
            using static Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public async Task MyTestMethod()
                {
                    Exception exception = [|ThrowsExactly<InvalidOperationException>(() => BarAsync().GetAwaiter().GetResult())|];
                    Assert.IsNotNull(exception);
                    await Task.CompletedTask;
                }

                private Task BarAsync() => Task.CompletedTask;
            }
            """;

        string fixedCode = """
            using System;
            using System.Threading.Tasks;
            using static Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public async Task MyTestMethod()
                {
                    Exception exception = await ThrowsExactlyAsync<InvalidOperationException>(() => BarAsync());
                    Assert.IsNotNull(exception);
                    await Task.CompletedTask;
                }

                private Task BarAsync() => Task.CompletedTask;
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenNonAsyncTaskReturningTestMethodHasReturnExpression_CodeFixConvertsReturnToAwait()
    {
        string code = """
            using System;
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public Task MyTestMethod()
                {
                    [|Assert.ThrowsExactly<InvalidOperationException>(() => BarAsync().GetAwaiter().GetResult())|];
                    return Task.CompletedTask;
                }

                private Task BarAsync() => Task.CompletedTask;
            }
            """;

        string fixedCode = """
            using System;
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public async Task MyTestMethod()
                {
                    await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => BarAsync());
                    await Task.CompletedTask;
                    return;
                }

                private Task BarAsync() => Task.CompletedTask;
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertThrowsExactlyBlocksOnTask_WithNamedArgumentsOutOfOrder_CodeFixUsesAsyncAssertion()
    {
        string code = """
            using System;
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    [|Assert.ThrowsExactly<InvalidOperationException>(message: "boom", action: () => BarAsync().GetAwaiter().GetResult())|];
                }

                private Task BarAsync() => Task.CompletedTask;
            }
            """;

        string fixedCode = """
            using System;
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public async Task MyTestMethod()
                {
                    await Assert.ThrowsExactlyAsync<InvalidOperationException>(message: "boom", action: () => BarAsync());
                }

                private Task BarAsync() => Task.CompletedTask;
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertionActionIsExplicitlyCast_CodeFixUsesAsyncAssertionAndRemovesCast()
    {
        string code = """
            using System;
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    [|Assert.ThrowsExactly<InvalidOperationException>(((Action)(() => BarAsync().GetAwaiter().GetResult())))|];
                }

                private Task BarAsync() => Task.CompletedTask;
            }
            """;

        string fixedCode = """
            using System;
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public async Task MyTestMethod()
                {
                    await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => BarAsync());
                }

                private Task BarAsync() => Task.CompletedTask;
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertionIsInsideLockStatement_NoDiagnostic()
    {
        string code = """
            using System;
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                private readonly object _gate = new();

                [TestMethod]
                public void MyTestMethod()
                {
                    lock (_gate)
                    {
                        Assert.ThrowsExactly<InvalidOperationException>(() => BarAsync().GetAwaiter().GetResult());
                    }
                }

                private Task BarAsync() => Task.CompletedTask;
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenAssertionIsInsideExceptionFilter_NoDiagnostic()
    {
        string code = """
            using System;
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    try
                    {
                        throw new Exception();
                    }
                    catch (Exception) when (Assert.ThrowsExactly<InvalidOperationException>(() => BarAsync().GetAwaiter().GetResult()) is not null)
                    {
                    }
                }

                private Task BarAsync() => Task.CompletedTask;
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenAssertionIsInsideNestedLambda_NoDiagnostic()
    {
        string code = """
            using System;
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    Action assertion = () => Assert.ThrowsExactly<InvalidOperationException>(() => BarAsync().GetAwaiter().GetResult());
                    assertion();
                }

                private Task BarAsync() => Task.CompletedTask;
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenAssertionIsInsideLocalFunction_NoDiagnostic()
    {
        string code = """
            using System;
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    void RunAssertion()
                    {
                        Assert.ThrowsExactly<InvalidOperationException>(() => BarAsync().GetAwaiter().GetResult());
                    }

                    RunAssertion();
                }

                private Task BarAsync() => Task.CompletedTask;
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenVisualBasicFunctionLambdaBlocksOnGenericTask_Diagnostic()
    {
        string code = """
            Imports System
            Imports System.Threading.Tasks
            Imports Microsoft.VisualStudio.TestTools.UnitTesting

            <TestClass>
            Public Class MyTestClass
                <TestMethod>
                Public Sub MyTestMethod()
                    [|Assert.ThrowsExactly(Of InvalidOperationException)(Function() BarAsync().GetAwaiter().GetResult())|]
                End Sub

                Private Function BarAsync() As Task(Of Integer)
                    Return Task.FromResult(42)
                End Function
            End Class
            """;

        await VerifyVB.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenAssertionDoesNotBlockOnTask_NoDiagnostic()
    {
        string code = """
            using System;
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public async Task MyTestMethod()
                {
                    await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => BarAsync());
                    Assert.ThrowsExactly<InvalidOperationException>(() => throw new InvalidOperationException());
                }

                private Task BarAsync() => Task.CompletedTask;
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }
}
