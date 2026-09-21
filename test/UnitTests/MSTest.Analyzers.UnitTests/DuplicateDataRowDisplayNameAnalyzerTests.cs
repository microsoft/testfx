// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.DuplicateDataRowDisplayNameAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = MSTest.Analyzers.Test.VisualBasicCodeFixVerifier<
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
    public async Task WhenVisualBasicDataRowsHaveDuplicateDisplayNames_Diagnostic()
    {
        string code = """
            Imports Microsoft.VisualStudio.TestTools.UnitTesting

            <TestClass>
            Public Class MyTestClass
                <TestMethod>
                <DataRow(1, DisplayName:="Duplicate")>
                <{|#0:DataRow(2, DisplayName:="Duplicate")|}>
                Public Sub TestMethod(value As Integer)
                End Sub
            End Class
            """;

        await VerifyVB.VerifyAnalyzerAsync(
            code,
            VerifyVB.Diagnostic()
                .WithLocation(0)
                .WithArguments("Duplicate"));
    }
}
