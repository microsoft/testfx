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
                    [|Directory.SetCurrentDirectory("/tmp")|];
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

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
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
                    [|Environment.CurrentDirectory = "/tmp"|];
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

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
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
                    [|Directory.SetCurrentDirectory("/tmp")|]
                End Sub
            End Class
            """;

        await VerifyVB.VerifyAnalyzerAsync(code);
    }
}
