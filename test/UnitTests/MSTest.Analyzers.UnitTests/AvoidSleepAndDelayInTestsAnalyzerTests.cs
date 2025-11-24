// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.AvoidSleepAndDelayInTestsAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class AvoidSleepAndDelayInTestsAnalyzerTests
{
    [TestMethod]
    public async Task WhenTestMethodUsesThreadSleep_Diagnostic()
    {
        string code = """
            using System.Threading;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    [|Thread.Sleep(1000)|];
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenTestMethodUsesTaskWait_Diagnostic()
    {
        string code = """
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    Task task = Task.CompletedTask;
                    [|task.Wait()|];
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenTestMethodUsesTaskWaitWithTimeout_Diagnostic()
    {
        string code = """
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    Task task = Task.CompletedTask;
                    [|task.Wait(1000)|];
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenTestInitializeUsesThreadSleep_Diagnostic()
    {
        string code = """
            using System.Threading;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestInitialize]
                public void MyTestInitialize()
                {
                    [|Thread.Sleep(100)|];
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenTestCleanupUsesThreadSleep_Diagnostic()
    {
        string code = """
            using System.Threading;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestCleanup]
                public void MyTestCleanup()
                {
                    [|Thread.Sleep(100)|];
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenClassInitializeUsesThreadSleep_Diagnostic()
    {
        string code = """
            using System.Threading;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [ClassInitialize]
                public static void MyClassInitialize(TestContext context)
                {
                    [|Thread.Sleep(100)|];
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenClassCleanupUsesThreadSleep_Diagnostic()
    {
        string code = """
            using System.Threading;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [ClassCleanup]
                public static void MyClassCleanup()
                {
                    [|Thread.Sleep(100)|];
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenAssemblyInitializeUsesThreadSleep_Diagnostic()
    {
        string code = """
            using System.Threading;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [AssemblyInitialize]
                public static void MyAssemblyInitialize(TestContext context)
                {
                    [|Thread.Sleep(100)|];
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenAssemblyCleanupUsesThreadSleep_Diagnostic()
    {
        string code = """
            using System.Threading;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [AssemblyCleanup]
                public static void MyAssemblyCleanup()
                {
                    [|Thread.Sleep(100)|];
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenNonTestMethodUsesThreadSleep_NoDiagnostic()
    {
        string code = """
            using System.Threading;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                public void NonTestMethod()
                {
                    Thread.Sleep(100);
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenNonTestMethodUsesTaskWait_NoDiagnostic()
    {
        string code = """
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                public void NonTestMethod()
                {
                    Task task = Task.CompletedTask;
                    task.Wait();
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenTestMethodUsesTaskDelay_NoDiagnostic()
    {
        string code = """
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public async Task MyTestMethod()
                {
                    await Task.Delay(100);
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenTestMethodAwaitsTask_NoDiagnostic()
    {
        string code = """
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public async Task MyTestMethod()
                {
                    Task task = Task.CompletedTask;
                    await task;
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenMultipleBlockingCallsInTestMethod_MultipleDiagnostics()
    {
        string code = """
            using System.Threading;
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    [|Thread.Sleep(100)|];
                    Task task = Task.CompletedTask;
                    [|task.Wait()|];
                    [|Thread.Sleep(200)|];
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenDataTestMethodUsesThreadSleep_Diagnostic()
    {
        string code = """
            using System.Threading;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [DataTestMethod]
                [DataRow(1)]
                public void MyTestMethod(int value)
                {
                    [|Thread.Sleep(100)|];
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }
}
