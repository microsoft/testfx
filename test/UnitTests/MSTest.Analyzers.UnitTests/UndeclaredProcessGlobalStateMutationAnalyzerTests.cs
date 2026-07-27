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
                    [|Environment.SetEnvironmentVariable("MY_VAR", "value")|];
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

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
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
                    [|Console.SetOut(TextWriter.Null)|];
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

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
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
    public async Task WhenTestMethodSetsEnvironmentVariableInAssemblyInitialize_DiagnosticButNoFix()
    {
        // AssemblyInitialize is an assembly-scoped fixture: discovery reads resource locks only from the test class
        // and the test method, so neither a method-level nor a class-level lock would take effect for it. The rule
        // still reports the unprotected mutation, but the fixer offers NO fix (a fix that silently does nothing would
        // be worse than none), so the fixed code is identical to the input.
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
                    [|Environment.SetEnvironmentVariable("MY_VAR", "value")|];
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
                    [|Environment.SetEnvironmentVariable("MY_VAR", "value")|]
                End Sub
            End Class
            """;

        await VerifyVB.VerifyAnalyzerAsync(code);
    }
}
