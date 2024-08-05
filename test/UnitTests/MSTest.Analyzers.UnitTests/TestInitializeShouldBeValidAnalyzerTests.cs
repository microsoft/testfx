// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.TestInitializeShouldBeValidAnalyzer,
    MSTest.Analyzers.TestInitializeShouldBeValidFixer>;

namespace MSTest.Analyzers.Test;

[TestGroup]
public sealed class TestInitializeShouldBeValidAnalyzerTests(ITestExecutionContext testExecutionContext) : TestBase(testExecutionContext)
{
    public async Task WhenTestInitializeIsPublic_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestInitialize]
                public void TestInitialize()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenTestInitializeIsNotOrdinary_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestInitialize]
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

    public async Task WhenTestInitializeIsPublic_InsideInternalClassWithDiscoverInternals_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: DiscoverInternals]

            [TestClass]
            internal class MyTestClass
            {
                [TestInitialize]
                public void TestInitialize()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenTestInitializeIsInternal_InsidePublicClassWithDiscoverInternals_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: DiscoverInternals]

            [TestClass]
            public class MyTestClass
            {
                [TestInitialize]
                internal void {|#0:TestInitialize|}()
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
                [TestInitialize]
                public void TestInitialize()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic().WithLocation(0).WithArguments("TestInitialize"),
            fixedCode);
    }

    [Arguments("protected")]
    [Arguments("internal")]
    [Arguments("internal protected")]
    [Arguments("private")]
    public async Task WhenTestInitializeIsNotPublic_Diagnostic(string accessibility)
    {
        string code = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestInitialize]
                {{accessibility}} void {|#0:TestInitialize|}()
                {
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestInitialize]
                public void TestInitialize()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic(TestInitializeShouldBeValidAnalyzer.Rule)
                .WithLocation(0)
                .WithArguments("TestInitialize"),
            fixedCode);
    }

    public async Task WhenTestInitializeIsAbstract_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public abstract class MyTestClass
            {
                [TestInitialize]
                public abstract void {|#0:TestInitialize|}();
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public abstract class MyTestClass
            {
                [TestInitialize]
                public void TestInitialize()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic().WithLocation(0).WithArguments("TestInitialize"),
            fixedCode);
    }

    public async Task WhenTestInitializeIsGeneric_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestInitialize]
                public void {|#0:TestInitialize|}<T>()
                {
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestInitialize]
                public void TestInitialize()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic().WithLocation(0).WithArguments("TestInitialize"),
            fixedCode);
    }

    public async Task WhenTestInitializeIsStatic_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestInitialize]
                public static void {|#0:TestInitialize|}()
                {
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestInitialize]
                public void TestInitialize()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic().WithLocation(0).WithArguments("TestInitialize"),
            fixedCode);
    }

    public async Task WhenTestInitializeHasParameters_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestInitialize]
                public void {|#0:TestInitialize|}(TestContext testContext)
                {
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestInitialize]
                public void TestInitialize()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic().WithLocation(0).WithArguments("TestInitialize"),
            fixedCode);
    }

    public async Task WhenTestInitializeReturnTypeIsNotValid_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Threading.Tasks;

            [TestClass]
            public class MyTestClass
            {
                [TestInitialize]
                public int {|#0:TestInitialize0|}()
                {
                    return 0;
                }

                [TestInitialize]
                public string {|#1:TestInitialize1|}()
                {
                    return "0";
                }

                [TestInitialize]
                public Task<int> {|#2:TestInitialize2|}()
                {
                    return Task.FromResult(0);
                }

                [TestInitialize]
                public ValueTask<int> {|#3:TestInitialize3|}()
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
                [TestInitialize]
                public void TestInitialize0()
                {
                }

                [TestInitialize]
                public void TestInitialize1()
                {
                }

                [TestInitialize]
                public Task {|CS0161:TestInitialize2|}()
                {
                }

                [TestInitialize]
                public ValueTask {|CS0161:TestInitialize3|}()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            [
                VerifyCS.Diagnostic().WithLocation(0).WithArguments("TestInitialize0"),
                VerifyCS.Diagnostic().WithLocation(1).WithArguments("TestInitialize1"),
                VerifyCS.Diagnostic().WithLocation(2).WithArguments("TestInitialize2"),
                VerifyCS.Diagnostic().WithLocation(3).WithArguments("TestInitialize3")
            ],
            fixedCode);
    }

    public async Task WhenTestInitializeReturnTypeIsValid_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Threading.Tasks;

            [TestClass]
            public class MyTestClass
            {
                [TestInitialize]
                public void TestInitialize0()
                {
                }

                [TestInitialize]
                public Task TestInitialize1()
                {
                    return Task.CompletedTask;
                }

                [TestInitialize]
                public ValueTask TestInitialize2()
                {
                    return ValueTask.CompletedTask;
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenTestInitializeIsAsyncVoid_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Threading.Tasks;

            [TestClass]
            public class MyTestClass
            {
                [TestInitialize]
                public async void {|#0:TestInitialize|}()
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
                [TestInitialize]
                public async Task TestInitialize()
                {
                    await Task.Delay(0);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic().WithLocation(0).WithArguments("TestInitialize"),
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
                [TestInitialize]
                public static async void {|#0:TestInitialize|}<T>(int i)
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
                [TestInitialize]
                public async Task TestInitialize()
                {
                    await Task.Delay(0);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic().WithLocation(0).WithArguments("TestInitialize"),
            fixedCode);
    }

    public async Task WhenTestInitializeIsNotOnClass_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public struct MyTestClass
            {
                [TestInitialize]
                public void [|TestInitialize|]()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenTestInitializeIsOnSealedClassNotMarkedWithTestClass_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public sealed class MyTestClass
            {
                [TestInitialize]
                public void [|TestInitialize|]()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenTestInitializeIsOnNonSealedClassNotMarkedWithTestClass_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class MyTestClass
            {
                [TestInitialize]
                public void TestInitialize()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenTestInitializeIsOnGenericClass_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass<T>
            {
                [TestInitialize]
                public void TestInitialize()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }
}
