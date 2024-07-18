// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.TestCleanupShouldBeValidAnalyzer,
    MSTest.Analyzers.TestCleanupShouldBeValidFixer>;

namespace MSTest.Analyzers.Test;

[TestGroup]
public sealed class TestCleanupShouldBeValidAnalyzerTests(ITestExecutionContext testExecutionContext) : TestBase(testExecutionContext)
{
    public async Task WhenTestCleanupIsPublic_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestCleanup]
                public void TestCleanup()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenTestCleanupIsPublic_InsideInternalClassWithDiscoverInternals_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: DiscoverInternals]

            [TestClass]
            internal class MyTestClass
            {
                [TestCleanup]
                public void TestCleanup()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenTestCleanupIsInternal_InsidePublicClassWithDiscoverInternals_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: DiscoverInternals]

            [TestClass]
            public class MyTestClass
            {
                [TestCleanup]
                internal void {|#0:TestCleanup|}()
                {
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: DiscoverInternals]

            [TestClass]
            public class MyTestClass
            {
                [TestCleanup]
                public void TestCleanup()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic(TestCleanupShouldBeValidAnalyzer.Rule)
                .WithLocation(0)
                .WithArguments("TestCleanup"),
            fixedCode);
    }

    [Arguments("protected")]
    [Arguments("internal")]
    [Arguments("internal protected")]
    [Arguments("private")]
    public async Task WhenTestCleanupIsNotPublic_Diagnostic(string accessibility)
    {
        string code = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestCleanup]
                {{accessibility}} void {|#0:TestCleanup|}()
                {
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestCleanup]
                public void TestCleanup()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic(TestCleanupShouldBeValidAnalyzer.Rule)
                .WithLocation(0)
                .WithArguments("TestCleanup"),
            fixedCode);
    }

    public async Task WhenTestCleanupIsNotOrdinary_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestCleanup]
                ~{|#0:MyTestClass|}()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic().WithLocation(0).WithArguments("Finalize"),
            code);
    }

    public async Task WhenTestCleanupIsAbstract_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public abstract class MyTestClass
            {
                [TestCleanup]
                public abstract void {|#0:TestCleanup|}();
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public abstract class MyTestClass
            {
                [TestCleanup]
                public void TestCleanup()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic().WithLocation(0).WithArguments("TestCleanup"),
            fixedCode);
    }

    public async Task WhenTestCleanupIsGeneric_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestCleanup]
                public void {|#0:TestCleanup|}<T>()
                {
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestCleanup]
                public void TestCleanup()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic().WithLocation(0).WithArguments("TestCleanup"),
            fixedCode);
    }

    public async Task WhenTestCleanupIsStatic_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestCleanup]
                public static void {|#0:TestCleanup|}()
                {
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestCleanup]
                public void TestCleanup()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic().WithLocation(0).WithArguments("TestCleanup"),
            fixedCode);
    }

    public async Task WhenTestCleanupHasParameters_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestCleanup]
                public void {|#0:TestCleanup|}(TestContext testContext)
                {
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestCleanup]
                public void TestCleanup()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic().WithLocation(0).WithArguments("TestCleanup"),
            fixedCode);
    }

    public async Task WhenTestCleanupReturnTypeIsNotValid_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Threading.Tasks;

            [TestClass]
            public class MyTestClass
            {
                [TestCleanup]
                public int {|#0:TestCleanup0|}()
                {
                    return 0;
                }

                [TestCleanup]
                public string {|#1:TestCleanup1|}()
                {
                    return "0";
                }

                [TestCleanup]
                public Task<int> {|#2:TestCleanup2|}()
                {
                    return Task.FromResult(0);
                }

                [TestCleanup]
                public ValueTask<int> {|#3:TestCleanup3|}()
                {
                    return ValueTask.FromResult(0);
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Threading.Tasks;

            [TestClass]
            public class MyTestClass
            {
                [TestCleanup]
                public void TestCleanup0()
                {
                }

                [TestCleanup]
                public void TestCleanup1()
                {
                }

                [TestCleanup]
                public Task {|CS0161:TestCleanup2|}()
                {
                }

                [TestCleanup]
                public ValueTask {|CS0161:TestCleanup3|}()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            [
                VerifyCS.Diagnostic().WithLocation(0).WithArguments("TestCleanup0"),
                VerifyCS.Diagnostic().WithLocation(1).WithArguments("TestCleanup1"),
                VerifyCS.Diagnostic().WithLocation(2).WithArguments("TestCleanup2"),
                VerifyCS.Diagnostic().WithLocation(3).WithArguments("TestCleanup3")
            ],
            fixedCode);
    }

    public async Task WhenTestCleanupReturnTypeIsValid_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Threading.Tasks;

            [TestClass]
            public class MyTestClass
            {
                [TestCleanup]
                public void TestCleanup0()
                {
                }

                [TestCleanup]
                public Task TestCleanup1()
                {
                    return Task.CompletedTask;
                }

                [TestCleanup]
                public ValueTask TestCleanup2()
                {
                    return ValueTask.CompletedTask;
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenTestCleanupIsAsyncVoid_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Threading.Tasks;

            [TestClass]
            public class MyTestClass
            {
                [TestCleanup]
                public async void {|#0:TestCleanup|}()
                {
                    await Task.Delay(0);
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Threading.Tasks;

            [TestClass]
            public class MyTestClass
            {
                [TestCleanup]
                public async Task TestCleanup()
                {
                    await Task.Delay(0);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic().WithLocation(0).WithArguments("TestCleanup"),
            fixedCode);
    }

    public async Task WhenMultipleViolations_TheyAllGetFixed()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Threading.Tasks;

            [TestClass]
            public class MyTestClass
            {
                [TestCleanup]
                public static async void {|#0:TestCleanup|}<T>(int i)
                {
                    await Task.Delay(0);
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Threading.Tasks;

            [TestClass]
            public class MyTestClass
            {
                [TestCleanup]
                public async Task TestCleanup()
                {
                    await Task.Delay(0);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic().WithLocation(0).WithArguments("TestCleanup"),
            fixedCode);
    }

    public async Task WhenTestCleanupIsNotOnClass_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public struct MyTestClass
            {
                [TestCleanup]
                public void [|TestCleanup|]()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenTestCleanupIsOnSealedClassNotMarkedWithTestClass_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public sealed class MyTestClass
            {
                [TestCleanup]
                public void [|TestCleanup|]()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenTestCleanupIsOnNonSealedClassNotMarkedWithTestClass_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class MyTestClass
            {
                [TestCleanup]
                public void TestCleanup()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenTestCleanupIsOnGenericClass_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass<T>
            {
                [TestCleanup]
                public void TestCleanup()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }
}
