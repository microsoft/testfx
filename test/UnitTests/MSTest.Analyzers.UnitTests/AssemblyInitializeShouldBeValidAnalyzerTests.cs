// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Internal.Framework;
using Microsoft.Testing.TestInfrastructure;

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.AssemblyInitializeShouldBeValidAnalyzer,
    MSTest.Analyzers.AssemblyInitializeShouldBeValidFixer>;

namespace MSTest.Analyzers.Test;

[TestGroup]
public sealed class AssemblyInitializeShouldBeValidAnalyzerTests(ITestExecutionContext testExecutionContext) : TestBase(testExecutionContext)
{
    public async Task WhenAssemblyInitializeIsPublic_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [AssemblyInitialize]
                public static void AssemblyInitialize(TestContext testContext)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenAssemblyInitializeIsPublic_InsideInternalClassWithDiscoverInternals_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: DiscoverInternals]

            [TestClass]
            internal class MyTestClass
            {
                [AssemblyInitialize]
                public static void AssemblyInitialize(TestContext testContext)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenAssemblyInitializeIsInsideAGenericClass_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass<T>
            {
                [AssemblyInitialize]
                public static void {|#0:AssemblyInitialize|}(TestContext testContext)
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic().WithLocation(0).WithArguments("AssemblyInitialize"),
            code);
    }

    public async Task WhenAssemblyInitializeIsInternal_InsidePublicClassWithDiscoverInternals_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: DiscoverInternals]

            [TestClass]
            public class MyTestClass
            {
                [AssemblyInitialize]
                internal static void {|#0:AssemblyInitialize|}(TestContext testContext)
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
                [AssemblyInitialize]
                public static void AssemblyInitialize(TestContext testContext)
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic().WithLocation(0).WithArguments("AssemblyInitialize"),
            fixedCode);
    }

    [Arguments("protected")]
    [Arguments("internal")]
    [Arguments("internal protected")]
    [Arguments("private")]
    public async Task WhenAssemblyInitializeIsNotPublic_Diagnostic(string accessibility)
    {
        string code = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [AssemblyInitialize]
                {{accessibility}} static void {|#0:AssemblyInitialize|}(TestContext testContext)
                {
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [AssemblyInitialize]
                public static void AssemblyInitialize(TestContext testContext)
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic().WithLocation(0).WithArguments("AssemblyInitialize"),
            fixedCode);
    }

    public async Task WhenAssemblyInitializeIsNotOrdinary_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [AssemblyInitialize]
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

    public async Task WhenAssemblyInitializeIsGeneric_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [AssemblyInitialize]
                public static void {|#0:AssemblyInitialize|}<T>(TestContext testContext)
                {
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [AssemblyInitialize]
                public static void AssemblyInitialize(TestContext testContext)
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic().WithLocation(0).WithArguments("AssemblyInitialize"),
            fixedCode);
    }

    public async Task WhenAssemblyInitializeIsNotStatic_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [AssemblyInitialize]
                public void {|#0:AssemblyInitialize|}(TestContext testContext)
                {
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [AssemblyInitialize]
                public static void AssemblyInitialize(TestContext testContext)
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic().WithLocation(0).WithArguments("AssemblyInitialize"),
            fixedCode);
    }

    public async Task WhenAssemblyInitializeDoesNotHaveParameters_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [AssemblyInitialize]
                public static void {|#0:AssemblyInitialize|}()
                {
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [AssemblyInitialize]
                public static void AssemblyInitialize(TestContext testContext)
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic().WithLocation(0).WithArguments("AssemblyInitialize"),
            fixedCode);
    }

    public async Task WhenAssemblyInitializeReturnTypeIsNotValid_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Threading.Tasks;

            [TestClass]
            public class MyTestClass
            {
                [AssemblyInitialize]
                public static int {|#0:AssemblyInitialize0|}(TestContext testContext)
                {
                    return 0;
                }

                [AssemblyInitialize]
                public static string {|#1:AssemblyInitialize1|}(TestContext testContext)
                {
                    return "0";
                }

                [AssemblyInitialize]
                public static async Task<int> {|#2:AssemblyInitialize2|}(TestContext testContext)
                {
                    await Task.Delay(0);
                    return 0;
                }

                [AssemblyInitialize]
                public static Task<int> {|#3:AssemblyInitialize3|}(TestContext testContext)
                {
                    return Task.FromResult(0);
                }

                [AssemblyInitialize]
                public static async ValueTask<int> {|#4:AssemblyInitialize4|}(TestContext testContext)
                {
                    await Task.Delay(0);
                    return 0;
                }

                [AssemblyInitialize]
                public static ValueTask<int> {|#5:AssemblyInitialize5|}(TestContext testContext)
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
                [AssemblyInitialize]
                public static void AssemblyInitialize0(TestContext testContext)
                {
                }

                [AssemblyInitialize]
                public static void AssemblyInitialize1(TestContext testContext)
                {
                }

                [AssemblyInitialize]
                public static async Task AssemblyInitialize2(TestContext testContext)
                {
                    await Task.Delay(0);
                }

                [AssemblyInitialize]
                public static Task {|CS0161:AssemblyInitialize3|}(TestContext testContext)
                {
                }

                [AssemblyInitialize]
                public static async ValueTask AssemblyInitialize4(TestContext testContext)
                {
                    await Task.Delay(0);
                }

                [AssemblyInitialize]
                public static ValueTask {|CS0161:AssemblyInitialize5|}(TestContext testContext)
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            [
                VerifyCS.Diagnostic().WithLocation(0).WithArguments("AssemblyInitialize0"),
                VerifyCS.Diagnostic().WithLocation(1).WithArguments("AssemblyInitialize1"),
                VerifyCS.Diagnostic().WithLocation(2).WithArguments("AssemblyInitialize2"),
                VerifyCS.Diagnostic().WithLocation(3).WithArguments("AssemblyInitialize3"),
                VerifyCS.Diagnostic().WithLocation(4).WithArguments("AssemblyInitialize4"),
                VerifyCS.Diagnostic().WithLocation(5).WithArguments("AssemblyInitialize5")
            ],
            fixedCode);
    }

    public async Task WhenAssemblyInitializeReturnTypeIsValid_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Threading.Tasks;

            [TestClass]
            public class MyTestClass
            {
                [AssemblyInitialize]
                public static void AssemblyInitialize0(TestContext testContext)
                {
                }

                [AssemblyInitialize]
                public static Task AssemblyInitialize1(TestContext testContext)
                {
                    return Task.CompletedTask;
                }

                [AssemblyInitialize]
                public static ValueTask AssemblyInitialize2(TestContext testContext)
                {
                    return ValueTask.CompletedTask;
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenAssemblyInitializeIsAsyncVoid_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Threading.Tasks;

            [TestClass]
            public class MyTestClass
            {
                [AssemblyInitialize]
                public static async void {|#0:AssemblyInitialize|}(TestContext testContext)
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
                [AssemblyInitialize]
                public static async Task AssemblyInitialize(TestContext testContext)
                {
                    await Task.Delay(0);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic().WithLocation(0).WithArguments("AssemblyInitialize"),
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
                [AssemblyInitialize]
                public async void {|#0:AssemblyInitialize|}<T>(int i)
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
                [AssemblyInitialize]
                public static async Task AssemblyInitialize(TestContext testContext)
                {
                    await Task.Delay(0);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic().WithLocation(0).WithArguments("AssemblyInitialize"),
            fixedCode);
    }
}
