// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using Microsoft.Testing.TestInfrastructure;

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.UseArchitectureConditionAttributeInsteadOfRuntimeCheckAnalyzer,
    MSTest.Analyzers.UseArchitectureConditionAttributeInsteadOfRuntimeCheckFixer>;
using VerifyVB = MSTest.Analyzers.Test.VisualBasicCodeFixVerifier<
    MSTest.Analyzers.UseArchitectureConditionAttributeInsteadOfRuntimeCheckAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace MSTest.Analyzers.Test;

// '[ArchitectureCondition]' and 'TestArchitectures' only exist in the .NET flavor of MSTest, so the analyzer
// intentionally does nothing when the test project compiles against the .NET Framework reference assemblies.
#if NET
[TestClass]
public sealed class UseArchitectureConditionAttributeInsteadOfRuntimeCheckAnalyzerTests
{
    [TestMethod]
    public async Task WhenNoArchitectureCheckUsed_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    // No RuntimeInformation.ProcessArchitecture check
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenArchitectureCheckWithEarlyReturn_NotEquals_Diagnostic()
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
                    [|if (RuntimeInformation.ProcessArchitecture != Architecture.X64)
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
                [ArchitectureCondition(TestArchitectures.X64)]
                public void TestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenArchitectureCheckWithEarlyReturn_Equals_Diagnostic()
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
                    [|if (RuntimeInformation.ProcessArchitecture == Architecture.X86)
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
                [ArchitectureCondition(ConditionMode.Exclude, TestArchitectures.X86)]
                public void TestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenArchitectureCheckWithAssertInconclusive_Diagnostic()
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
                    [|if (RuntimeInformation.ProcessArchitecture != Architecture.Arm64)
                    {
                        Assert.Inconclusive("Only for arm64");
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
                [ArchitectureCondition(TestArchitectures.Arm64)]
                public void TestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenArchitectureCheckWithoutBlock_Diagnostic()
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
                    [|if (RuntimeInformation.ProcessArchitecture != Architecture.Wasm)
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
                [ArchitectureCondition(TestArchitectures.Wasm)]
                public void TestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenArchitectureIsOnTheLeftOfTheComparison_Diagnostic()
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
                    [|if (Architecture.X64 != RuntimeInformation.ProcessArchitecture)
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
                [ArchitectureCondition(TestArchitectures.X64)]
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
            using System.Runtime.InteropServices;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    [|if (RuntimeInformation.ProcessArchitecture != Architecture.X64)
                    {
                        return;
                    }|]

                    Assert.IsTrue(true);
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
                [ArchitectureCondition(TestArchitectures.X64)]
                public void TestMethod()
                {
                    Assert.IsTrue(true);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenArchitectureCheckIsNotTheFirstStatement_NoDiagnostic()
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
                    Assert.IsTrue(true);

                    if (RuntimeInformation.ProcessArchitecture != Architecture.X64)
                    {
                        return;
                    }
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenArchitectureCheckHasElseBranch_NoDiagnostic()
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
                    if (RuntimeInformation.ProcessArchitecture != Architecture.X64)
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
    public async Task WhenArchitectureCheckBodyIsNotAGuard_NoDiagnostic()
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
                    if (RuntimeInformation.ProcessArchitecture != Architecture.X64)
                    {
                        Assert.IsTrue(true);
                    }
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenArchitectureCheckIsInNonTestMethod_NoDiagnostic()
    {
        string code = """
            using System.Runtime.InteropServices;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                public void Helper()
                {
                    if (RuntimeInformation.ProcessArchitecture != Architecture.X64)
                    {
                        return;
                    }
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenComparingADifferentProperty_NoDiagnostic()
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
                    if (RuntimeInformation.OSArchitecture != Architecture.X64)
                    {
                        return;
                    }
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenTestArchitecturesLacksTheComparedArchitecture_NoDiagnostic()
    {
        // The verified compilation targets net9.0, so 'Architecture.RiscV64' resolves, while the referenced MSTest
        // build is the net8.0 asset whose 'TestArchitectures' has no matching member. Reporting here would offer a
        // fix that generates uncompilable 'TestArchitectures.RiscV64', so the analyzer must stay silent.
        string code = """
            using System.Runtime.InteropServices;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    if (RuntimeInformation.ProcessArchitecture != Architecture.RiscV64)
                    {
                        return;
                    }
                }
            }
            """;

        var test = new VerifyCS.Test
        {
            TestCode = code,
            FixedCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90
                .WithNuGetConfigFilePath(Path.Combine(RootFinder.Find(), "NuGet.config")),
        };

        await test.RunAsync();
    }

    [TestMethod]
    public async Task WhenMethodHasACustomConditionAttribute_NoDiagnostic()
    {
        // A custom condition's 'GroupName' is an arbitrary property implementation. If it happens to return
        // "ArchitectureCondition", adding '[ArchitectureCondition]' would OR the two instead of ANDing them.
        string code = """
            using System.Runtime.InteropServices;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public sealed class MyConditionAttribute : ConditionBaseAttribute
            {
                public MyConditionAttribute()
                    : base(ConditionMode.Include)
                {
                }

                public override string GroupName => "ArchitectureCondition";

                public override bool IsConditionMet => true;
            }

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [MyCondition]
                public void TestMethod()
                {
                    if (RuntimeInformation.ProcessArchitecture != Architecture.X64)
                    {
                        return;
                    }
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenMethodAlreadyHasArchitectureCondition_NoDiagnostic()
    {
        string code = """
            using System.Runtime.InteropServices;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [ArchitectureCondition(TestArchitectures.X64)]
                public void TestMethod()
                {
                    if (RuntimeInformation.ProcessArchitecture != Architecture.Arm64)
                    {
                        return;
                    }
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenArchitectureCheckWithEarlyReturn_VisualBasic_Diagnostic()
    {
        string code = """
            Imports System.Runtime.InteropServices
            Imports Microsoft.VisualStudio.TestTools.UnitTesting

            <TestClass>
            Public Class MyTestClass
                <TestMethod>
                Public Sub TestMethod()
                    [|If RuntimeInformation.ProcessArchitecture <> Architecture.X64 Then
                        Return
                    End If|]
                End Sub
            End Class
            """;

        await VerifyVB.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenNoArchitectureCheckUsed_VisualBasic_NoDiagnostic()
    {
        string code = """
            Imports System.Runtime.InteropServices
            Imports Microsoft.VisualStudio.TestTools.UnitTesting

            <TestClass>
            Public Class MyTestClass
                <TestMethod>
                Public Sub TestMethod()
                    If RuntimeInformation.OSArchitecture <> Architecture.X64 Then
                        Return
                    End If
                End Sub
            End Class
            """;

        await VerifyVB.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenGuardReturnsAValue_NoDiagnostic()
    {
        // A test method that isn't 'async' can return a value from the guard. Removing it would drop the call.
        string code = """
            using System.Runtime.InteropServices;
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public Task TestMethod()
                {
                    if (RuntimeInformation.ProcessArchitecture != Architecture.X64)
                    {
                        return CleanupAsync();
                    }

                    return Task.CompletedTask;
                }

                private static Task CleanupAsync() => Task.CompletedTask;
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenComparingAgainstAField_NoDiagnostic()
    {
        string code = """
            using System.Runtime.InteropServices;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                private static readonly Architecture Expected = Architecture.X64;

                [TestMethod]
                public void TestMethod()
                {
                    if (RuntimeInformation.ProcessArchitecture != Expected)
                    {
                        return;
                    }
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }
}
#else
// On the .NET Framework test target the verifier references the netfx flavor of MSTest, which has no
// 'ArchitectureConditionAttribute'. That is the surface the analyzer's type-presence bailout exists for, so it is the
// one place the bailout can actually be asserted.
[TestClass]
public sealed class UseArchitectureConditionAttributeInsteadOfRuntimeCheckOnNetFrameworkTests
{
    [TestMethod]
    public async Task WhenArchitectureConditionAttributeIsNotAvailable_NoDiagnostic()
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
                    if (RuntimeInformation.ProcessArchitecture != Architecture.X64)
                    {
                        return;
                    }
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }
}
#endif
