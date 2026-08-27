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
    [DataRow("AppendAllText", "\"shared.txt\", \"entry\"")]
    [DataRow("AppendAllLines", "\"shared.txt\", new[] { \"line\" }")]
    [DataRow("WriteAllBytes", "\"shared.txt\", new byte[] { 1 }")]
    [DataRow("WriteAllLines", "\"shared.txt\", new[] { \"line\" }")]
    [DataRow("Create", "\"shared.txt\"")]
    [DataRow("SetAttributes", "\"shared.txt\", FileAttributes.ReadOnly")]
    [DataRow("SetUnixFileMode", "\"shared.txt\", UnixFileMode.UserRead")]
    [DataRow("SetCreationTime", "\"shared.txt\", default")]
    [DataRow("SetCreationTimeUtc", "\"shared.txt\", default")]
    [DataRow("SetLastAccessTime", "\"shared.txt\", default")]
    [DataRow("SetLastAccessTimeUtc", "\"shared.txt\", default")]
    [DataRow("SetLastWriteTime", "\"shared.txt\", default")]
    [DataRow("SetLastWriteTimeUtc", "\"shared.txt\", default")]
    [DataRow("Encrypt", "\"shared.txt\"")]
    [DataRow("Decrypt", "\"shared.txt\"")]
    [DataRow("AppendText", "\"shared.txt\"")]
    [DataRow("CreateText", "\"shared.txt\"")]
    [DataRow("CreateSymbolicLink", "\"shared.txt\", \"target.txt\"")]
    public async Task WhenTestMethodCallsMutatingFileMethodWithConstantPath_Diagnostic(string methodName, string arguments)
    {
        string code = $$"""
            using System.IO;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: Parallelize(Workers = 0, Scope = ExecutionScope.MethodLevel)]

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    {|#0:File.{{methodName}}({{arguments}})|};
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
    public async Task WhenTestMethodCreatesFileSymbolicLinkToConstantTarget_NoDiagnostic()
    {
        // Only 'path' is created by File.CreateSymbolicLink; 'pathToTarget' merely names the file the link points at,
        // so a constant target - like a shared fixture file - must not be flagged.
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
                    string link = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                    File.CreateSymbolicLink(link, "shared-fixture.txt");
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    [DataRow("AppendAllTextAsync", "\"shared.txt\", \"entry\"")]
    [DataRow("AppendAllLinesAsync", "\"shared.txt\", new[] { \"line\" }")]
    [DataRow("WriteAllBytesAsync", "\"shared.txt\", new byte[] { 1 }")]
    [DataRow("WriteAllLinesAsync", "\"shared.txt\", new[] { \"line\" }")]
    [DataRow("WriteAllTextAsync", "\"shared.txt\", \"content\"")]
    public async Task WhenTestMethodCallsAsyncMutatingFileMethodWithConstantPath_Diagnostic(string methodName, string arguments)
    {
        string code = $$"""
            using System.IO;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: Parallelize(Workers = 0, Scope = ExecutionScope.MethodLevel)]

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    _ = {|#0:File.{{methodName}}({{arguments}})|};
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
    public async Task WhenAssemblyInitializeWritesConstantPath_NoDiagnostic()
    {
        // [AssemblyInitialize] is serialized before any tests run, so its mutation cannot race a concurrent test.
        string code = """
            using System.IO;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: Parallelize(Workers = 0, Scope = ExecutionScope.MethodLevel)]

            [TestClass]
            public class MyTestClass
            {
                [AssemblyInitialize]
                public static void AssemblySetup(TestContext context)
                {
                    File.WriteAllText("setup.txt", "data");
                }

                [TestMethod]
                public void MyTestMethod() { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenGlobalTestInitializeWritesConstantPath_Diagnostic()
    {
        // [GlobalTestInitialize] runs around every test and can race concurrent tests.
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
        // File.Delete removes the entry named by its single 'path' argument, so a constant path is a mutation
        // that races with any other test touching the same file.
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
        // Directory.Delete mutates the entry named by 'path'; the recursive overload adds only a bool, which the
        // string-typed parameter filter skips, so the constant directory path is still the reported argument.
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
                    {|#0:Directory.Delete("shared-dir", recursive: true)|};
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
    public async Task WhenTestMethodMovesDirectoryFromConstantSource_Diagnostic()
    {
        // Directory.Move deletes its source directory, so a constant 'sourceDirName' is a mutation and must be
        // flagged even when the destination is per-test unique.
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
                    string destination = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                    {|#0:Directory.Move("source-dir", destination)|};
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(SharedFileSystemPathInTestAnalyzer.Rule)
                .WithLocation(0)
                .WithArguments("source-dir"));
    }

    [TestMethod]
    public async Task WhenTestMethodMovesDirectoryToConstantDestination_Diagnostic()
    {
        // Directory.Move also creates its destination directory, so a constant 'destDirName' is a mutation on its
        // own, even when the source is per-test unique.
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
                    string source = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                    {|#0:Directory.Move(source, "dest-dir")|};
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(SharedFileSystemPathInTestAnalyzer.Rule)
                .WithLocation(0)
                .WithArguments("dest-dir"));
    }

    [TestMethod]
    public async Task WhenTestMethodMovesDirectoryBetweenConstantPaths_SingleDiagnosticForSource()
    {
        // Both Directory.Move positions are mutations, but the analyzer reports the first constant mutated path it
        // finds rather than one diagnostic per position, so this invocation yields a single diagnostic naming the
        // source. Pinning that keeps the "report once per invocation" contract observable.
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

    [TestMethod]
    public async Task WhenTestMethodReadsConstantDirectoryPath_NoDiagnostic()
    {
        // The Directory.* allowlist must stay as narrow as the File.* one: enumerating or probing a shared directory
        // at a fixed path does not mutate it, so the delete/move coverage above is pinned from both directions.
        // 'Directory.GetLastWriteTime' additionally pins the Get*/Set* asymmetry: only the Set*Time* family mutates.
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
                    bool exists = Directory.Exists("shared-dir");
                    string[] files = Directory.GetFiles("shared-dir");
                    DateTime lastWrite = Directory.GetLastWriteTime("shared-dir");
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenTestMethodCreatesDirectorySymbolicLinkWithConstantPath_Diagnostic()
    {
        // Directory.CreateSymbolicLink creates the entry named by 'path', so a constant link path collides with any
        // other test creating the same link.
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
                    string target = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                    {|#0:Directory.CreateSymbolicLink("shared-link", target)|};
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(SharedFileSystemPathInTestAnalyzer.Rule)
                .WithLocation(0)
                .WithArguments("shared-link"));
    }

    [TestMethod]
    public async Task WhenTestMethodCreatesDirectorySymbolicLinkToConstantTarget_NoDiagnostic()
    {
        // Only 'path' is created by Directory.CreateSymbolicLink; 'pathToTarget' merely names an existing directory
        // the link points at, so a constant target - like a shared fixture directory - must not be flagged.
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
                    string link = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                    Directory.CreateSymbolicLink(link, "shared-fixture-dir");
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    [DataRow("SetCreationTime")]
    [DataRow("SetCreationTimeUtc")]
    [DataRow("SetLastAccessTime")]
    [DataRow("SetLastAccessTimeUtc")]
    [DataRow("SetLastWriteTime")]
    [DataRow("SetLastWriteTimeUtc")]
    public async Task WhenTestMethodSetsDirectoryTimestampWithConstantPath_Diagnostic(string methodName)
    {
        // The Directory.Set*Time* family rewrites the metadata of the entry named by 'path'. That is a mutation two
        // parallel tests can interleave on, so every member of the allowlist arm must fire on a constant path. The
        // timestamp value is irrelevant here - only string-typed parameters are inspected - so 'default' keeps the
        // snippet neutral between the local-time and the *Utc members.
        string code = $$"""
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
                    {|#0:Directory.{{methodName}}("shared-dir", default(DateTime))|};
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
    public async Task WhenDoNotParallelizeDeclared_NoDiagnostic()
    {
        // A class-level [DoNotParallelize] runs every test of the class in the sequential phase, so no other test can
        // be touching the same path at the same time and the collision risk the rule reports about disappears.
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
                    File.WriteAllText("shared.txt", "data");
                    File.Delete("shared.txt");
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenTestMethodHasMethodLevelDoNotParallelize_NoDiagnostic()
    {
        // A method-level [DoNotParallelize] is honored by discovery for real test methods, so it opts the mutation out
        // of the parallel phase just as the class-level attribute does.
        string code = """
            using System.IO;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: Parallelize(Workers = 0, Scope = ExecutionScope.MethodLevel)]

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [DoNotParallelize]
                public void MyTestMethod()
                {
                    File.Delete("shared.txt");
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenFixtureMethodHasMethodLevelDoNotParallelize_Diagnostic()
    {
        // [DoNotParallelize] on a fixture method is ignored at runtime - only test methods carry the flag through
        // discovery - so it must not silence a constant-path mutation inside [TestInitialize].
        string code = """
            using System.IO;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: Parallelize(Workers = 0, Scope = ExecutionScope.MethodLevel)]

            [TestClass]
            public class MyTestClass
            {
                [TestInitialize]
                [DoNotParallelize]
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
}
