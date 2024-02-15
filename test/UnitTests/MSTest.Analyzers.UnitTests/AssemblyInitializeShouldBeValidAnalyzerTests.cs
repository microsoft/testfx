// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Framework;
using Microsoft.Testing.TestInfrastructure;

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.AssemblyInitializeShouldBeValidAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace MSTest.Analyzers.Test;

[TestGroup]
public sealed class AssemblyInitializeShouldBeValidAnalyzerTests(ITestExecutionContext testExecutionContext) : TestBase(testExecutionContext)
{
    public async Task WhenAssemblyInitializeIsPublic_NoDiagnostic()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [AssemblyInitialize]
                public static void AssemblyInitialize(TestContext context)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenAssemblyInitializeIsPublic_InsideInternalClassWithDiscoverInternals_NoDiagnostic()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            
            [assembly: DiscoverInternals]

            [TestClass]
            internal class MyTestClass
            {
                [AssemblyInitialize]
                public static void AssemblyInitialize(TestContext context)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenAssemblyInitializeIsInternal_InsidePublicClassWithDiscoverInternals_Diagnostic()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: DiscoverInternals]

            [TestClass]
            public class MyTestClass
            {
                [AssemblyInitialize]
                internal static void {|#0:AssemblyInitialize|}(TestContext context)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(AssemblyInitializeShouldBeValidAnalyzer.PublicRule)
                .WithLocation(0)
                .WithArguments("AssemblyInitialize"));
    }

    [Arguments("protected")]
    [Arguments("internal")]
    [Arguments("internal protected")]
    [Arguments("private")]
    public async Task WhenAssemblyInitializeIsNotPublic_Diagnostic(string accessibility)
    {
        var code = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [AssemblyInitialize]
                {{accessibility}} static void {|#0:AssemblyInitialize|}(TestContext context)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(AssemblyInitializeShouldBeValidAnalyzer.PublicRule)
                .WithLocation(0)
                .WithArguments("AssemblyInitialize"));
    }

    public async Task WhenAssemblyInitializeIsNotOrdinary_Diagnostic()
    {
        var code = """
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

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(AssemblyInitializeShouldBeValidAnalyzer.OrdinaryRule)
                .WithLocation(0)
                .WithArguments("Finalize"));
    }

    public async Task WhenAssemblyInitializeIsGeneric_Diagnostic()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [AssemblyInitialize]
                public static void {|#0:AssemblyInitialize|}<T>(TestContext context)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(AssemblyInitializeShouldBeValidAnalyzer.NotGenericRule)
                .WithLocation(0)
                .WithArguments("AssemblyInitialize"));
    }

    public async Task WhenAssemblyInitializeIsNotStatic_Diagnostic()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [AssemblyInitialize]
                public void {|#0:AssemblyInitialize|}(TestContext context)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(AssemblyInitializeShouldBeValidAnalyzer.StaticRule)
                .WithLocation(0)
                .WithArguments("AssemblyInitialize"));
    }

    public async Task WhenAssemblyInitializeDoesNotHaveParameters_Diagnostic()
    {
        var code = """
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

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(AssemblyInitializeShouldBeValidAnalyzer.SingleContextParameterRule)
                .WithLocation(0)
                .WithArguments("AssemblyInitialize"));
    }

    public async Task WhenAssemblyInitializeReturnTypeIsNotValid_Diagnostic()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Threading.Tasks;

            [TestClass]
            public class MyTestClass
            {
                [AssemblyInitialize]
                public static int {|#0:AssemblyInitialize0|}(TestContext context)
                {
                    return 0;
                }

                [AssemblyInitialize]
                public static string {|#1:AssemblyInitialize1|}(TestContext context)
                {
                    return "0";
                }

                [AssemblyInitialize]
                public static Task<int> {|#2:AssemblyInitialize2|}(TestContext context)
                {
                    return Task.FromResult(0);
                }

                [AssemblyInitialize]
                public static ValueTask<int> {|#3:AssemblyInitialize3|}(TestContext context)
                {
                    return ValueTask.FromResult(0);
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(AssemblyInitializeShouldBeValidAnalyzer.ReturnTypeRule)
                .WithLocation(0)
                .WithArguments("AssemblyInitialize0"),
            VerifyCS.Diagnostic(AssemblyInitializeShouldBeValidAnalyzer.ReturnTypeRule)
                .WithLocation(1)
                .WithArguments("AssemblyInitialize1"),
            VerifyCS.Diagnostic(AssemblyInitializeShouldBeValidAnalyzer.ReturnTypeRule)
                .WithLocation(2)
                .WithArguments("AssemblyInitialize2"),
            VerifyCS.Diagnostic(AssemblyInitializeShouldBeValidAnalyzer.ReturnTypeRule)
                .WithLocation(3)
                .WithArguments("AssemblyInitialize3"));
    }

    public async Task WhenAssemblyInitializeReturnTypeIsValid_NoDiagnostic()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Threading.Tasks;

            [TestClass]
            public class MyTestClass
            {
                [AssemblyInitialize]
                public static void AssemblyInitialize0(TestContext context)
                {
                }

                [AssemblyInitialize]
                public static Task AssemblyInitialize1(TestContext context)
                {
                    return Task.CompletedTask;
                }

                [AssemblyInitialize]
                public static ValueTask AssemblyInitialize2(TestContext context)
                {
                    return ValueTask.CompletedTask;
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenAssemblyInitializeIsAsyncVoid_Diagnostic()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Threading.Tasks;

            [TestClass]
            public class MyTestClass
            {
                [AssemblyInitialize]
                public static async void {|#0:AssemblyInitialize|}(TestContext context)
                {
                    await Task.Delay(0);
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(AssemblyInitializeShouldBeValidAnalyzer.NotAsyncVoidRule)
                .WithLocation(0)
                .WithArguments("AssemblyInitialize"));
    }
}
