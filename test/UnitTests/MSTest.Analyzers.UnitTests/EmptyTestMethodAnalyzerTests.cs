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
                public void TestWithStatements()
                {
                    var x = 1;
                    Assert.AreEqual(1, x);
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenTestMethodIsEmpty_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void {|#0:EmptyTestMethod|}()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code,
            VerifyCS.Diagnostic(EmptyTestMethodAnalyzer.EmptyTestMethodRule)
                .WithLocation(0)
                .WithArguments("EmptyTestMethod"));
    }

    [TestMethod]
    public async Task WhenTestMethodHasExpressionBody_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestWithExpressionBody() => Assert.IsTrue(true);
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenMethodIsNotTestMethod_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                public void EmptyNormalMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
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
                public void {|#0:EmptyDataTestMethod|}(int value)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code,
            VerifyCS.Diagnostic(EmptyTestMethodAnalyzer.EmptyTestMethodRule)
                .WithLocation(0)
                .WithArguments("EmptyDataTestMethod"));
    }

    [TestMethod]
    public async Task WhenAsyncTestMethodIsEmpty_Diagnostic()
    {
        string code = """
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public async Task {|#0:EmptyAsyncTestMethod|}()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code,
            VerifyCS.Diagnostic(EmptyTestMethodAnalyzer.EmptyTestMethodRule)
                .WithLocation(0)
                .WithArguments("EmptyAsyncTestMethod"));
    }

    [TestMethod]
    public async Task WhenAsyncTestMethodHasAwaitStatement_NoDiagnostic()
    {
        string code = """
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public async Task AsyncTestWithContent()
                {
                    await Task.Delay(1);
                    Assert.IsTrue(true);
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenMultipleEmptyTestMethods_MultipleDiagnostics()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void {|#0:EmptyTest1|}()
                {
                }

                [TestMethod]
                public void {|#1:EmptyTest2|}()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code,
            VerifyCS.Diagnostic(EmptyTestMethodAnalyzer.EmptyTestMethodRule)
                .WithLocation(0)
                .WithArguments("EmptyTest1"),
            VerifyCS.Diagnostic(EmptyTestMethodAnalyzer.EmptyTestMethodRule)
                .WithLocation(1)
                .WithArguments("EmptyTest2"));
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
                public void {|#0:TestWithOnlyComments|}()
                {
                    // This is a comment
                    /* This is also a comment */
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code,
            VerifyCS.Diagnostic(EmptyTestMethodAnalyzer.EmptyTestMethodRule)
                .WithLocation(0)
                .WithArguments("TestWithOnlyComments"));
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
                public void {|#0:TestWithOnlyWhitespace|}()
                {
                    
                    
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code,
            VerifyCS.Diagnostic(EmptyTestMethodAnalyzer.EmptyTestMethodRule)
                .WithLocation(0)
                .WithArguments("TestWithOnlyWhitespace"));
    }

    [TestMethod]
    public async Task WhenAbstractTestMethod_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public abstract class MyTestClass
            {
                [TestMethod]
                public abstract void AbstractTestMethod();
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenDataTestMethodHasStatements_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [DataTestMethod]
                [DataRow(1)]
                [DataRow(2)]
                public void DataTestWithStatements(int value)
                {
                    Assert.IsTrue(value > 0);
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenTestMethodWithExpressionBodyHasAssertion_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestWithExpressionAssertion() => Assert.AreEqual(1, 1);
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenAsyncTestMethodWithExpressionBody_NoDiagnostic()
    {
        string code = """
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public async Task AsyncTestWithExpressionBody() => await Task.CompletedTask;
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }
}
