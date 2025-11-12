// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.DoNotNegateBooleanAssertionAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class DoNotDuplicateTestMethodAnalyzerTests
{
    [TestMethod]
    public async Task WhenTestClassHasNoDuplicateMethods_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class TestClass1
            {
                [TestMethod]
                public void TestMethod1()
                {
                }

                [TestMethod]
                public void TestMethod2()
                {
                }

                [TestMethod]
                public void TestMethod3()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenTestClassHasDuplicateMethodNames_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class TestClass1
            {
                [TestMethod]
                public void TestMethod1()
                {
                }

                [TestMethod]
                public void [|TestMethod1|]()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenTestClassHasMultipleDuplicateMethodNames_DiagnosticForEach()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class TestClass1
            {
                [TestMethod]
                public void TestMethod1()
                {
                }

                [TestMethod]
                public void [|TestMethod1|]()
                {
                }

                [TestMethod]
                public void [|TestMethod1|]()
                {
                }

                [TestMethod]
                public void TestMethod2()
                {
                }

                [TestMethod]
                public void [|TestMethod2|]()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenTestClassHasDuplicateMethodSignatures_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class TestClass1
            {
                [TestMethod]
                public void TestMethod1()
                {
                }

                [TestMethod]
                public void [|TestMethod1|]()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenTestClassHasMethodsWithSameNameButDifferentParameters_NoDiagnostic()
    {
        // Note: While method overloading is technically allowed in C#,
        // MSTest doesn't support parameterized test methods without DataRow/DynamicData
        // This test documents current behavior - in practice, overloaded test methods
        // should be avoided unless using data-driven testing
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class TestClass1
            {
                [TestMethod]
                public void TestMethod1()
                {
                }

                // This is not a valid test method pattern, but analyzer focuses on exact duplicates
                public void TestMethod1(int parameter)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenTestClassHasDataTestMethod_NoDiagnosticForDifferentDataRows()
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
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenNonTestClassHasDuplicateMethods_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class NotATestClass
            {
                public void Method1()
                {
                }

                public void Method1()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenTestClassHasNonTestMethodsDuplicated_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class TestClass1
            {
                [TestMethod]
                public void TestMethod1()
                {
                }

                // Not a test method, so duplication is a compiler error, not our concern
                private void HelperMethod()
                {
                }

                private void HelperMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenTestMethodsInheritedFromBase_ChecksAcrossHierarchy()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class BaseTestClass
            {
                [TestMethod]
                public void TestMethod1()
                {
                }
            }

            [TestClass]
            public class DerivedTestClass : BaseTestClass
            {
                [TestMethod]
                public void [|TestMethod1|]() // Hides base method
                {
                }
            }
            """;

        // Note: This test documents expected behavior for inherited test methods
        // The analyzer should warn about hiding base test methods
        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenTestClassHasDuplicateMethodsWithDifferentAttributes_StillDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class TestClass1
            {
                [TestMethod]
                [TestCategory("Category1")]
                public void TestMethod1()
                {
                }

                [TestMethod]
                [TestCategory("Category2")]
                public void [|TestMethod1|]()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenTestClassHasDuplicateDataTestMethods_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class TestClass1
            {
                [DataTestMethod]
                [DataRow(1)]
                public void TestMethod1(int value)
                {
                }

                [DataTestMethod]
                [DataRow(2)]
                public void [|TestMethod1|](int value)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenAbstractTestClassHasDuplicateMethods_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public abstract class AbstractTestClass
            {
                [TestMethod]
                public void TestMethod1()
                {
                }

                [TestMethod]
                public void [|TestMethod1|]()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenPartialTestClassHasDuplicateMethodsAcrossParts_Diagnostic()
    {
        string code1 = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public partial class TestClass1
            {
                [TestMethod]
                public void TestMethod1()
                {
                }
            }
            """;

        string code2 = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public partial class TestClass1
            {
                [TestMethod]
                public void [|TestMethod1|]()
                {
                }
            }
            """;

        await new VerifyCS.Test
        {
            TestState =
            {
                Sources = { code1, code2 },
            },
        }.RunAsync();
    }

    [TestMethod]
    public async Task WhenTestClassHasThreeDuplicateMethods_DiagnosticForSecondAndThird()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class TestClass1
            {
                [TestMethod]
                public void TestMethod1()
                {
                }

                [TestMethod]
                public void [|TestMethod1|]()
                {
                }

                [TestMethod]
                public void [|TestMethod1|]()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }
}
