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
                    {|#0:File.WriteAllText("C:\\temp\\shared.txt", "data")|};
                }
            }
            """;

        // Assert the offending path rendered in the message, not just the diagnostic's presence.
        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(SharedFileSystemPathInTestAnalyzer.Rule)
                .WithLocation(0)
                .WithArguments("C:\\temp\\shared.txt"));
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
                    {|#0:File.WriteAllText("/tmp/shared.txt", "data")|};
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(SharedFileSystemPathInTestAnalyzer.Rule)
                .WithLocation(0)
                .WithArguments("/tmp/shared.txt"));
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
                    {|#0:File.WriteAllText("output.txt", "data")|};
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(SharedFileSystemPathInTestAnalyzer.Rule)
                .WithLocation(0)
                .WithArguments("output.txt"));
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
                    {|#0:Directory.CreateDirectory("shared-dir")|};
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(SharedFileSystemPathInTestAnalyzer.Rule)
                .WithLocation(0)
                .WithArguments("shared-dir"));
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
    public async Task WhenTestMethodOpensFileForReading_NoDiagnostic()
    {
        // 'File.Open' can request a read-only handle via its FileMode/FileAccess arguments, so it is intentionally
        // excluded from the mutating allowlist - flagging every 'Open' would be a false positive. Only the
        // unambiguously-writing 'OpenWrite' is flagged.
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
                    using FileStream stream = File.Open("fixture.txt", FileMode.Open, FileAccess.Read);
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
                    {|#0:File.WriteAllText("/tmp/shared.txt", "data")|}
                End Sub
            End Class
            """;

        await VerifyVB.VerifyAnalyzerAsync(
            code,
            VerifyVB.Diagnostic(SharedFileSystemPathInTestAnalyzer.Rule)
                .WithLocation(0)
                .WithArguments("/tmp/shared.txt"));
    }

    [TestMethod]
    public async Task WhenTestMethodCopiesFromConstantSource_NoDiagnostic()
    {
        // File.Copy only reads its source, so a constant source path is safe (reading a shared fixture at a fixed
        // location does not race). The destination here is a variable, so nothing is flagged.
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
                    string destination = Path.GetTempFileName();
                    File.Copy("C:\\temp\\source.txt", destination);
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenTestMethodCopiesToConstantDestination_Diagnostic()
    {
        // File.Copy writes its destination, so a constant destination path is a mutation and must be flagged.
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
                    string source = Path.GetTempFileName();
                    {|#0:File.Copy(source, "C:\\temp\\dest.txt")|};
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(SharedFileSystemPathInTestAnalyzer.Rule)
                .WithLocation(0)
                .WithArguments("C:\\temp\\dest.txt"));
    }

    [TestMethod]
    public async Task WhenTestMethodReplacesWithConstantBackup_Diagnostic()
    {
        // File.Replace creates its backup file, so a constant backup path is a mutation even though the source is read.
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
                    string source = Path.GetTempFileName();
                    string destination = Path.GetTempFileName();
                    {|#0:File.Replace(source, destination, "C:\\temp\\shared.bak")|};
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(SharedFileSystemPathInTestAnalyzer.Rule)
                .WithLocation(0)
                .WithArguments("C:\\temp\\shared.bak"));
    }

    [TestMethod]
    public async Task WhenTestMethodMovesFromConstantSource_Diagnostic()
    {
        // File.Move deletes its source, so a constant source path is a mutation and must be flagged.
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
                    string destination = Path.GetTempFileName();
                    {|#0:File.Move("C:\\temp\\source.txt", destination)|};
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(SharedFileSystemPathInTestAnalyzer.Rule)
                .WithLocation(0)
                .WithArguments("C:\\temp\\source.txt"));
    }

    [TestMethod]
    public async Task WhenTestMethodOpensFileForWriting_Diagnostic()
    {
        // File.OpenWrite is the only File.Open* overload that is unambiguously a mutation (it always opens for
        // writing), so it must be flagged - unlike the general File.Open which can request a read-only handle.
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
                    using FileStream stream = {|#0:File.OpenWrite("shared.log")|};
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(SharedFileSystemPathInTestAnalyzer.Rule)
                .WithLocation(0)
                .WithArguments("shared.log"));
    }

    [TestMethod]
    public async Task WhenTestInitializeWritesConstantPath_Diagnostic()
    {
        // [TestInitialize] is a class-scoped fixture that runs before each test method.  Under method-level
        // parallelization it can race with other tests, so a constant-path write inside it must be flagged.
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
                    {|#0:File.WriteAllText("setup.txt", "data")|};
                }

                [TestMethod]
                public void MyTestMethod() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(SharedFileSystemPathInTestAnalyzer.Rule)
                .WithLocation(0)
                .WithArguments("setup.txt"));
    }

    [TestMethod]
    public async Task WhenTestMethodDeletesFileWithConstantPath_Diagnostic()
    {
        // File.Delete is a mutating operation: removing a shared file by constant path
        // is a race condition under method-level parallelization.
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
                    {|#0:File.Delete("shared.txt")|};
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(SharedFileSystemPathInTestAnalyzer.Rule)
                .WithLocation(0)
                .WithArguments("shared.txt"));
    }

    [TestMethod]
    public async Task WhenTestMethodDeletesDirectoryWithConstantPath_Diagnostic()
    {
        // Directory.Delete is a mutating operation: removing a shared directory by constant path
        // is a race condition under method-level parallelization.
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
                    {|#0:Directory.Delete("shared-dir")|};
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(SharedFileSystemPathInTestAnalyzer.Rule)
                .WithLocation(0)
                .WithArguments("shared-dir"));
    }

    [TestMethod]
    public async Task WhenTestMethodMovesDirectory_BothSourceAndDestAreDiagnostic()
    {
        // Directory.Move mutates both the source (it gets deleted) and the destination (it gets created).
        // The analyzer fires once per invocation with the first constant mutated path found.
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
                    {|#0:Directory.Move("source-dir", "dest-dir")|};
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(SharedFileSystemPathInTestAnalyzer.Rule)
                .WithLocation(0)
                .WithArguments("source-dir"));
    }
}
