// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.UseExecutableConditionAttributeInsteadOfProcessCheckAnalyzer,
    MSTest.Analyzers.UseExecutableConditionAttributeInsteadOfProcessCheckFixer>;

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class UseExecutableConditionAttributeInsteadOfProcessCheckAnalyzerTests
{
    [TestMethod]
    [DataRow("false == File.Exists(\"tools/my-tool\")", DisplayName = "FalseOnLeft")]
    [DataRow("true != File.Exists(\"tools/my-tool\")", DisplayName = "TrueInequalityOnLeft")]
    [DataRow("File.Exists(\"tools/my-tool\") != true", DisplayName = "TrueInequalityOnRight")]
    public async Task EquivalentMissingFileComparisons_ProduceTheSameFullFix(string condition)
    {
        string source = $$"""
            using System.Diagnostics;
            using System.IO;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class Tests
            {
                [TestMethod]
                public void Check()
                {
                    [|if ({{condition}})
                    {
                        return;
                    }|]
                    Process.Start("tools/my-tool");
                }
            }
            """;
        const string expected = """
            using System.Diagnostics;
            using System.IO;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class Tests
            {
                [TestMethod]
                [ExecutableCondition("tools/my-tool")]
                public void Check()
                {
                    Process.Start("tools/my-tool");
                }
            }
            """;
        await VerifyCS.VerifyCodeFixAsync(source, expected);
    }

    [TestMethod]
    [DataRow("File.Exists(\"tool\") == true", "Process.Start(\"tool\")", DisplayName = "ExistsRatherThanMissing")]
    [DataRow("true == File.Exists(\"tool\")", "Process.Start(\"tool\")", DisplayName = "ReversedExistsComparison")]
    [DataRow("File.Exists(\"tool\") == flag", "Process.Start(\"tool\")", DisplayName = "RuntimeBoolean")]
    [DataRow("File.Exists(path) == false", "Process.Start(\"tool\")", DisplayName = "RuntimeCheckedPath")]
    [DataRow("!File.Exists(\"\")", "Process.Start(\"tool\")", DisplayName = "EmptyCheckedPath")]
    [DataRow("File.Exists(\"tool\")", "Process.Start(\"tool\")", DisplayName = "PositiveCheck")]
    [DataRow("!File.Exists(\"tool\")", "Process.Start(info)", DisplayName = "RuntimeStartInfo")]
    [DataRow("!File.Exists(\"tool\")", "Process.Start(new ProcessStartInfo())", DisplayName = "StartInfoWithoutFileName")]
    [DataRow("!File.Exists(\"tool\")", "Process.Start(new ProcessStartInfo(path))", DisplayName = "RuntimeConstructorFileName")]
    public async Task UnprovenMissingExecutableChecks_DoNotOfferAnAttribute(string condition, string start)
    {
        string source = $$"""
            using System.Diagnostics;
            using System.IO;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class Tests
            {
                [TestMethod]
                public void Check(bool flag, string path, ProcessStartInfo info)
                {
                    if ({{condition}})
                    {
                        return;
                    }
                    {{start}};
                }
            }
            """;
        await VerifyCS.VerifyAnalyzerAsync(source);
    }

    [TestMethod]
    public async Task WhenMissingExecutableGuardPrecedesProcessStart_Diagnostic()
    {
        string code = """
            using System.Diagnostics;
            using System.IO;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    [|if (!File.Exists("tools/my-tool"))
                    {
                        return;
                    }|]

                    Process.Start("tools/my-tool");
                }
            }
            """;

        string fixedCode = """
            using System.Diagnostics;
            using System.IO;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [ExecutableCondition("tools/my-tool")]
                public void TestMethod()
                {
                    Process.Start("tools/my-tool");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenFileExistsIsComparedToFalseAndProcessStartInfoIsUsed_Diagnostic()
    {
        string code = """
            using System.Diagnostics;
            using System.IO;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    [|if (File.Exists("git") == false)
                    {
                        Assert.Inconclusive("git is required");
                    }|]

                    Process.Start(new ProcessStartInfo("git", "--version"));
                }
            }
            """;

        string fixedCode = """
            using System.Diagnostics;
            using System.IO;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [ExecutableCondition("git")]
                public void TestMethod()
                {
                    Process.Start(new ProcessStartInfo("git", "--version"));
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenConstantExecutableIsUsed_Diagnostic()
    {
        string code = """
            using System.Diagnostics;
            using System.IO;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                private const string Tool = "dotnet";

                [TestMethod]
                public void TestMethod()
                {
                    [|if (File.Exists(Tool) is false)
                    {
                        return;
                    }|]

                    Process.Start(Tool, "--info");
                }
            }
            """;

        string fixedCode = """
            using System.Diagnostics;
            using System.IO;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                private const string Tool = "dotnet";

                [TestMethod]
                [ExecutableCondition("dotnet")]
                public void TestMethod()
                {
                    Process.Start(Tool, "--info");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenProcessStartUsesOutOfOrderNamedArguments_Diagnostic()
    {
        string code = """
            using System.Diagnostics;
            using System.IO;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    [|if (!File.Exists("git"))
                    {
                        return;
                    }|]

                    Process.Start(arguments: "--version", fileName: "git");
                }
            }
            """;

        string fixedCode = """
            using System.Diagnostics;
            using System.IO;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [ExecutableCondition("git")]
                public void TestMethod()
                {
                    Process.Start(arguments: "--version", fileName: "git");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenProcessStartArgumentsMatchCheckedFileButFileNameDoesNot_NoDiagnostic()
    {
        string code = """
            using System.Diagnostics;
            using System.IO;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    if (!File.Exists("--version"))
                    {
                        return;
                    }

                    Process.Start(arguments: "--version", fileName: "git");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenProcessStartInfoUsesOutOfOrderNamedArguments_Diagnostic()
    {
        string code = """
            using System.Diagnostics;
            using System.IO;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    [|if (!File.Exists("git"))
                    {
                        return;
                    }|]

                    Process.Start(new ProcessStartInfo(arguments: "--version", fileName: "git"));
                }
            }
            """;

        string fixedCode = """
            using System.Diagnostics;
            using System.IO;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [ExecutableCondition("git")]
                public void TestMethod()
                {
                    Process.Start(new ProcessStartInfo(arguments: "--version", fileName: "git"));
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenProcessStartInfoInitializerOverridesCheckedFile_NoDiagnostic()
    {
        string code = """
            using System.Diagnostics;
            using System.IO;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    if (!File.Exists("git"))
                    {
                        return;
                    }

                    Process.Start(new ProcessStartInfo("git") { FileName = "dotnet" });
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenCheckedFileIsNotStarted_NoDiagnostic()
    {
        string code = """
            using System.Diagnostics;
            using System.IO;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    if (!File.Exists("input.json"))
                    {
                        return;
                    }

                    Process.Start("dotnet");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenGuardIsNotFirstStatement_NoDiagnostic()
    {
        string code = """
            using System.Diagnostics;
            using System.IO;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.IsTrue(true);

                    if (!File.Exists("dotnet"))
                    {
                        return;
                    }

                    Process.Start("dotnet");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenMethodAlreadyHasCondition_NoDiagnostic()
    {
        string code = """
            using System.Diagnostics;
            using System.IO;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [ExecutableCondition("git")]
                public void TestMethod()
                {
                    if (!File.Exists("dotnet"))
                    {
                        return;
                    }

                    Process.Start("dotnet");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }
}
