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
    public async Task WhenFreeBSDSupportedPlatformHasNoOSCondition_AddsIncludeCondition()
    {
        string code = """
            using System.Runtime.Versioning;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [{|#0:SupportedOSPlatform("freebsd")|}]
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
                [SupportedOSPlatform("freebsd")]
                [OSCondition(OperatingSystems.FreeBSD)]
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
    public async Task WhenMacOSSupportedPlatformHasNoOSCondition_AddsOSXIncludeCondition()
    {
        string code = """
            using System.Runtime.Versioning;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [{|#0:SupportedOSPlatform("macos")|}]
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
                [SupportedOSPlatform("macos")]
                [OSCondition(OperatingSystems.OSX)]
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
    public async Task WhenExplicitIncludeOSConditionIsEquivalent_NoDiagnostic()
    {
        string code = """
            using System.Runtime.Versioning;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [SupportedOSPlatform("windows")]
            [TestClass]
            [OSCondition(ConditionMode.Include, OperatingSystems.Windows)]
            public class MyTestClass
            {
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenUnsupportedPlatformUsesComplementaryIncludeCondition_UpdatesToExcludeMode()
    {
        string code = """
            using System.Runtime.Versioning;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [{|#0:UnsupportedOSPlatform("windows")|}]
            [TestClass]
            [OSCondition(OperatingSystems.Linux | OperatingSystems.OSX | OperatingSystems.FreeBSD)]
            public class MyTestClass
            {
            }
            """;

        string fixedCode = """
            using System.Runtime.Versioning;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [UnsupportedOSPlatform("windows")]
            [TestClass]
            [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
            public class MyTestClass
            {
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic().WithLocation(0).WithArguments("MyTestClass"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenSupportedPlatformUsesComplementaryExcludeCondition_UpdatesToIncludeMode()
    {
        string code = """
            using System.Runtime.Versioning;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [{|#0:SupportedOSPlatform("windows")|}]
            [TestClass]
            [OSCondition(ConditionMode.Exclude, OperatingSystems.Linux | OperatingSystems.OSX | OperatingSystems.FreeBSD)]
            public class MyTestClass
            {
            }
            """;

        string fixedCode = """
            using System.Runtime.Versioning;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [SupportedOSPlatform("windows")]
            [TestClass]
            [OSCondition(OperatingSystems.Windows)]
            public class MyTestClass
            {
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic().WithLocation(0).WithArguments("MyTestClass"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenContainingClassOSConditionIsEquivalentToMethodPlatform_NoDiagnostic()
    {
        string code = """
            using System.Runtime.Versioning;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            [OSCondition(OperatingSystems.Windows)]
            public class MyTestClass
            {
                [TestMethod]
                [SupportedOSPlatform("windows")]
                public void TestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenContainingClassCompatibilityAndConditionConstrainMethod_NoDiagnostic()
    {
        string code = """
            using System.Runtime.Versioning;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [SupportedOSPlatform("windows")]
            [TestClass]
            [OSCondition(OperatingSystems.Windows)]
            public class MyTestClass
            {
                [TestMethod]
                [UnsupportedOSPlatform("linux")]
                public void TestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenAssemblyCompatibilityAndClassConditionConstrainMethod_NoDiagnostic()
    {
        string code = """
            using System.Runtime.Versioning;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: SupportedOSPlatform("windows")]

            [TestClass]
            [OSCondition(OperatingSystems.Windows)]
            public class MyTestClass
            {
                [TestMethod]
                [UnsupportedOSPlatform("linux")]
                public void TestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenOuterTypeCompatibilityConflictsWithNestedMethod_DiagnosticWithoutFix()
    {
        string code = """
            using System.Runtime.Versioning;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [SupportedOSPlatform("windows")]
            public class OuterClass
            {
                [TestClass]
                [OSCondition(OperatingSystems.Windows)]
                public class MyTestClass
                {
                    [TestMethod]
                    [{|#0:SupportedOSPlatform("linux")|}]
                    public void TestMethod()
                    {
                    }
                }
            }
            """;

        // The outer Windows-only scope and Linux-only method have an empty effective platform set, so the
        // analyzer reports the inconsistency without registering an OSCondition code fix.
        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic().WithLocation(0).WithArguments("TestMethod"),
            code);
    }

    [TestMethod]
    public async Task WhenAssemblyOnlyRestrictsTestClass_AddsEffectiveCondition()
    {
        string code = """
            using System.Runtime.Versioning;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: SupportedOSPlatform("windows")]

            [TestClass]
            public class {|#0:MyTestClass|}
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

            [assembly: SupportedOSPlatform("windows")]

            [TestClass]
            [OSCondition(OperatingSystems.Windows)]
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
    public async Task WhenOuterTypeOnlyRestrictsNestedTestClass_AddsEffectiveCondition()
    {
        string code = """
            using System.Runtime.Versioning;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [SupportedOSPlatform("windows")]
            public class OuterClass
            {
                [TestClass]
                public class {|#0:MyTestClass|}
                {
                    [TestMethod]
                    public void TestMethod()
                    {
                    }
                }
            }
            """;

        string fixedCode = """
            using System.Runtime.Versioning;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [SupportedOSPlatform("windows")]
            public class OuterClass
            {
                [TestClass]
                [OSCondition(OperatingSystems.Windows)]
                public class MyTestClass
                {
                    [TestMethod]
                    public void TestMethod()
                    {
                    }
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic().WithLocation(0).WithArguments("MyTestClass"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenUnsupportedPlatformHasMessage_AddsExcludeCondition()
    {
        string code = """
            using System.Runtime.Versioning;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [{|#0:UnsupportedOSPlatform("windows", "Not supported on Windows")|}]
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
                [UnsupportedOSPlatform("windows", "Not supported on Windows")]
                [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
                public void TestMethod()
                {
                }
            }
            """;

        var test = new VerifyCS.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
            TestCode = code,
            FixedCode = fixedCode,
        };
        test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic().WithLocation(0).WithArguments("TestMethod"));

        await test.RunAsync();
    }

    [TestMethod]
    public async Task WhenContainingClassOSConditionConflictsWithMethodPlatform_DiagnosticWithoutFix()
    {
        string code = """
            using System.Runtime.Versioning;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            [OSCondition(OperatingSystems.Linux)]
            public class MyTestClass
            {
                [TestMethod]
                [{|#0:SupportedOSPlatform("windows")|}]
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
    public async Task WhenMethodAndClassOSConditionsConflict_DiagnosticWithoutFix()
    {
        string code = """
            using System.Runtime.Versioning;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            [OSCondition(OperatingSystems.Linux)]
            public class MyTestClass
            {
                [TestMethod]
                [{|#0:SupportedOSPlatform("windows")|}]
                [OSCondition(OperatingSystems.Windows)]
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
    public async Task WhenMethodAndClassOSConditionsComposeToExpectedPlatform_NoDiagnostic()
    {
        string code = """
            using System.Runtime.Versioning;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            [OSCondition(OperatingSystems.Windows)]
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

    [TestMethod]
    public async Task WhenOSConditionIsInconsistentInVisualBasic_Diagnostic()
    {
        string code = """
            Imports System.Runtime.Versioning
            Imports Microsoft.VisualStudio.TestTools.UnitTesting

            <TestClass>
            Public Class MyTestClass
                <TestMethod>
                <{|#0:SupportedOSPlatform("windows")|}>
                <OSCondition(OperatingSystems.Linux)>
                Public Sub TestMethod()
                End Sub
            End Class
            """;

        await VerifyVB.VerifyAnalyzerAsync(
            code,
            VerifyVB.Diagnostic().WithLocation(0).WithArguments("TestMethod"));
    }
}
