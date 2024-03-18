﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Framework;
using Microsoft.Testing.TestInfrastructure;

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.DataRowShouldBeValidAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace MSTest.Analyzers.Test;

[TestGroup]
public sealed class DataRowShouldBeValidAnalyzerTests(ITestExecutionContext testExecutionContext) : TestBase(testExecutionContext)
{
    public async Task WhenDataRowIsCorrectlyDefinedWithOneArgument_NoDiagnostic()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [DataRow(1)]
                [TestMethod]
                public void TestMethod1(int a)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenDataRowIsCorrectlyDefinedWithOneArgumentAndWithDataTestMethodAttribute_NoDiagnostic()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [DataRow(1)]
                [DataTestMethod]
                public void TestMethod1(int a)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenDataRowIsCorrectlyDefinedWithOneArgumentAndWithDerivedTestMethodAttribute_NoDiagnostic()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class DerivedTestMethod : TestMethodAttribute
            {
            }

            [TestClass]
            public class MyTestClass
            {
                [DataRow(1)]
                [DerivedTestMethod]
                public void TestMethod1(int a)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenDataRowIsCorrectlyDefinedWithThreeArguments_NoDiagnostic()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [DataRow(1, 2, 3)]
                [TestMethod]
                public void TestMethod1(int a, int b, int c)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenDataRowIsCorrectlyDefinedWithThreeArgumentsAndMethodHasParamsArgument_NoDiagnostic()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [DataRow(1, 2, 3)]
                [TestMethod]
                public void TestMethod1(params object[] o)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenDataRowIsCorrectlyDefinedWithThreeArgumentsAndMethodHasArrayArgument_NoDiagnostic()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [DataRow(1, 2, 3)]
                [TestMethod]
                public void TestMethod1(object[] o)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenDataRowHasThreeArgumentsAndMethodHasAnIntegerAndAnArrayArgument_Diagnostic()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [{|#0:DataRow(1, 2, 3)|}]
                [TestMethod]
                public void TestMethod1(int i, object[] o)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(DataRowShouldBeValidAnalyzer.ArgumentCountMismatchRule)
                .WithLocation(0)
                .WithArguments(3, 2));
    }

    public async Task WhenDataRowIsCorrectlyDefinedWithOneArgumentAndMethodHasAPrimitiveTypeAndAParamsArgument_NoDiagnostic()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [DataRow(1)]
                [TestMethod]
                public void TestMethod1(int a, params object[] o)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenDataRowIsCorrectlyDefinedWithThreeArgumentsAndMethodHasAPrimitiveTypeAndAParamsArgument_NoDiagnostic()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [DataRow(1, 2, 3)]
                [TestMethod]
                public void TestMethod1(int a, params object[] o)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenDataRowIsCorrectlyDefinedWithOneArgumentAndMethodHasAPrimitiveTypeAndAParamsStringArgument_NoDiagnostic()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [DataRow(1)]
                [TestMethod]
                public void TestMethod1(int a, params string[] o)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenDataRowIsCorrectlyDefinedWithOneArgumentAndMethodHasAPrimitiveTypeAndADefaultArgument_NoDiagnostic()
    {
        var code = """
            #nullable enable

            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [DataRow(1)]
                [TestMethod]
                public void TestMethod1(int a, object?[]? o = null)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenDataRowIsCorrectlyDefinedWithOneArgumentAndIntegersAreAssignableToDoubles_NoDiagnostic()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [DataRow(1)]
                [TestMethod]
                public void TestMethod1(double d)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenDataRowIsCorrectlyDefinedWithOneArgumentAndCharsAreAssignableToIntegers_NoDiagnostic()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [DataRow('c')]
                [TestMethod]
                public void TestMethod1(int i)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenDataRowIsCorrectlyDefinedWithOneArgumentAndNullsAreAssignableToIntegers_NoDiagnostic()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [DataRow(null)]
                [TestMethod]
                public void TestMethod1(int i)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenDataRowHasOneNullArgumentAndMethodHasNoArguments_Diagnostic()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [{|#0:DataRow(null)|}]
                [TestMethod]
                public void TestMethod1()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(DataRowShouldBeValidAnalyzer.ArgumentCountMismatchRule)
                .WithLocation(0)
                .WithArguments(1, 0));
    }

    public async Task WhenDataRowIsNotSetOnATestMethod_Diagnostic()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [DataRow(1)]
                public void {|#0:TestMethod1|}(int i)
                {
                }
            }
            """
        ;

        await VerifyCS.VerifyAnalyzerAsync(code, VerifyCS.Diagnostic(DataRowShouldBeValidAnalyzer.DataRowOnTestMethodRule).WithLocation(0));
    }

