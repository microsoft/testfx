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
                    await [|Task.Delay(1000)|];
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

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
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

        await VerifyCS.VerifyCodeFixAsync(code, code);
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

        await VerifyCS.VerifyCodeFixAsync(code, code);
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

        await VerifyCS.VerifyCodeFixAsync(code, code);
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
                    await [|Task.Delay(1000)|];
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

                [TestInitialize]
                public async Task TestInit()
                {
                    await Task.Delay(1000, TestContext.CancellationTokenSource.Token);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
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
                    await [|Task.Delay(1000)|];
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

                [TestCleanup]
                public async Task TestCleanup()
                {
                    await Task.Delay(1000, TestContext.CancellationTokenSource.Token);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
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
                    await [|Task.Delay(1000)|];
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
                [ClassInitialize]
                public static async Task ClassInit(TestContext testContext)
                {
                    await Task.Delay(1000, testContext.CancellationTokenSource.Token);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenInClassCleanupWithTestContextParameter_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Threading;
            using System.Threading.Tasks;

            [TestClass]
            public class MyTestClass
            {
                [ClassCleanup]
                public static async Task ClassCleanup(TestContext testContext)
                {
                    await [|Task.Delay(1000)|];
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
                [ClassCleanup]
                public static async Task ClassCleanup(TestContext testContext)
                {
                    await Task.Delay(1000, testContext.CancellationTokenSource.Token);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenInClassCleanupWithoutTestContextParameter_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Threading;
            using System.Threading.Tasks;

            [TestClass]
            public class MyTestClass
            {
                [ClassCleanup]
                public static async Task ClassCleanup()
                {
                    await [|Task.Delay(1000)|];
                }
            }
            """;

        // Codefix now adds TestContext parameter when missing.
        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Threading;
            using System.Threading.Tasks;

            [TestClass]
            public class MyTestClass
            {
                [ClassCleanup]
                public static async Task ClassCleanup(TestContext testContext)
                {
                    await Task.Delay(1000, testContext.CancellationTokenSource.Token);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenInTestMethodWithoutTestContextInScope_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Threading;
            using System.Threading.Tasks;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public async Task Test1()
                {
                    await [|Task.Delay(1000)|];
                }

                [TestMethod]
                [DataRow(0)]
                [DataRow(1)]
                public async Task Test2(int _)
                {
                    await [|Task.Delay(1000)|];
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
                public async Task Test1()
                {
                    await Task.Delay(1000, TestContext.CancellationTokenSource.Token);
                }

                [TestMethod]
                [DataRow(0)]
                [DataRow(1)]
                public async Task Test2(int _)
                {
                    await Task.Delay(1000, TestContext.CancellationTokenSource.Token);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenInTestMethodWithTestContextFieldInScope_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Threading;
            using System.Threading.Tasks;

            [TestClass]
            public class MyTestClass
            {
                private readonly TestContext _testContext;

                public MyTestClass(TestContext testContext)
                    => _testContext = testContext;

                [TestMethod]
                public async Task Test1()
                {
                    await [|Task.Delay(1000)|];
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
                private readonly TestContext _testContext;
            
                public MyTestClass(TestContext testContext)
                    => _testContext = testContext;

                [TestMethod]
                public async Task Test1()
                {
                    await Task.Delay(1000, _testContext.CancellationTokenSource.Token);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenInTestMethodWithTestContextInScopeViaPrimaryConstructor_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Threading;
            using System.Threading.Tasks;

            [TestClass]
            public class MyTestClass(TestContext MyTestContext)
            {
                [TestMethod]
                public async Task Test1()
                {
                    _ = MyTestContext;
                    await [|Task.Delay(1000)|];
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Threading;
            using System.Threading.Tasks;

            [TestClass]
            public class MyTestClass(TestContext MyTestContext)
            {
                [TestMethod]
                public async Task Test1()
                {
                    _ = MyTestContext;
                    await Task.Delay(1000, MyTestContext.CancellationTokenSource.Token);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenWithCancellationTokenNone_NoDiagnostic()
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
                    await Task.Delay(1000, CancellationToken.None);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
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
                    var response = await [|client.GetAsync("https://example.com")|];
                }
            }
            """;

        string fixedCode = """
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
                    var response = await client.GetAsync("https://example.com", TestContext.CancellationTokenSource.Token);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenMultipleFixesInSameClassWithoutTestContext_ShouldAddPropertyOnce()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Threading;
            using System.Threading.Tasks;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public async Task Test1()
                {
                    await [|Task.Delay(1000)|];
                    await [|Task.Delay(2000)|];
                }

                [TestMethod]
                public async Task Test2()
                {
                    await [|Task.Delay(3000)|];
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
                public async Task Test1()
                {
                    await Task.Delay(1000, TestContext.CancellationTokenSource.Token);
                    await Task.Delay(2000, TestContext.CancellationTokenSource.Token);
                }

                [TestMethod]
                public async Task Test2()
                {
                    await Task.Delay(3000, TestContext.CancellationTokenSource.Token);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenInAssemblyCleanupWithoutTestContextParameter_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Threading;
            using System.Threading.Tasks;

            [TestClass]
            public class MyTestClass
            {
                [AssemblyCleanup]
                public static async Task AssemblyCleanup()
                {
                    await [|Task.Delay(1000)|];
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
                [AssemblyCleanup]
                public static async Task AssemblyCleanup(TestContext testContext)
                {
                    await Task.Delay(1000, testContext.CancellationTokenSource.Token);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }
}
