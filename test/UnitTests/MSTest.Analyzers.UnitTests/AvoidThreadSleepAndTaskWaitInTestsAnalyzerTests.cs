// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.AvoidThreadSleepAndTaskWaitInTestsAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = MSTest.Analyzers.Test.VisualBasicCodeFixVerifier<
    MSTest.Analyzers.AvoidThreadSleepAndTaskWaitInTestsAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace MSTest.Analyzers.UnitTests;

[TestClass]
public sealed class AvoidThreadSleepAndTaskWaitInTestsAnalyzerTests
{
    [TestMethod]
    public async Task ThreadSleepInTestMethod_Diagnostic()
    {
        string code = """
            using System.Threading;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    [|Thread.Sleep(100)|];
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task ThreadSleepWithTimeSpanInTestMethod_Diagnostic()
    {
        string code = """
            using System;
            using System.Threading;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    [|Thread.Sleep(TimeSpan.FromSeconds(1))|];
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task ThreadSleepInDataTestMethod_Diagnostic()
    {
        string code = """
            using System.Threading;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [DataTestMethod]
                [DataRow(1)]
                public void TestMethod(int value)
                {
                    [|Thread.Sleep(100)|];
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task ThreadSleepInTestInitialize_Diagnostic()
    {
        string code = """
            using System.Threading;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestInitialize]
                public void Setup()
                {
                    [|Thread.Sleep(100)|];
                }

                [TestMethod]
                public void TestMethod() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task ThreadSleepInTestCleanup_Diagnostic()
    {
        string code = """
            using System.Threading;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestCleanup]
                public void Cleanup()
                {
                    [|Thread.Sleep(100)|];
                }

                [TestMethod]
                public void TestMethod() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task ThreadSleepInClassInitialize_Diagnostic()
    {
        string code = """
            using System.Threading;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [ClassInitialize]
                public static void ClassInit(TestContext context)
                {
                    [|Thread.Sleep(100)|];
                }

                [TestMethod]
                public void TestMethod() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task ThreadSleepInAssemblyInitialize_Diagnostic()
    {
        string code = """
            using System.Threading;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [AssemblyInitialize]
                public static void AssemblyInit(TestContext context)
                {
                    [|Thread.Sleep(100)|];
                }

                [TestMethod]
                public void TestMethod() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task ThreadSleepInHelperMethodInTestClass_NoDiagnostic()
    {
        string code = """
            using System.Threading;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                private void Helper()
                {
                    Thread.Sleep(100);
                }

                [TestMethod]
                public void TestMethod() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task ThreadSleepInNonTestClass_NoDiagnostic()
    {
        string code = """
            using System.Threading;

            public class MyClass
            {
                public void DoSomething()
                {
                    Thread.Sleep(100);
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task ThreadSleepInLocalFunctionInsideTestMethod_Diagnostic()
    {
        string code = """
            using System.Threading;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    void Local()
                    {
                        [|Thread.Sleep(100)|];
                    }

                    Local();
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task ThreadSleepInLambdaInsideTestMethod_Diagnostic()
    {
        string code = """
            using System;
            using System.Threading;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Action action = () => [|Thread.Sleep(100)|];
                    action();
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task TaskWaitInTestMethod_Diagnostic()
    {
        string code = """
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Task task = Task.CompletedTask;
                    [|task.Wait()|];
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task TaskWaitWithTimeoutInTestMethod_Diagnostic()
    {
        string code = """
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Task task = Task.CompletedTask;
                    [|task.Wait(1000)|];
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task TaskOfTWaitInTestMethod_Diagnostic()
    {
        string code = """
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Task<int> task = Task.FromResult(42);
                    [|task.Wait()|];
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task TaskOfTResultInTestMethod_Diagnostic()
    {
        string code = """
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Task<int> task = Task.FromResult(42);
                    int value = [|task.Result|];
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task AwaitTaskInTestMethod_NoDiagnostic()
    {
        string code = """
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public async Task TestMethod()
                {
                    await Task.Delay(100);
                    int value = await Task.FromResult(42);
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task TaskWaitInHelperMethodInTestClass_NoDiagnostic()
    {
        string code = """
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                private void Helper()
                {
                    Task task = Task.CompletedTask;
                    task.Wait();
                }

                [TestMethod]
                public void TestMethod() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task TaskFactoryStartNewWaitInTestMethod_Diagnostic()
    {
        string code = """
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    [|Task.Run(() => { }).Wait()|];
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task SemaphoreWaitInTestMethod_NoDiagnostic()
    {
        // SemaphoreSlim.Wait is intentionally not in scope; it is a synchronization primitive,
        // not a sync-over-async pattern, so we should not report it.
        string code = """
            using System.Threading;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    var sem = new SemaphoreSlim(1);
                    sem.Wait();
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task ThreadSleepInClassCleanup_Diagnostic()
    {
        string code = """
            using System.Threading;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [ClassCleanup]
                public static void ClassCleanup()
                {
                    [|Thread.Sleep(100)|];
                }

                [TestMethod]
                public void TestMethod() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task ThreadSleepInAssemblyCleanup_Diagnostic()
    {
        string code = """
            using System.Threading;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [AssemblyCleanup]
                public static void AssemblyCleanup()
                {
                    [|Thread.Sleep(100)|];
                }

                [TestMethod]
                public void TestMethod() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task ThreadSleepInGlobalTestInitialize_Diagnostic()
    {
        string code = """
            using System.Threading;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public static class MyGlobalHooks
            {
                [GlobalTestInitialize]
                public static void GlobalInit()
                {
                    [|Thread.Sleep(100)|];
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task ThreadSleepInGlobalTestCleanup_Diagnostic()
    {
        string code = """
            using System.Threading;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public static class MyGlobalHooks
            {
                [GlobalTestCleanup]
                public static void GlobalCleanup()
                {
                    [|Thread.Sleep(100)|];
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task ThreadSleepInDerivedTestMethodAttribute_Diagnostic()
    {
        string code = """
            using System.Threading;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public sealed class MyCustomTestMethodAttribute : TestMethodAttribute { }

            [TestClass]
            public class MyTestClass
            {
                [MyCustomTestMethod]
                public void TestMethod()
                {
                    [|Thread.Sleep(100)|];
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task TaskWaitAllInTestMethod_Diagnostic()
    {
        string code = """
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Task t1 = Task.CompletedTask;
                    Task t2 = Task.CompletedTask;
                    [|Task.WaitAll(t1, t2)|];
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task TaskWaitAnyInTestMethod_Diagnostic()
    {
        string code = """
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Task t1 = Task.CompletedTask;
                    Task t2 = Task.CompletedTask;
                    [|Task.WaitAny(t1, t2)|];
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task TaskWaitAllInHelperMethodInTestClass_NoDiagnostic()
    {
        string code = """
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                private void Helper()
                {
                    Task t1 = Task.CompletedTask;
                    Task.WaitAll(t1);
                }

                [TestMethod]
                public void TestMethod() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task ThreadSleepInExpressionBodiedTestMethod_Diagnostic()
    {
        string code = """
            using System.Threading;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod() => [|Thread.Sleep(100)|];
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task TaskWaitInLocalFunctionInsideTestMethod_Diagnostic()
    {
        string code = """
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    void Local()
                    {
                        Task task = Task.CompletedTask;
                        [|task.Wait()|];
                    }

                    Local();
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task TaskWaitInLambdaInsideTestMethod_Diagnostic()
    {
        string code = """
            using System;
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Action action = () =>
                    {
                        Task task = Task.CompletedTask;
                        [|task.Wait()|];
                    };
                    action();
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task TaskResultInLambdaInsideTestMethod_Diagnostic()
    {
        string code = """
            using System;
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Func<int> func = () => [|Task.FromResult(42).Result|];
                    _ = func();
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task TaskWaitInLambdaInsideTestInitialize_Diagnostic()
    {
        string code = """
            using System;
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestInitialize]
                public void Setup()
                {
                    Action action = () =>
                    {
                        Task task = Task.CompletedTask;
                        [|task.Wait()|];
                    };
                    action();
                }

                [TestMethod]
                public void TestMethod() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task TaskWaitAllInLambdaInsideTestMethod_Diagnostic()
    {
        string code = """
            using System;
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Action action = () => [|Task.WaitAll(Task.CompletedTask)|];
                    action();
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task TaskWaitInVisualBasicTestMethod_Diagnostic()
    {
        string code = """
            Imports System.Threading.Tasks
            Imports Microsoft.VisualStudio.TestTools.UnitTesting

            <TestClass>
            Public Class MyTestClass
                <TestMethod>
                Public Sub TestMethod()
                    Dim t As Task = Task.CompletedTask
                    [|t.Wait()|]
                End Sub
            End Class
            """;

        await VerifyVB.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task TaskWaitAllInVisualBasicTestMethod_Diagnostic()
    {
        string code = """
            Imports System.Threading.Tasks
            Imports Microsoft.VisualStudio.TestTools.UnitTesting

            <TestClass>
            Public Class MyTestClass
                <TestMethod>
                Public Sub TestMethod()
                    Dim t1 As Task = Task.CompletedTask
                    Dim t2 As Task = Task.CompletedTask
                    [|Task.WaitAll(t1, t2)|]
                End Sub
            End Class
            """;

        await VerifyVB.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task TaskWaitAnyInVisualBasicTestMethod_Diagnostic()
    {
        string code = """
            Imports System.Threading.Tasks
            Imports Microsoft.VisualStudio.TestTools.UnitTesting

            <TestClass>
            Public Class MyTestClass
                <TestMethod>
                Public Sub TestMethod()
                    Dim t1 As Task = Task.CompletedTask
                    Dim t2 As Task = Task.CompletedTask
                    [|Task.WaitAny(t1, t2)|]
                End Sub
            End Class
            """;

        await VerifyVB.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task ThreadSleepInVisualBasicTestMethod_Diagnostic()
    {
        string code = """
            Imports System.Threading
            Imports Microsoft.VisualStudio.TestTools.UnitTesting

            <TestClass>
            Public Class MyTestClass
                <TestMethod>
                Public Sub TestMethod()
                    [|Thread.Sleep(100)|]
                End Sub
            End Class
            """;

        await VerifyVB.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task TaskOfTResultInVisualBasicTestMethod_Diagnostic()
    {
        string code = """
            Imports System.Threading.Tasks
            Imports Microsoft.VisualStudio.TestTools.UnitTesting

            <TestClass>
            Public Class MyTestClass
                <TestMethod>
                Public Sub TestMethod()
                    Dim t As Task(Of Integer) = Task.FromResult(42)
                    Dim value As Integer = [|t.Result|]
                End Sub
            End Class
            """;

        await VerifyVB.VerifyAnalyzerAsync(code);
    }
}
