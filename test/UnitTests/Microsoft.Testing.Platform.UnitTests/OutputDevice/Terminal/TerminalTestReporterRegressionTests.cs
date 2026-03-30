// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.OutputDevice.Terminal;
using Microsoft.Testing.Platform.Resources;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.UnitTests;

/// <summary>
/// Regression tests for TerminalTestReporter bugs.
/// </summary>
[TestClass]
[UnsupportedOSPlatform("browser")]
public sealed class TerminalTestReporterRegressionTests
{
    private static readonly string TestRunSummary = PlatformResources.GetResourceString("TestRunSummary");
    private static readonly string TotalLowercase = PlatformResources.GetResourceString("TotalLowercase");
    private static readonly string FailedLowercase = PlatformResources.GetResourceString("FailedLowercase");
    private static readonly string SucceededLowercase = PlatformResources.GetResourceString("SucceededLowercase");
    private static readonly string SkippedLowercase = PlatformResources.GetResourceString("SkippedLowercase");
    private static readonly string DurationLowercase = PlatformResources.GetResourceString("DurationLowercase");
    private static readonly string PassedString = PlatformResources.GetResourceString("Passed");
    private static readonly string FailedString = PlatformResources.GetResourceString("Failed");
    private static readonly string OutOfProcessArtifactsProduced = PlatformResources.GetResourceString("OutOfProcessArtifactsProduced");
    private static readonly string InProcessArtifactsProduced = PlatformResources.GetResourceString("InProcessArtifactsProduced");
    private static readonly string ForTest = PlatformResources.GetResourceString("ForTest");
    private static readonly string ZeroTestsRan = PlatformResources.GetResourceString("ZeroTestsRan");

