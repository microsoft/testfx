// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using Microsoft.Testing.Platform.Helpers;

using Moq;

namespace Microsoft.Testing.Platform.MSBuild.UnitTests;

[TestClass]
public sealed class InvokeTestingPlatformTaskTests
{
    [TestMethod]
    [DataRow("##[group]Tests: MyAssembly (net9.0)", true)]
    [DataRow("##[endgroup]", true)]
    [DataRow("##[section]Section", true)]
    [DataRow("##vso[task.logissue type=error]boom", true)]
    [DataRow("##vso[task.uploadsummary]/path/to/summary.md", true)]
    [DataRow("Passed! - MyTest (1ms)", false)]
    [DataRow("Running tests: MyAssembly.dll", false)]
    [DataRow("  ##[group]indented is not a command", false)]
    [DataRow("#[group]not-a-command", false)]
    [DataRow("", false)]
    public void IsAzureDevOpsLoggingCommand_ClassifiesLinesCorrectly(string line, bool expected)
        => Assert.AreEqual(expected, InvokeTestingPlatformTask.IsAzureDevOpsLoggingCommand(line));

    [TestMethod]
    public void LogEventsFromTextOutput_AzureDevOpsCommands_AreWrittenToStdoutAtColumnZero_NotThroughMSBuildLog()
    {
        List<string> loggedMessages = [];
        Mock<IBuildEngine> buildEngine = new();
        buildEngine
            .Setup(x => x.LogMessageEvent(It.IsAny<BuildMessageEventArgs>()))
            .Callback<BuildMessageEventArgs>(e => loggedMessages.Add(e.Message ?? string.Empty));

        TestableInvokeTestingPlatformTask task = new() { BuildEngine = buildEngine.Object };

        TextWriter originalOut = Console.Out;
        using StringWriter capturedStdout = new();
        Console.SetOut(capturedStdout);
        try
        {
            task.InvokeLogEventsFromTextOutput("##[group]Tests: MyAssembly (net9.0)");
            task.InvokeLogEventsFromTextOutput("##[endgroup]");
            task.InvokeLogEventsFromTextOutput("normal output line");
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        string[] stdoutLines = capturedStdout.ToString().Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);

        // Azure DevOps commands are written verbatim to stdout at column 0 (exact element match => no indentation).
        Assert.IsTrue(stdoutLines.Contains("##[group]Tests: MyAssembly (net9.0)"), capturedStdout.ToString());
        Assert.IsTrue(stdoutLines.Contains("##[endgroup]"), capturedStdout.ToString());

        // ...and are NOT routed through the MSBuild logger, which would indent them and break Azure DevOps parsing.
        string joinedMessages = string.Join('\n', loggedMessages);
        Assert.DoesNotContain("##[group]", joinedMessages);
        Assert.DoesNotContain("##[endgroup]", joinedMessages);

        // Regular output keeps flowing through the MSBuild logger and does not leak to stdout.
        Assert.Contains("normal output line", loggedMessages);
        Assert.DoesNotContain("normal output line", capturedStdout.ToString());
    }

    private sealed class TestableInvokeTestingPlatformTask : InvokeTestingPlatformTask
    {
        public TestableInvokeTestingPlatformTask()
            : base(new StubFileSystem())
        {
        }

        public void InvokeLogEventsFromTextOutput(string singleLine)
            => LogEventsFromTextOutput(singleLine, MessageImportance.High);
    }

    private sealed class StubFileSystem : IFileSystem
    {
        public bool Exist(string path) => false;

        public void CreateDirectory(string directory) => throw new NotSupportedException();

        public Stream CreateNew(string path) => throw new NotSupportedException();

        public void CopyFile(string source, string destination) => throw new NotSupportedException();

        public void WriteAllText(string path, string? contents) => throw new NotSupportedException();
    }
}
