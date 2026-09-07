// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.OSPlatformAttributesShouldBeConsistentAnalyzer,
    MSTest.Analyzers.OSPlatformAttributesShouldBeConsistentFixer>;
using VerifyVB = MSTest.Analyzers.Test.VisualBasicCodeFixVerifier<
    MSTest.Analyzers.OSPlatformAttributesShouldBeConsistentAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class OSPlatformAttributesShouldBeConsistentAnalyzerTests
{
    [TestMethod]
    public async Task WhenSupportedPlatformHasNoOSCondition_AddsIncludeCondition()
    {
        string code = """
            using System.Runtime.Versioning;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [{|#0:SupportedOSPlatform("linux")|}]
                public void TestMethod()
                {
                }
            }
            """;

        string fixedCode = """
            using System.Runtime.Versioning;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [SupportedOSPlatform("linux")]
                [OSCondition(OperatingSystems.Linux)]
                public void TestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic().WithLocation(0).WithArguments("TestMethod"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenUnsupportedPlatformsHaveNoOSCondition_AddsCombinedExcludeCondition()
    {
        string code = """
            using System.Runtime.Versioning;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [{|#0:UnsupportedOSPlatform("windows")|}]
            [UnsupportedOSPlatform("OSX")]
            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                }
            }
            """;

        string fixedCode = """
            using System.Runtime.Versioning;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [UnsupportedOSPlatform("windows")]
            [UnsupportedOSPlatform("OSX")]
            [TestClass]
            [OSCondition(ConditionMode.Exclude, OperatingSystems.OSX | OperatingSystems.Windows)]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic().WithLocation(0).WithArguments("MyTestClass"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenOSConditionIsEquivalent_NoDiagnostic()
    {
        string code = """
            using System.Runtime.Versioning;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [SupportedOSPlatform("macos")]
            [SupportedOSPlatform("linux")]
            [TestClass]
            [OSCondition(OperatingSystems.Linux | OperatingSystems.OSX)]
            public class MyTestClass
            {
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenExcludeOSConditionIsEquivalent_NoDiagnostic()
    {
        string code = """
            using System.Runtime.Versioning;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [UnsupportedOSPlatform("windows")]
            [UnsupportedOSPlatform("linux")]
            [TestClass]
            [OSCondition(ConditionMode.Exclude, OperatingSystems.Linux | OperatingSystems.Windows)]
            public class MyTestClass
            {
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenOSConditionIsInconsistent_UpdatesCondition()
    {
        string code = """
            using System.Runtime.Versioning;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [{|#0:SupportedOSPlatform("windows")|}]
                [OSCondition(OperatingSystems.Linux)]
                public void TestMethod()
                {
                }
            }
            """;

        string fixedCode = """
            using System.Runtime.Versioning;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [SupportedOSPlatform("windows")]
                [OSCondition(OperatingSystems.Windows)]
                public void TestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic().WithLocation(0).WithArguments("TestMethod"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenOSConditionIsInconsistent_PreservesNamedArguments()
    {
        string code = """
            using System.Runtime.Versioning;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [{|#0:SupportedOSPlatform("windows")|}]
                [OSCondition(OperatingSystems.Linux, IgnoreMessage = "Requires Windows")]
                public void TestMethod()
                {
                }
            }
            """;

        string fixedCode = """
            using System.Runtime.Versioning;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [SupportedOSPlatform("windows")]
                [OSCondition(OperatingSystems.Windows, IgnoreMessage = "Requires Windows")]
                public void TestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic().WithLocation(0).WithArguments("TestMethod"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenOSConditionIsOnAnotherPartialDeclaration_UpdatesItsDocument()
    {
        var test = new VerifyCS.Test
        {
            TestState =
            {
                Sources =
                {
                    """
                    using System.Runtime.Versioning;
                    using Microsoft.VisualStudio.TestTools.UnitTesting;

                    [{|#0:SupportedOSPlatform("windows")|}]
                    [TestClass]
                    public partial class MyTestClass
                    {
                    }
                    """,
                    """
                    using Microsoft.VisualStudio.TestTools.UnitTesting;

                    [OSCondition(OperatingSystems.Linux)]
                    public partial class MyTestClass
                    {
                    }
                    """,
                },
                ExpectedDiagnostics =
                {
                    VerifyCS.Diagnostic().WithLocation(0).WithArguments("MyTestClass"),
                },
            },
            FixedState =
            {
                Sources =
                {
                    """
                    using System.Runtime.Versioning;
                    using Microsoft.VisualStudio.TestTools.UnitTesting;

                    [SupportedOSPlatform("windows")]
                    [TestClass]
                    public partial class MyTestClass
                    {
                    }
                    """,
                    """
                    using Microsoft.VisualStudio.TestTools.UnitTesting;

                    [OSCondition(OperatingSystems.Windows)]
                    public partial class MyTestClass
                    {
                    }
                    """,
                },
            },
        };

        await test.RunAsync();
    }

    [TestMethod]
    public async Task WhenExistingOSConditionConstructorIsMalformed_UpdatesAttributeWithoutDuplicatingIt()
    {
        string code = """
            using System.Runtime.Versioning;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [{|#0:SupportedOSPlatform("windows")|}]
                [OSCondition]
                public void TestMethod()
                {
                }
            }
            """;

        string fixedCode = """
            using System.Runtime.Versioning;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [SupportedOSPlatform("windows")]
                [OSCondition(OperatingSystems.Windows)]
                public void TestMethod()
                {
                }
            }
            """;

        var test = new VerifyCS.Test
        {
            TestCode = code,
            FixedCode = fixedCode,
            CompilerDiagnostics = CompilerDiagnostics.None,
        };
        test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic().WithLocation(0).WithArguments("TestMethod"));

        await test.RunAsync();
    }

    [TestMethod]
    public async Task WhenPlatformsUseMixedModes_DiagnosticWithoutFix()
    {
        string code = """
            using System.Runtime.Versioning;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [{|#0:SupportedOSPlatform("linux")|}]
                [UnsupportedOSPlatform("windows")]
                public void TestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic().WithLocation(0).WithArguments("TestMethod"),
            code);
    }

    [TestMethod]
    public async Task WhenPlatformHasVersion_DiagnosticWithoutFix()
    {
        string code = """
            using System.Runtime.Versioning;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [{|#0:SupportedOSPlatform("windows10.0")|}]
                public void TestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic().WithLocation(0).WithArguments("TestMethod"),
            code);
    }

    [TestMethod]
    public async Task WhenPlatformIsNotSupportedByOSCondition_DiagnosticWithoutFix()
    {
        string code = """
            using System.Runtime.Versioning;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [{|#0:SupportedOSPlatform("android")|}]
                public void TestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic().WithLocation(0).WithArguments("TestMethod"),
            code);
    }

    [TestMethod]
    public async Task WhenPlatformAttributeIsOnNonTest_NoDiagnostic()
    {
        string code = """
            using System.Runtime.Versioning;

            [SupportedOSPlatform("linux")]
            public class MyClass
            {
                [UnsupportedOSPlatform("windows")]
                public void Method()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenSupportedPlatformHasNoOSConditionInVisualBasic_Diagnostic()
    {
        string code = """
            Imports System.Runtime.Versioning
            Imports Microsoft.VisualStudio.TestTools.UnitTesting

            <TestClass>
            Public Class MyTestClass
                <TestMethod>
                <{|#0:SupportedOSPlatform("linux")|}>
                Public Sub TestMethod()
                End Sub
            End Class
            """;

        await VerifyVB.VerifyAnalyzerAsync(
            code,
            VerifyVB.Diagnostic().WithLocation(0).WithArguments("TestMethod"));
    }
}
