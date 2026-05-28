// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
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
}
