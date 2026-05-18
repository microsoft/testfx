// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

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
                }

                private Task BarAsync() => Task.CompletedTask;
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenNonAsyncValueTaskReturningTestMethodHasReturnExpression_CodeFixConvertsReturnToAwait()
    {
        string code = """
            using System;
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public ValueTask MyTestMethod()
                {
                    [|Assert.ThrowsExactly<InvalidOperationException>(() => BarAsync().GetAwaiter().GetResult())|];
                    return ValueTask.CompletedTask;
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
                public async ValueTask MyTestMethod()
                {
                    await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => BarAsync());
                    await ValueTask.CompletedTask;
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
    public async Task WhenAssertionActionIsAnonymousMethod_CodeFixUsesAsyncAssertionAndRemovesBlockingCall()
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
                    [|Assert.ThrowsExactly<InvalidOperationException>(delegate { BarAsync().GetAwaiter().GetResult(); })|];
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
    public async Task WhenAssertionResultIsUsedInMemberAccess_CodeFixParenthesizesAwait()
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
                    string message = [|Assert.ThrowsExactly<InvalidOperationException>(() => BarAsync().GetAwaiter().GetResult())|].Message;
                    Assert.IsNotNull(message);
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
                    string message = (await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => BarAsync())).Message;
                    Assert.IsNotNull(message);
                }

                private Task BarAsync() => Task.CompletedTask;
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenVoidTestMethodIsExpressionBodied_CodeFixConvertsBodyToBlock()
    {
        string code = """
            using System;
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod() => [|Assert.ThrowsExactly<InvalidOperationException>(() => BarAsync().GetAwaiter().GetResult())|];

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
    public async Task WhenAssertionIsInsideUnsafeBlock_NoDiagnostic()
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
                    unsafe
                    {
                        Assert.ThrowsExactly<InvalidOperationException>(() => BarAsync().GetAwaiter().GetResult());
                    }
                }

                private Task BarAsync() => Task.CompletedTask;
            }
            """;

        var test = new VerifyCS.Test
        {
            TestCode = code,
        };

        test.SolutionTransforms.Add((solution, projectId) =>
        {
            var compilationOptions = (CSharpCompilationOptions)solution.GetProject(projectId)!.CompilationOptions!;
            return solution.WithProjectCompilationOptions(projectId, compilationOptions.WithAllowUnsafe(true));
        });

        await test.RunAsync(CancellationToken.None);
    }

    [TestMethod]
    public async Task WhenAssertionUsesInterpolatedStringHandlerOverload_NoDiagnostic()
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
                    Assert.ThrowsExactly<InvalidOperationException>(() => BarAsync().GetAwaiter().GetResult(), $"Message {GetMessage()}");
                }

                private Task BarAsync() => Task.CompletedTask;
                private string GetMessage() => "message";
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenVoidTestMethodCannotChangeReturnType_NoDiagnostic()
    {
        string code = """
            using System;
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public interface ITest
            {
                void MyTestMethod();
            }

            public class BaseTestClass
            {
                public virtual void MyTestMethod()
                {
                }
            }

            [TestClass]
            public class OverrideTestClass : BaseTestClass
            {
                [TestMethod]
                public override void MyTestMethod()
                {
                    Assert.ThrowsExactly<InvalidOperationException>(() => BarAsync().GetAwaiter().GetResult());
                }

                private Task BarAsync() => Task.CompletedTask;
            }

            [TestClass]
            public class ImplicitInterfaceTestClass : ITest
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    Assert.ThrowsExactly<InvalidOperationException>(() => BarAsync().GetAwaiter().GetResult());
                }

                private Task BarAsync() => Task.CompletedTask;
            }

            [TestClass]
            public class ExplicitInterfaceTestClass : ITest
            {
                [TestMethod]
                void ITest.MyTestMethod()
                {
                    Assert.ThrowsExactly<InvalidOperationException>(() => BarAsync().GetAwaiter().GetResult());
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

    [TestMethod]
    public async Task WhenAssertionActionIsExplicitDelegateCreation_CodeFixUnwrapsDelegateCreation()
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
                    [|Assert.ThrowsExactly<InvalidOperationException>(new Action(() => BarAsync().GetAwaiter().GetResult()))|];
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
    public async Task WhenAssertionActionIsExplicitDelegateCreationWithAnonymousMethod_CodeFixUnwrapsDelegateCreation()
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
                    [|Assert.ThrowsExactly<InvalidOperationException>(new Action(delegate { BarAsync().GetAwaiter().GetResult(); }))|];
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
    public async Task WhenAssertionActionIsTargetTypedDelegateCreation_CodeFixUnwrapsDelegateCreation()
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
                    [|Assert.ThrowsExactly<InvalidOperationException>(new(() => BarAsync().GetAwaiter().GetResult()))|];
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
    public async Task WhenNonAsyncTaskMethodHasReturnInsideLockBlock_DiagnosticReportedButNoCodeFixOffered()
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
                public Task MyTestMethod()
                {
                    [|Assert.ThrowsExactly<InvalidOperationException>(() => BarAsync().GetAwaiter().GetResult())|];
                    lock (_gate)
                    {
                        return Task.CompletedTask;
                    }
                }

                private Task BarAsync() => Task.CompletedTask;
            }
            """;

        // The diagnostic is still reported, but the fixer cannot safely transform the
        // method because it would emit an 'await' inside the lock body. No code fix is
        // offered, so the expected fixed code is identical to the original.
        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenNonAsyncValueTaskMethodHasReturnInsideUnsafeBlock_DiagnosticReportedButNoCodeFixOffered()
    {
        string code = """
            using System;
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public ValueTask MyTestMethod()
                {
                    [|Assert.ThrowsExactly<InvalidOperationException>(() => BarAsync().GetAwaiter().GetResult())|];
                    unsafe
                    {
                        return ValueTask.CompletedTask;
                    }
                }

                private Task BarAsync() => Task.CompletedTask;
            }
            """;

        // The diagnostic is still reported, but the fixer cannot safely transform the
        // method because it would emit an 'await' inside the unsafe block. No code fix is
        // offered, so the expected fixed code is identical to the original.
        var test = new VerifyCS.Test
        {
            TestCode = code,
            FixedCode = code,
        };

        test.SolutionTransforms.Add((solution, projectId) =>
        {
            var compilationOptions = (CSharpCompilationOptions)solution.GetProject(projectId)!.CompilationOptions!;
            return solution.WithProjectCompilationOptions(projectId, compilationOptions.WithAllowUnsafe(true));
        });

        await test.RunAsync(CancellationToken.None);
    }

    [TestMethod]
    public async Task WhenNonAsyncTaskMethodHasReturnInsideFixedBlock_DiagnosticReportedButNoCodeFixOffered()
    {
        string code = """
            using System;
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public unsafe Task MyTestMethod()
                {
                    [|Assert.ThrowsExactly<InvalidOperationException>(() => BarAsync().GetAwaiter().GetResult())|];
                    int[] data = { 1, 2, 3 };
                    fixed (int* p = data)
                    {
                        return Task.CompletedTask;
                    }
                }

                private Task BarAsync() => Task.CompletedTask;
            }
            """;

        // The diagnostic is still reported, but the fixer cannot safely transform the
        // method because it would emit an 'await' inside the fixed statement body. No code fix is
        // offered, so the expected fixed code is identical to the original.
        var test = new VerifyCS.Test
        {
            TestCode = code,
            FixedCode = code,
        };

        test.SolutionTransforms.Add((solution, projectId) =>
        {
            var compilationOptions = (CSharpCompilationOptions)solution.GetProject(projectId)!.CompilationOptions!;
            return solution.WithProjectCompilationOptions(projectId, compilationOptions.WithAllowUnsafe(true));
        });

        await test.RunAsync(CancellationToken.None);
    }
}
