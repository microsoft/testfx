// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.FlowTestContextCancellationTokenAnalyzer,
    MSTest.Analyzers.FlowTestContextCancellationTokenFixer>;

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class FlowTestContextCancellationTokenAnalyzerTests
{
    [TestMethod]
    public async Task WhenTaskDelayWithoutCancellationToken_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Threading;
            using System.Threading.Tasks;

            [TestClass]
            public class MyTestClass
            {
                public TestContext TestContext { get; set; }

                [TestMethod]
                public async Task MyTestMethod()
                {
                    {|#0:await Task.Delay(1000)|};
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code,
            VerifyCS.Diagnostic().WithLocation(0));
    }

    [TestMethod]
    public async Task WhenMethodCallAlreadyHasCancellationToken_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Threading;
            using System.Threading.Tasks;

            [TestClass]
            public class MyTestClass
            {
                public TestContext TestContext { get; set; }

                [TestMethod]
                public async Task MyTestMethod()
                {
                    await Task.Delay(1000, TestContext.CancellationTokenSource.Token);
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenNotInTestMethod_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Threading;
            using System.Threading.Tasks;

            public class MyClass
            {
                public async Task MyMethod()
                {
                    await Task.Delay(1000);
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenMethodHasNoOverloadWithCancellationToken_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System;
            using System.Threading;
            using System.Threading.Tasks;

            [TestClass]
            public class MyTestClass
            {
                public TestContext TestContext { get; set; }

                [TestMethod]
                public void MyTestMethod()
                {
                    Console.WriteLine("Hello World");
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenCodeFixApplied_AddsTestContextCancellationToken()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Threading;
            using System.Threading.Tasks;

            [TestClass]
            public class MyTestClass
            {
                public TestContext TestContext { get; set; }

                [TestMethod]
                public async Task MyTestMethod()
                {
                    {|#0:await Task.Delay(1000)|};
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Threading;
            using System.Threading.Tasks;

            [TestClass]
            public class MyTestClass
            {
                public TestContext TestContext { get; set; }

                [TestMethod]
                public async Task MyTestMethod()
                {
                    await Task.Delay(1000, TestContext.CancellationTokenSource.Token);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code,
            VerifyCS.Diagnostic().WithLocation(0),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenInTestInitialize_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Threading;
            using System.Threading.Tasks;

            [TestClass]
            public class MyTestClass
            {
                public TestContext TestContext { get; set; }

                [TestInitialize]
                public async Task TestInit()
                {
                    {|#0:await Task.Delay(1000)|};
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code,
            VerifyCS.Diagnostic().WithLocation(0));
    }

    [TestMethod]
    public async Task WhenInTestCleanup_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Threading;
            using System.Threading.Tasks;

            [TestClass]
            public class MyTestClass
            {
                public TestContext TestContext { get; set; }

                [TestCleanup]
                public async Task TestCleanup()
                {
                    {|#0:await Task.Delay(1000)|};
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code,
            VerifyCS.Diagnostic().WithLocation(0));
    }

    [TestMethod]
    public async Task WhenInClassInitialize_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Threading;
            using System.Threading.Tasks;

            [TestClass]
            public class MyTestClass
            {
                [ClassInitialize]
                public static async Task ClassInit(TestContext testContext)
                {
                    {|#0:await Task.Delay(1000)|};
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code,
            VerifyCS.Diagnostic().WithLocation(0));
    }

    [TestMethod]
    public async Task WhenWithCancellationTokenNone_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Threading;
            using System.Threading.Tasks;

            [TestClass]
            public class MyTestClass
            {
                public TestContext TestContext { get; set; }

                [TestMethod]
                public async Task MyTestMethod()
                {
                    {|#0:await Task.Delay(1000, CancellationToken.None)|};
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code,
            VerifyCS.Diagnostic().WithLocation(0));
    }

    [TestMethod]
    public async Task WhenCodeFixAppliedToReplaceCancellationTokenNone_ReplacesWithTestContext()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Threading;
            using System.Threading.Tasks;

            [TestClass]
            public class MyTestClass
            {
                public TestContext TestContext { get; set; }

                [TestMethod]
                public async Task MyTestMethod()
                {
                    {|#0:await Task.Delay(1000, CancellationToken.None)|};
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Threading;
            using System.Threading.Tasks;

            [TestClass]
            public class MyTestClass
            {
                public TestContext TestContext { get; set; }

                [TestMethod]
                public async Task MyTestMethod()
                {
                    await Task.Delay(1000, TestContext.CancellationTokenSource.Token);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code,
            VerifyCS.Diagnostic().WithLocation(0),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenHttpClientMethodWithoutCancellationToken_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Net.Http;
            using System.Threading;
            using System.Threading.Tasks;

            [TestClass]
            public class MyTestClass
            {
                public TestContext TestContext { get; set; }

                [TestMethod]
                public async Task MyTestMethod()
                {
                    using var client = new HttpClient();
                    {|#0:var response = await client.GetAsync("https://example.com")|};
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code,
            VerifyCS.Diagnostic().WithLocation(0));
    }
}