// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.CurrentDirectoryMutationUnderParallelizationAnalyzer,
    MSTest.Analyzers.AddResourceLockFixer>;

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
    public async Task WhenDirectoryMutationHasReadOnlyMethodLock_DiagnosticAndExclusiveFix()
    {
        string code = """
            using System.IO;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            [assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]

            [TestClass]
            public class Tests
            {
                [TestMethod]
                [ResourceLock(WellKnownResources.CurrentDirectory, Mode = ResourceAccessMode.Read)]
                public void Check()
                {
                    {|#0:Directory.SetCurrentDirectory("fixture")|};
                }
            }
            """;

        string fixedCode = """
            using System.IO;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            [assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]

            [TestClass]
            public class Tests
            {
                [TestMethod]
                [ResourceLock(WellKnownResources.CurrentDirectory, Mode = ResourceAccessMode.Read)]
                [ResourceLock(WellKnownResources.CurrentDirectory)]
                public void Check()
                {
                    Directory.SetCurrentDirectory("fixture");
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
    public async Task WhenCurrentDirectoryAssignmentsHaveClassReadLock_Diagnostics()
    {
        const string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            [assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]

            [TestClass]
            [ResourceLock(WellKnownResources.CurrentDirectory, Mode = ResourceAccessMode.Read)]
            public class Tests
            {
                [TestMethod]
                public void Check()
                {
                    {|#0:Environment.CurrentDirectory = "fixture"|};
                    {|#1:Environment.CurrentDirectory += "child"|};
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(CurrentDirectoryMutationUnderParallelizationAnalyzer.Rule).WithLocation(0).WithArguments("Environment.CurrentDirectory"),
            VerifyCS.Diagnostic(CurrentDirectoryMutationUnderParallelizationAnalyzer.Rule).WithLocation(1).WithArguments("Environment.CurrentDirectory"));
    }

    [TestMethod]
    public async Task WhenFixtureHasClassReadAndMethodWriteLock_Diagnostic()
    {
        const string code = """
            using System.IO;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            [assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]

            [TestClass]
            [ResourceLock(WellKnownResources.CurrentDirectory, Mode = ResourceAccessMode.Read)]
            public class Tests
            {
                [TestInitialize]
                [ResourceLock(WellKnownResources.CurrentDirectory)]
                public void Initialize() => {|#0:Directory.SetCurrentDirectory("fixture")|};

                [TestMethod]
                public void Check() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(CurrentDirectoryMutationUnderParallelizationAnalyzer.Rule)
                .WithLocation(0)
                .WithArguments("Directory.SetCurrentDirectory"));
    }

    [TestMethod]
    public async Task WhenCurrentDirectoryReadAndWriteLocksShareKey_StrongestModeIsExclusive()
    {
        const string code = """
            using System.IO;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            [assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]

            [TestClass]
            [ResourceLock(WellKnownResources.CurrentDirectory, Mode = ResourceAccessMode.Read)]
            public class ClassReadMethodWrite
            {
                [TestMethod]
                [ResourceLock(WellKnownResources.CurrentDirectory)]
                public void Check() => Directory.SetCurrentDirectory("fixture");
            }

            [TestClass]
            [ResourceLock(WellKnownResources.CurrentDirectory)]
            public class ClassWriteMethodRead
            {
                [TestMethod]
                [ResourceLock(WellKnownResources.CurrentDirectory, Mode = ResourceAccessMode.Read)]
                public void Check() => Directory.SetCurrentDirectory("fixture");
            }

            [TestClass]
            public class DuplicateMethodLocks
            {
                [TestMethod]
                [ResourceLock(WellKnownResources.CurrentDirectory, Mode = ResourceAccessMode.Read)]
                [ResourceLock(WellKnownResources.CurrentDirectory)]
                public void ReadThenWrite() => Directory.SetCurrentDirectory("fixture");

                [TestMethod]
                [ResourceLock(WellKnownResources.CurrentDirectory)]
                [ResourceLock(WellKnownResources.CurrentDirectory, Mode = ResourceAccessMode.Read)]
                public void WriteThenRead() => Directory.SetCurrentDirectory("fixture");
            }

            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenClassAndMethodBothHaveCurrentDirectoryReadLocks_Diagnostic()
    {
        const string code = """
            using System.IO;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            [assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]

            [TestClass]
            [ResourceLock(WellKnownResources.CurrentDirectory, Mode = ResourceAccessMode.Read)]
            public class Tests
            {
                [TestMethod]
                [ResourceLock(WellKnownResources.CurrentDirectory, Mode = ResourceAccessMode.Read)]
                public void Check() => {|#0:Directory.SetCurrentDirectory("fixture")|};
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(CurrentDirectoryMutationUnderParallelizationAnalyzer.Rule)
                .WithLocation(0)
                .WithArguments("Directory.SetCurrentDirectory"));
    }

    [TestMethod]
    public async Task WhenCurrentDirectoryWriteLockHasDifferentKey_ReadLockIsNotUpgraded()
    {
        const string code = """
            using System.IO;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            [assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]

            [TestClass]
            public class Tests
            {
                [TestMethod]
                [ResourceLock(WellKnownResources.CurrentDirectory, Mode = ResourceAccessMode.Read)]
                [ResourceLock("system.Environment.CurrentDirectory")]
                public void DifferentCase() => {|#0:Directory.SetCurrentDirectory("fixture")|};

                [TestMethod]
                [ResourceLock(WellKnownResources.CurrentDirectory, Mode = ResourceAccessMode.Read)]
                [ResourceLock(WellKnownResources.EnvironmentVariables)]
                public void DifferentResource() => {|#1:Directory.SetCurrentDirectory("fixture")|};
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(CurrentDirectoryMutationUnderParallelizationAnalyzer.Rule).WithLocation(0).WithArguments("Directory.SetCurrentDirectory"),
            VerifyCS.Diagnostic(CurrentDirectoryMutationUnderParallelizationAnalyzer.Rule).WithLocation(1).WithArguments("Directory.SetCurrentDirectory"));
    }

    [TestMethod]
    public async Task WhenCurrentDirectoryModeIsDefaultExplicitOrUnknown_NoDiagnostic()
    {
        const string code = """
            using System.IO;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            [assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]

            [TestClass]
            public class Tests
            {
                [TestMethod]
                [ResourceLock(WellKnownResources.CurrentDirectory)]
                public void Default() => Directory.SetCurrentDirectory("fixture");

                [TestMethod]
                [ResourceLock(WellKnownResources.CurrentDirectory, Mode = ResourceAccessMode.ReadWrite)]
                public void Explicit() => Directory.SetCurrentDirectory("fixture");

                [TestMethod]
                [ResourceLock(WellKnownResources.CurrentDirectory, Mode = default)]
                public void ExplicitDefault() => Directory.SetCurrentDirectory("fixture");

                [TestMethod]
                [ResourceLock(WellKnownResources.CurrentDirectory, Mode = (ResourceAccessMode)42)]
                public void Unknown() => Directory.SetCurrentDirectory("fixture");
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenEquivalentConstantCurrentDirectoryKeyHasExclusiveLock_NoDiagnostic()
    {
        const string code = """
            using System.IO;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            [assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]

            [TestClass]
            public class Tests
            {
                [TestMethod]
                [ResourceLock("System.Environment." + "CurrentDirectory")]
                public void Check() => Directory.SetCurrentDirectory("fixture");
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenInheritedCurrentDirectoryClassLocksIncludeWrite_NoDiagnostic()
    {
        const string code = """
            using System.IO;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            [assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]

            [ResourceLock(WellKnownResources.CurrentDirectory, Mode = ResourceAccessMode.Read)]
            public class ReadClassBase { }

            [ResourceLock(WellKnownResources.CurrentDirectory)]
            public class WriteClassBase { }

            [TestClass]
            [ResourceLock(WellKnownResources.CurrentDirectory)]
            public class DerivedClassWrite : ReadClassBase
            {
                [TestMethod]
                public void Check() => Directory.SetCurrentDirectory("fixture");
            }

            [TestClass]
            [ResourceLock(WellKnownResources.CurrentDirectory, Mode = ResourceAccessMode.Read)]
            public class InheritedClassWrite : WriteClassBase
            {
                [TestMethod]
                public void Check() => Directory.SetCurrentDirectory("fixture");
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenInheritedCurrentDirectoryMethodLocksIncludeWrite_NoDiagnostic()
    {
        const string code = """
            using System.IO;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            [assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]

            public class ReadMethodBase
            {
                [TestMethod]
                [ResourceLock(WellKnownResources.CurrentDirectory, Mode = ResourceAccessMode.Read)]
                public virtual void Check() { }
            }

            public class WriteMethodBase
            {
                [TestMethod]
                [ResourceLock(WellKnownResources.CurrentDirectory)]
                public virtual void Check() { }
            }

            [TestClass]
            public class DerivedMethodWrite : ReadMethodBase
            {
                [TestMethod]
                [ResourceLock(WellKnownResources.CurrentDirectory)]
                public override void Check() => Directory.SetCurrentDirectory("fixture");
            }

            [TestClass]
            public class InheritedMethodWrite : WriteMethodBase
            {
                [TestMethod]
                [ResourceLock(WellKnownResources.CurrentDirectory, Mode = ResourceAccessMode.Read)]
                public override void Check() => Directory.SetCurrentDirectory("fixture");
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenCurrentDirectoryOverrideWithoutTestAttributeHasInheritedReadLock_NoDiagnostic()
    {
        // TestMethodAttribute is not inherited, unlike ResourceLockAttribute.
        const string code = """
            using System.IO;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            [assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]

            public class Base
            {
                [TestMethod]
                [ResourceLock(WellKnownResources.CurrentDirectory, Mode = ResourceAccessMode.Read)]
                public virtual void Check() { }
            }

            [TestClass]
            public class Tests : Base
            {
                public override void Check() => Directory.SetCurrentDirectory("fixture");
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenCurrentDirectoryReadLockIsInheritedFromBaseClass_Diagnostic()
    {
        const string code = """
            using System.IO;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            [assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]

            [ResourceLock(WellKnownResources.CurrentDirectory, Mode = ResourceAccessMode.Read)]
            public class Base { }

            [TestClass]
            public class Tests : Base
            {
                [TestMethod]
                public void Check() => {|#0:Directory.SetCurrentDirectory("fixture")|};
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(CurrentDirectoryMutationUnderParallelizationAnalyzer.Rule)
                .WithLocation(0)
                .WithArguments("Directory.SetCurrentDirectory"));
    }

    [TestMethod]
    public async Task WhenCurrentDirectoryReadLockIsInheritedFromOverriddenMethod_Diagnostic()
    {
        const string code = """
            using System.IO;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            [assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]

            public class Base
            {
                [TestMethod]
                [ResourceLock(WellKnownResources.CurrentDirectory, Mode = ResourceAccessMode.Read)]
                public virtual void Check() { }
            }

            [TestClass]
            public class Tests : Base
            {
                [TestMethod]
                public override void Check() => {|#0:Directory.SetCurrentDirectory("fixture")|};
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(CurrentDirectoryMutationUnderParallelizationAnalyzer.Rule)
                .WithLocation(0)
                .WithArguments("Directory.SetCurrentDirectory"));
    }

    [TestMethod]
    public async Task WhenInheritedTestMethodHasReadLockedDeclaringClass_Diagnostic()
    {
        const string code = """
            using System.IO;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            [assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]

            [ResourceLock(WellKnownResources.CurrentDirectory, Mode = ResourceAccessMode.Read)]
            public class Base
            {
                [TestMethod]
                public void Check() => {|#0:Directory.SetCurrentDirectory("fixture")|};
            }

            [TestClass]
            public class Tests : Base { }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(CurrentDirectoryMutationUnderParallelizationAnalyzer.Rule)
                .WithLocation(0)
                .WithArguments("Directory.SetCurrentDirectory"));
    }

    [TestMethod]
    public async Task WhenSiblingMethodHasCurrentDirectoryWriteLock_ReadLockedMethodStillReports()
    {
        const string code = """
            using System.IO;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            [assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]

            [TestClass]
            public class Tests
            {
                [TestMethod]
                [ResourceLock(WellKnownResources.CurrentDirectory, Mode = ResourceAccessMode.Read)]
                public void Mutate() => {|#0:Directory.SetCurrentDirectory("fixture")|};

                [TestMethod]
                [ResourceLock(WellKnownResources.CurrentDirectory)]
                public void Other() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(CurrentDirectoryMutationUnderParallelizationAnalyzer.Rule)
                .WithLocation(0)
                .WithArguments("Directory.SetCurrentDirectory"));
    }

    [TestMethod]
    public async Task WhenCurrentDirectoryReadLockIsCombinedWithParallelizationOptOut_NoDiagnostic()
    {
        const string classAndMethodOptOut = """
            using System.IO;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            [assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]

            [TestClass]
            [DoNotParallelize]
            [ResourceLock(WellKnownResources.CurrentDirectory, Mode = ResourceAccessMode.Read)]
            public class OptedOut
            {
                [TestMethod]
                public void Check() => Directory.SetCurrentDirectory("fixture");
            }

            [TestClass]
            public class MethodOptOut
            {
                [TestMethod]
                [DoNotParallelize]
                [ResourceLock(WellKnownResources.CurrentDirectory, Mode = ResourceAccessMode.Read)]
                public void Check() => Directory.SetCurrentDirectory("fixture");
            }
            """;

        const string assemblyOptOut = """
            using System.IO;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            [assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]
            [assembly: DoNotParallelize]

            [TestClass]
            [ResourceLock(WellKnownResources.CurrentDirectory, Mode = ResourceAccessMode.Read)]
            public class Tests
            {
                [TestMethod]
                public void Check() => Directory.SetCurrentDirectory("fixture");
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(classAndMethodOptOut);
        await VerifyCS.VerifyAnalyzerAsync(assemblyOptOut);
    }

    [TestMethod]
    public async Task WhenAssemblyFixtureHasCurrentDirectoryReadLock_NoDiagnostic()
    {
        const string code = """
            using System.IO;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            [assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]

            [TestClass]
            [ResourceLock(WellKnownResources.CurrentDirectory, Mode = ResourceAccessMode.Read)]
            public class Tests
            {
                [AssemblyInitialize]
                public static void Initialize(TestContext context) => Directory.SetCurrentDirectory("fixture");

                [AssemblyCleanup]
                public static void Cleanup() => Directory.SetCurrentDirectory("fixture");
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenGlobalFixtureHasCurrentDirectoryReadClassLock_DiagnosticWithoutFix()
    {
        const string code = """
            using System.IO;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            [assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]

            [TestClass]
            [ResourceLock(WellKnownResources.CurrentDirectory, Mode = ResourceAccessMode.Read)]
            public class Tests
            {
                [GlobalTestInitialize]
                public static void Initialize(TestContext context)
                {
                    {|#0:Directory.SetCurrentDirectory("fixture")|};
                }

                [TestMethod]
                public void Check() { }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic(CurrentDirectoryMutationUnderParallelizationAnalyzer.Rule)
                .WithLocation(0)
                .WithArguments("Directory.SetCurrentDirectory"),
            code);
    }
}
