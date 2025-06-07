// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.PreferTestMethodOverDataTestMethodAnalyzer,
    MSTest.Analyzers.PreferTestMethodOverDataTestMethodFixer>;

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class PreferTestMethodOverDataTestMethodAnalyzerTests
{
    [TestMethod]
    public async Task WhenUsingTestMethod_NoDiagnostic()
    {
        string code = """
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

    [TestMethod]
    public async Task WhenUsingDataTestMethod_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [{|#0:DataTestMethod|}]
                public void MyTestMethod()
                {
                }
            }
            """;

        var expected = VerifyCS.Diagnostic(PreferTestMethodOverDataTestMethodAnalyzer.PreferTestMethodOverDataTestMethodRule)
            .WithLocation(0);

        await VerifyCS.VerifyAnalyzerAsync(code, expected);
    }

    [TestMethod]
    public async Task WhenUsingDataTestMethodWithParameters_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [{|#0:DataTestMethod|}]
                [DataRow(1, 2)]
                public void MyTestMethod(int a, int b)
                {
                }
            }
            """;

        var expected = VerifyCS.Diagnostic(PreferTestMethodOverDataTestMethodAnalyzer.PreferTestMethodOverDataTestMethodRule)
            .WithLocation(0);

        await VerifyCS.VerifyAnalyzerAsync(code, expected);
    }

    [TestMethod]
    public async Task WhenUsingBothTestMethodAndDataTestMethod_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [{|#0:DataTestMethod|}]
                public void MyTestMethod()
                {
                }
            }
            """;

        var expected = VerifyCS.Diagnostic(PreferTestMethodOverDataTestMethodAnalyzer.PreferTestMethodOverDataTestMethodRule)
            .WithLocation(0);

        await VerifyCS.VerifyAnalyzerAsync(code, expected);
    }

    [TestMethod]
    public async Task WhenUsingDataTestMethodWithDisplayName_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [{|#0:DataTestMethod("Display Name")|}]
                public void MyTestMethod()
                {
                }
            }
            """;

        var expected = VerifyCS.Diagnostic(PreferTestMethodOverDataTestMethodAnalyzer.PreferTestMethodOverDataTestMethodRule)
            .WithLocation(0);

        await VerifyCS.VerifyAnalyzerAsync(code, expected);
    }

    [TestMethod]
    public async Task WhenUsingDataTestMethod_CodeFixReplacesWithTestMethod()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [{|#0:DataTestMethod|}]
                public void MyTestMethod()
                {
                }
            }
            """;

        string fixedCode = """
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

        var expected = VerifyCS.Diagnostic(PreferTestMethodOverDataTestMethodAnalyzer.PreferTestMethodOverDataTestMethodRule)
            .WithLocation(0);

        await VerifyCS.VerifyCodeFixAsync(code, expected, fixedCode);
    }

    [TestMethod]
    public async Task WhenUsingBothTestMethodAndDataTestMethod_CodeFixRemovesDataTestMethod()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [{|#0:DataTestMethod|}]
                public void MyTestMethod()
                {
                }
            }
            """;

        string fixedCode = """
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

        var expected = VerifyCS.Diagnostic(PreferTestMethodOverDataTestMethodAnalyzer.PreferTestMethodOverDataTestMethodRule)
            .WithLocation(0);

        await VerifyCS.VerifyCodeFixAsync(code, expected, fixedCode);
    }

    [TestMethod]
    public async Task WhenUsingDataTestMethodWithMultipleAttributesInSameList_CodeFixRemovesOnlyDataTestMethod()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod, {|#0:DataTestMethod|}]
                public void MyTestMethod()
                {
                }
            }
            """;

        string fixedCode = """
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

        var expected = VerifyCS.Diagnostic(PreferTestMethodOverDataTestMethodAnalyzer.PreferTestMethodOverDataTestMethodRule)
            .WithLocation(0);

        await VerifyCS.VerifyCodeFixAsync(code, expected, fixedCode);
    }

    [TestMethod]
    public async Task WhenUsingDataTestMethodWithParameterizedTests_CodeFixReplacesWithTestMethod()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [{|#0:DataTestMethod|}]
                [DataRow(1, 2)]
                [DataRow(3, 4)]
                public void MyTestMethod(int a, int b)
                {
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [DataRow(1, 2)]
                [DataRow(3, 4)]
                public void MyTestMethod(int a, int b)
                {
                }
            }
            """;

        var expected = VerifyCS.Diagnostic(PreferTestMethodOverDataTestMethodAnalyzer.PreferTestMethodOverDataTestMethodRule)
            .WithLocation(0);

        await VerifyCS.VerifyCodeFixAsync(code, expected, fixedCode);
    }
}