    public async Task WhenDataRowHasNoArgsButMethodHasOneArgument_Diagnostic()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [{|#0:DataRow()|}]
                [TestMethod]
                public void TestMethod1(int i)
                {
                }
            }
            """
        ;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(DataRowShouldBeValidAnalyzer.ArgumentCountMismatchRule)
                .WithLocation(0)
                .WithArguments(0, 1));
    }

    public async Task WhenDataRowHasArgumentMismatchWithTestMethod_Diagnostic()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [{|#0:DataRow(1, 2, 3)|}]
                [TestMethod]
                public void TestMethod1(int i, int j, int k, int l)
                {
                }
            }
            """
        ;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(DataRowShouldBeValidAnalyzer.ArgumentCountMismatchRule)
                .WithLocation(0)
                .WithArguments(3, 4));
    }

    public async Task WhenDataRowHasArgumentMismatchWithTestMethod2_Diagnostic()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [{|#0:DataRow(1, 2, 3)|}]
                [TestMethod]
                public void TestMethod1(int i, int j)
                {
                }
            }
            """
        ;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(DataRowShouldBeValidAnalyzer.ArgumentCountMismatchRule)
                .WithLocation(0)
                .WithArguments(3, 2));
    }

    public async Task WhenDataRowHasArgumentMismatchWithTestMethod3_Diagnostic()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [{|#0:DataRow(1, 2, 3)|}]
                [TestMethod]
                public void TestMethod1(int i, object o)
                {
                }
            }
            """
        ;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(DataRowShouldBeValidAnalyzer.ArgumentCountMismatchRule)
                .WithLocation(0)
                .WithArguments(3, 2));
    }

    public async Task WhenDataRowHasTypeMismatchWithTestMethod_Diagnostic()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [{|#0:DataRow(1, 2, 3)|}]
                [TestMethod]
                public void TestMethod1(int i, int j, string s)
                {
                }
            }
            """
        ;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(DataRowShouldBeValidAnalyzer.ArgumentTypeMismatchRule)
                .WithLocation(0)
                .WithArguments((2, 2)));
    }

    public async Task DefaultArguments()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [DataRow(1L, "s")]                      // Correct count (using optional default value)
                [DataRow(1L, "s", true)]                // Correct count
                [{|#0:DataRow(1L, "s", true, "a")|}]    // Too many args
                [{|#1:DataRow(1L)|}]                    // Not enough args
                [TestMethod]
                public void TestMethod1(long l, string s, bool b = false)
                {
                }

                [DataRow(1L, "s")]                          // Correct count (using optional default value)
                [DataRow(1L, "s", true)]                    // Correct count (using some default)
                [DataRow(1L, "s", true, 1.0)]               // Correct count
                [DataRow(1L, "s", true, 1.0, "a", false)]   // Extra args are swallowed by params
                [{|#2:DataRow(1L)|}]                        // Not enough args
                [TestMethod]
                public void TestMethod2(long l, string s, bool b = false, double d = 1.0, params object[] others)
                {
                }
            }
            """
        ;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(DataRowShouldBeValidAnalyzer.ArgumentCountMismatchRule).WithLocation(0).WithArguments(4, 3),
            VerifyCS.Diagnostic(DataRowShouldBeValidAnalyzer.ArgumentCountMismatchRule).WithLocation(1).WithArguments(1, 3),
            VerifyCS.Diagnostic(DataRowShouldBeValidAnalyzer.ArgumentCountMismatchRule).WithLocation(2).WithArguments(1, 5));
    }
}
