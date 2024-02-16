// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Framework;
using Microsoft.Testing.TestInfrastructure;

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.AssemblyCleanupShouldBeValidAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace MSTest.Analyzers.Test;

[TestGroup]
public sealed class AssemblyCleanupShouldBeValidAnalyzerTests(ITestExecutionContext testExecutionContext) : TestBase(testExecutionContext)
{
    public async Task WhenAssemblyCleanupIsPublic_NoDiagnostic()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [AssemblyCleanup]
                public void AssemblyCleanup()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenAssemblyCleanupIsNotOrdinary_Diagnostic()
    {
        var code = """
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

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(AssemblyCleanupShouldBeValidAnalyzer.OrdinaryRule)
                .WithLocation(0)
                .WithArguments("Finalize"));
    }

    public async Task WhenAssemblyCleanupIsPublic_InsideInternalClassWithDiscoverInternals_NoDiagnostic()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: DiscoverInternals]

            [TestClass]
            internal class MyTestClass
            {
                [AssemblyCleanup]
                public void AssemblyCleanup()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenAssemblyCleanupIsInternal_InsidePublicClassWithDiscoverInternals_Diagnostic()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: DiscoverInternals]

            [TestClass]
            public class MyTestClass
            {
                [AssemblyCleanup]
                internal void {|#0:AssemblyCleanup|}()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(AssemblyCleanupShouldBeValidAnalyzer.PublicRule)
                .WithLocation(0)
                .WithArguments("AssemblyCleanup"));
    }

    [Arguments("protected")]
    [Arguments("internal")]
    [Arguments("internal protected")]
    [Arguments("private")]
    public async Task WhenAssemblyCleanupIsNotPublic_Diagnostic(string accessibility)
    {
        var code = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [AssemblyCleanup]
                {{accessibility}} void {|#0:AssemblyCleanup|}()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(AssemblyCleanupShouldBeValidAnalyzer.PublicRule)
                .WithLocation(0)
                .WithArguments("AssemblyCleanup"));
    }

    public async Task WhenAssemblyCleanupIsAbstract_Diagnostic()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public abstract class MyTestClass
            {
                [AssemblyCleanup]
                public abstract void {|#0:AssemblyCleanup|}();
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(AssemblyCleanupShouldBeValidAnalyzer.NotAbstractRule)
                .WithLocation(0)
                .WithArguments("AssemblyCleanup"));
    }

    public async Task WhenAssemblyCleanupIsGeneric_Diagnostic()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [AssemblyCleanup]
                public void {|#0:AssemblyCleanup|}<T>()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(AssemblyCleanupShouldBeValidAnalyzer.NotGenericRule)
                .WithLocation(0)
                .WithArguments("AssemblyCleanup"));
    }

    public async Task WhenAssemblyCleanupIsStatic_Diagnostic()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [AssemblyCleanup]
                public static void {|#0:AssemblyCleanup|}()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(AssemblyCleanupShouldBeValidAnalyzer.NotStaticRule)
                .WithLocation(0)
                .WithArguments("AssemblyCleanup"));
    }

    public async Task WhenAssemblyCleanupHasParameters_Diagnostic()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [AssemblyCleanup]
                public void {|#0:AssemblyCleanup|}(TestContext testContext)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(AssemblyCleanupShouldBeValidAnalyzer.NoParametersRule)
                .WithLocation(0)
                .WithArguments("AssemblyCleanup"));
    }

    public async Task WhenAssemblyCleanupReturnTypeIsNotValid_Diagnostic()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Threading.Tasks;

            [TestClass]
            public class MyTestClass
            {
                [AssemblyCleanup]
                public int {|#0:AssemblyCleanup0|}()
                {
                    return 0;
                }

                [AssemblyCleanup]
                public string {|#1:AssemblyCleanup1|}()
                {
                    return "0";
                }

                [AssemblyCleanup]
                public Task<int> {|#2:AssemblyCleanup2|}()
                {
                    return Task.FromResult(0);
                }

                [AssemblyCleanup]
                public ValueTask<int> {|#3:AssemblyCleanup3|}()
                {
                    return ValueTask.FromResult(0);
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(AssemblyCleanupShouldBeValidAnalyzer.ReturnTypeRule)
                .WithLocation(0)
                .WithArguments("AssemblyCleanup0"),
            VerifyCS.Diagnostic(AssemblyCleanupShouldBeValidAnalyzer.ReturnTypeRule)
                .WithLocation(1)
                .WithArguments("AssemblyCleanup1"),
            VerifyCS.Diagnostic(AssemblyCleanupShouldBeValidAnalyzer.ReturnTypeRule)
                .WithLocation(2)
                .WithArguments("AssemblyCleanup2"),
            VerifyCS.Diagnostic(AssemblyCleanupShouldBeValidAnalyzer.ReturnTypeRule)
                .WithLocation(3)
                .WithArguments("AssemblyCleanup3"));
    }

    public async Task WhenAssemblyCleanupReturnTypeIsValid_NoDiagnostic()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Threading.Tasks;

            [TestClass]
            public class MyTestClass
            {
                [AssemblyCleanup]
                public void AssemblyCleanup0()
                {
                }

                [AssemblyCleanup]
                public Task AssemblyCleanup1()
                {
                    return Task.CompletedTask;
                }

                [AssemblyCleanup]
                public ValueTask AssemblyCleanup2()
                {
                    return ValueTask.CompletedTask;
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenAssemblyCleanupIsAsyncVoid_Diagnostic()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Threading.Tasks;

            [TestClass]
            public class MyTestClass
            {
                [AssemblyCleanup]
                public async void {|#0:AssemblyCleanup|}()
                {
                    await Task.Delay(0);
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(AssemblyCleanupShouldBeValidAnalyzer.NotAsyncVoidRule)
                .WithLocation(0)
                .WithArguments("AssemblyCleanup"));
    }
}
