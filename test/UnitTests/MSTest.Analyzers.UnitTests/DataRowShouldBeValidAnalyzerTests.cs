// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.DataRowShouldBeValidAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class DataRowShouldBeValidAnalyzerTests
{
    [TestMethod]
    public async Task WhenDataRowIsCorrectlyDefinedWithOneArgument_NoDiagnostic()
    {
        string code = """
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

    [TestMethod]
    public async Task WhenDataRowIsCorrectlyDefinedWithOneArgumentAndWithDataTestMethodAttribute_NoDiagnostic()
    {
        string code = """
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

    [TestMethod]
    public async Task WhenDataRowIsCorrectlyDefinedWithOneArgumentAndWithDerivedTestMethodAttribute_NoDiagnostic()
    {
        string code = """
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

    [TestMethod]
    public async Task WhenDataRowIsCorrectlyDefinedWithThreeArguments_NoDiagnostic()
    {
        string code = """
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

    [TestMethod]
    public async Task WhenDataRowIsCorrectlyDefinedWithThreeArgumentsAndMethodHasParamsArgument_NoDiagnostic()
    {
        string code = """
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

    [TestMethod]
    public async Task WhenDataRowIsCorrectlyDefinedWithThreeArgumentsAndMethodHasArrayArgument_NoDiagnostic()
    {
        string code = """
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

    [TestMethod]
    public async Task WhenDataRowPassesOneItemAndParameterExpectsArray_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [{|#0:DataRow(1)|}]
                [TestMethod]
                public void TestMethod1(object[] o)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(DataRowShouldBeValidAnalyzer.ArgumentTypeMismatchRule)
                .WithLocation(0)
                .WithArguments("Parameter 'o' expects type 'object[]', but the provided value has type 'int'"));
    }

    [TestMethod]
    public async Task WhenDataRowHasThreeArgumentsAndMethodHasAnIntegerAndAnArrayArgument_Diagnostic()
    {
        string code = """
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

    [TestMethod]
    public async Task WhenDataRowIsCorrectlyDefinedWithOneArgumentAndMethodHasAPrimitiveTypeAndAParamsArgument_NoDiagnostic()
    {
        string code = """
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

    [TestMethod]
    public async Task WhenDataRowIsCorrectlyDefinedWithThreeArgumentsAndMethodHasAPrimitiveTypeAndAParamsArgument_NoDiagnostic()
    {
        string code = """
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

    [TestMethod]
    public async Task WhenDataRowIsCorrectlyDefinedWithOneArgumentAndMethodHasAPrimitiveTypeAndAParamsStringArgument_NoDiagnostic()
    {
        string code = """
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

    [TestMethod]
    public async Task WhenDataRowIsCorrectlyDefinedWithOneArgumentAndMethodHasAPrimitiveTypeAndADefaultArgument_NoDiagnostic()
    {
        string code = """
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

    [TestMethod]
    public async Task WhenDataRowIsCorrectlyDefinedWithOneArgumentAndIntegersAreAssignableToDoubles_NoDiagnostic()
    {
        string code = """
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

    [TestMethod]
    public async Task WhenDataRowIsCorrectlyDefinedWithOneArgumentAndCharsAreAssignableToIntegers_NoDiagnostic()
    {
        string code = """
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

    [TestMethod]
    public async Task WhenDataRowIsCorrectlyDefinedWithOneArgumentAndNullsAreAssignableToIntegers_NoDiagnostic()
    {
        string code = """
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

    [TestMethod]
    public async Task WhenDataRowHasOneNullArgumentAndMethodHasNoArguments_Diagnostic()
    {
        string code = """
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

    [TestMethod]
    public async Task WhenDataRowIsNotSetOnATestMethod_Diagnostic()
    {
        string code = """
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

    [TestMethod]
    public async Task WhenDataRowHasNoArgsButMethodHasOneArgument_Diagnostic()
    {
        string code = """
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

    [TestMethod]
    public async Task WhenDataRowHasArgumentMismatchWithTestMethod_Diagnostic()
    {
        string code = """
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

    [TestMethod]
    public async Task WhenDataRowHasArgumentMismatchWithTestMethod2_Diagnostic()
    {
        string code = """
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

    [TestMethod]
    public async Task WhenDataRowHasArgumentMismatchWithTestMethod3_Diagnostic()
    {
        string code = """
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

    [TestMethod]
    public async Task WhenDataRowHasTypeMismatchWithTestMethod_Diagnostic()
    {
        string code = """
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
                .WithArguments("Parameter 's' expects type 'string', but the provided value has type 'int'"));
    }

    [TestMethod]
    public async Task WhenDataRowHasDecimalDoubleTypeMismatch_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [{|#0:DataRow("Luxury Car", "Alice Johnson", 1500.00, "https://example.com/luxurycar.jpg", "https://example.com/luxurycar")|}]
                [TestMethod]
                public void TestMethod1(string name, string reservedBy, decimal price, string imageUrl, string link)
                {
                }
            }
            """
        ;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(DataRowShouldBeValidAnalyzer.ArgumentTypeMismatchRule)
                .WithLocation(0)
                .WithArguments("Parameter 'price' expects type 'decimal', but the provided value has type 'double'"));
    }

    [TestMethod]
    public async Task WhenDataRowHasMultipleTypeMismatches_SingleDiagnosticWithAllMismatches()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [{|#0:DataRow(1, 2, 3)|}]
                [TestMethod]
                public void TestMethod1(string s, decimal d, bool b)
                {
                }
            }
            """
        ;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(DataRowShouldBeValidAnalyzer.ArgumentTypeMismatchRule)
                .WithLocation(0)
                .WithArguments("Parameter 's' expects type 'string', but the provided value has type 'int'; Parameter 'b' expects type 'bool', but the provided value has type 'int'"));
    }

    [TestMethod]
    public async Task DefaultArguments()
    {
        string code = """
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

    [TestMethod]
    public async Task Testfx_2606_NullArgumentForArray()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            
            [TestClass]
            public class MyTestClass
            {
                [DataTestMethod]
                [DataRow(
                    "123",
                    new string[] { "something" },
                    null)]
                [DataRow(
                    "123",
                    null,
                    new string[] { "something" })]
                public void TestSomething(
                    string x,
                    string[] y,
                    string[] z)
                {
                    Assert.AreEqual("123", x);
                    Assert.IsNotNull(y);
                    Assert.IsNull(z);
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task Issue2856_ArraysInDataRow_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [DataRow(new int[] { })]
                [DataRow(new int[] { 11 })]
                [DataRow(new int[] { 11, 1337, 12 })]
                public void ItemsTest(int[] input)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenMethodIsGeneric()
    {
        string code = """
            using System;
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class TestClass
            {
                [TestMethod]
                [DataRow(0)] // This is an unfortunate false negative that will blow up at runtime.
                public void AMethodWithBadConstraints<T>(T p) where T : IDisposable
                    => Assert.Fail($"Test method 'AMethodWithBadConstraints' did run with T type being '{typeof(T)}'.");

                [TestMethod]
                [DataRow((byte)1)]
                [DataRow((int)2)]
                [DataRow("Hello world")]
                [{|#0:DataRow(null)|}]
                public void ParameterizedMethodSimple<T>(T parameter)
                    => Assert.Fail($"Test method 'ParameterizedMethodSimple' did run with parameter '{parameter?.ToString() ?? "<null>"}' and type '{typeof(T)}'.");

                [TestMethod]
                [{|#1:DataRow((byte)1, "Hello world", (int)2, 3)|}]
                [DataRow(null, "Hello world", "Hello again", 3)]
                [{|#2:DataRow("Hello hello", "Hello world", null, null)|}]
                [{|#3:{|#4:DataRow(null, null, null, null)|}|}]
                public void ParameterizedMethodTwoGenericParametersAndFourMethodParameters<T1, T2>(T2 p1, string p2, T2 p3, T1 p4)
                    => Assert.Fail($"Test method 'ParameterizedMethodTwoGenericParametersAndFourMethodParameters' did run with parameters '{p1?.ToString() ?? "<null>"}', '{p2 ?? "<null>"}', '{p3?.ToString() ?? "<null>"}', '{p4?.ToString() ?? "<null>"}' and generic types '{typeof(T1)}', '{typeof(T2)}'.");

                [TestMethod]
                [{|#5:DataRow((byte)1)|}]
                [{|#6:DataRow((byte)1, 2)|}]
                [{|#7:DataRow("Hello world")|}]
                [{|#8:DataRow(null)|}]
                [{|#9:DataRow(null, "Hello world")|}]
                public void ParameterizedMethodSimpleParams<T>(params T[] parameter)
                    => Assert.Fail($"Test method 'ParameterizedMethodSimple' did run with parameter '{string.Join(",", parameter)}' and type '{typeof(T)}'.");
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            // /0/Test0.cs(17,6): warning MSTEST0014: The type of the generic parameter 'T' could not be inferred.
            VerifyCS.Diagnostic(DataRowShouldBeValidAnalyzer.GenericTypeArgumentNotResolvedRule).WithLocation(0).WithArguments("T"),
            // /0/Test0.cs(22,6): warning MSTEST0014: Found two conflicting types for generic parameter 'T2'. The conflicting types are 'Byte' and 'Int32'.
            VerifyCS.Diagnostic(DataRowShouldBeValidAnalyzer.GenericTypeArgumentConflictingTypesRule).WithLocation(1).WithArguments("T2", "Byte", "Int32"),
            // /0/Test0.cs(24,6): warning MSTEST0014: The type of the generic parameter 'T1' could not be inferred.
            VerifyCS.Diagnostic(DataRowShouldBeValidAnalyzer.GenericTypeArgumentNotResolvedRule).WithLocation(2).WithArguments("T1"),
            // /0/Test0.cs(25,6): warning MSTEST0014: The type of the generic parameter 'T1' could not be inferred.
            VerifyCS.Diagnostic(DataRowShouldBeValidAnalyzer.GenericTypeArgumentNotResolvedRule).WithLocation(3).WithArguments("T1"),
            // /0/Test0.cs(25,6): warning MSTEST0014: The type of the generic parameter 'T2' could not be inferred.
            VerifyCS.Diagnostic(DataRowShouldBeValidAnalyzer.GenericTypeArgumentNotResolvedRule).WithLocation(4).WithArguments("T2"),
            // /0/Test0.cs(30,6): warning MSTEST0014: The type of the generic parameter 'T' could not be inferred.
            VerifyCS.Diagnostic(DataRowShouldBeValidAnalyzer.GenericTypeArgumentNotResolvedRule).WithLocation(5).WithArguments("T"),
            // /0/Test0.cs(31,6): warning MSTEST0014: The type of the generic parameter 'T' could not be inferred.
            VerifyCS.Diagnostic(DataRowShouldBeValidAnalyzer.GenericTypeArgumentNotResolvedRule).WithLocation(6).WithArguments("T"),
            // /0/Test0.cs(32,6): warning MSTEST0014: The type of the generic parameter 'T' could not be inferred.
            VerifyCS.Diagnostic(DataRowShouldBeValidAnalyzer.GenericTypeArgumentNotResolvedRule).WithLocation(7).WithArguments("T"),
            // /0/Test0.cs(33,6): warning MSTEST0014: The type of the generic parameter 'T' could not be inferred.
            VerifyCS.Diagnostic(DataRowShouldBeValidAnalyzer.GenericTypeArgumentNotResolvedRule).WithLocation(8).WithArguments("T"),
            // /0/Test0.cs(34,6): warning MSTEST0014: The type of the generic parameter 'T' could not be inferred.
            VerifyCS.Diagnostic(DataRowShouldBeValidAnalyzer.GenericTypeArgumentNotResolvedRule).WithLocation(9).WithArguments("T"));
    }

    [TestMethod]
    public async Task WhenMethodIsGenericWithEnumArgument()
    {
        string code = """
            using System;
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class TestClass
            {
                [TestMethod]
                [DataRow(ConsoleColor.Red)]
                public void TestMethod<T>(T t)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenMethodIsNestedGeneric()
    {
        string code = """
            using System;
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class TestClass
            {
                [TestMethod]
                [DataRow(new int[] { 0, 1 })]
                public void TestMethodWithGenericIntArray<T>(T[] p)
                {
                }

                [TestMethod]
                [DataRow(new int[] { })]
                public void TestMethodWithGenericIntArrayEmpty<T>(T[] p)
                {
                }

                [TestMethod]
                [DataRow(new ConsoleColor[] { ConsoleColor.Green, ConsoleColor.Red })]
                public void TestMethodWithGenericEnumArray<T>(T[] p)
                {
                }

                [TestMethod]
                [DataRow(new ConsoleColor[] { })]
                public void TestMethodWithGenericEnumArrayEmpty<T>(T[] p)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }
}