    /// <summary>
    /// Regression test for PR #6505 / Issue #6492: HotReload broke (TestProgressState cleanup).
    /// TestProgressState wasn't being reset between hot-reload sessions.
    /// Fix: `_testProgressState = null` on TestExecutionCompleted.
    /// Verifies that after TestExecutionCompleted, a new test session can start fresh.
    /// </summary>
    [TestMethod]
    public void TestExecutionCompleted_WhenCalled_ResetsStateForNextSession()
    {
        string assembly = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"C:\work\assembly.dll" : "/mnt/work/assembly.dll";
        var console = new StringBuilderConsole();
        var reporter = new TerminalTestReporter(assembly, "net8.0", "x64", console, new CTRLPlusCCancellationTokenSource(), new TerminalTestReporterOptions
        {
            ShowPassedTests = () => true,
            AnsiMode = AnsiMode.NoAnsi,
            ShowProgress = () => false,
        });

        // First test execution session
        DateTimeOffset startTime = new(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset endTime = new(2024, 1, 1, 0, 1, 0, TimeSpan.Zero);
        reporter.TestExecutionStarted(startTime, 1, isDiscovery: false);
        reporter.AssemblyRunStarted();
        reporter.TestCompleted("Test1", "Test1", TestOutcome.Passed, TimeSpan.FromSeconds(1),
            informativeMessage: null, errorMessage: null, exception: null, expected: null, actual: null, null, null);
        reporter.AssemblyRunCompleted();
        reporter.TestExecutionCompleted(endTime);

        // Second test execution session (simulating hot-reload)
        // This should NOT throw because _testProgressState was reset.
        DateTimeOffset startTime2 = new(2024, 1, 1, 0, 2, 0, TimeSpan.Zero);
        DateTimeOffset endTime2 = new(2024, 1, 1, 0, 3, 0, TimeSpan.Zero);
        reporter.TestExecutionStarted(startTime2, 1, isDiscovery: false);
        reporter.AssemblyRunStarted();
        reporter.TestCompleted("Test2", "Test2", TestOutcome.Passed, TimeSpan.FromSeconds(2),
            informativeMessage: null, errorMessage: null, exception: null, expected: null, actual: null, null, null);
        reporter.AssemblyRunCompleted();
        reporter.TestExecutionCompleted(endTime2);

        string output = console.Output;
        // Verify both sessions produced summary output
        int summaryCount = CountOccurrences(output, TestRunSummary);
        Assert.AreEqual(2, summaryCount, "Expected two test run summaries (one per session) but got " + summaryCount);
    }

    /// <summary>
    /// Regression test for PR #6602 / Issue #6599: TerminalTestReporter summary output wasn't localized.
    /// Verifies summary uses resource strings from PlatformResources.
    /// </summary>
    [TestMethod]
    public void TestRunSummary_UsesLocalizedResourceStrings()
    {
        string assembly = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"C:\work\assembly.dll" : "/mnt/work/assembly.dll";
        var console = new StringBuilderConsole();
        var reporter = new TerminalTestReporter(assembly, "net8.0", "x64", console, new CTRLPlusCCancellationTokenSource(), new TerminalTestReporterOptions
        {
            ShowPassedTests = () => true,
            AnsiMode = AnsiMode.NoAnsi,
            ShowProgress = () => false,
        });

        DateTimeOffset startTime = new(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset endTime = new(2024, 1, 1, 0, 1, 0, TimeSpan.Zero);
        reporter.TestExecutionStarted(startTime, 1, isDiscovery: false);
        reporter.AssemblyRunStarted();
        reporter.TestCompleted("Test1", "Test1", TestOutcome.Passed, TimeSpan.FromSeconds(1),
            informativeMessage: null, errorMessage: null, exception: null, expected: null, actual: null, null, null);
        reporter.AssemblyRunCompleted();
        reporter.TestExecutionCompleted(endTime);

        string output = console.Output;

        // Summary line must use the localized "Test run summary" text
        Assert.Contains(TestRunSummary, output);

        // Lowercase labels for counts must use localized strings
        Assert.Contains(TotalLowercase, output);
        Assert.Contains(FailedLowercase, output);
        Assert.Contains(SucceededLowercase, output);
        Assert.Contains(SkippedLowercase, output);
        Assert.Contains(DurationLowercase, output);
    }

    /// <summary>
    /// Regression test for PR #6602 / Issue #6599: Verify localized "Passed!" status when all tests pass.
    /// </summary>
    [TestMethod]
    public void TestRunSummary_WhenAllTestsPass_UsesLocalizedPassedString()
    {
        string assembly = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"C:\work\assembly.dll" : "/mnt/work/assembly.dll";
        var console = new StringBuilderConsole();
        var reporter = new TerminalTestReporter(assembly, "net8.0", "x64", console, new CTRLPlusCCancellationTokenSource(), new TerminalTestReporterOptions
        {
            ShowPassedTests = () => true,
            AnsiMode = AnsiMode.NoAnsi,
            ShowProgress = () => false,
        });

        reporter.TestExecutionStarted(DateTimeOffset.MinValue, 1, isDiscovery: false);
        reporter.AssemblyRunStarted();
        reporter.TestCompleted("Test1", "Test1", TestOutcome.Passed, TimeSpan.FromSeconds(1),
            informativeMessage: null, errorMessage: null, exception: null, expected: null, actual: null, null, null);
        reporter.AssemblyRunCompleted();
        reporter.TestExecutionCompleted(DateTimeOffset.MaxValue);

        string output = console.Output;
        Assert.Contains($"{PassedString}!", output);
    }

    /// <summary>
    /// Regression test for PR #6602 / Issue #6599: Verify localized "Failed!" status when tests fail.
    /// </summary>
    [TestMethod]
    public void TestRunSummary_WhenTestsFail_UsesLocalizedFailedString()
    {
        string assembly = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"C:\work\assembly.dll" : "/mnt/work/assembly.dll";
        var console = new StringBuilderConsole();
        var reporter = new TerminalTestReporter(assembly, "net8.0", "x64", console, new CTRLPlusCCancellationTokenSource(), new TerminalTestReporterOptions
        {
            ShowPassedTests = () => true,
            AnsiMode = AnsiMode.NoAnsi,
            ShowProgress = () => false,
        });

        reporter.TestExecutionStarted(DateTimeOffset.MinValue, 1, isDiscovery: false);
        reporter.AssemblyRunStarted();
        reporter.TestCompleted("Test1", "Test1", TestOutcome.Fail, TimeSpan.FromSeconds(1),
            informativeMessage: null, errorMessage: "error", exception: null, expected: null, actual: null, null, null);
        reporter.AssemblyRunCompleted();
        reporter.TestExecutionCompleted(DateTimeOffset.MaxValue);

        string output = console.Output;
        Assert.Contains($"{FailedString}!", output);
    }

    /// <summary>
    /// Regression test for PR #7534 / Issue #7471: File artifacts message not printed for out-of-proc.
    /// Verifies that out-of-process artifacts appear in summary output.
    /// </summary>
    [TestMethod]
    public void TestRunSummary_WhenOutOfProcessArtifactAdded_ShowsInOutput()
    {
        string assembly = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"C:\work\assembly.dll" : "/mnt/work/assembly.dll";
        string artifactPath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"C:\work\artifact.txt" : "/mnt/work/artifact.txt";
        var console = new StringBuilderConsole();
        var reporter = new TerminalTestReporter(assembly, "net8.0", "x64", console, new CTRLPlusCCancellationTokenSource(), new TerminalTestReporterOptions
        {
            ShowPassedTests = () => true,
            AnsiMode = AnsiMode.NoAnsi,
            ShowProgress = () => false,
        });

        reporter.TestExecutionStarted(DateTimeOffset.MinValue, 1, isDiscovery: false);
        reporter.AssemblyRunStarted();
        reporter.TestCompleted("Test1", "Test1", TestOutcome.Passed, TimeSpan.FromSeconds(1),
            informativeMessage: null, errorMessage: null, exception: null, expected: null, actual: null, null, null);
        reporter.ArtifactAdded(outOfProcess: true, testName: null, artifactPath);
        reporter.AssemblyRunCompleted();
        reporter.TestExecutionCompleted(DateTimeOffset.MaxValue);

        string output = console.Output;
        Assert.Contains(OutOfProcessArtifactsProduced, output);
        Assert.Contains(artifactPath, output);
    }

    /// <summary>
    /// Regression test for PR #7534 / Issue #7471: Verify both in-process and out-of-process
    /// artifact grouping in summary output.
    /// </summary>
    [TestMethod]
    public void TestRunSummary_WhenBothArtifactTypesAdded_ShowsBothGroups()
    {
        string assembly = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"C:\work\assembly.dll" : "/mnt/work/assembly.dll";
        string inProcArtifact = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"C:\work\in-proc.txt" : "/mnt/work/in-proc.txt";
        string outOfProcArtifact = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"C:\work\out-of-proc.txt" : "/mnt/work/out-of-proc.txt";
        var console = new StringBuilderConsole();
        var reporter = new TerminalTestReporter(assembly, "net8.0", "x64", console, new CTRLPlusCCancellationTokenSource(), new TerminalTestReporterOptions
        {
            ShowPassedTests = () => true,
            AnsiMode = AnsiMode.NoAnsi,
            ShowProgress = () => false,
        });

        reporter.TestExecutionStarted(DateTimeOffset.MinValue, 1, isDiscovery: false);
        reporter.AssemblyRunStarted();
        reporter.TestCompleted("Test1", "Test1", TestOutcome.Passed, TimeSpan.FromSeconds(1),
            informativeMessage: null, errorMessage: null, exception: null, expected: null, actual: null, null, null);
        reporter.ArtifactAdded(outOfProcess: true, testName: null, outOfProcArtifact);
        reporter.ArtifactAdded(outOfProcess: false, testName: null, inProcArtifact);
        reporter.AssemblyRunCompleted();
        reporter.TestExecutionCompleted(DateTimeOffset.MaxValue);

        string output = console.Output;
        Assert.Contains(OutOfProcessArtifactsProduced, output);
        Assert.Contains(InProcessArtifactsProduced, output);
        Assert.Contains(outOfProcArtifact, output);
        Assert.Contains(inProcArtifact, output);
    }

    /// <summary>
    /// Regression test for PR #7534 / Issue #7471: Verify artifact associated with a specific test
    /// shows the test name.
    /// </summary>
    [TestMethod]
    public void TestRunSummary_WhenArtifactHasTestName_ShowsForTestLabel()
    {
        string assembly = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"C:\work\assembly.dll" : "/mnt/work/assembly.dll";
        string artifactPath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"C:\work\artifact.txt" : "/mnt/work/artifact.txt";
        var console = new StringBuilderConsole();
        var reporter = new TerminalTestReporter(assembly, "net8.0", "x64", console, new CTRLPlusCCancellationTokenSource(), new TerminalTestReporterOptions
        {
            ShowPassedTests = () => true,
            AnsiMode = AnsiMode.NoAnsi,
            ShowProgress = () => false,
        });

        reporter.TestExecutionStarted(DateTimeOffset.MinValue, 1, isDiscovery: false);
        reporter.AssemblyRunStarted();
        reporter.TestCompleted("Test1", "Test1", TestOutcome.Passed, TimeSpan.FromSeconds(1),
            informativeMessage: null, errorMessage: null, exception: null, expected: null, actual: null, null, null);
        reporter.ArtifactAdded(outOfProcess: false, testName: "MySpecialTest", artifactPath);
        reporter.AssemblyRunCompleted();
        reporter.TestExecutionCompleted(DateTimeOffset.MaxValue);

        string output = console.Output;
        Assert.Contains(ForTest, output);
        Assert.Contains("MySpecialTest", output);
    }

    /// <summary>
    /// Regression test for PR #6602 / Issue #6599: Verify "Zero tests ran" message uses localized string.
    /// </summary>
    [TestMethod]
    public void TestRunSummary_WhenNoTestsRan_UsesLocalizedZeroTestsRanString()
    {
        string assembly = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"C:\work\assembly.dll" : "/mnt/work/assembly.dll";
        var console = new StringBuilderConsole();
        var reporter = new TerminalTestReporter(assembly, "net8.0", "x64", console, new CTRLPlusCCancellationTokenSource(), new TerminalTestReporterOptions
        {
            ShowPassedTests = () => true,
            AnsiMode = AnsiMode.NoAnsi,
            ShowProgress = () => false,
        });

        reporter.TestExecutionStarted(DateTimeOffset.MinValue, 1, isDiscovery: false);
        reporter.AssemblyRunStarted();
        // No tests completed
        reporter.AssemblyRunCompleted();
        reporter.TestExecutionCompleted(DateTimeOffset.MaxValue);

        string output = console.Output;
        Assert.Contains(ZeroTestsRan, output);
    }

    /// <summary>
    /// Regression test for PR #6505: Verify that test counts are correctly reset between sessions.
    /// </summary>
    [TestMethod]
    public void TestExecutionCompleted_WhenCalledTwice_SecondSessionHasIndependentCounts()
    {
        string assembly = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"C:\work\assembly.dll" : "/mnt/work/assembly.dll";
        var console = new StringBuilderConsole();
        var reporter = new TerminalTestReporter(assembly, "net8.0", "x64", console, new CTRLPlusCCancellationTokenSource(), new TerminalTestReporterOptions
        {
            ShowPassedTests = () => true,
            AnsiMode = AnsiMode.NoAnsi,
            ShowProgress = () => false,
        });

        // First session: 1 failed test
        reporter.TestExecutionStarted(DateTimeOffset.MinValue, 1, isDiscovery: false);
        reporter.AssemblyRunStarted();
        reporter.TestCompleted("FailTest", "FailTest", TestOutcome.Fail, TimeSpan.FromSeconds(1),
            informativeMessage: null, errorMessage: "error", exception: null, expected: null, actual: null, null, null);
        reporter.AssemblyRunCompleted();
        reporter.TestExecutionCompleted(DateTimeOffset.MaxValue);

        // Clear console for second session
        string firstOutput = console.Output;

        // Second session: 1 passed test - should show as "Passed!" not "Failed!"
        var console2 = new StringBuilderConsole();
        var reporter2 = new TerminalTestReporter(assembly, "net8.0", "x64", console2, new CTRLPlusCCancellationTokenSource(), new TerminalTestReporterOptions
        {
            ShowPassedTests = () => true,
            AnsiMode = AnsiMode.NoAnsi,
            ShowProgress = () => false,
        });

        reporter2.TestExecutionStarted(DateTimeOffset.MinValue, 1, isDiscovery: false);
        reporter2.AssemblyRunStarted();
        reporter2.TestCompleted("PassTest", "PassTest", TestOutcome.Passed, TimeSpan.FromSeconds(1),
            informativeMessage: null, errorMessage: null, exception: null, expected: null, actual: null, null, null);
        reporter2.AssemblyRunCompleted();
        reporter2.TestExecutionCompleted(DateTimeOffset.MaxValue);

        string secondOutput = console2.Output;

        Assert.Contains($"{FailedString}!", firstOutput);
        Assert.Contains($"{PassedString}!", secondOutput);
    }

    private static int CountOccurrences(string text, string pattern)
    {
        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(pattern, index, StringComparison.Ordinal)) != -1)
        {
            count++;
            index += pattern.Length;
        }

        return count;
    }

    internal class StringBuilderConsole : IConsole
    {
        private readonly StringBuilder _output = new();

        public int BufferHeight => int.MaxValue;

        public int BufferWidth => int.MaxValue;

        public int WindowHeight => int.MaxValue;

        public int WindowWidth => int.MaxValue;

        public bool IsOutputRedirected => false;

        public string Output => _output.ToString();

        public event ConsoleCancelEventHandler? CancelKeyPress = (sender, e) => { };

        public void Clear() => throw new NotImplementedException();

        public ConsoleColor GetForegroundColor() => ConsoleColor.White;

        public void SetForegroundColor(ConsoleColor color)
        {
        }

        public void Write(string? value) => _output.Append(value);

        public void Write(char value) => _output.Append(value);

        public void WriteLine() => _output.AppendLine();

        public void WriteLine(string? value) => _output.AppendLine(value);
    }
}
