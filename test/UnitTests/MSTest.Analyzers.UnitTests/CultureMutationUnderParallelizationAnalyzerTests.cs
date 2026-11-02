// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.CultureMutationUnderParallelizationAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = MSTest.Analyzers.Test.VisualBasicCodeFixVerifier<
    MSTest.Analyzers.CultureMutationUnderParallelizationAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class CultureMutationUnderParallelizationAnalyzerTests
{
    [TestMethod]
    public async Task WhenTestMethodSetsDefaultThreadCurrentCulture_Diagnostic()
    {
        string code = """
            using System.Globalization;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: Parallelize(Workers = 0, Scope = ExecutionScope.MethodLevel)]

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    {|#0:CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture|};
                }
            }
            """;

        // Assert the rendered API argument in the message, not just the diagnostic's presence, so a change to the
        // message format or to the reported API name is caught.
        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(CultureMutationUnderParallelizationAnalyzer.Rule)
                .WithLocation(0)
                .WithArguments("CultureInfo.DefaultThreadCurrentCulture"));
    }

    [TestMethod]
    public async Task WhenTestMethodSetsDefaultThreadCurrentUICulture_Diagnostic()
    {
        string code = """
            using System.Globalization;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: Parallelize(Workers = 0, Scope = ExecutionScope.MethodLevel)]

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    {|#0:CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture|};
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(CultureMutationUnderParallelizationAnalyzer.Rule)
                .WithLocation(0)
                .WithArguments("CultureInfo.DefaultThreadCurrentUICulture"));
    }

    [TestMethod]
    public async Task WhenTestMethodSetsThreadCurrentCulture_NoDiagnostic()
    {
        // Thread.CurrentThread.CurrentCulture / CurrentUICulture setters are intentionally OMITTED. On modern .NET
        // they delegate to an AsyncLocal-backed value that flows with the ExecutionContext, so the mutation does not
        // corrupt sibling tests or leak onto a later-pooled thread. Only the process-wide DefaultThreadCurrent* forms
        // are flagged.
        string code = """
            using System.Globalization;
            using System.Threading;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: Parallelize(Workers = 0, Scope = ExecutionScope.MethodLevel)]

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
                    Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenTestMethodNullCoalesceAssignsDefaultThreadCurrentCulture_Diagnostic()
    {
        // A null-coalescing assignment ('??=') still writes the static field when it is null, so it must be flagged
        // just like a plain assignment.
        string code = """
            using System.Globalization;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: Parallelize(Workers = 0, Scope = ExecutionScope.MethodLevel)]

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    {|#0:CultureInfo.DefaultThreadCurrentCulture ??= CultureInfo.InvariantCulture|};
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(CultureMutationUnderParallelizationAnalyzer.Rule)
                .WithLocation(0)
                .WithArguments("CultureInfo.DefaultThreadCurrentCulture"));
    }

    [TestMethod]
    public async Task WhenTestMethodSetsCurrentCulture_NoDiagnostic()
    {
        // CultureInfo.CurrentCulture / CurrentUICulture setters are intentionally OMITTED: on modern .NET the
        // value flows with the ExecutionContext and does not corrupt sibling test contexts.
        string code = """
            using System.Globalization;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: Parallelize(Workers = 0, Scope = ExecutionScope.MethodLevel)]

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
                    CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenResourceLockDeclared_NoDiagnostic()
    {
        // Any declared [ResourceLock] is treated as the author having coordinated culture access.
        string code = """
            using System.Globalization;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: Parallelize(Workers = 0, Scope = ExecutionScope.MethodLevel)]

            [TestClass]
            public class MyTestClass
            {
                [ResourceLock("Culture")]
                [TestMethod]
                public void MyTestMethod()
                {
                    CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenDoNotParallelizeDeclared_NoDiagnostic()
    {
        string code = """
            using System.Globalization;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: Parallelize(Workers = 0, Scope = ExecutionScope.MethodLevel)]

            [TestClass]
            [DoNotParallelize]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenParallelizeNotDeclared_NoDiagnostic()
    {
        string code = """
            using System.Globalization;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenReadingDefaultThreadCurrentCulture_NoDiagnostic()
    {
        string code = """
            using System.Globalization;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: Parallelize(Workers = 0, Scope = ExecutionScope.MethodLevel)]

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    CultureInfo culture = CultureInfo.DefaultThreadCurrentCulture;
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenNonTestClass_NoDiagnostic()
    {
        string code = """
            using System.Globalization;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: Parallelize(Workers = 0, Scope = ExecutionScope.MethodLevel)]

            public class NotATest
            {
                public void SomeMethod()
                {
                    CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenTestMethodSetsDefaultThreadCurrentCulture_VisualBasic_Diagnostic()
    {
        string code = """
            Imports System.Globalization
            Imports Microsoft.VisualStudio.TestTools.UnitTesting

            <Assembly: Parallelize(Workers:=0, Scope:=ExecutionScope.MethodLevel)>

            <TestClass>
            Public Class MyTestClass
                <TestMethod>
                Public Sub MyTestMethod()
                    {|#0:CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture|}
                End Sub
            End Class
            """;

        await VerifyVB.VerifyAnalyzerAsync(
            code,
            VerifyVB.Diagnostic(CultureMutationUnderParallelizationAnalyzer.Rule)
                .WithLocation(0)
                .WithArguments("CultureInfo.DefaultThreadCurrentCulture"));
    }

    [TestMethod]
    public async Task WhenTestInitializeSetsDefaultThreadCurrentCulture_Diagnostic()
    {
        // [TestInitialize] is a class-scoped fixture included in GetFixtureAttributeSymbols, so mutations inside
        // it must be reported just like mutations inside a test method.
        string code = """
            using System.Globalization;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: Parallelize(Workers = 0, Scope = ExecutionScope.MethodLevel)]

            [TestClass]
            public class MyTestClass
            {
                [TestInitialize]
                public void Setup()
                {
                    {|#0:CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture|};
                }

                [TestMethod]
                public void MyTestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(CultureMutationUnderParallelizationAnalyzer.Rule)
                .WithLocation(0)
                .WithArguments("CultureInfo.DefaultThreadCurrentCulture"));
    }

    [TestMethod]
    public async Task WhenAssemblyInitializeSetsDefaultThreadCurrentCulture_NoDiagnostic()
    {
        // [AssemblyInitialize] is deliberately excluded from GetFixtureAttributeSymbols: it is serialized and
        // every worker awaits it before running any test, so a process-global mutation there cannot race a
        // concurrent test. Flagging it would be a false positive.
        string code = """
            using System.Globalization;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: Parallelize(Workers = 0, Scope = ExecutionScope.MethodLevel)]

            [TestClass]
            public class MyTestClass
            {
                [AssemblyInitialize]
                public static void AssemblySetup(TestContext context)
                {
                    CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
                }

                [TestMethod]
                public void MyTestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenGlobalTestInitializeSetsDefaultThreadCurrentCulture_Diagnostic()
    {
        // Unlike assembly initialization, a global fixture runs around every test and can race concurrent tests.
        string code = """
            using System.Globalization;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: Parallelize(Workers = 0, Scope = ExecutionScope.MethodLevel)]

            [TestClass]
            public class MyTestClass
            {
                [GlobalTestInitialize]
                public static void GlobalSetup(TestContext context)
                {
                    {|#0:CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture|};
                }

                [TestMethod]
                public void MyTestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(CultureMutationUnderParallelizationAnalyzer.Rule)
                .WithLocation(0)
                .WithArguments("CultureInfo.DefaultThreadCurrentCulture"));
    }
}
