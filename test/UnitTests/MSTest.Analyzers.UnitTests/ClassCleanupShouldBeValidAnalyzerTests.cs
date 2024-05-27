// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Internal.Framework;
using Microsoft.Testing.TestInfrastructure;

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.ClassCleanupShouldBeValidAnalyzer,
    MSTest.Analyzers.ClassCleanupShouldBeValidFixer>;

namespace MSTest.Analyzers.Test;

[TestGroup]
public sealed class ClassCleanupShouldBeValidAnalyzerTests(ITestExecutionContext testExecutionContext) : TestBase(testExecutionContext)
{
    public async Task WhenClassCleanupIsPublic_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [ClassCleanup]
                public static void ClassCleanup()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenClassCleanupIsGenericWithInheritanceModeSet_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass<T>
            {
                [ClassCleanup(inheritanceBehavior: InheritanceBehavior.BeforeEachDerivedClass)]
                public static void ClassCleanup()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenClassCleanupIsGenericWithInheritanceModeSetToNone_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass<T>
            {
                [ClassCleanup(InheritanceBehavior.None)]
                public static void {|#0:ClassCleanup|}()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic().WithLocation(0).WithArguments("ClassCleanup"),
            code);
    }

    public async Task WhenClassCleanupIsGenericWithoutSettingInheritanceMode_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass<T>
            {
                [ClassCleanup]
                public static void {|#0:ClassCleanup|}()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic().WithLocation(0).WithArguments("ClassCleanup"),
            code);
    }

    public async Task WhenClassCleanupIsNotOrdinary_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [ClassCleanup]
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

    public async Task WhenClassCleanupIsPublic_InsideInternalClassWithDiscoverInternals_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: DiscoverInternals]

            [TestClass]
            internal class MyTestClass
            {
                [ClassCleanup]
                public static void ClassCleanup()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenClassCleanupIsInternal_InsidePublicClassWithDiscoverInternals_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: DiscoverInternals]

            [TestClass]
            public class MyTestClass
            {
                [ClassCleanup]
                internal static void {|#0:ClassCleanup|}()
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
                [ClassCleanup]
                public static void ClassCleanup()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic().WithLocation(0).WithArguments("ClassCleanup"),
            fixedCode);
    }

    [Arguments("protected")]
    [Arguments("internal")]
    [Arguments("internal protected")]
    [Arguments("private")]
    public async Task WhenClassCleanupIsNotPublic_Diagnostic(string accessibility)
    {
        string code = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [ClassCleanup]
                {{accessibility}} static void {|#0:ClassCleanup|}()
                {
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [ClassCleanup]
                public static void ClassCleanup()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic().WithLocation(0).WithArguments("ClassCleanup"),
            fixedCode);
    }

    public async Task WhenClassCleanupIsGeneric_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [ClassCleanup]
                public static void {|#0:ClassCleanup|}<T>()
                {
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [ClassCleanup]
                public static void ClassCleanup()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic().WithLocation(0).WithArguments("ClassCleanup"),
            fixedCode);
    }

    public async Task WhenClassCleanupIsNotStatic_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [ClassCleanup]
                public void {|#0:ClassCleanup|}()
                {
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [ClassCleanup]
                public static void ClassCleanup()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic().WithLocation(0).WithArguments("ClassCleanup"),
            fixedCode);
    }

    public async Task WhenClassCleanupHasParameters_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [ClassCleanup]
                public static void {|#0:ClassCleanup|}(TestContext testContext)
                {
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [ClassCleanup]
                public static void ClassCleanup()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic().WithLocation(0).WithArguments("ClassCleanup"),
            fixedCode);
    }

    public async Task WhenClassCleanupReturnTypeIsNotValid_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Threading.Tasks;

            [TestClass]
            public class MyTestClass
            {
                [ClassCleanup]
                public static int {|#0:ClassCleanup0|}()
                {
                    return 0;
                }

                [ClassCleanup]
                public static string {|#1:ClassCleanup1|}()
                {
                    return "0";
                }

                [ClassCleanup]
                public static Task<int> {|#2:ClassCleanup2|}()
                {
                    return Task.FromResult(0);
                }

                [ClassCleanup]
                public static ValueTask<int> {|#3:ClassCleanup3|}()
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
                [ClassCleanup]
                public static void ClassCleanup0()
                {
                }

                [ClassCleanup]
                public static void ClassCleanup1()
                {
                }

                [ClassCleanup]
                public static Task {|CS0161:ClassCleanup2|}()
                {
                }

                [ClassCleanup]
                public static ValueTask {|CS0161:ClassCleanup3|}()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            [
                VerifyCS.Diagnostic().WithLocation(0).WithArguments("ClassCleanup0"),
                VerifyCS.Diagnostic().WithLocation(1).WithArguments("ClassCleanup1"),
                VerifyCS.Diagnostic().WithLocation(2).WithArguments("ClassCleanup2"),
                VerifyCS.Diagnostic().WithLocation(3).WithArguments("ClassCleanup3")
            ],
            fixedCode);
    }

    public async Task WhenClassCleanupReturnTypeIsValid_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Threading.Tasks;

            [TestClass]
            public class MyTestClass
            {
                [ClassCleanup]
                public static void ClassCleanup0()
                {
                }

                [ClassCleanup]
                public static Task ClassCleanup1()
                {
                    return Task.CompletedTask;
                }

                [ClassCleanup]
                public static ValueTask ClassCleanup2()
                {
                    return ValueTask.CompletedTask;
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenClassCleanupIsAsyncVoid_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Threading.Tasks;

            [TestClass]
            public class MyTestClass
            {
                [ClassCleanup]
                public static async void {|#0:ClassCleanup|}()
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
                [ClassCleanup]
                public static async Task ClassCleanup()
                {
                    await Task.Delay(0);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic().WithLocation(0).WithArguments("ClassCleanup"),
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
                [ClassCleanup]
                public async void {|#0:ClassCleanup|}<T>(int i)
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
                [ClassCleanup]
                public static async Task ClassCleanup()
                {
                    await Task.Delay(0);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic().WithLocation(0).WithArguments("ClassCleanup"),
            fixedCode);
    }
}
