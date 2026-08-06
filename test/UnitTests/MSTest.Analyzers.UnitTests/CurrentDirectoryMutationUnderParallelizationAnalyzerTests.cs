// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.CurrentDirectoryMutationUnderParallelizationAnalyzer,
    MSTest.Analyzers.AddResourceLockFixer>;
using VerifyVB = MSTest.Analyzers.Test.VisualBasicCodeFixVerifier<
    MSTest.Analyzers.CurrentDirectoryMutationUnderParallelizationAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class CurrentDirectoryMutationUnderParallelizationAnalyzerTests
{
    [TestMethod]
    public async Task WhenTestMethodSetsCurrentDirectoryViaDirectory_Diagnostic()
    {
        string code = """
            using System.IO;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: Parallelize(Workers = 0, Scope = ExecutionScope.MethodLevel)]

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    {|#0:Directory.SetCurrentDirectory("/tmp")|};
                }
            }
            """;

        string fixedCode = """
            using System.IO;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: Parallelize(Workers = 0, Scope = ExecutionScope.MethodLevel)]

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [ResourceLock(WellKnownResources.CurrentDirectory)]
                public void MyTestMethod()
                {
                    Directory.SetCurrentDirectory("/tmp");
                }
            }
            """;

        // Assert the rendered API argument in the message, not just the diagnostic's presence.
        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic(CurrentDirectoryMutationUnderParallelizationAnalyzer.Rule)
                .WithLocation(0)
                .WithArguments("Directory.SetCurrentDirectory"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenTestMethodSetsEnvironmentCurrentDirectory_Diagnostic()
    {
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: Parallelize(Workers = 0, Scope = ExecutionScope.MethodLevel)]

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    {|#0:Environment.CurrentDirectory = "/tmp"|};
                }
            }
            """;

        string fixedCode = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: Parallelize(Workers = 0, Scope = ExecutionScope.MethodLevel)]

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [ResourceLock(WellKnownResources.CurrentDirectory)]
                public void MyTestMethod()
                {
                    Environment.CurrentDirectory = "/tmp";
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic(CurrentDirectoryMutationUnderParallelizationAnalyzer.Rule)
                .WithLocation(0)
                .WithArguments("Environment.CurrentDirectory"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenTestMethodCompoundAssignsEnvironmentCurrentDirectory_Diagnostic()
    {
        // A compound assignment ('+=') reads then writes the process-global current directory, so it must be flagged
        // like a plain assignment. No code fix is asserted here beyond the lock the fixer adds.
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: Parallelize(Workers = 0, Scope = ExecutionScope.MethodLevel)]

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    {|#0:Environment.CurrentDirectory += "sub"|};
                }
            }
            """;

        string fixedCode = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: Parallelize(Workers = 0, Scope = ExecutionScope.MethodLevel)]

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [ResourceLock(WellKnownResources.CurrentDirectory)]
                public void MyTestMethod()
                {
                    Environment.CurrentDirectory += "sub";
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic(CurrentDirectoryMutationUnderParallelizationAnalyzer.Rule)
                .WithLocation(0)
                .WithArguments("Environment.CurrentDirectory"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenTestMethodCoalesceAssignsEnvironmentCurrentDirectory_NoDiagnostic()
    {
        // 'Environment.CurrentDirectory ??= x' can never reach the setter: the getter is declared non-nullable
        // ('public static string CurrentDirectory') and throws rather than returning null on failure, so the
        // coalescing form is a guaranteed non-mutation and flagging it would be a guaranteed false positive.
        // Contrast R3, which does handle '??=' because CultureInfo.DefaultThreadCurrentCulture is declared nullable
        // and so genuinely can be written by the coalescing form.
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: Parallelize(Workers = 0, Scope = ExecutionScope.MethodLevel)]

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    Environment.CurrentDirectory ??= "sub";
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenResourceLockDeclared_NoDiagnostic()
    {
        // A declared current-directory lock means the author coordinated the mutation, so R2 stays silent. The
        // "coordinated, but avoid CWD entirely" judgement is owned by the sibling parallel-safety-audit skill.
        string code = """
            using System.IO;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: Parallelize(Workers = 0, Scope = ExecutionScope.MethodLevel)]

            [TestClass]
            public class MyTestClass
            {
                [ResourceLock(WellKnownResources.CurrentDirectory)]
                [TestMethod]
                public void MyTestMethod()
                {
                    Directory.SetCurrentDirectory("/tmp");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenDoNotParallelizeDeclared_NoDiagnostic()
    {
        string code = """
            using System.IO;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: Parallelize(Workers = 0, Scope = ExecutionScope.MethodLevel)]

            [TestClass]
            [DoNotParallelize]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    Directory.SetCurrentDirectory("/tmp");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenParallelizeNotDeclared_NoDiagnostic()
    {
        string code = """
            using System.IO;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    Directory.SetCurrentDirectory("/tmp");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenReadingCurrentDirectory_NoDiagnostic()
    {
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: Parallelize(Workers = 0, Scope = ExecutionScope.MethodLevel)]

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    string dir = Environment.CurrentDirectory;
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenNonTestClass_NoDiagnostic()
    {
        string code = """
            using System.IO;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: Parallelize(Workers = 0, Scope = ExecutionScope.MethodLevel)]

            public class NotATest
            {
                public void SomeMethod()
                {
                    Directory.SetCurrentDirectory("/tmp");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenTestInitializeSetsCurrentDirectoryViaDirectory_Diagnostic()
    {
        // [TestInitialize] is a class-scoped fixture included in GetFixtureAttributeSymbols, so a mutation inside it
        // must be reported just like one inside a test method.
        string code = """
            using System.IO;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: Parallelize(Workers = 0, Scope = ExecutionScope.MethodLevel)]

            [TestClass]
            public class MyTestClass
            {
                [TestInitialize]
                public void Setup()
                {
                    {|#0:Directory.SetCurrentDirectory("/tmp")|};
                }

                [TestMethod]
                public void MyTestMethod()
                {
                }
            }
            """;

        // Discovery reads resource locks only from the test class and the test method, so a lock on the fixture method
        // itself would be ignored. The fixer therefore annotates the enclosing test class, where the lock takes effect.
        string fixedCode = """
            using System.IO;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: Parallelize(Workers = 0, Scope = ExecutionScope.MethodLevel)]

            [TestClass]
            [ResourceLock(WellKnownResources.CurrentDirectory)]
            public class MyTestClass
            {
                [TestInitialize]
                public void Setup()
                {
                    Directory.SetCurrentDirectory("/tmp");
                }

                [TestMethod]
                public void MyTestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic(CurrentDirectoryMutationUnderParallelizationAnalyzer.Rule)
                .WithLocation(0)
                .WithArguments("Directory.SetCurrentDirectory"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenAssemblyInitializeSetsCurrentDirectoryViaDirectory_NoDiagnostic()
    {
        // [AssemblyInitialize] is deliberately excluded from GetFixtureAttributeSymbols: it is serialized and every
        // worker awaits it before running any test, so a process-global mutation there cannot race a concurrent test.
        // Flagging it would be a false positive.
        string code = """
            using System.IO;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: Parallelize(Workers = 0, Scope = ExecutionScope.MethodLevel)]

            [TestClass]
            public class MyTestClass
            {
                [AssemblyInitialize]
                public static void AssemblyInit(TestContext context)
                {
                    Directory.SetCurrentDirectory("/tmp");
                }

                [TestMethod]
                public void MyTestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenGlobalTestInitializeSetsCurrentDirectoryViaDirectory_DiagnosticWithoutFix()
    {
        // [GlobalTestInitialize] runs around every test in the assembly, so the mutation genuinely races and is
        // reported. But it is a global fixture, not a class-scoped one: it is in GetFixtureAttributeSymbols yet
        // absent from GetClassScopedFixtureAttributeSymbols, so GetResourceLockFixScope returns null and the
        // analyzer omits the fixer properties. This pins the third branch - report, but offer no fix - because a
        // global fixture has no effective lock target: the enclosing class is not one, since locking it would
        // serialize only that class's tests while the fixture kept racing the tests of every other class. (That
        // is a different mechanism from the class-scoped case above, where the class lock *is* the effective
        // target and it is a lock on the fixture method that discovery would ignore.) Asserting the code is
        // unchanged is what catches a regression that started offering that do-nothing fix.
        string code = """
            using System.IO;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: Parallelize(Workers = 0, Scope = ExecutionScope.MethodLevel)]

            [TestClass]
            public class MyTestClass
            {
                [GlobalTestInitialize]
                public static void GlobalSetup(TestContext context)
                {
                    {|#0:Directory.SetCurrentDirectory("/tmp")|};
                }

                [TestMethod]
                public void MyTestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic(CurrentDirectoryMutationUnderParallelizationAnalyzer.Rule)
                .WithLocation(0)
                .WithArguments("Directory.SetCurrentDirectory"),
            code);
    }

    [TestMethod]
    public async Task WhenTestMethodSetsCurrentDirectory_VisualBasic_Diagnostic()
    {
        string code = """
            Imports System.IO
            Imports Microsoft.VisualStudio.TestTools.UnitTesting

            <Assembly: Parallelize(Workers:=0, Scope:=ExecutionScope.MethodLevel)>

            <TestClass>
            Public Class MyTestClass
                <TestMethod>
                Public Sub MyTestMethod()
                    {|#0:Directory.SetCurrentDirectory("/tmp")|}
                End Sub
            End Class
            """;

        await VerifyVB.VerifyAnalyzerAsync(
            code,
            VerifyVB.Diagnostic(CurrentDirectoryMutationUnderParallelizationAnalyzer.Rule)
                .WithLocation(0)
                .WithArguments("Directory.SetCurrentDirectory"));
    }
}
