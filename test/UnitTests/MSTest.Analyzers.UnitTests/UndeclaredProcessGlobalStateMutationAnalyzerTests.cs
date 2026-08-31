// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.UndeclaredProcessGlobalStateMutationAnalyzer,
    MSTest.Analyzers.AddResourceLockFixer>;
using VerifyVB = MSTest.Analyzers.Test.VisualBasicCodeFixVerifier<
    MSTest.Analyzers.UndeclaredProcessGlobalStateMutationAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class UndeclaredProcessGlobalStateMutationAnalyzerTests
{
    [TestMethod]
    public async Task WhenTestMethodSetsEnvironmentVariable_Diagnostic()
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
                    {|#0:Environment.SetEnvironmentVariable("MY_VAR", "value")|};
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
                [ResourceLock(WellKnownResources.EnvironmentVariables)]
                public void MyTestMethod()
                {
                    Environment.SetEnvironmentVariable("MY_VAR", "value");
                }
            }
            """;

        // Assert the rendered message arguments (API name + WellKnownResources member), not just the diagnostic's
        // presence, so a change to the message format or to the mapped resource member is caught.
        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic(UndeclaredProcessGlobalStateMutationAnalyzer.Rule)
                .WithLocation(0)
                .WithArguments("Environment.SetEnvironmentVariable", "EnvironmentVariables"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenTestMethodSetsConsoleOut_Diagnostic()
    {
        string code = """
            using System;
            using System.IO;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: Parallelize(Workers = 0, Scope = ExecutionScope.MethodLevel)]

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    {|#0:Console.SetOut(TextWriter.Null)|};
                }
            }
            """;

        string fixedCode = """
            using System;
            using System.IO;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: Parallelize(Workers = 0, Scope = ExecutionScope.MethodLevel)]

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [ResourceLock(WellKnownResources.Console)]
                public void MyTestMethod()
                {
                    Console.SetOut(TextWriter.Null);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic(UndeclaredProcessGlobalStateMutationAnalyzer.Rule)
                .WithLocation(0)
                .WithArguments("Console.SetOut", "Console"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenTestMethodSetsEnvironmentVariableInTestInitialize_Diagnostic()
    {
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: Parallelize(Workers = 0, Scope = ExecutionScope.MethodLevel)]

            [TestClass]
            public class MyTestClass
            {
                [TestInitialize]
                public void Setup()
                {
                    [|Environment.SetEnvironmentVariable("MY_VAR", "value")|];
                }

                [TestMethod]
                public void MyTestMethod()
                {
                }
            }
            """;

        // TestInitialize is a class-scoped fixture: discovery reads resource locks only from the test class and the
        // test method, so a lock on the fixture method itself would be ignored. The fixer therefore places the lock on
        // the enclosing test class, which is where it actually takes effect.
        string fixedCode = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: Parallelize(Workers = 0, Scope = ExecutionScope.MethodLevel)]

            [TestClass]
            [ResourceLock(WellKnownResources.EnvironmentVariables)]
            public class MyTestClass
            {
                [TestInitialize]
                public void Setup()
                {
                    Environment.SetEnvironmentVariable("MY_VAR", "value");
                }

                [TestMethod]
                public void MyTestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenTestMethodSetsEnvironmentVariableInAssemblyInitialize_NoDiagnostic()
    {
        // AssemblyInitialize cannot race a test: it is serialized behind TestAssemblyInfo's SemaphoreSlim(1, 1)
        // and every worker awaits it before running its test, so no test body overlaps it. A process-global
        // mutation here is ordinary global setup, not a race, and reporting it would be a false positive.
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: Parallelize(Workers = 0, Scope = ExecutionScope.MethodLevel)]

            [TestClass]
            public class MyTestClass
            {
                [AssemblyInitialize]
                public static void AssemblyInit(TestContext context)
                {
                    Environment.SetEnvironmentVariable("MY_VAR", "value");
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
    public async Task WhenTestMethodSetsEnvironmentVariableInAssemblyCleanup_NoDiagnostic()
    {
        // AssemblyCleanup runs only after the last runnable test in the whole assembly, so like AssemblyInitialize
        // it cannot overlap a concurrent test.
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: Parallelize(Workers = 0, Scope = ExecutionScope.MethodLevel)]

            [TestClass]
            public class MyTestClass
            {
                [AssemblyCleanup]
                public static void AssemblyClean()
                {
                    Environment.SetEnvironmentVariable("MY_VAR", "value");
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
    public async Task WhenTestMethodSetsEnvironmentVariableInGlobalTestInitialize_DiagnosticWithoutFix()
    {
        // A global fixture can race every test in the assembly, but it has no effective ResourceLock target.
        // Verify that the diagnostic is reported without offering a code fix.
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: Parallelize(Workers = 0, Scope = ExecutionScope.MethodLevel)]

            [TestClass]
            public class MyTestClass
            {
                [GlobalTestInitialize]
                public static void GlobalSetup(TestContext context)
                {
                    {|#0:Environment.SetEnvironmentVariable("MY_VAR", "value")|};
                }

                [TestMethod]
                public void MyTestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic(UndeclaredProcessGlobalStateMutationAnalyzer.Rule)
                .WithLocation(0)
                .WithArguments("Environment.SetEnvironmentVariable", "EnvironmentVariables"),
            code);
    }

    [TestMethod]
    public async Task WhenTestMethodSetsEnvironmentVariableInClassInitialize_Diagnostic()
    {
        // Contrast with the two tests above: ClassInitialize runs per class, so under MethodLevel parallelization
        // another class's tests can be running concurrently and the mutation genuinely races. This is the boundary
        // of the assembly-fixture exclusion, so it must keep firing.
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: Parallelize(Workers = 0, Scope = ExecutionScope.MethodLevel)]

            [TestClass]
            public class MyTestClass
            {
                [ClassInitialize]
                public static void ClassInit(TestContext context)
                {
                    {|#0:Environment.SetEnvironmentVariable("MY_VAR", "value")|};
                }

                [TestMethod]
                public void MyTestMethod()
                {
                }
            }
            """;

        string fixedCode = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: Parallelize(Workers = 0, Scope = ExecutionScope.MethodLevel)]

            [TestClass]
            [ResourceLock(WellKnownResources.EnvironmentVariables)]
            public class MyTestClass
            {
                [ClassInitialize]
                public static void ClassInit(TestContext context)
                {
                    Environment.SetEnvironmentVariable("MY_VAR", "value");
                }

                [TestMethod]
                public void MyTestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic(UndeclaredProcessGlobalStateMutationAnalyzer.Rule)
                .WithLocation(0)
                .WithArguments("Environment.SetEnvironmentVariable", "EnvironmentVariables"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenParallelizeNotDeclared_NoDiagnostic()
    {
        // Firing gate: without [assembly: Parallelize] (and without the editorconfig opt-in) the rule stays silent.
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    Environment.SetEnvironmentVariable("MY_VAR", "value");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenResourceLockDeclared_NoDiagnostic()
    {
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: Parallelize(Workers = 0, Scope = ExecutionScope.MethodLevel)]

            [TestClass]
            public class MyTestClass
            {
                [ResourceLock(WellKnownResources.EnvironmentVariables)]
                [TestMethod]
                public void MyTestMethod()
                {
                    Environment.SetEnvironmentVariable("MY_VAR", "value");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenDoNotParallelizeDeclared_NoDiagnostic()
    {
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: Parallelize(Workers = 0, Scope = ExecutionScope.MethodLevel)]

            [TestClass]
            [DoNotParallelize]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    Environment.SetEnvironmentVariable("MY_VAR", "value");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenNotTestMethod_NoDiagnostic()
    {
        // A plain (non-test) method in a test class must stay silent.
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: Parallelize(Workers = 0, Scope = ExecutionScope.MethodLevel)]

            [TestClass]
            public class MyTestClass
            {
                public void Helper()
                {
                    Environment.SetEnvironmentVariable("MY_VAR", "value");
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
    public async Task WhenNonTestClass_NoDiagnostic()
    {
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: Parallelize(Workers = 0, Scope = ExecutionScope.MethodLevel)]

            public class NotATest
            {
                public void SomeMethod()
                {
                    Environment.SetEnvironmentVariable("MY_VAR", "value");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenReadingEnvironmentVariable_NoDiagnostic()
    {
        // Reads are not flagged - only mutations.
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
                    string value = Environment.GetEnvironmentVariable("MY_VAR");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenTestMethodSetsEnvironmentVariable_VisualBasic_Diagnostic()
    {
        string code = """
            Imports System
            Imports Microsoft.VisualStudio.TestTools.UnitTesting

            <Assembly: Parallelize(Workers:=0, Scope:=ExecutionScope.MethodLevel)>

            <TestClass>
            Public Class MyTestClass
                <TestMethod>
                Public Sub MyTestMethod()
                    {|#0:Environment.SetEnvironmentVariable("MY_VAR", "value")|}
                End Sub
            End Class
            """;

        await VerifyVB.VerifyAnalyzerAsync(
            code,
            VerifyVB.Diagnostic(UndeclaredProcessGlobalStateMutationAnalyzer.Rule)
                .WithLocation(0)
                .WithArguments("Environment.SetEnvironmentVariable", "EnvironmentVariables"));
    }

    [TestMethod]
    public async Task WhenTestMethodHasMethodLevelDoNotParallelize_NoDiagnostic()
    {
        // A [DoNotParallelize] on the test method itself opts that method out of parallelization,
        // so the mutation is safe and must stay silent.
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: Parallelize(Workers = 0, Scope = ExecutionScope.MethodLevel)]

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [DoNotParallelize]
                public void MyTestMethod()
                {
                    Environment.SetEnvironmentVariable("MY_VAR", "value");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenFixtureMethodHasMethodLevelDoNotParallelize_Diagnostic()
    {
        // [DoNotParallelize] on a fixture method (here [TestInitialize]) has no effect - the adapter only
        // honors it on real test methods - so the mutation still races and the diagnostic must fire.
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: Parallelize(Workers = 0, Scope = ExecutionScope.MethodLevel)]

            [TestClass]
            public class MyTestClass
            {
                [TestInitialize]
                [DoNotParallelize]
                public void MyInit()
                {
                    {|#0:Environment.SetEnvironmentVariable("MY_VAR", "value")|};
                }

                [TestMethod]
                public void MyTestMethod()
                {
                }
            }
            """;

        string fixedCode = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: Parallelize(Workers = 0, Scope = ExecutionScope.MethodLevel)]

            [TestClass]
            [ResourceLock(WellKnownResources.EnvironmentVariables)]
            public class MyTestClass
            {
                [TestInitialize]
                [DoNotParallelize]
                public void MyInit()
                {
                    Environment.SetEnvironmentVariable("MY_VAR", "value");
                }

                [TestMethod]
                public void MyTestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic(UndeclaredProcessGlobalStateMutationAnalyzer.Rule)
                .WithLocation(0)
                .WithArguments("Environment.SetEnvironmentVariable", "EnvironmentVariables"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenDerivedDoNotParallelizeOnClass_NoDiagnostic()
    {
        // A user attribute deriving from DoNotParallelizeAttribute must be honored the same way the adapter
        // honors it (it matches derived attributes), so the mutation stays silent.
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: Parallelize(Workers = 0, Scope = ExecutionScope.MethodLevel)]

            public class MyDoNotParallelizeAttribute : DoNotParallelizeAttribute
            {
            }

            [TestClass]
            [MyDoNotParallelize]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    Environment.SetEnvironmentVariable("MY_VAR", "value");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenRecordTestClassFixture_FixAddedToRecord()
    {
        // The class-level code fix must land on a record test class too, not only class declarations.
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: Parallelize(Workers = 0, Scope = ExecutionScope.MethodLevel)]

            [TestClass]
            public record MyTestClass
            {
                [TestInitialize]
                public void MyInit()
                {
                    {|#0:Environment.SetEnvironmentVariable("MY_VAR", "value")|};
                }

                [TestMethod]
                public void MyTestMethod()
                {
                }
            }
            """;

        string fixedCode = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: Parallelize(Workers = 0, Scope = ExecutionScope.MethodLevel)]

            [TestClass]
            [ResourceLock(WellKnownResources.EnvironmentVariables)]
            public record MyTestClass
            {
                [TestInitialize]
                public void MyInit()
                {
                    Environment.SetEnvironmentVariable("MY_VAR", "value");
                }

                [TestMethod]
                public void MyTestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic(UndeclaredProcessGlobalStateMutationAnalyzer.Rule)
                .WithLocation(0)
                .WithArguments("Environment.SetEnvironmentVariable", "EnvironmentVariables"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenFixtureMethodHasMethodLevelResourceLock_Diagnostic()
    {
        // TypeEnumerator merges the class locks with the locks read from the *test method* itself, and only ever
        // while building a UnitTestElement for a discovered test method. A [ResourceLock] on a fixture method is
        // therefore never read at runtime, so it must not suppress the diagnostic - the mutation still races.
        // The offered fix lifts the lock to the test class, which is the scope discovery does honor.
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: Parallelize(Workers = 0, Scope = ExecutionScope.MethodLevel)]

            [TestClass]
            public class MyTestClass
            {
                [TestInitialize]
                [ResourceLock(WellKnownResources.EnvironmentVariables)]
                public void MyInit()
                {
                    {|#0:Environment.SetEnvironmentVariable("MY_VAR", "value")|};
                }

                [TestMethod]
                public void MyTestMethod()
                {
                }
            }
            """;

        string fixedCode = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: Parallelize(Workers = 0, Scope = ExecutionScope.MethodLevel)]

            [TestClass]
            [ResourceLock(WellKnownResources.EnvironmentVariables)]
            public class MyTestClass
            {
                [TestInitialize]
                [ResourceLock(WellKnownResources.EnvironmentVariables)]
                public void MyInit()
                {
                    Environment.SetEnvironmentVariable("MY_VAR", "value");
                }

                [TestMethod]
                public void MyTestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic(UndeclaredProcessGlobalStateMutationAnalyzer.Rule)
                .WithLocation(0)
                .WithArguments("Environment.SetEnvironmentVariable", "EnvironmentVariables"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenClassLevelResourceLockCoversFixture_NoDiagnostic()
    {
        // The precision counterpart of the test above: a *class-level* [ResourceLock] is read by discovery and
        // applies to every test in the class, so a fixture mutation it covers must stay silent. This guards the
        // fixture-scope narrowing from over-firing on correctly coordinated code.
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: Parallelize(Workers = 0, Scope = ExecutionScope.MethodLevel)]

            [TestClass]
            [ResourceLock(WellKnownResources.EnvironmentVariables)]
            public class MyTestClass
            {
                [TestInitialize]
                public void MyInit()
                {
                    Environment.SetEnvironmentVariable("MY_VAR", "value");
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
    public async Task WhenAlwaysModeConfiguredWithoutParallelize_Diagnostic()
    {
        // Firing gate, second branch: the mstest_parallel_safety_mode = always opt-in turns the rules on even
        // when [assembly: Parallelize] is absent, which is how a suite that opts in via runsettings or the
        // MSTestParallelizeScope MSBuild property (neither visible to an analyzer) gets coverage.
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    {|#0:Environment.SetEnvironmentVariable("MY_VAR", "value")|};
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
                [ResourceLock(WellKnownResources.EnvironmentVariables)]
                public void MyTestMethod()
                {
                    Environment.SetEnvironmentVariable("MY_VAR", "value");
                }
            }
            """;

        var test = new VerifyCS.Test
        {
            TestCode = code,
            FixedCode = fixedCode,
        };
        test.TestState.AnalyzerConfigFiles.Add(("/.globalconfig", """
            is_global = true

            mstest_parallel_safety_mode = always
            """));
        test.ExpectedDiagnostics.Add(
            VerifyCS.Diagnostic(UndeclaredProcessGlobalStateMutationAnalyzer.Rule)
                .WithLocation(0)
                .WithArguments("Environment.SetEnvironmentVariable", "EnvironmentVariables"));
        await test.RunAsync();
    }

    [TestMethod]
    public async Task WhenAlwaysModeConfiguredInEditorConfigSection_Diagnostic()
    {
        // Firing gate, per-tree branch: a value set in an ordinary [*.cs] .editorconfig section is exposed
        // per-syntax-tree rather than through GlobalOptions, so IsAlwaysModeConfigured falls back to scanning the
        // trees. The .globalconfig test above only covers the GlobalOptions path and would not catch a regression
        // that dropped this fallback.
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    {|#0:Environment.SetEnvironmentVariable("MY_VAR", "value")|};
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
                [ResourceLock(WellKnownResources.EnvironmentVariables)]
                public void MyTestMethod()
                {
                    Environment.SetEnvironmentVariable("MY_VAR", "value");
                }
            }
            """;

        var test = new VerifyCS.Test
        {
            TestCode = code,
            FixedCode = fixedCode,
        };
        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", """
            root = true

            [*.cs]
            mstest_parallel_safety_mode = always
            """));
        test.ExpectedDiagnostics.Add(
            VerifyCS.Diagnostic(UndeclaredProcessGlobalStateMutationAnalyzer.Rule)
                .WithLocation(0)
                .WithArguments("Environment.SetEnvironmentVariable", "EnvironmentVariables"));
        await test.RunAsync();
    }

    [TestMethod]
    public async Task WhenOverrideInheritsDoNotParallelizeFromBaseMethod_NoDiagnostic()
    {
        // DoNotParallelizeAttribute is Inherited = true and the adapter reads member attributes with
        // GetCustomAttributes(..., inherit: true), so an override inherits the base method's opt-out and runs
        // sequentially. Inspecting only the override's own attributes would miss that and report a false positive.
        // [TestMethod] is reapplied because TestMethodAttribute is Inherited = false.
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: Parallelize(Workers = 0, Scope = ExecutionScope.MethodLevel)]

            public class MyBaseClass
            {
                [TestMethod]
                [DoNotParallelize]
                public virtual void MyTestMethod()
                {
                }
            }

            [TestClass]
            public class MyTestClass : MyBaseClass
            {
                [TestMethod]
                public override void MyTestMethod()
                {
                    Environment.SetEnvironmentVariable("MY_VAR", "value");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenOverrideInheritsResourceLockFromBaseMethod_NoDiagnostic()
    {
        // ResourceLockAttribute is declared Inherited = true, so a lock on the overridden method still applies to
        // the override at runtime and the mutation is coordinated.
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: Parallelize(Workers = 0, Scope = ExecutionScope.MethodLevel)]

            public class MyBaseClass
            {
                [TestMethod]
                [ResourceLock(WellKnownResources.EnvironmentVariables)]
                public virtual void MyTestMethod()
                {
                }
            }

            [TestClass]
            public class MyTestClass : MyBaseClass
            {
                [TestMethod]
                public override void MyTestMethod()
                {
                    Environment.SetEnvironmentVariable("MY_VAR", "value");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenAssemblyDoNotParallelizeWithParallelize_NoDiagnostic()
    {
        // Firing gate, kill switch: [assembly: DoNotParallelize] disables in-assembly parallelization entirely,
        // so no parallel-safety rule can describe a live defect even though [assembly: Parallelize] is present.
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: Parallelize(Workers = 0, Scope = ExecutionScope.MethodLevel)]
            [assembly: DoNotParallelize]

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    Environment.SetEnvironmentVariable("MY_VAR", "value");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenAssemblyDoNotParallelizeWithAlwaysMode_NoDiagnostic()
    {
        // Firing gate, precedence: the assembly opt-out wins over the mstest_parallel_safety_mode = always
        // opt-in when both are present, so the rules stay silent.
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: DoNotParallelize]

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    Environment.SetEnvironmentVariable("MY_VAR", "value");
                }
            }
            """;

        var test = new VerifyCS.Test
        {
            TestCode = code,
            FixedCode = code,
        };
        test.TestState.AnalyzerConfigFiles.Add(("/.globalconfig", """
            is_global = true

            mstest_parallel_safety_mode = always
            """));
        await test.RunAsync();
    }
}
