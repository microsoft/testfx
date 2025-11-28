// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.UseOSConditionAttributeInsteadOfRuntimeCheckAnalyzer,
    MSTest.Analyzers.UseOSConditionAttributeInsteadOfRuntimeCheckFixer>;

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class UseOSConditionAttributeInsteadOfRuntimeCheckAnalyzerTests
{
    [TestMethod]
    public async Task WhenNoRuntimeCheckUsed_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    // No RuntimeInformation.IsOSPlatform check
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenRuntimeCheckWithEarlyReturn_NotNegated_Diagnostic()
    {
        string code = """
            using System.Runtime.InteropServices;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    [|if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        return;
                    }|]
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenRuntimeCheckWithEarlyReturn_Negated_Diagnostic()
    {
        string code = """
            using System.Runtime.InteropServices;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    [|if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        return;
                    }|]
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenRuntimeCheckWithAssertInconclusive_Diagnostic()
    {
        string code = """
            using System.Runtime.InteropServices;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    [|if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    {
                        Assert.Inconclusive("This test only runs on Linux");
                    }|]
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenRuntimeCheckOnLinux_Diagnostic()
    {
        string code = """
            using System.Runtime.InteropServices;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    [|if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    {
                        return;
                    }|]
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenRuntimeCheckOnOSX_Diagnostic()
    {
        string code = """
            using System.Runtime.InteropServices;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    [|if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    {
                        return;
                    }|]
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenNotInTestMethod_NoDiagnostic()
    {
        string code = """
            using System.Runtime.InteropServices;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                public void HelperMethod()
                {
                    if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        return;
                    }
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenRuntimeCheckWithOtherStatements_NoDiagnostic()
    {
        string code = """
            using System.Runtime.InteropServices;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        // Some other logic
                        var x = 5;
                    }
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenRuntimeCheckWithElseBranch_NoDiagnostic()
    {
        string code = """
            using System.Runtime.InteropServices;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        return;
                    }
                    else
                    {
                        // Do something
                    }
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task CodeFix_WhenNegatedRuntimeCheck_AddsIncludeAttribute()
    {
        string code = """
            using System.Runtime.InteropServices;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    [|if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        return;
                    }|]
                }
            }
            """;

        string fixedCode = """
            using System.Runtime.InteropServices;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [OSCondition(ConditionMode.Include, OperatingSystems.Windows)]
                [TestMethod]
                public void TestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task CodeFix_WhenNotNegatedRuntimeCheck_AddsExcludeAttribute()
    {
        string code = """
            using System.Runtime.InteropServices;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    [|if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        return;
                    }|]
                }
            }
            """;

        string fixedCode = """
            using System.Runtime.InteropServices;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
                [TestMethod]
                public void TestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task CodeFix_WithAssertInconclusive_AddsAttribute()
    {
        string code = """
            using System.Runtime.InteropServices;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    [|if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    {
                        Assert.Inconclusive("Linux only");
                    }|]
                }
            }
            """;

        string fixedCode = """
            using System.Runtime.InteropServices;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [OSCondition(ConditionMode.Include, OperatingSystems.Linux)]
                [TestMethod]
                public void TestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenRuntimeCheckWithSingleStatementReturn_Diagnostic()
    {
        string code = """
            using System.Runtime.InteropServices;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    [|if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                        return;|]
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }
}
