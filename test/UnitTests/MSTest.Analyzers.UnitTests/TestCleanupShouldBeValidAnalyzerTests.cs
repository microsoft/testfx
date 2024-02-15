// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Framework;
using Microsoft.Testing.TestInfrastructure;

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.TestCleanupShouldBeValidAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace MSTest.Analyzers.Test;

[TestGroup]
public sealed class TestCleanupShouldBeValidAnalyzerTests(ITestExecutionContext testExecutionContext) : TestBase(testExecutionContext)
{
    public async Task WhenTestCleanupIsPublic_NoDiagnostic()
    {
        var code = """
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
        var code = """
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
        var code = """
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

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(TestCleanupShouldBeValidAnalyzer.PublicRule)
                .WithLocation(0)
                .WithArguments("TestCleanup"));
    }
    
    public async Task WhenTestCleanupIsNotOrdinary_Diagnostic()
    {
        var code = """
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

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(TestCleanupShouldBeValidAnalyzer.OrdinaryRule)
                .WithLocation(0)
                .WithArguments("Finalize"));
    }

    public async Task WhenTestCleanupIsAbstract_Diagnostic()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public abstract class MyTestClass
            {
                [TestCleanup]
                public abstract void {|#0:TestCleanup|}();
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(TestCleanupShouldBeValidAnalyzer.NotAbstractRule)
                .WithLocation(0)
                .WithArguments("TestCleanup"));
    }

    public async Task WhenTestCleanupIsGeneric_Diagnostic()
    {
        var code = """
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

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(TestCleanupShouldBeValidAnalyzer.NotGenericRule)
                .WithLocation(0)
                .WithArguments("TestCleanup"));
    }

    [Arguments("protected")]
    [Arguments("internal")]
    [Arguments("internal protected")]
    [Arguments("private")]
    public async Task WhenTestCleanupIsNotPublic_Diagnostic(string accessibility)
    {
        var code = $$"""
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

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(TestCleanupShouldBeValidAnalyzer.PublicRule)
                .WithLocation(0)
                .WithArguments("TestCleanup"));
    }

    public async Task WhenTestCleanupIsStatic_Diagnostic()
    {
        var code = """
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

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(TestCleanupShouldBeValidAnalyzer.NotStaticRule)
                .WithLocation(0)
                .WithArguments("TestCleanup"));
    }

    public async Task WhenTestCleanupHasParameters_Diagnostic()
    {
        var code = """
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

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(TestCleanupShouldBeValidAnalyzer.NoParametersRule)
                .WithLocation(0)
                .WithArguments("TestCleanup"));
    }

    public async Task WhenTestCleanupReturnTypeIsNotValid_Diagnostic()
    {
        var code = """
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

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(TestCleanupShouldBeValidAnalyzer.ReturnTypeRule)
                .WithLocation(0)
                .WithArguments("TestCleanup0"),
            VerifyCS.Diagnostic(TestCleanupShouldBeValidAnalyzer.ReturnTypeRule)
                .WithLocation(1)
                .WithArguments("TestCleanup1"),
            VerifyCS.Diagnostic(TestCleanupShouldBeValidAnalyzer.ReturnTypeRule)
                .WithLocation(2)
                .WithArguments("TestCleanup2"),
            VerifyCS.Diagnostic(TestCleanupShouldBeValidAnalyzer.ReturnTypeRule)
                .WithLocation(3)
                .WithArguments("TestCleanup3"));
    }

    public async Task WhenTestCleanupReturnTypeIsValid_NoDiagnostic()
    {
        var code = """
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
        var code = """
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

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(TestCleanupShouldBeValidAnalyzer.NotAsyncVoidRule)
                .WithLocation(0)
                .WithArguments("TestCleanup"));
    }
}
