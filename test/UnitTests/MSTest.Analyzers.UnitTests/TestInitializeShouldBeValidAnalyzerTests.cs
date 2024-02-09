// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Framework;
using Microsoft.Testing.TestInfrastructure;

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.TestInitializeShouldBeValidAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace MSTest.Analyzers.Test;

[TestGroup]
public sealed class TestInitializeShouldBeValidAnalyzerTests(ITestExecutionContext testExecutionContext) : TestBase(testExecutionContext)
{
    public async Task WhenTestInitializeIsPublic_NoDiagnostic()
    {
        var code = """
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
        var code = """
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

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(TestInitializeShouldBeValidAnalyzer.OrdinaryRule)
                .WithLocation(0)
                .WithArguments("Finalize"));
    }

    public async Task WhenTestInitializeIsAbstract_Diagnostic()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public abstract class MyTestClass
            {
                [TestInitialize]
                public abstract void {|#0:TestInitialize|}();
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(TestInitializeShouldBeValidAnalyzer.NotAbstractRule)
                .WithLocation(0)
                .WithArguments("TestInitialize"));
    }

    public async Task WhenTestInitializeIsGeneric_Diagnostic()
    {
        var code = """
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

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(TestInitializeShouldBeValidAnalyzer.NotGenericRule)
                .WithLocation(0)
                .WithArguments("TestInitialize"));
    }

    [Arguments("protected")]
    [Arguments("internal")]
    [Arguments("internal protected")]
    [Arguments("private")]
    public async Task WhenTestInitializeIsNotPublic_Diagnostic(string accessibility)
    {
        var code = $$"""
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

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(TestInitializeShouldBeValidAnalyzer.PublicRule)
                .WithLocation(0)
                .WithArguments("TestInitialize"));
    }

    public async Task WhenTestInitializeIsStatic_Diagnostic()
    {
        var code = """
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

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(TestInitializeShouldBeValidAnalyzer.NotStaticRule)
                .WithLocation(0)
                .WithArguments("TestInitialize"));
    }

    public async Task WhenTestInitializeHasParameters_Diagnostic()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestInitialize]
                public void {|#0:TestInitialize|}(int x)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(TestInitializeShouldBeValidAnalyzer.NoParametersRule)
                .WithLocation(0)
                .WithArguments("TestInitialize"));
    }

    public async Task WhenTestInitializeReturnTypeIsNotValid_Diagnostic()
    {
        var code = """
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

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(TestInitializeShouldBeValidAnalyzer.ReturnTypeRule)
                .WithLocation(0)
                .WithArguments("TestInitialize0"),
            VerifyCS.Diagnostic(TestInitializeShouldBeValidAnalyzer.ReturnTypeRule)
                .WithLocation(1)
                .WithArguments("TestInitialize1"),
            VerifyCS.Diagnostic(TestInitializeShouldBeValidAnalyzer.ReturnTypeRule)
                .WithLocation(2)
                .WithArguments("TestInitialize2"),
            VerifyCS.Diagnostic(TestInitializeShouldBeValidAnalyzer.ReturnTypeRule)
                .WithLocation(3)
                .WithArguments("TestInitialize3"));
    }

    public async Task WhenTestInitializeReturnTypeIsValid_NoDiagnostic()
    {
        var code = """
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
        var code = """
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

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(TestInitializeShouldBeValidAnalyzer.NotAsyncVoidRule)
                .WithLocation(0)
                .WithArguments("TestInitialize"));
    }
}
