// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Framework;
using Microsoft.Testing.TestInfrastructure;

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.ClassCleanupShouldBeValidAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace MSTest.Analyzers.Test;

[TestGroup]
public sealed class ClassCleanupShouldBeValidAnalyzerTests(ITestExecutionContext testExecutionContext) : TestBase(testExecutionContext)
{
    public async Task WhenClassCleanIsPublic_NoDiagnostic()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [ClassClean]
                public static void ClassClean()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenClassCleanIsNotOrdinary_Diagnostic()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [ClassClean]
                ~{|#0:MyTestClass|}()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(ClassCleanupShouldBeValidAnalyzer.OrdinaryRule)
                .WithLocation(0)
                .WithArguments("Finalize"));
    }

    public async Task WhenClassCleanIsPublic_InsideInternalClassWithDiscoverInternals_NoDiagnostic()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: DiscoverInternals]

            [TestClass]
            internal class MyTestClass
            {
                [ClassClean]
                public static void ClassClean()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenClassCleanIsInternal_InsidePublicClassWithDiscoverInternals_Diagnostic()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: DiscoverInternals]

            [TestClass]
            public class MyTestClass
            {
                [ClassClean]
                internal static void {|#0:ClassClean|}()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(ClassCleanupShouldBeValidAnalyzer.PublicRule)
                .WithLocation(0)
                .WithArguments("ClassClean"));
    }

    [Arguments("protected")]
    [Arguments("internal")]
    [Arguments("internal protected")]
    [Arguments("private")]
    public async Task WhenClassCleanIsNotPublic_Diagnostic(string accessibility)
    {
        var code = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [ClassClean]
                {{accessibility}} static void {|#0:ClassClean|}()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(ClassCleanupShouldBeValidAnalyzer.PublicRule)
                .WithLocation(0)
                .WithArguments("ClassClean"));
    }

    public async Task WhenClassCleanIsGeneric_Diagnostic()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [ClassClean]
                public static void {|#0:ClassClean|}<T>()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(ClassCleanupShouldBeValidAnalyzer.NotGenericRule)
                .WithLocation(0)
                .WithArguments("ClassClean"));
    }

    public async Task WhenClassCleanIsNotStatic_Diagnostic()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [ClassClean]
                public void {|#0:ClassClean|}()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(ClassCleanupShouldBeValidAnalyzer.StaticRule)
                .WithLocation(0)
                .WithArguments("ClassClean"));
    }

    public async Task WhenClassCleanHasParameters_Diagnostic()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [ClassClean]
                public static void {|#0:ClassClean|}(TestContext testContext)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(ClassCleanupShouldBeValidAnalyzer.NoParametersRule)
                .WithLocation(0)
                .WithArguments("ClassClean"));
    }

    public async Task WhenClassCleanReturnTypeIsNotValid_Diagnostic()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Threading.Tasks;

            [TestClass]
            public class MyTestClass
            {
                [ClassClean]
                public static int {|#0:ClassClean0|}()
                {
                    return 0;
                }

                [ClassClean]
                public static string {|#1:ClassClean1|}()
                {
                    return "0";
                }

                [ClassClean]
                public static Task<int> {|#2:ClassClean2|}()
                {
                    return Task.FromResult(0);
                }

                [ClassClean]
                public static ValueTask<int> {|#3:ClassClean3|}()
                {
                    return ValueTask.FromResult(0);
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(ClassCleanupShouldBeValidAnalyzer.ReturnTypeRule)
                .WithLocation(0)
                .WithArguments("ClassClean0"),
            VerifyCS.Diagnostic(ClassCleanupShouldBeValidAnalyzer.ReturnTypeRule)
                .WithLocation(1)
                .WithArguments("ClassClean1"),
            VerifyCS.Diagnostic(ClassCleanupShouldBeValidAnalyzer.ReturnTypeRule)
                .WithLocation(2)
                .WithArguments("ClassClean2"),
            VerifyCS.Diagnostic(ClassCleanupShouldBeValidAnalyzer.ReturnTypeRule)
                .WithLocation(3)
                .WithArguments("ClassClean3"));
    }

    public async Task WhenClassCleanReturnTypeIsValid_NoDiagnostic()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Threading.Tasks;

            [TestClass]
            public class MyTestClass
            {
                [ClassClean]
                public static void ClassClean0()
                {
                }

                [ClassClean]
                public static Task ClassClean1()
                {
                    return Task.CompletedTask;
                }

                [ClassClean]
                public static ValueTask ClassClean2()
                {
                    return ValueTask.CompletedTask;
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenClassCleanIsAsyncVoid_Diagnostic()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Threading.Tasks;

            [TestClass]
            public class MyTestClass
            {
                [ClassClean]
                public static async void {|#0:ClassClean|}()
                {
                    await Task.Delay(0);
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(ClassCleanupShouldBeValidAnalyzer.NotAsyncVoidRule)
                .WithLocation(0)
                .WithArguments("ClassClean"));
    }
}
