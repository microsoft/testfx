// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.PreferConstantForResourceLockAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = MSTest.Analyzers.Test.VisualBasicCodeFixVerifier<
    MSTest.Analyzers.PreferConstantForResourceLockAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class PreferConstantForResourceLockAnalyzerTests
{
    [TestMethod]
    public async Task WhenResourceKeyIsBareLiteralOnMethod_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [ResourceLock([|"my-resource"|])]
                [TestMethod]
                public void MyTestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenResourceKeyIsBareLiteralOnClass_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [ResourceLock([|"my-resource"|])]
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
    public async Task WhenResourceKeyIsWellKnownResourcesConstant_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [ResourceLock(WellKnownResources.EnvironmentVariables)]
                [TestMethod]
                public void MyTestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenResourceKeyIsUserConstant_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public static class MyResources
            {
                public const string Database = "database";
            }

            [TestClass]
            public class MyTestClass
            {
                [ResourceLock(MyResources.Database)]
                [TestMethod]
                public void MyTestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenResourceKeyIsNameofExpression_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [ResourceLock(nameof(MyTestClass))]
                [TestMethod]
                public void MyTestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenResourceKeyIsLiteralConcatenation_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [ResourceLock([|"my"|] + "-resource")]
                [TestMethod]
                public void MyTestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenLiteralIsUsedWithMode_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [ResourceLock([|"my-resource"|], Mode = ResourceAccessMode.Read)]
                [TestMethod]
                public void MyTestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenMultipleLocksMixLiteralAndConstant_DiagnosticOnlyOnLiteral()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [ResourceLock(WellKnownResources.Console)]
                [ResourceLock([|"my-resource"|])]
                [TestMethod]
                public void MyTestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    // The analyzer declares LanguageNames.VisualBasic and deliberately inspects syntax *token text* rather
    // than C#-specific syntax nodes, so it is language-neutral by construction. These tests pin that: without
    // them the VB path is advertised but never executed, and a future change to C#-specific nodes would
    // silently drop VB support rather than failing here.
    [TestMethod]
    public async Task WhenResourceKeyIsBareLiteralInVisualBasic_Diagnostic()
    {
        string code = """
            Imports Microsoft.VisualStudio.TestTools.UnitTesting

            <TestClass>
            Public Class MyTestClass
                <ResourceLock([|"my-resource"|])>
                <TestMethod>
                Public Sub MyTestMethod()
                End Sub
            End Class
            """;

        await VerifyVB.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenResourceKeyIsConstantInVisualBasic_NoDiagnostic()
    {
        string code = """
            Imports Microsoft.VisualStudio.TestTools.UnitTesting

            <TestClass>
            Public Class MyTestClass
                Private Const MyResource As String = "my-resource"

                <ResourceLock(MyResource)>
                <TestMethod>
                Public Sub MyTestMethod()
                End Sub

                <ResourceLock(WellKnownResources.Console)>
                <TestMethod>
                Public Sub MyOtherTestMethod()
                End Sub
            End Class
            """;

        await VerifyVB.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenVisualBasicAttributeListHasMultipleLocks_DiagnosticOnlyOnLiteral()
    {
        // VB allows several attributes inside one angle-bracket list, so both locks share a single attribute
        // list here. The analyzer must still report per attribute application rather than per list.
        string code = """
            Imports Microsoft.VisualStudio.TestTools.UnitTesting

            <TestClass>
            Public Class MyTestClass
                <TestMethod, ResourceLock(WellKnownResources.Console), ResourceLock([|"my-resource"|])>
                Public Sub MyTestMethod()
                End Sub
            End Class
            """;

        await VerifyVB.VerifyAnalyzerAsync(code);
    }
}
