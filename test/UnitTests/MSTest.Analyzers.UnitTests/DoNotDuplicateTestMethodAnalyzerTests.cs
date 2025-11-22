// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.DoNotDuplicateTestMethodAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class DoNotDuplicateTestMethodAnalyzerTests
{
    [TestMethod]
    public async Task WhenTestMethodsHaveDifferentImplementations_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class TestClass1
            {
                [TestMethod]
                public void TestMethod1()
                {
                    int x = 1;
                    Assert.AreEqual(1, x);
                }

                [TestMethod]
                public void TestMethod2()
                {
                    int y = 2;
                    Assert.AreEqual(2, y);
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenTestMethodsHaveIdenticalImplementations_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class TestClass1
            {
                [TestMethod]
                public void TestMethod1()
                {
                    int x = 1;
                    Assert.AreEqual(1, x);
                }

                [TestMethod]
                public void {|#0:TestMethod2|}()
                {
                    int x = 1;
                    Assert.AreEqual(1, x);
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic().WithLocation(0).WithArguments("TestMethod2", "TestMethod1"));
    }

    [TestMethod]
    public async Task WhenTestMethodsHaveVerySimilarImplementations_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class TestClass1
            {
                [TestMethod]
                public void TestAddition()
                {
                    int result = 2 + 2;
                    Assert.AreEqual(4, result);
                }

                [TestMethod]
                public void {|#0:TestAdditionAgain|}()
                {
                    int result = 2 + 2;
                    Assert.AreEqual(4, result);
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic().WithLocation(0).WithArguments("TestAdditionAgain", "TestAddition"));
    }

    [TestMethod]
    public async Task WhenThreeTestMethodsHaveIdenticalImplementations_MultipleDiagnostics()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class TestClass1
            {
                [TestMethod]
                public void TestMethod1()
                {
                    Assert.IsTrue(true);
                }

                [TestMethod]
                public void {|#0:TestMethod2|}()
                {
                    Assert.IsTrue(true);
                }

                [TestMethod]
                public void {|#1:TestMethod3|}()
                {
                    Assert.IsTrue(true);
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic().WithLocation(0).WithArguments("TestMethod2", "TestMethod1"),
            VerifyCS.Diagnostic().WithLocation(1).WithArguments("TestMethod3", "TestMethod1"),
            VerifyCS.Diagnostic().WithLocation(1).WithArguments("TestMethod3", "TestMethod2"));
    }

    [TestMethod]
    public async Task WhenTestMethodsInDifferentClasses_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class TestClass1
            {
                [TestMethod]
                public void TestMethod1()
                {
                    Assert.IsTrue(true);
                }
            }

            [TestClass]
            public class TestClass2
            {
                [TestMethod]
                public void TestMethod2()
                {
                    Assert.IsTrue(true);
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenTestMethodsHaveExpressionBodies_DetectsDuplicates()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class TestClass1
            {
                [TestMethod]
                public void TestMethod1() => Assert.IsTrue(true);

                [TestMethod]
                public void {|#0:TestMethod2|}() => Assert.IsTrue(true);
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic().WithLocation(0).WithArguments("TestMethod2", "TestMethod1"));
    }

    [TestMethod]
    public async Task WhenDataTestMethodsHaveIdenticalBodies_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class TestClass1
            {
                [DataTestMethod]
                [DataRow(1)]
                [DataRow(2)]
                public void TestMethod1(int value)
                {
                    Assert.IsTrue(value > 0);
                }

                [DataTestMethod]
                [DataRow(3)]
                [DataRow(4)]
                public void {|#0:TestMethod2|}(int value)
                {
                    Assert.IsTrue(value > 0);
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic().WithLocation(0).WithArguments("TestMethod2", "TestMethod1"));
    }

    [TestMethod]
    public async Task WhenTestMethodsHaveDifferentComments_StillDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class TestClass1
            {
                [TestMethod]
                public void TestMethod1()
                {
                    // This is test 1
                    Assert.IsTrue(true);
                }

                [TestMethod]
                public void {|#0:TestMethod2|}()
                {
                    // This is test 2
                    Assert.IsTrue(true);
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic().WithLocation(0).WithArguments("TestMethod2", "TestMethod1"));
    }

    [TestMethod]
    public async Task WhenTestMethodsHaveSlightlyDifferentLogic_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class TestClass1
            {
                [TestMethod]
                public void TestMethod1()
                {
                    int x = 1;
                    int y = 2;
                    int z = 3;
                    Assert.AreEqual(6, x + y + z);
                }

                [TestMethod]
                public void TestMethod2()
                {
                    int a = 10;
                    int b = 20;
                    Assert.AreEqual(30, a + b);
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenTestClassInheritsFromBase_DetectsDuplicatesInDerivedClass()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class BaseTestClass
            {
                [TestMethod]
                public void BaseTestMethod()
                {
                    Assert.IsTrue(true);
                }
            }

            [TestClass]
            public class DerivedTestClass : BaseTestClass
            {
                [TestMethod]
                public void TestMethod1()
                {
                    Assert.IsFalse(false);
                }

                [TestMethod]
                public void {|#0:TestMethod2|}()
                {
                    Assert.IsFalse(false);
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic().WithLocation(0).WithArguments("TestMethod2", "TestMethod1"));
    }

    [TestMethod]
    public async Task WhenTestMethodsHaveCustomTestMethodAttribute_DetectsDuplicates()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class CustomTestMethodAttribute : TestMethodAttribute
            {
            }

            [TestClass]
            public class TestClass1
            {
                [CustomTestMethod]
                public void TestMethod1()
                {
                    Assert.IsTrue(true);
                }

                [CustomTestMethod]
                public void {|#0:TestMethod2|}()
                {
                    Assert.IsTrue(true);
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic().WithLocation(0).WithArguments("TestMethod2", "TestMethod1"));
    }

    [TestMethod]
    public async Task WhenTestMethodsHaveComplexIdenticalLogic_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Collections.Generic;
            using System.Linq;

            [TestClass]
            public class TestClass1
            {
                [TestMethod]
                public void TestMethod1()
                {
                    var list = new List<int> { 1, 2, 3, 4, 5 };
                    var sum = list.Sum();
                    Assert.AreEqual(15, sum);
                }

                [TestMethod]
                public void {|#0:TestMethod2|}()
                {
                    var list = new List<int> { 1, 2, 3, 4, 5 };
                    var sum = list.Sum();
                    Assert.AreEqual(15, sum);
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic().WithLocation(0).WithArguments("TestMethod2", "TestMethod1"));
    }

    [TestMethod]
    public async Task WhenOneTestMethodIsSubsetOfAnother_NoDiagnosticIfBelowThreshold()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class TestClass1
            {
                [TestMethod]
                public void TestMethod1()
                {
                    int x = 1;
                    Assert.AreEqual(1, x);
                }

                [TestMethod]
                public void TestMethod2()
                {
                    int x = 1;
                    int y = 2;
                    int z = 3;
                    Assert.AreEqual(1, x);
                    Assert.AreEqual(2, y);
                    Assert.AreEqual(3, z);
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenTestMethodsHaveSameNameButDifferentParameters_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class TestClass1
            {
                [TestMethod]
                public void TestMethod1()
                {
                    Assert.IsTrue(true);
                }

                [DataTestMethod]
                [DataRow(1)]
                public void TestMethod1(int value)
                {
                    Assert.IsTrue(true);
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenPartialClassHasDuplicateTestMethods_Diagnostic()
    {
        string code1 = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public partial class TestClass1
            {
                [TestMethod]
                public void TestMethod1()
                {
                    Assert.IsTrue(true);
                }
            }
            """;

        string code2 = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public partial class TestClass1
            {
                [TestMethod]
                public void {|#0:TestMethod2|}()
                {
                    Assert.IsTrue(true);
                }
            }
            """;

        await new VerifyCS.Test
        {
            TestState =
            {
                Sources = { code1, code2 },
                ExpectedDiagnostics =
                {
                    VerifyCS.Diagnostic().WithLocation(0).WithArguments("TestMethod2", "TestMethod1"),
                },
            },
        }.RunAsync();
    }
}
