// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.EmptyTestMethodAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class EmptyTestMethodAnalyzerTests
{
    [TestMethod]
    public async Task WhenTestMethodHasStatements_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    Assert.IsTrue(true);
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenTestMethodIsCompletelyEmpty_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void {|#0:MyTestMethod|}()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(EmptyTestMethodAnalyzer.EmptyTestMethodRule)
                .WithLocation(0)
                .WithArguments("MyTestMethod"));
    }

    [TestMethod]
    public async Task WhenTestMethodHasOnlyComments_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void {|#0:MyTestMethod|}()
                {
                    // This is a comment
                    /* This is another comment */
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(EmptyTestMethodAnalyzer.EmptyTestMethodRule)
                .WithLocation(0)
                .WithArguments("MyTestMethod"));
    }

    [TestMethod]
    public async Task WhenTestMethodHasOnlyWhitespace_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void {|#0:MyTestMethod|}()
                {
                    
                    
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(EmptyTestMethodAnalyzer.EmptyTestMethodRule)
                .WithLocation(0)
                .WithArguments("MyTestMethod"));
    }

    [TestMethod]
    public async Task WhenRegularMethodIsEmpty_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                public void MyMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenTestMethodHasVariableDeclaration_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    var x = 5;
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenTestMethodHasMethodCall_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    DoSomething();
                }

                private void DoSomething() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenDerivedTestMethodAttributeIsEmpty_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class CustomTestMethodAttribute : TestMethodAttribute
            {
            }

            [TestClass]
            public class MyTestClass
            {
                [CustomTestMethod]
                public void {|#0:MyTestMethod|}()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(EmptyTestMethodAnalyzer.EmptyTestMethodRule)
                .WithLocation(0)
                .WithArguments("MyTestMethod"));
    }

    [TestMethod]
    public async Task WhenDataTestMethodIsEmpty_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [DataTestMethod]
                [DataRow(1)]
                public void {|#0:MyTestMethod|}(int value)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(EmptyTestMethodAnalyzer.EmptyTestMethodRule)
                .WithLocation(0)
                .WithArguments("MyTestMethod"));
    }

    [TestMethod]
    public async Task WhenAsyncTestMethodIsEmpty_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Threading.Tasks;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public async Task {|#0:MyTestMethod|}()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(EmptyTestMethodAnalyzer.EmptyTestMethodRule)
                .WithLocation(0)
                .WithArguments("MyTestMethod"));
    }

    [TestMethod]
    public async Task WhenAsyncTestMethodHasAwait_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Threading.Tasks;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public async Task MyTestMethod()
                {
                    await Task.Delay(1);
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }
}