// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Framework;
using Microsoft.Testing.TestInfrastructure;

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.TestMethodShouldBeValidAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace MSTest.Analyzers.Test;

[TestGroup]
public sealed class TestMethodShouldBeValidAnalyzerTests(ITestExecutionContext testExecutionContext) : TestBase(testExecutionContext)
{
    public async Task WhenTestMethodIsPublic_NoDiagnostic()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [Arguments("protected")]
    [Arguments("internal")]
    [Arguments("internal protected")]
    [Arguments("private")]
    public async Task WhenTestMethodIsNotPublic_Diagnostic(string accessibility)
    {
        var code = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                {{accessibility}} void {|#0:MyTestMethod|}()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(TestMethodShouldBeValidAnalyzer.PublicRule)
                .WithLocation(0)
                .WithArguments("MyTestMethod"));
    }

    public async Task WhenMethodIsNotPublicAndNotTestMethod_NoDiagnostic()
    {
        var code = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                private void PrivateMethod()
                {
                }

                protected void ProtectedMethod()
                {
                }

                internal protected void InternalProtectedMethod()
                {
                }

                internal void InternalMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenTestMethodIsStatic_Diagnostic()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public static void {|#0:MyTestMethod|}()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(TestMethodShouldBeValidAnalyzer.NotStaticRule)
                .WithLocation(0)
                .WithArguments("MyTestMethod"));
    }

    public async Task WhenTestMethodIsAbstract_Diagnostic()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public abstract class MyTestClass
            {
                [TestMethod]
                public abstract void {|#0:MyTestMethod|}();
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(TestMethodShouldBeValidAnalyzer.NotAbstractRule)
                .WithLocation(0)
                .WithArguments("MyTestMethod"));
    }

    public async Task WhenTestMethodIsGeneric_Diagnostic()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void {|#0:MyTestMethod|}<T>()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(TestMethodShouldBeValidAnalyzer.NotGenericRule)
                .WithLocation(0)
                .WithArguments("MyTestMethod"));
    }

    public async Task WhenTestMethodIsNotOrdinary_Diagnostic()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                ~{|#0:MyTestClass|}()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(TestMethodShouldBeValidAnalyzer.OrdinaryRule)
                .WithLocation(0)
                .WithArguments("Finalize"));
    }

    public async Task WhenTestMethodReturnTypeIsNotValid_Diagnostic()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Threading.Tasks;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public int {|#0:MyTestMethod0|}()
                {
                    return 42;
                }

                [TestMethod]
                public string {|#1:MyTestMethod1|}()
                {
                    return "42";
                }

                [TestMethod]
                public Task<int> {|#2:MyTestMethod2|}()
                {
                    return Task.FromResult(42);
                }

                [TestMethod]
                public ValueTask {|#3:MyTestMethod3|}()
                {
                    return ValueTask.CompletedTask;
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(TestMethodShouldBeValidAnalyzer.ReturnTypeRule)
                .WithLocation(0)
                .WithArguments("MyTestMethod0"),
            VerifyCS.Diagnostic(TestMethodShouldBeValidAnalyzer.ReturnTypeRule)
                .WithLocation(1)
                .WithArguments("MyTestMethod1"),
            VerifyCS.Diagnostic(TestMethodShouldBeValidAnalyzer.ReturnTypeRule)
                .WithLocation(2)
                .WithArguments("MyTestMethod2"),
            VerifyCS.Diagnostic(TestMethodShouldBeValidAnalyzer.ReturnTypeRule)
                .WithLocation(3)
                .WithArguments("MyTestMethod3"));
    }

    public async Task WhenTestMethodReturnTypeIsValid_NoDiagnostic()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Threading.Tasks;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod0()
                {
                }

                [TestMethod]
                public Task MyTestMethod1()
                {
                    return Task.CompletedTask;
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenTestMethodIsAsyncVoid_Diagnostic()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Threading.Tasks;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public async void {|#0:MyTestMethod|}()
                {
                    await Task.Delay(0);
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(TestMethodShouldBeValidAnalyzer.NotAsyncVoidRule)
                .WithLocation(0)
                .WithArguments("MyTestMethod"));
    }

    public async Task WhenTestMethodIsInternalAndDiscoverInternals_NoDiagnostic()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Threading.Tasks;

            [assembly: DiscoverInternals]

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                internal void MyTestMethod()
                {
                }
            }

            [TestClass]
            internal class MyTestClass2
            {
                [TestMethod]
                internal void MyTestMethod()
                {
                }
            }

            [TestClass]
            internal class MyTestClass3
            {
                [TestMethod]
                public void MyTestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenTestMethodIsPrivateAndDiscoverInternals_Diagnostic()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Threading.Tasks;

            [assembly: DiscoverInternals]

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                private void {|#0:MyTestMethod|}()
                {
                }
            }
            
            public class Outer
            {
                [TestClass]
                private class MyTestClass2
                {
                    [TestMethod]
                    public void {|#1:MyTestMethod|}()
                    {
                    }
                }

                [TestClass]
                private class MyTestClass3
                {
                    [TestMethod]
                    private void {|#2:MyTestMethod|}()
                    {
                    }
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(TestMethodShouldBeValidAnalyzer.PublicOrInternalRule)
                .WithLocation(0)
                .WithArguments("MyTestMethod"),
            VerifyCS.Diagnostic(TestMethodShouldBeValidAnalyzer.PublicOrInternalRule)
                .WithLocation(1)
                .WithArguments("MyTestMethod"),
            VerifyCS.Diagnostic(TestMethodShouldBeValidAnalyzer.PublicOrInternalRule)
                .WithLocation(2)
                .WithArguments("MyTestMethod"));
    }
}
