// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.UseCIConditionAttributeInsteadOfEnvironmentCheckAnalyzer,
    MSTest.Analyzers.UseCIConditionAttributeInsteadOfEnvironmentCheckFixer>;
using VerifyVB = MSTest.Analyzers.Test.VisualBasicCodeFixVerifier<
    MSTest.Analyzers.UseCIConditionAttributeInsteadOfEnvironmentCheckAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class UseCIConditionAttributeInsteadOfEnvironmentCheckAnalyzerTests
{
    [TestMethod]
    public async Task WhenNoEnvironmentCheckUsed_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    // No Environment.GetEnvironmentVariable check
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenVariableIsComparedToNullWithEarlyReturn_IncludeMode()
    {
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    [|if (Environment.GetEnvironmentVariable("CI") == null)
                    {
                        return;
                    }|]
                }
            }
            """;

        string fixedCode = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [CICondition(ConditionMode.Include)]
                public void TestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenVariableIsComparedToNotNullWithEarlyReturn_ExcludeMode()
    {
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    [|if (Environment.GetEnvironmentVariable("CI") != null)
                    {
                        return;
                    }|]
                }
            }
            """;

        string fixedCode = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [CICondition(ConditionMode.Exclude)]
                public void TestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenVariableIsNullPattern_IncludeMode()
    {
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    [|if (Environment.GetEnvironmentVariable("CI") is null)
                    {
                        return;
                    }|]
                }
            }
            """;

        string fixedCode = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [CICondition(ConditionMode.Include)]
                public void TestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenVariableIsNotNullPatternWithAssertInconclusive_ExcludeMode()
    {
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    [|if (Environment.GetEnvironmentVariable("CI") is not null)
                    {
                        Assert.Inconclusive("Not supported in CI");
                    }|]
                }
            }
            """;

        string fixedCode = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [CICondition(ConditionMode.Exclude)]
                public void TestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenNullIsOnTheLeftOfTheComparison_IncludeMode()
    {
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    [|if (null == Environment.GetEnvironmentVariable("CI"))
                        return;|]
                }
            }
            """;

        string fixedCode = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [CICondition(ConditionMode.Include)]
                public void TestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenTestMethodHasOtherStatements_TheyArePreserved()
    {
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    [|if (Environment.GetEnvironmentVariable("CI") == null)
                    {
                        return;
                    }|]

                    Assert.IsTrue(true);
                }
            }
            """;

        string fixedCode = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [CICondition(ConditionMode.Include)]
                public void TestMethod()
                {
                    Assert.IsTrue(true);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenTestMethodAttributeIsFullyQualified_FixIsFullyQualified()
    {
        string code = """
            using System;

            [Microsoft.VisualStudio.TestTools.UnitTesting.TestClass]
            public class MyTestClass
            {
                [Microsoft.VisualStudio.TestTools.UnitTesting.TestMethod]
                public void TestMethod()
                {
                    [|if (Environment.GetEnvironmentVariable("CI") == null)
                    {
                        return;
                    }|]
                }
            }
            """;

        string fixedCode = """
            using System;

            [Microsoft.VisualStudio.TestTools.UnitTesting.TestClass]
            public class MyTestClass
            {
                [Microsoft.VisualStudio.TestTools.UnitTesting.TestMethod]
                [Microsoft.VisualStudio.TestTools.UnitTesting.CICondition(Microsoft.VisualStudio.TestTools.UnitTesting.ConditionMode.Include)]
                public void TestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenNextStatementHasALeadingComment_TheCommentIsPreserved()
    {
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    [|if (Environment.GetEnvironmentVariable("CI") == null)
                    {
                        return;
                    }|]

                    // This comment must not swallow the assertion below.
                    Assert.IsTrue(true);
                }
            }
            """;

        string fixedCode = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [CICondition(ConditionMode.Include)]
                public void TestMethod()
                {
                    // This comment must not swallow the assertion below.
                    Assert.IsTrue(true);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenGuardIsWrappedInPreprocessorDirectives_NoDiagnostic()
    {
        // Removing the guard would delete its leading '#if' and orphan the '#endif', and the generated attribute
        // would apply to every build configuration rather than the conditional one. The check lives in the shared
        // 'ConditionGuardHelper', so this also covers MSTEST0079. It can't be asserted from the MSTEST0079 test class:
        // that class is '#if NET'-gated, and directives inside a raw string literal in a disabled region are still
        // lexed as directives and get normalized to column 0, which breaks the literal.
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    #if !SKIP_CI_GUARD
                    if (Environment.GetEnvironmentVariable("CI") == null)
                    {
                        return;
                    }
                    #endif

                    Assert.IsTrue(true);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenVariableIsAProviderSpecificFlag_NoDiagnostic()
    {
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    if (Environment.GetEnvironmentVariable("TF_BUILD") == null)
                    {
                        return;
                    }
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenVariableIsNotACIVariable_NoDiagnostic()
    {
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    if (Environment.GetEnvironmentVariable("MY_CUSTOM_VARIABLE") == null)
                    {
                        return;
                    }
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenVariableNameIsNotAConstant_NoDiagnostic()
    {
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod(string name)
                {
                    if (Environment.GetEnvironmentVariable(name) == null)
                    {
                        return;
                    }
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenCheckIsNotANullCheck_NoDiagnostic()
    {
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    if (Environment.GetEnvironmentVariable("CI") == "true")
                    {
                        return;
                    }
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenCheckIsNotTheFirstStatement_NoDiagnostic()
    {
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.IsTrue(true);

                    if (Environment.GetEnvironmentVariable("CI") == null)
                    {
                        return;
                    }
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenCheckHasElseBranch_NoDiagnostic()
    {
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    if (Environment.GetEnvironmentVariable("CI") == null)
                    {
                        return;
                    }
                    else
                    {
                        Assert.IsTrue(true);
                    }
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenCheckBodyIsNotAGuard_NoDiagnostic()
    {
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    if (Environment.GetEnvironmentVariable("CI") == null)
                    {
                        Assert.IsTrue(true);
                    }
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenMethodHasACustomConditionAttribute_NoDiagnostic()
    {
        // A custom condition's 'GroupName' is an arbitrary property implementation. If it happens to return
        // "CIConditionAttribute", adding '[CICondition]' would OR the two instead of ANDing them.
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public sealed class MyConditionAttribute : ConditionBaseAttribute
            {
                public MyConditionAttribute()
                    : base(ConditionMode.Include)
                {
                }

                public override string GroupName => "CIConditionAttribute";

                public override bool IsConditionMet => true;
            }

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [MyCondition]
                public void TestMethod()
                {
                    if (Environment.GetEnvironmentVariable("CI") == null)
                    {
                        return;
                    }
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenMethodAlreadyHasCICondition_NoDiagnostic()
    {
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [CICondition(ConditionMode.Include)]
                public void TestMethod()
                {
                    if (Environment.GetEnvironmentVariable("CI") == null)
                    {
                        return;
                    }
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenVariableIsNothing_VisualBasic_Diagnostic()
    {
        string code = """
            Imports System
            Imports Microsoft.VisualStudio.TestTools.UnitTesting

            <TestClass>
            Public Class MyTestClass
                <TestMethod>
                Public Sub TestMethod()
                    [|If Environment.GetEnvironmentVariable("CI") Is Nothing Then
                        Return
                    End If|]
                End Sub
            End Class
            """;

        await VerifyVB.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenVariableIsNotNothing_VisualBasic_Diagnostic()
    {
        string code = """
            Imports System
            Imports Microsoft.VisualStudio.TestTools.UnitTesting

            <TestClass>
            Public Class MyTestClass
                <TestMethod>
                Public Sub TestMethod()
                    [|If Environment.GetEnvironmentVariable("CI") IsNot Nothing Then
                        Return
                    End If|]
                End Sub
            End Class
            """;

        await VerifyVB.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenVariableIsNotACIVariable_VisualBasic_NoDiagnostic()
    {
        string code = """
            Imports System
            Imports Microsoft.VisualStudio.TestTools.UnitTesting

            <TestClass>
            Public Class MyTestClass
                <TestMethod>
                Public Sub TestMethod()
                    If Environment.GetEnvironmentVariable("MY_CUSTOM_VARIABLE") Is Nothing Then
                        Return
                    End If
                End Sub
            End Class
            """;

        await VerifyVB.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenCheckIsInNonTestMethod_NoDiagnostic()
    {
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                public void Helper()
                {
                    if (Environment.GetEnvironmentVariable("CI") == null)
                    {
                        return;
                    }
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }
}
