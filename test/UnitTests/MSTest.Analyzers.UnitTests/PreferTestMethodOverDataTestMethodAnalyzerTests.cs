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

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenUsingDataTestMethod_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [[|DataTestMethod|]]
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

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenUsingDataTestMethodWithDisplayName_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [[|DataTestMethod("Display Name")|]]
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
                [TestMethod("Display Name")]
                public void MyTestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenUsingDataTestMethodWithMultipleAttributesInSameList_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [Ignore, [|DataTestMethod|]]
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
                [Ignore, TestMethod]
                public void MyTestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenInheritingDataTestMethod_Diagnostic()
    {
        // TODO: Codefix doesn't handle this yet. So no codefix is offered.
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            internal sealed class [|MyDataTestMethodAttribute|] : DataTestMethodAttribute
            {
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenUsingDataTestMethodWithMultiLineBody_PreservesIndentation()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [[|DataTestMethod|]]
                public void MyTestMethod()
                {
                    var result = SomeMethod(
                        1,
                        2,
                        3);
                }

                private int SomeMethod(int a, int b, int c) => a + b + c;
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
                    var result = SomeMethod(
                        1,
                        2,
                        3);
                }

                private int SomeMethod(int a, int b, int c) => a + b + c;
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenDataTestMethodUsedWithDataRowAttribute_FixerPreservesDataRow()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [[|DataTestMethod|]]
                [DataRow(1)]
                [DataRow(2)]
                public void MyTestMethod(int value)
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
                [DataRow(1)]
                [DataRow(2)]
                public void MyTestMethod(int value)
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenDataTestMethodUsedOutsideTestClass_Diagnostic()
    {
        // The analyzer fires on DataTestMethod regardless of whether the containing
        // class has [TestClass]. DataTestMethod should always be replaced with TestMethod.
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class NonTestClass
            {
                [[|DataTestMethod|]]
                public void MyMethod()
                {
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class NonTestClass
            {
                [TestMethod]
                public void MyMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenClassInheritsTwoLevelsFromDataTestMethod_OnlyDirectInheritanceIsFlagged()
    {
        // The analyzer only flags *direct* inheritance from DataTestMethodAttribute.
        // A class that inherits from a class that already extends DataTestMethodAttribute
        // is NOT flagged — only the direct child is.
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            internal class [|MyDataTestMethodAttribute|] : DataTestMethodAttribute
            {
            }

            internal sealed class MyOtherDataTestMethodAttribute : MyDataTestMethodAttribute
            {
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }
}
