// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Internal.Framework;
using Microsoft.Testing.TestInfrastructure;

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.AssemblyCleanupShouldBeValidAnalyzer,
    MSTest.Analyzers.AssemblyCleanupShouldBeValidFixer>;

namespace MSTest.Analyzers.Test;

[TestGroup]
public sealed class AssemblyCleanupShouldBeValidAnalyzerTests(ITestExecutionContext testExecutionContext) : TestBase(testExecutionContext)
{
    public async Task WhenAssemblyCleanupIsPublic_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [AssemblyCleanup]
                public static void AssemblyCleanup()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenAssemblyCleanIsInsideAGenericClass_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass<T>
            {
                [AssemblyCleanup]
                public static void {|#0:AssemblyCleanup|}()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic().WithLocation(0).WithArguments("AssemblyCleanup"),
            code);
    }

    public async Task WhenAssemblyCleanupIsNotOrdinary_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [AssemblyCleanup]
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

    public async Task WhenAssemblyCleanupIsPublic_InsideInternalClassWithDiscoverInternals_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: DiscoverInternals]

            [TestClass]
            internal class MyTestClass
            {
                [AssemblyCleanup]
                public static void AssemblyCleanup()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenAssemblyCleanupIsInternal_InsidePublicClassWithDiscoverInternals_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: DiscoverInternals]

            [TestClass]
            public class MyTestClass
            {
                [AssemblyCleanup]
                internal static void {|#0:AssemblyCleanup|}()
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
                [AssemblyCleanup]
                public static void AssemblyCleanup()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic().WithLocation(0).WithArguments("AssemblyCleanup"),
            fixedCode);
    }

    [Arguments("protected")]
    [Arguments("internal")]
    [Arguments("internal protected")]
    [Arguments("private")]
    public async Task WhenAssemblyCleanupIsNotPublic_Diagnostic(string accessibility)
    {
        string code = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [AssemblyCleanup]
                {{accessibility}} static void {|#0:AssemblyCleanup|}()
                {
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [AssemblyCleanup]
                public static void AssemblyCleanup()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic().WithLocation(0).WithArguments("AssemblyCleanup"),
            fixedCode);
    }

    public async Task WhenAssemblyCleanupIsGeneric_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [AssemblyCleanup]
                public static void {|#0:AssemblyCleanup|}<T>()
                {
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [AssemblyCleanup]
                public static void AssemblyCleanup()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic().WithLocation(0).WithArguments("AssemblyCleanup"),
            fixedCode);
    }

    public async Task WhenAssemblyCleanupIsNotStatic_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [AssemblyCleanup]
                public void {|#0:AssemblyCleanup|}()
                {
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [AssemblyCleanup]
                public static void AssemblyCleanup()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic().WithLocation(0).WithArguments("AssemblyCleanup"),
            fixedCode);
    }

    public async Task WhenAssemblyCleanupHasParameters_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [AssemblyCleanup]
                public static void {|#0:AssemblyCleanup|}(TestContext testContext)
                {
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [AssemblyCleanup]
                public static void AssemblyCleanup()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic().WithLocation(0).WithArguments("AssemblyCleanup"),
            fixedCode);
    }

    public async Task WhenAssemblyCleanupReturnTypeIsNotValid_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Threading.Tasks;

            [TestClass]
            public class MyTestClass
            {
                [AssemblyCleanup]
                public static int {|#0:AssemblyCleanup0|}()
                {
                    int x = 1 + 2;
                    return 0;
                }

                [AssemblyCleanup]
                public static string {|#1:AssemblyCleanup1|}()
                {
                    int x = 1 + 2;
                    return "0";
                }

                [AssemblyCleanup]
                public static async Task<int> {|#2:AssemblyCleanup2|}()
                {
                    await Task.Delay(0);
                    return 0;
                }

                [AssemblyCleanup]
                public static Task<int> {|#3:AssemblyCleanup3|}()
                {
                    return Task.FromResult(0);
                }

                [AssemblyCleanup]
                public static async ValueTask<int> {|#4:AssemblyCleanup4|}()
                {
                    await Task.Delay(0);
                    return 0;
                }

                [AssemblyCleanup]
                public static ValueTask<int> {|#5:AssemblyCleanup5|}()
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
                [AssemblyCleanup]
                public static void AssemblyCleanup0()
                {
                    int x = 1 + 2;
                }

                [AssemblyCleanup]
                public static void AssemblyCleanup1()
                {
                    int x = 1 + 2;
                }

                [AssemblyCleanup]
                public static async Task AssemblyCleanup2()
                {
                    await Task.Delay(0);
                }

                [AssemblyCleanup]
                public static Task {|CS0161:AssemblyCleanup3|}()
                {
                }

                [AssemblyCleanup]
                public static async ValueTask AssemblyCleanup4()
                {
                    await Task.Delay(0);
                }

                [AssemblyCleanup]
                public static ValueTask {|CS0161:AssemblyCleanup5|}()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            new[]
            {
                VerifyCS.Diagnostic().WithLocation(0).WithArguments("AssemblyCleanup0"),
                VerifyCS.Diagnostic().WithLocation(1).WithArguments("AssemblyCleanup1"),
                VerifyCS.Diagnostic().WithLocation(2).WithArguments("AssemblyCleanup2"),
                VerifyCS.Diagnostic().WithLocation(3).WithArguments("AssemblyCleanup3"),
                VerifyCS.Diagnostic().WithLocation(4).WithArguments("AssemblyCleanup4"),
                VerifyCS.Diagnostic().WithLocation(5).WithArguments("AssemblyCleanup5"),
            },
            fixedCode);
    }

    public async Task WhenAssemblyCleanupReturnTypeIsValid_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Threading.Tasks;

            [TestClass]
            public class MyTestClass
            {
                [AssemblyCleanup]
                public static void AssemblyCleanup0()
                {
                }

                [AssemblyCleanup]
                public static Task AssemblyCleanup1()
                {
                    return Task.CompletedTask;
                }

                [AssemblyCleanup]
                public static ValueTask AssemblyCleanup2()
                {
                    return ValueTask.CompletedTask;
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenAssemblyCleanupIsAsyncVoid_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Threading.Tasks;

            [TestClass]
            public class MyTestClass
            {
                [AssemblyCleanup]
                public static async void {|#0:AssemblyCleanup|}()
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
                [AssemblyCleanup]
                public static async Task AssemblyCleanup()
                {
                    await Task.Delay(0);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic().WithLocation(0).WithArguments("AssemblyCleanup"),
            fixedCode);
    }
}
