// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.SharedFileSystemPathInTestAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = MSTest.Analyzers.Test.VisualBasicCodeFixVerifier<
    MSTest.Analyzers.SharedFileSystemPathInTestAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class SharedFileSystemPathInTestAnalyzerTests
{
    [TestMethod]
    public async Task WhenTestMethodWritesAbsolutePathLiteral_Diagnostic()
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
                    [|File.WriteAllText("C:\\temp\\shared.txt", "data")|];
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenTestMethodWritesUnixAbsolutePathLiteral_Diagnostic()
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
                    [|File.WriteAllText("/tmp/shared.txt", "data")|];
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenTestMethodWritesRelativePathLiteral_Diagnostic()
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
                    [|File.WriteAllText("output.txt", "data")|];
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenTestMethodCreatesDirectoryWithConstantPath_Diagnostic()
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
                    [|Directory.CreateDirectory("shared-dir")|];
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenTestMethodUsesSharedTempCombine_NoDiagnostic()
    {
        // Path.Combine merely constructs a string; the analyzer sees the call, not any colliding I/O. Dogfooding
        // proved the constructed temp path is overwhelmingly used as a hash input, a "does-not-exist" sentinel, or
        // mock data rather than for a shared write, so flagging construction is a false-positive generator. Real
        // shared-temp-file writes are caught at the File.WriteAllText/Directory.* call site instead.
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
                    string path = Path.Combine(Path.GetTempPath(), "shared.txt");
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenTestMethodReadsConstantPath_NoDiagnostic()
    {
        // Reads are excluded: reading a shared fixture at a fixed path is common and safe. Only mutating APIs fire.
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
                    string content = File.ReadAllText("fixture.txt");
                    bool exists = File.Exists("fixture.txt");
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenTestMethodUsesVariablePath_NoDiagnostic()
    {
        // The analyzer sees the call, not the resource; a variable path may be unique per test, so it must
        // stay silent. Tracing paths through helpers belongs to the parallel-safety-audit skill, not here.
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
                    string path = GetPath();
                    File.WriteAllText(path, "data");
                }

                private static string GetPath() => "x";
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenTestMethodUsesUniqueTempFileName_NoDiagnostic()
    {
        // Path.Combine(GetTempPath(), <non-constant>) is a per-test unique path and must stay silent.
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
                    string path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
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
                    File.WriteAllText("/tmp/shared.txt", "data");
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
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
                    File.WriteAllText("/tmp/shared.txt", "data");
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenTestMethodWritesAbsolutePathLiteral_VisualBasic_Diagnostic()
    {
        string code = """
            Imports System.IO
            Imports Microsoft.VisualStudio.TestTools.UnitTesting

            <Assembly: Parallelize(Workers:=0, Scope:=ExecutionScope.MethodLevel)>

            <TestClass>
            Public Class MyTestClass
                <TestMethod>
                Public Sub MyTestMethod()
                    [|File.WriteAllText("/tmp/shared.txt", "data")|]
                End Sub
            End Class
            """;

        await VerifyVB.VerifyAnalyzerAsync(code);
    }
}
