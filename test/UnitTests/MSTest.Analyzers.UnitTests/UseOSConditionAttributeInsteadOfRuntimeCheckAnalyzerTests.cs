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

        await VerifyCS.VerifyCodeFixAsync(code, code);
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

        string fixedCode = """
            using System.Runtime.InteropServices;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
                public void TestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
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

        string fixedCode = """
            using System.Runtime.InteropServices;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [OSCondition(OperatingSystems.Windows)]
                public void TestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
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

        string fixedCode = """
            using System.Runtime.InteropServices;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [OSCondition(OperatingSystems.Linux)]
                public void TestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
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

        string fixedCode = """
            using System.Runtime.InteropServices;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [OSCondition(OperatingSystems.OSX)]
                public void TestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
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

        await VerifyCS.VerifyCodeFixAsync(code, code);
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

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenRuntimeCheckWithElseBranch_NoDiagnostic()
    {
        string code = """
            using System;
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
                        Console.WriteLine("Running on Windows");
                    }
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
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

        string fixedCode = """
            using System.Runtime.InteropServices;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [OSCondition(OperatingSystems.Windows)]
                public void TestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenRuntimeCheckWithLeadingComment_Diagnostic()
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
                    // Skip on Windows
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
                [TestMethod]
                [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
                public void TestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenRuntimeCheckWithTrailingComment_Diagnostic()
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
                    }|] // This test only runs on Linux
                }
            }
            """;

        string fixedCode = """
            using System.Runtime.InteropServices;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [OSCondition(OperatingSystems.Linux)]
                public void TestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenRuntimeCheckWithCommentInsideBlock_Diagnostic()
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
                        // Early exit for Windows
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
                [TestMethod]
                [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
                public void TestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenRuntimeCheckNotAtBeginningOfMethod_NoDiagnostic()
    {
        string code = """
            using System;
            using System.Runtime.InteropServices;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    // arrange
                    Console.WriteLine("Setting up");

                    // some assertions
                    Assert.IsTrue(true);

                    // if windows, return - this should NOT be flagged
                    if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        return;
                    }

                    // some other assertions that are Windows only.
                    Console.WriteLine("Running Windows-only assertions");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenOperatingSystemIsWindows_Diagnostic()
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
                    [|if (!OperatingSystem.IsWindows())
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
                [OSCondition(OperatingSystems.Windows)]
                public void TestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenOperatingSystemIsLinux_Diagnostic()
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
                    [|if (!OperatingSystem.IsLinux())
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
                [OSCondition(OperatingSystems.Linux)]
                public void TestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenOperatingSystemIsMacOS_Diagnostic()
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
                    [|if (!OperatingSystem.IsMacOS())
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
                [OSCondition(OperatingSystems.OSX)]
                public void TestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenOperatingSystemIsWindows_NotNegated_Diagnostic()
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
                    [|if (OperatingSystem.IsWindows())
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
                [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
                public void TestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenOperatingSystemIsFreeBSD_Diagnostic()
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
                    [|if (!OperatingSystem.IsFreeBSD())
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
                [OSCondition(OperatingSystems.FreeBSD)]
                public void TestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenMethodAlreadyHasOSConditionAttribute_Diagnostic()
    {
        string code = """
            using System.Runtime.InteropServices;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [OSCondition(OperatingSystems.Linux)]
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
                [TestMethod]
                [OSCondition(OperatingSystems.Linux | OperatingSystems.Windows)]
                public void TestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenRuntimeCheckWithNestedAssertions_NoDiagnostic()
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
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        Assert.IsTrue(true);
                    }
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenOperatingSystemCheckWithNestedAssertions_NoDiagnostic()
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
                    if (OperatingSystem.IsWindows())
                    {
                        Assert.IsTrue(true);
                    }
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenRuntimeCheckWithNestedMultipleStatements_NoDiagnostic()
    {
        string code = """
            using System;
            using System.Runtime.InteropServices;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        Console.WriteLine("Windows-specific test");
                        Assert.IsTrue(true);
                    }
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenRuntimeCheckWithMultiLineTestBody_PreservesIndentation()
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

                    var result = SomeMethod(
                        1,
                        2,
                        3);
                }

                private int SomeMethod(int a, int b, int c) => a + b + c;
            }
            """;

        string fixedCode = """
            using System.Runtime.InteropServices;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [OSCondition(OperatingSystems.Windows)]
                public void TestMethod()
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
    public async Task WhenOSPlatformCreateWithKnownPlatform_Diagnostic()
    {
        // OSPlatform.Create("Windows") exercises the Create() branch in TryGetOSPlatformFromIsOSPlatformCall.
        // The platform name "Windows" maps to OperatingSystems.Windows, so the fixer applies.
        string code = """
            using System.Runtime.InteropServices;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    [|if (!RuntimeInformation.IsOSPlatform(OSPlatform.Create("Windows")))
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
                [TestMethod]
                [OSCondition(OperatingSystems.Windows)]
                public void TestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenOSPlatformCreateWithUnknownPlatform_Diagnostic()
    {
        // OSPlatform.Create("CustomOS") exercises the Create() branch but with a platform name
        // that has no matching OperatingSystems enum value. The analyzer fires but the fixer
        // cannot produce a code fix (no matching OperatingSystems member), so the code is unchanged.
        string code = """
            using System.Runtime.InteropServices;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    [|if (!RuntimeInformation.IsOSPlatform(OSPlatform.Create("CustomOS")))
                    {
                        return;
                    }|]
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenOperatingSystemIsIOS_Diagnostic()
    {
        // OperatingSystem.IsIOS() maps to OS platform "iOS", which has no corresponding
        // OperatingSystems enum value. The analyzer fires but the fixer cannot produce a code fix.
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    [|if (!OperatingSystem.IsIOS())
                    {
                        return;
                    }|]
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenOperatingSystemIsAndroid_Diagnostic()
    {
        // OperatingSystem.IsAndroid() maps to OS platform "Android", which has no corresponding
        // OperatingSystems enum value. The analyzer fires but the fixer cannot produce a code fix.
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    [|if (!OperatingSystem.IsAndroid())
                    {
                        return;
                    }|]
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }
}
