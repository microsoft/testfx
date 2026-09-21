// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.DuplicateDataRowDisplayNameAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class DuplicateDataRowDisplayNameAnalyzerTests
{
    [TestMethod]
    public async Task WhenDataRowsHaveNoDisplayName_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [DataRow(1)]
                [DataRow(2)]
                public void TestMethod(int value)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenDataRowsHaveDifferentDisplayNames_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [DataRow(1, DisplayName = "First")]
                [DataRow(2, DisplayName = "Second")]
                public void TestMethod(int value)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenDataRowsHaveDisplayNamesThatDifferByCase_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [DataRow(1, DisplayName = "Name")]
                [DataRow(2, DisplayName = "name")]
                public void TestMethod(int value)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenDataRowsHaveDuplicateNullEmptyOrWhitespaceDisplayNames_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [DataRow(1, DisplayName = null)]
                [DataRow(2, DisplayName = null)]
                [DataRow(3, DisplayName = "")]
                [DataRow(4, DisplayName = "")]
                [DataRow(5, DisplayName = " ")]
                [DataRow(6, DisplayName = " ")]
                [DataRow(7, DisplayName = "\r\n")]
                [DataRow(8, DisplayName = "\r\n")]
                public void TestMethod(int value)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenDataRowsHaveDuplicateDisplayNames_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [DataRow(1, DisplayName = "Duplicate")]
                [{|#0:DataRow(2, DisplayName = "Duplicate")|}]
                public void TestMethod(int value)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic()
                .WithLocation(0)
                .WithArguments("Duplicate"));
    }

    [TestMethod]
    public async Task WhenThreeDataRowsHaveSameDisplayName_DiagnosticOnEachDuplicate()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [DataRow(1, DisplayName = "Duplicate")]
                [{|#0:DataRow(2, DisplayName = "Duplicate")|}]
                [{|#1:DataRow(3, DisplayName = "Duplicate")|}]
                public void TestMethod(int value)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic()
                .WithLocation(0)
                .WithArguments("Duplicate"),
            VerifyCS.Diagnostic()
                .WithLocation(1)
                .WithArguments("Duplicate"));
    }

    [TestMethod]
    public async Task WhenEquivalentConstantDisplayNameExpressionsAreUsed_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                private const string Duplicate = "Duplicate";

                [TestMethod]
                [DataRow(1, DisplayName = "Duplicate")]
                [{|#0:DataRow(2, DisplayName = @"Duplicate")|}]
                [{|#1:DataRow(3, DisplayName = "\u0044uplicate")|}]
                [{|#2:DataRow(4, DisplayName = "Dupli" + "cate")|}]
                [{|#3:DataRow(5, DisplayName = Duplicate)|}]
                [{|#4:DataRow(6, DisplayName = nameof(Duplicate))|}]
                public void TestMethod(int value)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic().WithLocation(0).WithArguments("Duplicate"),
            VerifyCS.Diagnostic().WithLocation(1).WithArguments("Duplicate"),
            VerifyCS.Diagnostic().WithLocation(2).WithArguments("Duplicate"),
            VerifyCS.Diagnostic().WithLocation(3).WithArguments("Duplicate"),
            VerifyCS.Diagnostic().WithLocation(4).WithArguments("Duplicate"));
    }

    [TestMethod]
    public async Task WhenDuplicateDisplayNamesAreInterleaved_DiagnosticOnLaterOccurrences()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [DataRow(1, DisplayName = "First")]
                [DataRow(2, DisplayName = "Second")]
                [{|#0:DataRow(3, DisplayName = "First")|}]
                [{|#1:DataRow(4, DisplayName = "Second")|}]
                public void TestMethod(int value)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic().WithLocation(0).WithArguments("First"),
            VerifyCS.Diagnostic().WithLocation(1).WithArguments("Second"));
    }

    [TestMethod]
    public async Task WhenDuplicateDisplayNamesContainControlCharacters_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [DataRow(1, DisplayName = "Line1\nLine2\0")]
                [{|#0:DataRow(2, DisplayName = "Line1\u000ALine2\u0000")|}]
                public void TestMethod(int value)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic()
                .WithLocation(0)
                .WithArguments("Line1\nLine2\0"));
    }

    [TestMethod]
    public async Task WhenDerivedTestMethodAttributeHasDuplicateDisplayNames_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public sealed class CustomTestMethodAttribute : TestMethodAttribute
            {
            }

            [TestClass]
            public class MyTestClass
            {
                [CustomTestMethod]
                [DataRow(1, DisplayName = "Duplicate")]
                [{|#0:DataRow(2, DisplayName = "Duplicate")|}]
                public void TestMethod(int value)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic()
                .WithLocation(0)
                .WithArguments("Duplicate"));
    }

    [TestMethod]
    public async Task WhenDifferentMethodsUseSameDisplayName_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [DataRow(1, DisplayName = "Shared")]
                public void FirstTestMethod(int value)
                {
                }

                [TestMethod]
                [DataRow(2, DisplayName = "Shared")]
                public void SecondTestMethod(int value)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenNonTestMethodHasDuplicateDisplayNames_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class MyTestClass
            {
                [DataRow(1, DisplayName = "Duplicate")]
                [DataRow(2, DisplayName = "Duplicate")]
                public void HelperMethod(int value)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }
}
