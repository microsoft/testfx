// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.ConflictingDataRowDisplayNamesAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = MSTest.Analyzers.Test.VisualBasicCodeFixVerifier<
    MSTest.Analyzers.ConflictingDataRowDisplayNamesAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class ConflictingDataRowDisplayNamesAnalyzerTests
{
    [TestMethod]
    [DataRow("\"full\"", "\"label\"", true)]
    [DataRow("FullName", "ArgumentName", true)]
    [DataRow("nameof(MyTestClass)", "\"la\" + \"bel\"", true)]
    [DataRow("null", "\"label\"", false)]
    [DataRow("\"\"", "\"label\"", false)]
    [DataRow("\" \\t\"", "\"label\"", false)]
    [DataRow("\"full\"", "null", false)]
    [DataRow("\"full\"", "\"\"", false)]
    [DataRow("\"full\"", "\" \\t\"", false)]
    public async Task Attribute_ReportsOnlyEffectiveConflicts(string fullName, string argumentName, bool reportsDiagnostic)
    {
        string attribute = $"DataRow(1, DisplayName = {fullName}, ArgumentsDisplayName = {argumentName})";
        if (reportsDiagnostic)
        {
            attribute = "{|#0:" + attribute + "|}";
        }

        string code = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                private const string FullName = "full";
                private const string ArgumentName = "label";

                [TestMethod]
                [{{attribute}}]
                public void Test(int value) { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code, reportsDiagnostic ? [VerifyCS.Diagnostic().WithLocation(0)] : []);
    }

    [TestMethod]
    [DataRow("DataRowAttribute", "\"full\"", "\"label\"", true)]
    [DataRow("DataRowAttribute", "null", "\"label\"", false)]
    [DataRow("DataRowAttribute", "\"\"", "\"label\"", false)]
    [DataRow("DataRowAttribute", "\" \\t\"", "\"label\"", false)]
    [DataRow("DataRowAttribute", "\"full\"", "null", false)]
    [DataRow("DataRowAttribute", "\"full\"", "\"\"", false)]
    [DataRow("DataRowAttribute", "\"full\"", "\" \\t\"", false)]
    [DataRow("TestDataRow<int>", "\"full\"", "\"label\"", true)]
    [DataRow("TestDataRow<int>", "\"\"", "\"label\"", true)]
    [DataRow("TestDataRow<int>", "\" \\t\"", "\"label\"", true)]
    [DataRow("TestDataRow<int>", "null", "\"label\"", false)]
    [DataRow("TestDataRow<int>", "\"full\"", "null", false)]
    [DataRow("TestDataRow<int>", "\"full\"", "\"\"", false)]
    [DataRow("TestDataRow<int>", "\"full\"", "\" \\t\"", false)]
    public async Task ObjectInitializer_ReportsOnlyEffectiveConflicts(string typeName, string fullName, string argumentName, bool reportsDiagnostic)
    {
        string creation = $"new {typeName}(1) {{ DisplayName = {fullName}, ArgumentsDisplayName = {argumentName} }}";
        if (reportsDiagnostic)
        {
            creation = "{|#0:" + creation + "|}";
        }

        string code = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class MyClass
            {
                public object Create() => {{creation}};
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code, reportsDiagnostic ? [VerifyCS.Diagnostic().WithLocation(0)] : []);
    }

    [TestMethod]
    public async Task ImplicitCreationAndReversedPropertyOrder_ReportDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class MyClass
            {
                public TestDataRow<int> Create()
                    => {|#0:new(1) { ArgumentsDisplayName = "label", DisplayName = "full" }|};
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code, VerifyCS.Diagnostic().WithLocation(0));
    }

    [TestMethod]
    public async Task MissingPropertiesAndSeparateInstances_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class MyClass
            {
                private object[] rows =
                {
                    new DataRowAttribute(1),
                    new DataRowAttribute(2) { DisplayName = "full" },
                    new DataRowAttribute(3) { ArgumentsDisplayName = "label" },
                    new TestDataRow<int>(1),
                    new TestDataRow<int>(2) { DisplayName = "full" },
                    new TestDataRow<int>(3) { ArgumentsDisplayName = "label" },
                };
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task ValuesNotKnownAtCompileTime_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class MyClass
            {
                public object[] Create(string fullName, string label) => new object[]
                {
                    new DataRowAttribute(1) { DisplayName = fullName, ArgumentsDisplayName = "label" },
                    new DataRowAttribute(2) { DisplayName = "full", ArgumentsDisplayName = label },
                    new TestDataRow<int>(1) { DisplayName = fullName, ArgumentsDisplayName = "label" },
                    new TestDataRow<int>(2) { DisplayName = "full", ArgumentsDisplayName = label },
                };
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task CustomDataRowAttributeAndUnrelatedTypes_NoDiagnostic()
    {
        string code = """
            using System.Reflection;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class CustomDataRowAttribute : DataRowAttribute
            {
                public override string GetDisplayName(MethodInfo method, object[] data)
                    => DisplayName + ArgumentsDisplayName;
            }
            public class OtherRow
            {
                public string DisplayName { get; set; }
                public string ArgumentsDisplayName { get; set; }
            }
            public class MyClass
            {
                [CustomDataRow(DisplayName = "full", ArgumentsDisplayName = "label")]
                public void Test() { }
                public object[] Create() => new object[]
                {
                    new CustomDataRowAttribute { DisplayName = "full", ArgumentsDisplayName = "label" },
                    new OtherRow { DisplayName = "full", ArgumentsDisplayName = "label" },
                };
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task GeneratedCode_NoDiagnostic()
    {
        var test = new VerifyCS.Test();
        test.TestState.Sources.Add(("Test.g.cs", """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class MyClass
            {
                public object Create() => new TestDataRow<int>(1) { DisplayName = "full", ArgumentsDisplayName = "label" };
            }
            """));

        await test.RunAsync(TestContext.CancellationToken);
    }

    [TestMethod]
    public async Task OlderFrameworkWithoutArgumentLabels_NoDiagnostic()
    {
        var test = new VerifyCS.Test
        {
            TestCode = """
                namespace Microsoft.VisualStudio.TestTools.UnitTesting
                {
                    public sealed class DataRowAttribute : System.Attribute
                    {
                        public string DisplayName { get; set; }
                    }
                }
                public class MyClass
                {
                    [Microsoft.VisualStudio.TestTools.UnitTesting.DataRow(DisplayName = "full")]
                    public void Test() { }
                }
                """,
        };
        test.TestState.AdditionalReferences.Clear();

        await test.RunAsync(TestContext.CancellationToken);
    }

    [TestMethod]
    public async Task VisualBasicAttributeAndObjectInitializer_ReportDiagnostic()
    {
        string code = """
            Imports Microsoft.VisualStudio.TestTools.UnitTesting
            <TestClass>
            Public Class MyTestClass
                <TestMethod>
                <{|#0:DataRow(1, DisplayName:="full", ArgumentsDisplayName:="label")|}>
                Public Sub Test(value As Integer)
                End Sub

                Public Function Create() As Object
                    Return {|#1:New TestDataRow(Of Integer)(1) With {.ArgumentsDisplayName = "label", .DisplayName = ""}|}
                End Function
            End Class
            """;

        await VerifyVB.VerifyAnalyzerAsync(code, VerifyVB.Diagnostic().WithLocation(0), VerifyVB.Diagnostic().WithLocation(1));
    }

    [TestMethod]
    public async Task VisualBasicUnsetAndUnknownNames_NoDiagnostic()
    {
        string code = """
            Imports Microsoft.VisualStudio.TestTools.UnitTesting
            Public Class SampleClass
                <DataRow(1, DisplayName:="", ArgumentsDisplayName:="label")>
                Public Sub Test(value As Integer)
                End Sub

                Public Function Create(fullName As String) As Object()
                    Return {
                        New TestDataRow(Of Integer)(1) With {.DisplayName = Nothing, .ArgumentsDisplayName = "label"},
                        New TestDataRow(Of Integer)(2) With {.DisplayName = "full", .ArgumentsDisplayName = ""},
                        New TestDataRow(Of Integer)(3) With {.DisplayName = fullName, .ArgumentsDisplayName = "label"}
                    }
                End Function
            End Class
            """;

        await VerifyVB.VerifyAnalyzerAsync(code);
    }

    public TestContext TestContext { get; set; } = default!;
}
