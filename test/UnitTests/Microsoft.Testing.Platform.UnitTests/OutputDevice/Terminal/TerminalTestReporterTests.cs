// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.OutputDevice.Terminal;
using Microsoft.Testing.Platform.Resources;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
[UnsupportedOSPlatform("browser")]
public sealed class TerminalTestReporterTests
{
    [TestMethod]
    public void ExceptionFlattener_WhenNestedInnerExceptions_ShouldKeepAllMessagesInOrder()
    {
        var exception = new Exception("outer", new InvalidOperationException("inner-1", new ArgumentException("inner-2")));

        FlatException[] flattenedExceptions = ExceptionFlattener.Flatten(null, exception);

        Assert.HasCount(3, flattenedExceptions);
        Assert.AreEqual("outer", flattenedExceptions[0].ErrorMessage);
        Assert.AreEqual("inner-1", flattenedExceptions[1].ErrorMessage);
        Assert.AreEqual("inner-2", flattenedExceptions[2].ErrorMessage);
    }

    [TestMethod]
    public void ExceptionFlattener_WhenAggregateException_ShouldKeepTopLevelThenFlattenedInnerExceptions()
    {
        var exception = new AggregateException(
            "top",
            new InvalidOperationException("inner-1"),
            new ArgumentException("inner-2"));

        FlatException[] flattenedExceptions = ExceptionFlattener.Flatten(null, exception);

        Assert.HasCount(3, flattenedExceptions);
        Assert.AreEqual(typeof(AggregateException).FullName, flattenedExceptions[0].ErrorType);
        Assert.AreEqual("inner-1", flattenedExceptions[1].ErrorMessage);
        Assert.AreEqual("inner-2", flattenedExceptions[2].ErrorMessage);
    }

    [TestMethod]
    public void AppendStackFrameFormatsStackTraceLineCorrectly()
    {
        var terminal = new StringBuilderTerminal();
        Exception err;
        try
        {
            throw new Exception();
        }
        catch (Exception ex)
        {
            err = ex;
        }

        string firstStackTraceLine = err.StackTrace!.Replace("\r", string.Empty).Split('\n')[0];
        TerminalTestReporter.AppendStackFrame(terminal, firstStackTraceLine);

#if NETCOREAPP
        Assert.Contains("    at Microsoft.Testing.Platform.UnitTests.TerminalTestReporterTests.AppendStackFrameFormatsStackTraceLineCorrectly() in ", terminal.Output);
#else
        // This is caused by us using portable symbols, and .NET Framework 4.6.2, once we update to .NET Framework 4.7.2 the path to file will be included in the stacktrace and this won't be necessary.
        // See first point here: https://learn.microsoft.com/en-us/dotnet/core/diagnostics/symbols#support-for-portable-pdbs
        Assert.Contains("    at Microsoft.Testing.Platform.UnitTests.TerminalTestReporterTests.AppendStackFrameFormatsStackTraceLineCorrectly()", terminal.Output);
#endif
        // Line number without the respective file
        Assert.DoesNotContain(" :0", terminal.Output);
    }

    // Code with line when we have symbols
    [DataRow(
        "   at MicrosoftTestingPlatformEntryPoint.Main(String[]) in /_/TUnit.TestProject/obj/Release/net8.0/osx-x64/MicrosoftTestingPlatformEntryPoint.cs:line 16",
        "    at MicrosoftTestingPlatformEntryPoint.Main(String[]) in /_/TUnit.TestProject/obj/Release/net8.0/osx-x64/MicrosoftTestingPlatformEntryPoint.cs:16")]
    // code without line when we don't have symbols
    [DataRow(
        "   at TestingPlatformEntryPoint.<Main>(String[])",
        "    at TestingPlatformEntryPoint.<Main>(String[])")]
    // stack trace when published as NativeAOT
    [DataRow(
        "   at BenchmarkTest.ExceptionThrower.<Nested1>d__2.MoveNext() + 0x9d",
        "    at BenchmarkTest.ExceptionThrower.<Nested1>d__2.MoveNext() + 0x9d")]
    // spanners that we want to keep, to not lose information
    [DataRow(
        "--- End of stack trace from previous location ---",
        "    --- End of stack trace from previous location ---")]
    [TestMethod]
    public void StackTraceRegexCapturesLines(string stackTraceLine, string expected)
    {
        var terminal = new StringBuilderTerminal();
        TerminalTestReporter.AppendStackFrame(terminal, stackTraceLine);

        // We add newline after every, but it is hard to put it in the attribute.
        expected += Environment.NewLine;

        Assert.AreEqual(expected, terminal.Output);
    }

    [TestMethod]
    public void NonAnsiTerminal_OutputFormattingIsCorrect()
    {
        string targetFramework = "net8.0";
        string architecture = "x64";
        string assembly = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"C:\work\assembly.dll" : "/mnt/work/assembly.dll";

        var stringBuilderConsole = new StringBuilderConsole();
        var terminalReporter = new TerminalTestReporter(stringBuilderConsole, static () => false, new TerminalTestReporterOptions
        {
            ShowPassedTests = () => true,

            // Like --no-ansi in commandline, should disable ANSI altogether.
            AnsiMode = AnsiMode.NoAnsi,

            ShowProgress = () => false,
        });

        DateTimeOffset startTime = DateTimeOffset.MinValue;
        DateTimeOffset endTime = DateTimeOffset.MaxValue;
        terminalReporter.TestExecutionStarted(startTime, 1, isDiscovery: false, isHelp: false, isRetry: false);

        string folder = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"C:\work\" : "/mnt/work/";

        terminalReporter.AssemblyRunStarted(assembly, targetFramework, architecture, "0", "0");
        string standardOutput = "Hello!";
        string errorOutput = "Oh no!";

        terminalReporter.TestCompleted("0", testNodeUid: "PassedTest1", "PassedTest1", TestOutcome.Passed, TimeSpan.FromSeconds(10),
            informativeMessage: null, errorMessage: null, exception: null, expected: null, actual: null, standardOutput, errorOutput);
        terminalReporter.TestCompleted("0", testNodeUid: "SkippedTest1", "SkippedTest1", TestOutcome.Skipped, TimeSpan.FromSeconds(10),
            informativeMessage: null, errorMessage: null, exception: null, expected: null, actual: null, standardOutput, errorOutput);
        // timed out + canceled + failed should all report as failed in summary
        terminalReporter.TestCompleted("0", testNodeUid: "TimedoutTest1", "TimedoutTest1", TestOutcome.Timeout, TimeSpan.FromSeconds(10),
            informativeMessage: null, errorMessage: null, exception: null, expected: null, actual: null, standardOutput, errorOutput);
        terminalReporter.TestCompleted("0", testNodeUid: "CanceledTest1", "CanceledTest1", TestOutcome.Canceled, TimeSpan.FromSeconds(10),
            informativeMessage: null, errorMessage: null, exception: null, expected: null, actual: null, standardOutput, errorOutput);
        terminalReporter.TestCompleted("0", testNodeUid: "FailedTest1", "FailedTest1", TestOutcome.Fail, TimeSpan.FromSeconds(10),
            informativeMessage: null, errorMessage: "Tests failed", exception: new StackTraceException(@$"   at FailingTest() in {folder}codefile.cs:line 10"), expected: "ABC", actual: "DEF", standardOutput, errorOutput);
        terminalReporter.ArtifactAdded(outOfProcess: true, assembly: assembly, targetFramework: targetFramework, architecture: architecture, executionId: "0", testName: null, @$"{folder}artifact1.txt");
        terminalReporter.ArtifactAdded(outOfProcess: false, assembly: assembly, targetFramework: targetFramework, architecture: architecture, executionId: "0", testName: null, @$"{folder}artifact2.txt");
        terminalReporter.AssemblyRunCompleted("0");
        terminalReporter.TestExecutionCompleted(endTime, exitCode: null);

        string output = stringBuilderConsole.Output;

        string expected = $"""
            passed PassedTest1 (10s 000ms)
              Standard output
                Hello!
              Error output
                Oh no!
            skipped SkippedTest1 (10s 000ms)
              Standard output
                Hello!
              Error output
                Oh no!
            failed (canceled) TimedoutTest1 (10s 000ms)
              Standard output
                Hello!
              Error output
                Oh no!
            failed (canceled) CanceledTest1 (10s 000ms)
              Standard output
                Hello!
              Error output
                Oh no!
            failed FailedTest1 (10s 000ms)
              Tests failed
              Expected
                ABC
              Actual
                DEF
                at FailingTest() in {folder}codefile.cs:10
              Standard output
                Hello!
              Error output
                Oh no!

              Out of process file artifacts produced:
                - {folder}artifact1.txt
              In process file artifacts produced:
                - {folder}artifact2.txt

            Test run summary: Failed! - {assembly} (net8.0|x64)
              total: 5
              failed: 3
              succeeded: 1
              skipped: 1
              duration: 3652058d 23h 59m 59s 999ms

            """;

        Assert.AreEqual(expected, ShowEscape(output));
    }

    [TestMethod]
    public void SimpleAnsiTerminal_OutputFormattingIsCorrect()
    {
        string targetFramework = "net8.0";
        string architecture = "x64";
        string assembly = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"C:\work\assembly.dll" : "/mnt/work/assembly.dll";

        var stringBuilderConsole = new StringBuilderConsole();
        var terminalReporter = new TerminalTestReporter(stringBuilderConsole, static () => false, new TerminalTestReporterOptions
        {
            ShowPassedTests = () => true,

            // Like if we autodetect that we are in CI (e.g. by looking at TF_BUILD, and we don't disable ANSI.
            AnsiMode = AnsiMode.SimpleAnsi,

            ShowProgress = () => false,
        });

        DateTimeOffset startTime = DateTimeOffset.MinValue;
        DateTimeOffset endTime = DateTimeOffset.MaxValue;
        terminalReporter.TestExecutionStarted(startTime, 1, isDiscovery: false, isHelp: false, isRetry: false);

        string folder = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"C:\work\" : "/mnt/work/";

        terminalReporter.AssemblyRunStarted(assembly, targetFramework, architecture, "0", "0");
        string standardOutput = "Hello!";
        string errorOutput = "Oh no!";

        terminalReporter.TestCompleted("0", testNodeUid: "PassedTest1", "PassedTest1", TestOutcome.Passed, TimeSpan.FromSeconds(10),
            informativeMessage: null, errorMessage: null, exception: null, expected: null, actual: null, standardOutput, errorOutput);
        terminalReporter.TestCompleted("0", testNodeUid: "SkippedTest1", "SkippedTest1", TestOutcome.Skipped, TimeSpan.FromSeconds(10),
            informativeMessage: null, errorMessage: null, exception: null, expected: null, actual: null, standardOutput, errorOutput);
        // timed out + canceled + failed should all report as failed in summary
        terminalReporter.TestCompleted("0", testNodeUid: "TimedoutTest1", "TimedoutTest1", TestOutcome.Timeout, TimeSpan.FromSeconds(10),
            informativeMessage: null, errorMessage: null, exception: null, expected: null, actual: null, standardOutput, errorOutput);
        terminalReporter.TestCompleted("0", testNodeUid: "CanceledTest1", "CanceledTest1", TestOutcome.Canceled, TimeSpan.FromSeconds(10),
            informativeMessage: null, errorMessage: null, exception: null, expected: null, actual: null, standardOutput, errorOutput);
        terminalReporter.TestCompleted("0", testNodeUid: "FailedTest1", "FailedTest1", TestOutcome.Fail, TimeSpan.FromSeconds(10),
            informativeMessage: null, errorMessage: "Tests failed", exception: new StackTraceException(@$"   at FailingTest() in {folder}codefile.cs:line 10"), expected: "ABC", actual: "DEF", standardOutput, errorOutput);
        terminalReporter.ArtifactAdded(outOfProcess: true, assembly: assembly, targetFramework: targetFramework, architecture: architecture, executionId: "0", testName: null, @$"{folder}artifact1.txt");
        terminalReporter.ArtifactAdded(outOfProcess: false, assembly: assembly, targetFramework: targetFramework, architecture: architecture, executionId: "0", testName: null, @$"{folder}artifact2.txt");
        terminalReporter.AssemblyRunCompleted("0");
        terminalReporter.TestExecutionCompleted(endTime, exitCode: null);

        string output = stringBuilderConsole.Output;

        string expected = $"""
            ␛[32mpassed␛[m PassedTest1 ␛[90m(10s 000ms)␛[m
            ␛[90m  Standard output
            ␛[90m    Hello!
            ␛[90m  Error output
            ␛[90m    Oh no!
            ␛[m␛[33mskipped␛[m SkippedTest1 ␛[90m(10s 000ms)␛[m
            ␛[90m  Standard output
            ␛[90m    Hello!
            ␛[90m  Error output
            ␛[90m    Oh no!
            ␛[m␛[31mfailed (canceled)␛[m TimedoutTest1 ␛[90m(10s 000ms)␛[m
            ␛[90m  Standard output
            ␛[90m    Hello!
            ␛[90m  Error output
            ␛[90m    Oh no!
            ␛[m␛[31mfailed (canceled)␛[m CanceledTest1 ␛[90m(10s 000ms)␛[m
            ␛[90m  Standard output
            ␛[90m    Hello!
            ␛[90m  Error output
            ␛[90m    Oh no!
            ␛[m␛[31mfailed␛[m FailedTest1 ␛[90m(10s 000ms)␛[m
            ␛[31m  Tests failed
            ␛[m␛[31m  Expected
            ␛[31m    ABC
            ␛[31m  Actual
            ␛[31m    DEF
            ␛[m␛[90m    at FailingTest() in {folder}codefile.cs:10␛[90m
            ␛[m␛[90m  Standard output
            ␛[90m    Hello!
            ␛[90m  Error output
            ␛[90m    Oh no!
            ␛[m
              Out of process file artifacts produced:
                - {folder}artifact1.txt
              In process file artifacts produced:
                - {folder}artifact2.txt

            ␛[31mTest run summary: Failed!␛[90m - ␛[m{folder}assembly.dll (net8.0|x64)
            ␛[m  total: 5
            ␛[31m  failed: 3
            ␛[m  succeeded: 1
              skipped: 1
              duration: 3652058d 23h 59m 59s 999ms

            """;

        Assert.AreEqual(expected, ShowEscape(output));
    }

    [TestMethod]
    public void AnsiTerminal_OutputFormattingIsCorrect()
    {
        string targetFramework = "net8.0";
        string architecture = "x64";
        string assembly = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"C:\work\assembly.dll" : "/mnt/work/assembly.dll";

        var stringBuilderConsole = new StringBuilderConsole();
        var terminalReporter = new TerminalTestReporter(stringBuilderConsole, static () => false, new TerminalTestReporterOptions
        {
            ShowPassedTests = () => true,
            // Like if we autodetect that we are in ANSI capable terminal.
            AnsiMode = AnsiMode.ForceAnsi,

            ShowProgress = () => false,
        });

        DateTimeOffset startTime = DateTimeOffset.MinValue;
        DateTimeOffset endTime = DateTimeOffset.MaxValue;
        terminalReporter.TestExecutionStarted(startTime, 1, isDiscovery: false, isHelp: false, isRetry: false);

        string folder = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"C:\work\" : "/mnt/work/";
        string folderLink = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"C:/work/" : "mnt/work/";
        string folderLinkNoSlash = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"C:/work" : "mnt/work";

        terminalReporter.AssemblyRunStarted(assembly, targetFramework, architecture, "0", "0");
        string standardOutput = "Hello!";
        string errorOutput = "Oh no!";

        terminalReporter.TestCompleted("0", testNodeUid: "PassedTest1", "PassedTest1", TestOutcome.Passed, TimeSpan.FromSeconds(10),
            informativeMessage: null, errorMessage: null, exception: null, expected: null, actual: null, standardOutput, errorOutput);
        terminalReporter.TestCompleted("0", testNodeUid: "SkippedTest1", "SkippedTest1", TestOutcome.Skipped, TimeSpan.FromSeconds(10),
            informativeMessage: null, errorMessage: null, exception: null, expected: null, actual: null, standardOutput, errorOutput);
        // timed out + canceled + failed should all report as failed in summary
        terminalReporter.TestCompleted("0", testNodeUid: "TimedoutTest1", "TimedoutTest1", TestOutcome.Timeout, TimeSpan.FromSeconds(10),
            informativeMessage: null, errorMessage: null, exception: null, expected: null, actual: null, standardOutput, errorOutput);
        terminalReporter.TestCompleted("0", testNodeUid: "CanceledTest1", "CanceledTest1", TestOutcome.Canceled, TimeSpan.FromSeconds(10),
            informativeMessage: null, errorMessage: null, exception: null, expected: null, actual: null, standardOutput, errorOutput);
        terminalReporter.TestCompleted("0", testNodeUid: "FailedTest1", "FailedTest1", TestOutcome.Fail, TimeSpan.FromSeconds(10),
            informativeMessage: null, errorMessage: "Tests failed", exception: new StackTraceException(@$"   at FailingTest() in {folder}codefile.cs:line 10"), expected: "ABC", actual: "DEF", standardOutput, errorOutput);
        terminalReporter.ArtifactAdded(outOfProcess: true, assembly: assembly, targetFramework: targetFramework, architecture: architecture, executionId: "0", testName: null, @$"{folder}artifact1.txt");
        terminalReporter.ArtifactAdded(outOfProcess: false, assembly: assembly, targetFramework: targetFramework, architecture: architecture, executionId: "0", testName: null, @$"{folder}artifact2.txt");
        terminalReporter.AssemblyRunCompleted("0");
        terminalReporter.TestExecutionCompleted(endTime, exitCode: null);

        string output = stringBuilderConsole.Output;

        string expected = $"""
            ␛[32mpassed␛[m PassedTest1 ␛[90m(10s 000ms)␛[m
            ␛[90m  Standard output
                Hello!
              Error output
                Oh no!
            ␛[m␛[33mskipped␛[m SkippedTest1 ␛[90m(10s 000ms)␛[m
            ␛[90m  Standard output
                Hello!
              Error output
                Oh no!
            ␛[m␛[31mfailed (canceled)␛[m TimedoutTest1 ␛[90m(10s 000ms)␛[m
            ␛[90m  Standard output
                Hello!
              Error output
                Oh no!
            ␛[m␛[31mfailed (canceled)␛[m CanceledTest1 ␛[90m(10s 000ms)␛[m
            ␛[90m  Standard output
                Hello!
              Error output
                Oh no!
            ␛[m␛[31mfailed␛[m FailedTest1 ␛[90m(10s 000ms)␛[m
            ␛[31m  Tests failed
            ␛[m␛[31m  Expected
                ABC
              Actual
                DEF
            ␛[m␛[90m    at FailingTest() in ␛[90m␛]8;;file:///{folderLink}codefile.cs␛\{folder}codefile.cs:10␛]8;;␛\␛[m␛[90m
            ␛[m␛[90m  Standard output
                Hello!
              Error output
                Oh no!
            ␛[m
              Out of process file artifacts produced:
                - ␛[90m␛]8;;file:///{folderLink}artifact1.txt␛\{folder}artifact1.txt␛]8;;␛\␛[m
              In process file artifacts produced:
                - ␛[90m␛]8;;file:///{folderLink}artifact2.txt␛\{folder}artifact2.txt␛]8;;␛\␛[m

            ␛[31mTest run summary: Failed!␛[90m - ␛[m␛[90m␛]8;;file:///{folderLinkNoSlash}␛\{folder}assembly.dll␛]8;;␛\␛[m (net8.0|x64)
            ␛[m  total: 5
            ␛[31m  failed: 3
            ␛[m  succeeded: 1
              skipped: 1
              duration: 3652058d 23h 59m 59s 999ms

            """;

        Assert.AreEqual(expected, ShowEscape(output));
    }

    [TestMethod]
    public void AnsiTerminal_OutputProgressFrameIsCorrect()
    {
        string targetFramework = "net8.0";
        string architecture = "x64";
        string assembly = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"C:\work\assembly.dll" : "/mnt/work/assembly.dll";

        var stringBuilderConsole = new StringBuilderConsole();
        var stopwatchFactory = new StopwatchFactory();
        var terminalReporter = new TerminalTestReporter(stringBuilderConsole, static () => false, new TerminalTestReporterOptions
        {
            ShowPassedTests = () => true,
            // Like if we autodetect that we are in ANSI capable terminal.
            AnsiMode = AnsiMode.ForceAnsi,

            ShowActiveTests = true,
            ShowProgress = () => true,
        })
        {
            CreateStopwatch = stopwatchFactory.CreateStopwatch,
        };

        var startHandle = new AutoResetEvent(initialState: false);
        var stopHandle = new AutoResetEvent(initialState: false);

        // Note: Ensure that we disable the timer updates, so that we can have deterministic output.
        terminalReporter.OnProgressStartUpdate += (sender, args) => startHandle.WaitOne();
        terminalReporter.OnProgressStopUpdate += (sender, args) => stopHandle.Set();

        DateTimeOffset startTime = DateTimeOffset.MinValue;
        DateTimeOffset endTime = DateTimeOffset.MaxValue;
        terminalReporter.TestExecutionStarted(startTime, 1, isDiscovery: false, isHelp: false, isRetry: false);

        string folder = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"C:\work\" : "/mnt/work/";

        terminalReporter.AssemblyRunStarted(assembly, targetFramework, architecture, "0", "0");
        string standardOutput = "Hello!";
        string errorOutput = "Oh no!";

        // Note: Add 1ms to make the order of the progress frame deterministic.
        // Otherwise all tests that run for 1m31s could show in any order.
        terminalReporter.TestInProgress(executionId: "0", testNodeUid: "PassedTest1", displayName: "PassedTest1");
        stopwatchFactory.AddTime(TimeSpan.FromMilliseconds(1));
        terminalReporter.TestInProgress(executionId: "0", testNodeUid: "SkippedTest1", displayName: "SkippedTest1");
        stopwatchFactory.AddTime(TimeSpan.FromMilliseconds(1));
        terminalReporter.TestInProgress(executionId: "0", testNodeUid: "InProgressTest1", displayName: "InProgressTest1");
        stopwatchFactory.AddTime(TimeSpan.FromMinutes(1));
        terminalReporter.TestInProgress(executionId: "0", testNodeUid: "InProgressTest2", displayName: "InProgressTest2");
        stopwatchFactory.AddTime(TimeSpan.FromSeconds(30));
        terminalReporter.TestInProgress(executionId: "0", testNodeUid: "InProgressTest3", displayName: "InProgressTest3");
        stopwatchFactory.AddTime(TimeSpan.FromSeconds(1));

        terminalReporter.TestCompleted("0", testNodeUid: "PassedTest1", "PassedTest1", TestOutcome.Passed, TimeSpan.FromSeconds(10),
            informativeMessage: null, errorMessage: null, exception: null, expected: null, actual: null, standardOutput, errorOutput);
        terminalReporter.TestCompleted("0", testNodeUid: "SkippedTest1", "SkippedTest1", TestOutcome.Skipped, TimeSpan.FromSeconds(10),
            informativeMessage: null, errorMessage: null, exception: null, expected: null, actual: null, standardOutput, errorOutput);

        string output = stringBuilderConsole.Output;
        startHandle.Set();
        stopHandle.WaitOne();

        // Note: On MacOS the busy indicator is not rendered.
        bool useBusyIndicator = !RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
        string busyIndicatorString = useBusyIndicator ? "␛]9;4;3;␛\\" : string.Empty;

        // Note: The progress is drawn after each completed event.
        string expected = $"""
            {busyIndicatorString}␛[?25l␛[32mpassed␛[m PassedTest1 ␛[90m(10s 000ms)␛[m
            ␛[90m  Standard output
                Hello!
              Error output
                Oh no!
            ␛[m
            [␛[32m✓1␛[m/␛[31mx0␛[m/␛[33m↓0␛[m] assembly.dll (net8.0|x64)␛[242G(1m 31s)
              SkippedTest1␛[242G(1m 31s)
              InProgressTest1␛[242G(1m 31s)
              InProgressTest2␛[245G(31s)
              InProgressTest3␛[246G(1s)
            ␛[7F
            ␛[J␛[33mskipped␛[m SkippedTest1 ␛[90m(10s 000ms)␛[m
            ␛[90m  Standard output
                Hello!
              Error output
                Oh no!
            ␛[m
            [␛[32m✓1␛[m/␛[31mx0␛[m/␛[33m↓1␛[m] assembly.dll (net8.0|x64)␛[242G(1m 31s)
              InProgressTest1␛[242G(1m 31s)
              InProgressTest2␛[245G(31s)
              InProgressTest3␛[246G(1s)

            """;

        Assert.AreEqual(expected, ShowEscape(output));
    }

    [TestMethod]
    public void TestProgressStateAwareTerminal_WriteToTerminal_ShouldEraseProgressThenRenderProgress()
    {
        var terminal = new RecordingTerminal();
        using var progressAwareTerminal = new TestProgressStateAwareTerminal(terminal, () => true, new CursorProgressRenderer());

        var stopwatchFactory = new StopwatchFactory();
        var progressState = new TestProgressState(1, "assembly.dll", "net8.0", "x64", stopwatchFactory.CreateStopwatch(), isDiscovery: false);

        progressAwareTerminal.StartShowingProgress(workerCount: 1);
        int slotIndex = progressAwareTerminal.AddWorker(progressState);
        progressAwareTerminal.UpdateWorker(slotIndex);

        progressAwareTerminal.WriteToTerminal(t => t.AppendLine("Slowest 10 tests"));
        progressAwareTerminal.StopShowingProgress();

        int writeStartIndex = terminal.Events.FindIndex(e => e == "StartUpdate");
        Assert.IsGreaterThanOrEqualTo(0, writeStartIndex, "StartUpdate should be called before writing to terminal.");

        int eraseIndex = terminal.Events.FindIndex(writeStartIndex + 1, e => e == "EraseProgress");
        Assert.IsGreaterThan(writeStartIndex, eraseIndex, "EraseProgress should be called after StartUpdate.");

        int writeIndex = terminal.Events.FindIndex(eraseIndex + 1, e => e == "AppendLine:Slowest 10 tests");
        Assert.IsGreaterThan(eraseIndex, writeIndex, "User output should be written after erasing progress.");

        int renderIndex = terminal.Events.FindIndex(writeIndex + 1, e => e == "RenderProgress");
        Assert.IsGreaterThan(writeIndex, renderIndex, "Progress should be rendered after user output.");

        int stopUpdateIndex = terminal.Events.FindIndex(renderIndex + 1, e => e == "StopUpdate");
        Assert.IsGreaterThan(renderIndex, stopUpdateIndex, "StopUpdate should be called after rendering progress.");
    }

    [TestMethod]
    public void TestProgressStateAwareTerminal_CanStopProgressAcrossMultipleSessions()
    {
        var terminal = new RecordingTerminal();
        using var progressAwareTerminal = new TestProgressStateAwareTerminal(terminal, () => true, new CursorProgressRenderer());

        progressAwareTerminal.StartShowingProgress(workerCount: 1);
        progressAwareTerminal.StopShowingProgress();

        progressAwareTerminal.StartShowingProgress(workerCount: 1);
        progressAwareTerminal.StopShowingProgress();

        Assert.HasCount(2, terminal.Events.Where(e => e == "StartBusyIndicator"));
        Assert.HasCount(2, terminal.Events.Where(e => e == "EraseProgress"));
        Assert.HasCount(2, terminal.Events.Where(e => e == "StopBusyIndicator"));
    }

    [TestMethod]
    public void NonAnsiTerminal_ShowOutputNone_DoesNotShowOutput()
    {
        string targetFramework = "net8.0";
        string architecture = "x64";
        string assembly = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"C:\work\assembly.dll" : "/mnt/work/assembly.dll";

        var stringBuilderConsole = new StringBuilderConsole();
        var terminalReporter = new TerminalTestReporter(stringBuilderConsole, static () => false, new TerminalTestReporterOptions
        {
            ShowPassedTests = () => true,
            AnsiMode = AnsiMode.NoAnsi,
            ShowProgress = () => false,
            ShowStdout = OutputShowMode.None,
            ShowStderr = OutputShowMode.None,
        });

        DateTimeOffset startTime = DateTimeOffset.MinValue;
        DateTimeOffset endTime = DateTimeOffset.MaxValue;
        terminalReporter.TestExecutionStarted(startTime, 1, isDiscovery: false, isHelp: false, isRetry: false);
        terminalReporter.AssemblyRunStarted(assembly, targetFramework, architecture, "0", "0");

        terminalReporter.TestCompleted("0", testNodeUid: "PassedTest1", "PassedTest1", TestOutcome.Passed, TimeSpan.FromSeconds(1),
            informativeMessage: null, errorMessage: null, exception: null, expected: null, actual: null, standardOutput: "Hello!", errorOutput: "Oh no!");
        terminalReporter.TestCompleted("0", testNodeUid: "FailedTest1", "FailedTest1", TestOutcome.Fail, TimeSpan.FromSeconds(1),
            informativeMessage: null, errorMessage: "Tests failed", exception: null, expected: null, actual: null, standardOutput: "Hello!", errorOutput: "Oh no!");

        terminalReporter.AssemblyRunCompleted("0");
        terminalReporter.TestExecutionCompleted(endTime, exitCode: null);

        string output = stringBuilderConsole.Output;

        Assert.DoesNotContain("Standard output", output);
        Assert.DoesNotContain("Error output", output);
        Assert.DoesNotContain("Hello!", output);
        Assert.DoesNotContain("Oh no!", output);
    }

    [TestMethod]
    public void NonAnsiTerminal_ShowOutputFailed_ShowsOutputOnlyForFailedTests()
    {
        string targetFramework = "net8.0";
        string architecture = "x64";
        string assembly = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"C:\work\assembly.dll" : "/mnt/work/assembly.dll";

        var stringBuilderConsole = new StringBuilderConsole();
        var terminalReporter = new TerminalTestReporter(stringBuilderConsole, static () => false, new TerminalTestReporterOptions
        {
            ShowPassedTests = () => true,
            AnsiMode = AnsiMode.NoAnsi,
            ShowProgress = () => false,
            ShowStdout = OutputShowMode.Failed,
            ShowStderr = OutputShowMode.Failed,
        });

        DateTimeOffset startTime = DateTimeOffset.MinValue;
        DateTimeOffset endTime = DateTimeOffset.MaxValue;
        terminalReporter.TestExecutionStarted(startTime, 1, isDiscovery: false, isHelp: false, isRetry: false);
        terminalReporter.AssemblyRunStarted(assembly, targetFramework, architecture, "0", "0");

        terminalReporter.TestCompleted("0", testNodeUid: "PassedTest1", "PassedTest1", TestOutcome.Passed, TimeSpan.FromSeconds(1),
            informativeMessage: null, errorMessage: null, exception: null, expected: null, actual: null, standardOutput: "passed-stdout", errorOutput: "passed-stderr");
        terminalReporter.TestCompleted("0", testNodeUid: "FailedTest1", "FailedTest1", TestOutcome.Fail, TimeSpan.FromSeconds(1),
            informativeMessage: null, errorMessage: "Tests failed", exception: null, expected: null, actual: null, standardOutput: "failed-stdout", errorOutput: "failed-stderr");

        terminalReporter.AssemblyRunCompleted("0");
        terminalReporter.TestExecutionCompleted(endTime, exitCode: null);

        string output = stringBuilderConsole.Output;

        // stdout/stderr for passed tests should NOT appear
        Assert.DoesNotContain("passed-stdout", output);
        Assert.DoesNotContain("passed-stderr", output);

        // stdout/stderr for failed tests SHOULD appear
        Assert.Contains("failed-stdout", output);
        Assert.Contains("failed-stderr", output);
    }

    private static string? ShowEscape(string? text)
    {
        string visibleEsc = "\x241b";
        return text?.Replace(AnsiCodes.Esc, visibleEsc);
    }

    internal sealed class StopwatchFactory
    {
        private TimeSpan _currentTime = TimeSpan.Zero;

        public void AddTime(TimeSpan time) => _currentTime += time;

        public IStopwatch CreateStopwatch() => new MockStopwatch(this, _currentTime);

        internal sealed class MockStopwatch : IStopwatch
        {
            private readonly StopwatchFactory _factory;

            public MockStopwatch(StopwatchFactory factory, TimeSpan startTime)
            {
                _factory = factory;
                StartTime = startTime;
            }

            private TimeSpan StartTime { get; }

            public TimeSpan Elapsed => _factory._currentTime - StartTime;

            public void Start()
            {
            }

            public void Stop()
            {
            }
        }
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
            // do nothing
        }

        public void Write(string? value) => _output.Append(value);

        public void Write(char value) => _output.Append(value);

        public void WriteLine() => _output.AppendLine();

        public void WriteLine(string? value) => _output.AppendLine(value);
    }

    internal class StringBuilderTerminal : ITerminal
    {
        private readonly StringBuilder _stringBuilder;

        public StringBuilderTerminal()
            => _stringBuilder = new();

        public string Output => _stringBuilder.ToString();

        public int Width => throw new NotImplementedException();

        public int Height => throw new NotImplementedException();

        public void Append(char value) => _stringBuilder.Append(value);

        public void Append(string value) => _stringBuilder.Append(value);

        public void AppendLine() => _stringBuilder.AppendLine();

        public void AppendLine(string value) => _stringBuilder.AppendLine(value);

        public void AppendLink(string path, int? lineNumber)
        {
            _stringBuilder.Append(path);
            _stringBuilder.Append(':');
            _stringBuilder.Append(lineNumber);
        }

        public void EraseProgress() => throw new NotImplementedException();

        public void HideCursor() => throw new NotImplementedException();

        public void RenderProgress(TestProgressState?[] progress) => throw new NotImplementedException();

        public void ResetColor()
        {
            // do nothing
        }

        public void SetColor(TerminalColor color)
        {
            // do nothing
        }

        public void ShowCursor() => throw new NotImplementedException();

        public void StartBusyIndicator() => throw new NotImplementedException();

        public void StartUpdate() => throw new NotImplementedException();

        public void StopBusyIndicator() => throw new NotImplementedException();

        public void StopUpdate() => throw new NotImplementedException();
    }

    private sealed class RecordingTerminal : ITerminal
    {
        public List<string> Events { get; } = [];

        public int Width => int.MaxValue;

        public int Height => int.MaxValue;

        public void Append(char value) => Events.Add($"Append:{value}");

        public void Append(string value) => Events.Add($"Append:{value}");

        public void AppendLine() => Events.Add("AppendLine");

        public void AppendLine(string value) => Events.Add($"AppendLine:{value}");

        public void AppendLink(string path, int? lineNumber) => Events.Add("AppendLink");

        public void EraseProgress() => Events.Add("EraseProgress");

        public void HideCursor() => Events.Add("HideCursor");

        public void MoveCursorUp(int lineCount) => Events.Add($"MoveCursorUp:{lineCount}");

        public void RenderProgress(TestProgressState?[] progress) => Events.Add("RenderProgress");

        public void ResetColor() => Events.Add("ResetColor");

        public void SetColor(TerminalColor color) => Events.Add($"SetColor:{color}");

        public void SetCursorHorizontal(int position) => Events.Add($"SetCursorHorizontal:{position}");

        public void ShowCursor() => Events.Add("ShowCursor");

        public void StartBusyIndicator() => Events.Add("StartBusyIndicator");

        public void StartUpdate() => Events.Add("StartUpdate");

        public void StopBusyIndicator() => Events.Add("StopBusyIndicator");

        public void StopUpdate() => Events.Add("StopUpdate");
    }

    private class StackTraceException : Exception
    {
        public StackTraceException(string stackTrace) => StackTrace = stackTrace;

        public override string? StackTrace { get; }
    }

    // Test data for all C0 control characters (U+0000-U+001F) that are normalized
    [DataRow('\x0000', '\x2400', "NULL")]
    [DataRow('\x0001', '\x2401', "START OF HEADING")]
    [DataRow('\x0002', '\x2402', "START OF TEXT")]
    [DataRow('\x0003', '\x2403', "END OF TEXT")]
    [DataRow('\x0004', '\x2404', "END OF TRANSMISSION")]
    [DataRow('\x0005', '\x2405', "ENQUIRY")]
    [DataRow('\x0006', '\x2406', "ACKNOWLEDGE")]
    [DataRow('\x0007', '\x2407', "BELL")]
    [DataRow('\x0008', '\x2408', "BACKSPACE")]
    [DataRow('\t', '\x2409', "TAB")]
    [DataRow('\n', '\x240A', "LINE FEED")]
    [DataRow('\x000B', '\x240B', "VERTICAL TAB")]
    [DataRow('\x000C', '\x240C', "FORM FEED")]
    [DataRow('\r', '\x240D', "CARRIAGE RETURN")]
    [DataRow('\x000E', '\x240E', "SHIFT OUT")]
    [DataRow('\x000F', '\x240F', "SHIFT IN")]
    [DataRow('\x0010', '\x2410', "DATA LINK ESCAPE")]
    [DataRow('\x0011', '\x2411', "DEVICE CONTROL ONE")]
    [DataRow('\x0012', '\x2412', "DEVICE CONTROL TWO")]
    [DataRow('\x0013', '\x2413', "DEVICE CONTROL THREE")]
    [DataRow('\x0014', '\x2414', "DEVICE CONTROL FOUR")]
    [DataRow('\x0015', '\x2415', "NEGATIVE ACKNOWLEDGE")]
    [DataRow('\x0016', '\x2416', "SYNCHRONOUS IDLE")]
    [DataRow('\x0017', '\x2417', "END OF TRANSMISSION BLOCK")]
    [DataRow('\x0018', '\x2418', "CANCEL")]
    [DataRow('\x0019', '\x2419', "END OF MEDIUM")]
    [DataRow('\x001A', '\x241A', "SUBSTITUTE")]
    [DataRow('\x001B', '\x241B', "ESCAPE")]
    [DataRow('\x001C', '\x241C', "FILE SEPARATOR")]
    [DataRow('\x001D', '\x241D', "GROUP SEPARATOR")]
    [DataRow('\x001E', '\x241E', "RECORD SEPARATOR")]
    [DataRow('\x001F', '\x241F', "UNIT SEPARATOR")]
    [TestMethod]
    public void TestDisplayNames_WithControlCharacters_AreNormalized(char controlChar, char expectedChar, string charName)
    {
        string targetFramework = "net8.0";
        string architecture = "x64";
        string assembly = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"C:\work\assembly.dll" : "/mnt/work/assembly.dll";

        var stringBuilderConsole = new StringBuilderConsole();
        var terminalReporter = new TerminalTestReporter(stringBuilderConsole, static () => false, new TerminalTestReporterOptions
        {
            ShowPassedTests = () => true,
            AnsiMode = AnsiMode.NoAnsi,
            ShowProgress = () => false,
        });

        DateTimeOffset startTime = DateTimeOffset.MinValue;
        DateTimeOffset endTime = DateTimeOffset.MaxValue;
        terminalReporter.TestExecutionStarted(startTime, 1, isDiscovery: false, isHelp: false, isRetry: false);

        terminalReporter.AssemblyRunStarted(assembly, targetFramework, architecture, "0", "0");

        // Test display name with the specific control character
        string testDisplayName = $"Test{controlChar}Name";
        terminalReporter.TestCompleted("0", testNodeUid: "Test1", testDisplayName, TestOutcome.Passed, TimeSpan.FromSeconds(1),
            informativeMessage: null, errorMessage: null, exception: null, expected: null, actual: null, standardOutput: null, errorOutput: null);

        terminalReporter.AssemblyRunCompleted("0");
        terminalReporter.TestExecutionCompleted(endTime, exitCode: null);

        string output = stringBuilderConsole.Output;

        // Verify that the control character is replaced with its Unicode control picture
        string normalizedDisplayName = $"Test{expectedChar}Name";
        Assert.Contains(normalizedDisplayName, output, $"{charName} should be replaced with {expectedChar}");

        // Verify that the literal control character is not present in the test display name
        // Note: We skip this assertion for whitespace characters (\t, \n, \r) because these
        // characters naturally appear in console output formatting (e.g., line breaks between tests)
        // and asserting their complete absence would cause false positives
        string literalDisplayName = $"Test{controlChar}Name";
        bool isWhitespaceChar = controlChar is '\t' or '\n' or '\r';
        if (!isWhitespaceChar)
        {
            Assert.DoesNotContain(literalDisplayName, output, $"Literal {charName} should not be present in test display name");
        }
    }

    // Test data for all C0 control characters (U+0000-U+001F) that are normalized
    [DataRow('\x0000', '\x2400', "NULL")]
    [DataRow('\x0001', '\x2401', "START OF HEADING")]
    [DataRow('\x0002', '\x2402', "START OF TEXT")]
    [DataRow('\x0003', '\x2403', "END OF TEXT")]
    [DataRow('\x0004', '\x2404', "END OF TRANSMISSION")]
    [DataRow('\x0005', '\x2405', "ENQUIRY")]
    [DataRow('\x0006', '\x2406', "ACKNOWLEDGE")]
    [DataRow('\x0007', '\x2407', "BELL")]
    [DataRow('\x0008', '\x2408', "BACKSPACE")]
    [DataRow('\t', '\x2409', "TAB")]
    [DataRow('\n', '\x240A', "LINE FEED")]
    [DataRow('\x000B', '\x240B', "VERTICAL TAB")]
    [DataRow('\x000C', '\x240C', "FORM FEED")]
    [DataRow('\r', '\x240D', "CARRIAGE RETURN")]
    [DataRow('\x000E', '\x240E', "SHIFT OUT")]
    [DataRow('\x000F', '\x240F', "SHIFT IN")]
    [DataRow('\x0010', '\x2410', "DATA LINK ESCAPE")]
    [DataRow('\x0011', '\x2411', "DEVICE CONTROL ONE")]
    [DataRow('\x0012', '\x2412', "DEVICE CONTROL TWO")]
    [DataRow('\x0013', '\x2413', "DEVICE CONTROL THREE")]
    [DataRow('\x0014', '\x2414', "DEVICE CONTROL FOUR")]
    [DataRow('\x0015', '\x2415', "NEGATIVE ACKNOWLEDGE")]
    [DataRow('\x0016', '\x2416', "SYNCHRONOUS IDLE")]
    [DataRow('\x0017', '\x2417', "END OF TRANSMISSION BLOCK")]
    [DataRow('\x0018', '\x2418', "CANCEL")]
    [DataRow('\x0019', '\x2419', "END OF MEDIUM")]
    [DataRow('\x001A', '\x241A', "SUBSTITUTE")]
    [DataRow('\x001B', '\x241B', "ESCAPE")]
    [DataRow('\x001C', '\x241C', "FILE SEPARATOR")]
    [DataRow('\x001D', '\x241D', "GROUP SEPARATOR")]
    [DataRow('\x001E', '\x241E', "RECORD SEPARATOR")]
    [DataRow('\x001F', '\x241F', "UNIT SEPARATOR")]
    [TestMethod]
    public void TestDiscovery_WithControlCharacters_AreNormalized(char controlChar, char expectedChar, string charName)
    {
        string targetFramework = "net8.0";
        string architecture = "x64";
        string assembly = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"C:\work\assembly.dll" : "/mnt/work/assembly.dll";

        var stringBuilderConsole = new StringBuilderConsole();
        var terminalReporter = new TerminalTestReporter(stringBuilderConsole, static () => false, new TerminalTestReporterOptions
        {
            ShowPassedTests = () => true,
            AnsiMode = AnsiMode.NoAnsi,
            ShowProgress = () => false,
        });

        DateTimeOffset startTime = DateTimeOffset.MinValue;
        DateTimeOffset endTime = DateTimeOffset.MaxValue;
        terminalReporter.TestExecutionStarted(startTime, 1, isDiscovery: true, isHelp: false, isRetry: false);

        terminalReporter.AssemblyRunStarted(assembly, targetFramework, architecture, "0", "0");

        // Test discovery with the specific control character
        string testDisplayName = $"Test{controlChar}Name";
        terminalReporter.TestDiscovered("0", testDisplayName);

        terminalReporter.AssemblyRunCompleted("0");
        terminalReporter.TestExecutionCompleted(endTime, exitCode: null);

        string output = stringBuilderConsole.Output;

        // Verify that the control character is replaced with its Unicode control picture
        string normalizedDisplayName = $"Test{expectedChar}Name";
        Assert.Contains(normalizedDisplayName, output, $"{charName} should be replaced with {expectedChar} in discovery");

        // Verify that the literal control character is not present in the test display name
        // Note: We skip this assertion for whitespace characters (\t, \n, \r) because these
        // characters naturally appear in console output formatting (e.g., line breaks between tests)
        // and asserting their complete absence would cause false positives
        string literalDisplayName = $"Test{controlChar}Name";
        bool isWhitespaceChar = controlChar is '\t' or '\n' or '\r';
        if (!isWhitespaceChar)
        {
            Assert.DoesNotContain(literalDisplayName, output, $"Literal {charName} should not be present in test display name");
        }
    }

    [TestMethod]
    public void TestProgressState_WhenCreatedWithDiscoveryTrue_ShouldHaveIsDiscoveryTrue()
    {
        // Arrange
        var stopwatch = new StopwatchFactory.MockStopwatch(new StopwatchFactory(), TimeSpan.Zero);

        // Act
        var progressState = new TestProgressState(1, "test.dll", "net8.0", "x64", stopwatch, isDiscovery: true);

        // Assert
        Assert.IsTrue(progressState.IsDiscovery);
        Assert.AreEqual(0, progressState.DiscoveredTests);
    }

    [TestMethod]
    public void TestProgressState_WhenCreatedWithFalseIsDiscoveryParameter_ShouldHaveIsDiscoveryFalse()
    {
        // Arrange
        var stopwatch = new StopwatchFactory.MockStopwatch(new StopwatchFactory(), TimeSpan.Zero);

        // Act
        var progressState = new TestProgressState(1, "test.dll", "net8.0", "x64", stopwatch, isDiscovery: false);

        // Assert
        Assert.IsFalse(progressState.IsDiscovery);
        Assert.AreEqual(0, progressState.DiscoveredTests);
    }

    [TestMethod]
    public void TestNodeResultsState_GetSingleActiveOrSummaryTask_WhenEmpty_ReturnsNull()
    {
        var state = new TestNodeResultsState(1);

        Assert.IsNull(state.GetSingleActiveOrSummaryTask());
    }

    [TestMethod]
    public void TestNodeResultsState_GetSingleActiveOrSummaryTask_WhenSingleTask_ReturnsThatTask()
    {
        var stopwatchFactory = new StopwatchFactory();
        var state = new TestNodeResultsState(1);
        state.AddRunningTestNode(id: 10, uid: "uid-1", name: "MyTest", stopwatchFactory.CreateStopwatch());

        TestDetailState? active = state.GetSingleActiveOrSummaryTask();

        Assert.IsNotNull(active);
        Assert.AreEqual("MyTest", active.Text);
    }

    [TestMethod]
    public void TestNodeResultsState_GetSingleActiveOrSummaryTask_WhenMultipleTasks_ReturnsFormattedSummary()
    {
        var stopwatchFactory = new StopwatchFactory();
        var state = new TestNodeResultsState(1);
        state.AddRunningTestNode(id: 10, uid: "uid-1", name: "FastTest", stopwatchFactory.CreateStopwatch());
        stopwatchFactory.AddTime(TimeSpan.FromSeconds(1));
        state.AddRunningTestNode(id: 11, uid: "uid-2", name: "SlowTest", stopwatchFactory.CreateStopwatch());
        state.AddRunningTestNode(id: 12, uid: "uid-3", name: "OtherTest", stopwatchFactory.CreateStopwatch());

        TestDetailState? active = state.GetSingleActiveOrSummaryTask();

        Assert.IsNotNull(active);
        // The summary text should report the total count (3), not any individual test name.
        string expectedSummary = string.Format(CultureInfo.CurrentCulture, PlatformResources.ActiveTestsRunning_FullTestsCount, 3);
        Assert.AreEqual(expectedSummary, active.Text);
        Assert.DoesNotContain("FastTest", active.Text);
        Assert.DoesNotContain("SlowTest", active.Text);
        Assert.DoesNotContain("OtherTest", active.Text);
    }

    [TestMethod]
    public void TestNodeResultsState_GetRunningTasks_WhenEmpty_ReturnsEmptyList()
    {
        var state = new TestNodeResultsState(1);

        List<TestDetailState> tasks = state.GetRunningTasks(maxCount: 5);

        Assert.IsEmpty(tasks);
    }

    [TestMethod]
    public void TestNodeResultsState_GetRunningTasks_WhenFewerThanMax_ReturnsAllSortedByElapsedDescending()
    {
        var stopwatchFactory = new StopwatchFactory();
        var state = new TestNodeResultsState(1);
        // Create the stopwatches in age order so we know what "elapsed descending" should look like:
        // "OldTest" runs the longest, "MiddleTest" next, "YoungTest" shortest.
        state.AddRunningTestNode(id: 10, uid: "uid-old", name: "OldTest", stopwatchFactory.CreateStopwatch());
        stopwatchFactory.AddTime(TimeSpan.FromSeconds(2));
        state.AddRunningTestNode(id: 11, uid: "uid-middle", name: "MiddleTest", stopwatchFactory.CreateStopwatch());
        stopwatchFactory.AddTime(TimeSpan.FromSeconds(2));
        state.AddRunningTestNode(id: 12, uid: "uid-young", name: "YoungTest", stopwatchFactory.CreateStopwatch());

        List<TestDetailState> tasks = state.GetRunningTasks(maxCount: 5);

        Assert.HasCount(3, tasks);
        Assert.AreEqual("OldTest", tasks[0].Text);
        Assert.AreEqual("MiddleTest", tasks[1].Text);
        Assert.AreEqual("YoungTest", tasks[2].Text);
    }

    [TestMethod]
    public void TestNodeResultsState_GetRunningTasks_WhenMoreThanMax_TruncatesAndAppendsSummary()
    {
        var stopwatchFactory = new StopwatchFactory();
        var state = new TestNodeResultsState(1);
        // 5 running tasks, ages decreasing so "Test0" is oldest, "Test4" is youngest.
        for (int i = 0; i < 5; i++)
        {
            state.AddRunningTestNode(id: 10 + i, uid: $"uid-{i}", name: $"Test{i}", stopwatchFactory.CreateStopwatch());
            stopwatchFactory.AddTime(TimeSpan.FromSeconds(1));
        }

        List<TestDetailState> tasks = state.GetRunningTasks(maxCount: 3);

        // Expect exactly maxCount entries: (maxCount - 1) oldest tasks + 1 summary line.
        Assert.HasCount(3, tasks);
        Assert.AreEqual("Test0", tasks[0].Text);
        Assert.AreEqual("Test1", tasks[1].Text);
        // The trailing summary mentions how many tasks are NOT shown (5 - 2 = 3).
        // Assert exact text (matching the maxCount=1 test's pattern) so this can't accidentally
        // pass for unrelated reasons (any '3' anywhere in a localized/format-changed string).
        string expectedSummary = $"... {string.Format(CultureInfo.CurrentCulture, PlatformResources.ActiveTestsRunning_MoreTestsCount, 3)}";
        Assert.AreEqual(expectedSummary, tasks[2].Text);
    }

    [TestMethod]
    public void TestNodeResultsState_GetRunningTasks_WhenMaxCountIsOneAndMultipleRunning_ReturnsOnlySummary()
    {
        var stopwatchFactory = new StopwatchFactory();
        var state = new TestNodeResultsState(1);
        state.AddRunningTestNode(id: 10, uid: "uid-1", name: "FirstTest", stopwatchFactory.CreateStopwatch());
        state.AddRunningTestNode(id: 11, uid: "uid-2", name: "SecondTest", stopwatchFactory.CreateStopwatch());

        List<TestDetailState> tasks = state.GetRunningTasks(maxCount: 1);

        Assert.HasCount(1, tasks);
        // When maxCount is 1 and we're over budget, no individual test fits — we only show the summary.
        string expectedSummary = string.Format(CultureInfo.CurrentCulture, PlatformResources.ActiveTestsRunning_FullTestsCount, 2);
        Assert.AreEqual(expectedSummary, tasks[0].Text);
        Assert.DoesNotContain("FirstTest", tasks[0].Text);
        Assert.DoesNotContain("SecondTest", tasks[0].Text);
    }

    [TestMethod]
    public void TestNodeResultsState_GetRunningTasks_ReturnsCachedBufferReusedAcrossCalls()
    {
        var stopwatchFactory = new StopwatchFactory();
        var state = new TestNodeResultsState(1);
        state.AddRunningTestNode(id: 10, uid: "uid-1", name: "T1", stopwatchFactory.CreateStopwatch());

        List<TestDetailState> first = state.GetRunningTasks(maxCount: 5);
        List<TestDetailState> second = state.GetRunningTasks(maxCount: 5);

        // The buffer is intentionally reused (documented contract) — same reference across calls.
        Assert.AreSame(first, second);
    }

    [TestMethod]
    public void TerminalTestReporter_WhenInDiscoveryMode_ShouldIncrementDiscoveredTests()
    {
        // Arrange
        string assembly = "test.dll";
        string targetFramework = "net8.0";
        string architecture = "x64";
        var stringBuilderConsole = new StringBuilderConsole();
        var terminalReporter = new TerminalTestReporter(stringBuilderConsole, static () => false, new TerminalTestReporterOptions
        {
            ShowPassedTests = () => false,
            AnsiMode = AnsiMode.NoAnsi,
            ShowProgress = () => false,
        });

        DateTimeOffset startTime = DateTimeOffset.MinValue;
        DateTimeOffset endTime = DateTimeOffset.MaxValue;

        // Act
        terminalReporter.TestExecutionStarted(startTime, 1, isDiscovery: true, isHelp: false, isRetry: false);
        terminalReporter.AssemblyRunStarted(assembly, targetFramework, architecture, "0", "0");
        terminalReporter.TestDiscovered("0", "TestMethod1");
        terminalReporter.TestDiscovered("0", "TestMethod2");
        terminalReporter.AssemblyRunCompleted("0");
        terminalReporter.TestExecutionCompleted(endTime, exitCode: null);

        string output = stringBuilderConsole.Output;

        // Assert - should contain information about 2 tests discovered
        Assert.IsTrue(output.Contains('2') || output.Contains("TestMethod1"), "Output should contain information about discovered tests");
    }

    [TestMethod]
    public void TerminalTestReporter_WhenOrchestratorDiscoveryDisplayNameIsNull_CountsTestAndFallsBackToUid()
    {
        // Arrange
        string assembly = "test.dll";
        string targetFramework = "net8.0";
        string architecture = "x64";
        var stringBuilderConsole = new StringBuilderConsole();
        var terminalReporter = new TerminalTestReporter(stringBuilderConsole, static () => false, new TerminalTestReporterOptions
        {
            ShowPassedTests = () => false,
            AnsiMode = AnsiMode.NoAnsi,
            ShowProgress = () => false,
        });

        DateTimeOffset startTime = DateTimeOffset.MinValue;
        DateTimeOffset endTime = DateTimeOffset.MaxValue;

        // Act
        terminalReporter.TestExecutionStarted(startTime, 1, isDiscovery: true, isHelp: false, isRetry: false);
        terminalReporter.AssemblyRunStarted(assembly, targetFramework, architecture, "0", "0");

        // No display name and no uid: counted, but must not add a blank indented entry.
        terminalReporter.TestDiscovered("0", displayName: null, uid: null, filePath: null, lineNumber: null);
        // No display name but a uid: the uid is used as the listed name.
        terminalReporter.TestDiscovered("0", displayName: null, uid: "uid-fallback", filePath: null, lineNumber: null);
        // Normal display name.
        terminalReporter.TestDiscovered("0", "TestMethod1", uid: "uid-1", filePath: null, lineNumber: null);

        // Assert - every discovered test is counted (even the unnamed one). TotalTests is computed from
        // DiscoveredTests in discovery mode and is cleared by TestExecutionCompleted, so assert it before that.
        Assert.AreEqual(3, terminalReporter.TotalTests);

        terminalReporter.AssemblyRunCompleted("0");
        terminalReporter.TestExecutionCompleted(endTime, exitCode: null);

        string[] outputLines = stringBuilderConsole.Output.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

        // Assert - no blank indented entry for the unnamed test, but the uid fallback and display name are listed.
        Assert.DoesNotContain(TerminalTestReporter.SingleIndentation, outputLines);
        Assert.Contains($"{TerminalTestReporter.SingleIndentation}uid-fallback", outputLines);
        Assert.Contains($"{TerminalTestReporter.SingleIndentation}TestMethod1", outputLines);
    }

    [TestMethod]
    public void TerminalTestReporter_OrchestratorTestInProgress_TracksActiveTestLikeCoreOverload()
    {
        // The orchestrator (dotnet test) TestInProgress overload carries extra assembly/target-framework/architecture
        // and per-attempt instance metadata for call-site parity, but must track the active test exactly like the core
        // (executionId, uid, displayName) overload. Verify the orchestrator-driven test surfaces in the active-test
        // progress frame: its display name only appears in the output if it was registered as a running test.
        string targetFramework = "net8.0";
        string architecture = "x64";
        string assembly = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"C:\work\assembly.dll" : "/mnt/work/assembly.dll";

        var stringBuilderConsole = new StringBuilderConsole();
        var stopwatchFactory = new StopwatchFactory();
        var terminalReporter = new TerminalTestReporter(stringBuilderConsole, static () => false, new TerminalTestReporterOptions
        {
            ShowPassedTests = () => true,
            AnsiMode = AnsiMode.ForceAnsi,
            ShowActiveTests = true,
            ShowProgress = () => true,
        })
        {
            CreateStopwatch = stopwatchFactory.CreateStopwatch,
        };

        var startHandle = new AutoResetEvent(initialState: false);
        var stopHandle = new AutoResetEvent(initialState: false);

        // Disable the timer updates so the captured output is deterministic.
        terminalReporter.OnProgressStartUpdate += (sender, args) => startHandle.WaitOne();
        terminalReporter.OnProgressStopUpdate += (sender, args) => stopHandle.Set();

        DateTimeOffset startTime = DateTimeOffset.MinValue;
        terminalReporter.TestExecutionStarted(startTime, 1, isDiscovery: false, isHelp: false, isRetry: false);
        terminalReporter.AssemblyRunStarted(assembly, targetFramework, architecture, "0", "0");

        // A core-overload active test we will complete to trigger a progress redraw.
        terminalReporter.TestInProgress(executionId: "0", testNodeUid: "Trigger", displayName: "Trigger");
        stopwatchFactory.AddTime(TimeSpan.FromMilliseconds(1));

        // The orchestrator overload (extra assembly/targetFramework/architecture/instanceId args). This test is never
        // completed, so its name can only appear in the output via the active-test progress frame.
        terminalReporter.TestInProgress(assembly, targetFramework, architecture, executionId: "0", instanceId: "0", testNodeUid: "OrchestratorActive1", displayName: "OrchestratorActive1");
        stopwatchFactory.AddTime(TimeSpan.FromSeconds(1));

        // Completing the trigger test redraws the progress frame, which lists the still-active orchestrator test.
        terminalReporter.TestCompleted("0", testNodeUid: "Trigger", "Trigger", TestOutcome.Passed, TimeSpan.FromSeconds(10),
            informativeMessage: null, errorMessage: null, exception: null, expected: null, actual: null, standardOutput: null, errorOutput: null);

        string output = stringBuilderConsole.Output;
        startHandle.Set();
        stopHandle.WaitOne();

        // The orchestrator-driven test is registered as active and therefore rendered in the progress frame.
        Assert.Contains("OrchestratorActive1", output);
    }

    [TestMethod]
    public void TerminalTestReporter_WhenMultipleAssemblies_AggregatesCountsAndOmitsAssemblyLinkOnVerdict()
    {
        // Arrange — two assemblies (the dotnet test orchestrator case), each registered under its own
        // execution id. The reporter must aggregate the per-assembly counts into a single run summary and,
        // unlike the single-assembly (in-process) case, must NOT append a per-assembly link to the verdict
        // line because the per-assembly identity is rendered in the progress area instead.
        string firstAssembly = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"C:\work\first.dll" : "/mnt/work/first.dll";
        string secondAssembly = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"C:\work\second.dll" : "/mnt/work/second.dll";

        var stringBuilderConsole = new StringBuilderConsole();
        var terminalReporter = new TerminalTestReporter(stringBuilderConsole, static () => false, new TerminalTestReporterOptions
        {
            ShowPassedTests = () => false,
            AnsiMode = AnsiMode.NoAnsi,
            ShowProgress = () => false,
        });

        DateTimeOffset startTime = DateTimeOffset.MinValue;
        DateTimeOffset endTime = DateTimeOffset.MaxValue;

        // Act
        terminalReporter.TestExecutionStarted(startTime, workerCount: 2, isDiscovery: false, isHelp: false, isRetry: false);

        terminalReporter.AssemblyRunStarted(firstAssembly, "net8.0", "x64", "exec-1", "exec-1");
        terminalReporter.AssemblyRunStarted(secondAssembly, "net9.0", "arm64", "exec-2", "exec-2");

        terminalReporter.TestCompleted("exec-1", testNodeUid: "A1", "A1", TestOutcome.Passed, TimeSpan.FromSeconds(1),
            informativeMessage: null, errorMessage: null, exception: null, expected: null, actual: null, standardOutput: null, errorOutput: null);
        terminalReporter.TestCompleted("exec-1", testNodeUid: "A2", "A2", TestOutcome.Fail, TimeSpan.FromSeconds(1),
            informativeMessage: null, errorMessage: "boom", exception: null, expected: null, actual: null, standardOutput: null, errorOutput: null);
        terminalReporter.TestCompleted("exec-2", testNodeUid: "B1", "B1", TestOutcome.Passed, TimeSpan.FromSeconds(1),
            informativeMessage: null, errorMessage: null, exception: null, expected: null, actual: null, standardOutput: null, errorOutput: null);
        terminalReporter.TestCompleted("exec-2", testNodeUid: "B2", "B2", TestOutcome.Skipped, TimeSpan.FromSeconds(1),
            informativeMessage: null, errorMessage: null, exception: null, expected: null, actual: null, standardOutput: null, errorOutput: null);

        terminalReporter.AssemblyRunCompleted("exec-1");
        terminalReporter.AssemblyRunCompleted("exec-2");

        // TotalTests aggregates across both assemblies (captured before TestExecutionCompleted clears state).
        Assert.AreEqual(4, terminalReporter.TotalTests);

        terminalReporter.TestExecutionCompleted(endTime, exitCode: null);

        string output = stringBuilderConsole.Output;

        // Assert — counts are aggregated across both assemblies.
        Assert.Contains("  total: 4", output);
        Assert.Contains("  failed: 1", output);
        Assert.Contains("  succeeded: 2", output);
        Assert.Contains("  skipped: 1", output);

        // The verdict line must be link-free for multiple assemblies: unlike the single-assembly case the
        // assembly path is never appended to the "Test run summary:" line.
        Assert.DoesNotContain(firstAssembly, output);
        Assert.DoesNotContain(secondAssembly, output);
    }

    // Ported from the dotnet/sdk TerminalTestReporterTests (regression for dotnet/sdk#51608) to validate the
    // orchestrator handshake-failure surface of the shared reporter: if a child test host process exits before a
    // session was ever started (so the execution id is never registered), the orchestrator overload of
    // AssemblyRunCompleted must not throw — it must surface the exit as a handshake failure and render the
    // actionable context (exit code + captured stdout/stderr) instead.
    [TestMethod]
    public void AssemblyRunCompleted_WhenExecutionIdUnknown_DoesNotThrowAndReportsHandshakeFailure()
    {
        var stringBuilderConsole = new StringBuilderConsole();
        var terminalReporter = new TerminalTestReporter(stringBuilderConsole, new TerminalTestReporterOptions
        {
            AnsiMode = AnsiMode.SimpleAnsi,
            ShowProgress = () => false,
        });

        // Must not throw even though "never-registered" was never passed to AssemblyRunStarted.
        terminalReporter.AssemblyRunCompleted(executionId: "never-registered", exitCode: 1, outputData: "stdout", errorData: "stderr");

        Assert.IsTrue(terminalReporter.HasHandshakeFailure);

        // Validate the rendered UI state: the immediate failure context is printed.
        string output = stringBuilderConsole.Output;
        Assert.Contains(TerminalResources.ZeroTestsRan, output);
        Assert.Contains($"{TerminalResources.ExitCode}: 1", output);
        Assert.Contains("stdout", output);
        Assert.Contains("stderr", output);
    }

    // Companion to the test above covering the full lifecycle: after a handshake failure, TestExecutionCompleted must
    // re-print the failure recap in the summary and (via runFailed |= HasHandshakeFailure) mark the run as failed even
    // though no test ever ran.
    [TestMethod]
    public void AssemblyRunCompleted_WhenExecutionIdUnknown_SummaryReprintsRecapAndReportsFailure()
    {
        var stringBuilderConsole = new StringBuilderConsole();
        var terminalReporter = new TerminalTestReporter(stringBuilderConsole, new TerminalTestReporterOptions
        {
            AnsiMode = AnsiMode.NoAnsi,
            ShowProgress = () => false,
        });

        terminalReporter.TestExecutionStarted(DateTimeOffset.MinValue, workerCount: 1, isDiscovery: false, isHelp: false, isRetry: false);
        terminalReporter.AssemblyRunCompleted("never-registered", exitCode: 1, outputData: "the out", errorData: "the err");

        // The flag is observable before the run completes (the orchestrator reads it to force a non-zero exit).
        Assert.IsTrue(terminalReporter.HasHandshakeFailure);

        terminalReporter.TestExecutionCompleted(DateTimeOffset.MaxValue, exitCode: null);

        string output = stringBuilderConsole.Output;

        // The end-of-run recap header is re-printed in the summary with the captured failure context.
        Assert.Contains(TerminalResources.HandshakeFailuresHeader, output);
        Assert.Contains($"{TerminalResources.ExitCode}: 1", output);
        Assert.Contains("the err", output);

        // The summary verdict escalates to "Failed!" rather than the benign "Zero tests ran": a handshake failure
        // must not be masked as an empty run (dotnet/sdk#51608). The per-assembly immediate-failure context above
        // still legitimately says "Zero tests ran" (the assembly really did register zero tests).
        Assert.Contains($"{TerminalResources.TestRunSummary} {TerminalResources.Failed}!", output);

        // Per-run state is reset after completion so a subsequent session starts fresh.
        Assert.IsFalse(terminalReporter.HasHandshakeFailure);
    }

    // Ported from the dotnet/sdk TerminalTestReporterTests (dotnet/sdk#52128) to validate the orchestrator per-assembly
    // summary of the shared reporter: when an assembly completes with ShowAssembly + ShowAssemblyStartAndComplete, the
    // mid-stream summary line must include the per-assembly counts in the compact bracketed form. NoAnsi is used so the
    // assertion is on plain text; it uses the same ASCII glyph set ([+P/xF/?S]) the SDK asserts via SimpleTerminal.
    [TestMethod]
    public void AssemblyRunCompleted_WithShowAssemblyStartAndComplete_PrintsPerAssemblyCounts()
    {
        var stringBuilderConsole = new StringBuilderConsole();
        var terminalReporter = new TerminalTestReporter(stringBuilderConsole, new TerminalTestReporterOptions
        {
            AnsiMode = AnsiMode.NoAnsi,
            ShowProgress = () => false,
            ShowAssembly = true,
            ShowAssemblyStartAndComplete = true,
        });

        terminalReporter.TestExecutionStarted(DateTimeOffset.MinValue, workerCount: 1, isDiscovery: false, isHelp: false, isRetry: false);

        string assembly = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"C:\repo\MyTests.dll" : "/repo/MyTests.dll";
        const string executionId = "exec-1";
        terminalReporter.AssemblyRunStarted(assembly, "net9.0", "x64", executionId, instanceId: "inst-1");

        ReportOrchestratorTest(terminalReporter, assembly, executionId, instanceId: "inst-1", testUid: "t-pass-1", TestOutcome.Passed);
        ReportOrchestratorTest(terminalReporter, assembly, executionId, instanceId: "inst-1", testUid: "t-pass-2", TestOutcome.Passed);
        ReportOrchestratorTest(terminalReporter, assembly, executionId, instanceId: "inst-1", testUid: "t-pass-3", TestOutcome.Passed);
        ReportOrchestratorTest(terminalReporter, assembly, executionId, instanceId: "inst-1", testUid: "t-skip-1", TestOutcome.Skipped);

        terminalReporter.AssemblyRunCompleted(executionId, exitCode: 0, outputData: null, errorData: null);

        string assemblyLine = GetAssemblySummaryLine(stringBuilderConsole.Output, assembly);
        Assert.Contains(ExpectedCounts(3, 0, 1), assemblyLine);
    }

    // Covers the three red branches of AppendAssemblyResult for the per-assembly summary line, which the happy-path
    // oracle test above does not reach: (failed > 0) -> "failed with N error(s)", (no tests) -> "Zero tests ran",
    // and (process failed but every test passed) -> "failed".
    [TestMethod]
    public void AssemblyRunCompleted_WhenAssemblyHasFailedTests_PrintsFailedWithErrors()
    {
        var stringBuilderConsole = new StringBuilderConsole();
        var terminalReporter = new TerminalTestReporter(stringBuilderConsole, new TerminalTestReporterOptions
        {
            AnsiMode = AnsiMode.NoAnsi,
            ShowProgress = () => false,
            ShowAssembly = true,
            ShowAssemblyStartAndComplete = true,
        });

        terminalReporter.TestExecutionStarted(DateTimeOffset.MinValue, workerCount: 1, isDiscovery: false, isHelp: false, isRetry: false);

        string assembly = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"C:\repo\Failing.dll" : "/repo/Failing.dll";
        const string executionId = "exec-failed";
        terminalReporter.AssemblyRunStarted(assembly, "net9.0", "x64", executionId, instanceId: "inst-1");

        ReportOrchestratorTest(terminalReporter, assembly, executionId, instanceId: "inst-1", testUid: "t-pass-1", TestOutcome.Passed);
        ReportOrchestratorTest(terminalReporter, assembly, executionId, instanceId: "inst-1", testUid: "t-fail-1", TestOutcome.Fail);

        terminalReporter.AssemblyRunCompleted(executionId, exitCode: 1, outputData: null, errorData: null);

        string assemblyLine = GetAssemblySummaryLine(stringBuilderConsole.Output, assembly);
        Assert.Contains(string.Format(CultureInfo.CurrentCulture, TerminalResources.FailedWithErrors, 1), assemblyLine);
        Assert.Contains(ExpectedCounts(1, 1, 0), assemblyLine);
    }

    [TestMethod]
    public void AssemblyRunCompleted_WhenNoTestsRanAndProcessFailed_PrintsZeroTestsRan()
    {
        var stringBuilderConsole = new StringBuilderConsole();
        var terminalReporter = new TerminalTestReporter(stringBuilderConsole, new TerminalTestReporterOptions
        {
            AnsiMode = AnsiMode.NoAnsi,
            ShowProgress = () => false,
            ShowAssembly = true,
            ShowAssemblyStartAndComplete = true,
        });

        terminalReporter.TestExecutionStarted(DateTimeOffset.MinValue, workerCount: 1, isDiscovery: false, isHelp: false, isRetry: false);

        string assembly = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"C:\repo\Empty.dll" : "/repo/Empty.dll";
        const string executionId = "exec-empty";
        terminalReporter.AssemblyRunStarted(assembly, "net9.0", "x64", executionId, instanceId: "inst-1");

        // No tests reported; the process exits non-zero.
        terminalReporter.AssemblyRunCompleted(executionId, exitCode: 1, outputData: null, errorData: null);

        string assemblyLine = GetAssemblySummaryLine(stringBuilderConsole.Output, assembly);
        Assert.Contains(TerminalResources.ZeroTestsRan, assemblyLine);
        Assert.Contains(ExpectedCounts(0, 0, 0), assemblyLine);
    }

    [TestMethod]
    public void AssemblyRunCompleted_WhenProcessFailedButAllTestsPassed_PrintsFailed()
    {
        var stringBuilderConsole = new StringBuilderConsole();
        var terminalReporter = new TerminalTestReporter(stringBuilderConsole, new TerminalTestReporterOptions
        {
            AnsiMode = AnsiMode.NoAnsi,
            ShowProgress = () => false,
            ShowAssembly = true,
            ShowAssemblyStartAndComplete = true,
        });

        terminalReporter.TestExecutionStarted(DateTimeOffset.MinValue, workerCount: 1, isDiscovery: false, isHelp: false, isRetry: false);

        string assembly = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"C:\repo\CrashedAfterPass.dll" : "/repo/CrashedAfterPass.dll";
        const string executionId = "exec-crash";
        terminalReporter.AssemblyRunStarted(assembly, "net9.0", "x64", executionId, instanceId: "inst-1");

        // All tests passed but the process exits non-zero (e.g. a crash after the run), so the assembly is not a success
        // even though FailedTests == 0 and TotalTests > 0.
        ReportOrchestratorTest(terminalReporter, assembly, executionId, instanceId: "inst-1", testUid: "t-pass-1", TestOutcome.Passed);

        terminalReporter.AssemblyRunCompleted(executionId, exitCode: 1, outputData: null, errorData: null);

        string assemblyLine = GetAssemblySummaryLine(stringBuilderConsole.Output, assembly);
        Assert.Contains(TerminalResources.FailedLowercase, assemblyLine);
        Assert.Contains(ExpectedCounts(1, 0, 0), assemblyLine);
    }

    // Ported from the dotnet/sdk TerminalTestReporterTests (dotnet/sdk#52128): in the final test-run summary, when
    // more than one assembly ran with ShowAssembly, each assembly entry must include its own per-assembly counts in
    // the compact bracketed form. NoAnsi is used so the assertion is on plain text (same ASCII glyph set).
    [TestMethod]
    public void TestExecutionCompleted_WithMultipleAssemblies_PrintsPerAssemblyCountsInSummary()
    {
        var stringBuilderConsole = new StringBuilderConsole();
        var terminalReporter = new TerminalTestReporter(stringBuilderConsole, new TerminalTestReporterOptions
        {
            AnsiMode = AnsiMode.NoAnsi,
            ShowProgress = () => false,
            ShowAssembly = true,

            // Suppress mid-stream per-assembly lines so we assert against the final summary only.
            ShowAssemblyStartAndComplete = false,
        });

        terminalReporter.TestExecutionStarted(DateTimeOffset.MinValue, workerCount: 2, isDiscovery: false, isHelp: false, isRetry: false);

        string assemblyA = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"C:\repo\A.Tests.dll" : "/repo/A.Tests.dll";
        string assemblyB = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"C:\repo\B.Tests.dll" : "/repo/B.Tests.dll";

        terminalReporter.AssemblyRunStarted(assemblyA, "net9.0", "x64", executionId: "exec-A", instanceId: "inst-A");
        terminalReporter.AssemblyRunStarted(assemblyB, "net9.0", "x64", executionId: "exec-B", instanceId: "inst-B");

        // Assembly A: 2 passed, 1 failed, 0 skipped.
        ReportOrchestratorTest(terminalReporter, assemblyA, "exec-A", "inst-A", "a-1", TestOutcome.Passed);
        ReportOrchestratorTest(terminalReporter, assemblyA, "exec-A", "inst-A", "a-2", TestOutcome.Passed);
        ReportOrchestratorTest(terminalReporter, assemblyA, "exec-A", "inst-A", "a-3", TestOutcome.Fail);

        // Assembly B: 5 passed, 0 failed, 2 skipped.
        ReportOrchestratorTest(terminalReporter, assemblyB, "exec-B", "inst-B", "b-1", TestOutcome.Passed);
        ReportOrchestratorTest(terminalReporter, assemblyB, "exec-B", "inst-B", "b-2", TestOutcome.Passed);
        ReportOrchestratorTest(terminalReporter, assemblyB, "exec-B", "inst-B", "b-3", TestOutcome.Passed);
        ReportOrchestratorTest(terminalReporter, assemblyB, "exec-B", "inst-B", "b-4", TestOutcome.Passed);
        ReportOrchestratorTest(terminalReporter, assemblyB, "exec-B", "inst-B", "b-5", TestOutcome.Passed);
        ReportOrchestratorTest(terminalReporter, assemblyB, "exec-B", "inst-B", "b-6", TestOutcome.Skipped);
        ReportOrchestratorTest(terminalReporter, assemblyB, "exec-B", "inst-B", "b-7", TestOutcome.Skipped);

        terminalReporter.AssemblyRunCompleted(executionId: "exec-A", exitCode: 1, outputData: null, errorData: null);
        terminalReporter.AssemblyRunCompleted(executionId: "exec-B", exitCode: 0, outputData: null, errorData: null);

        terminalReporter.TestExecutionCompleted(DateTimeOffset.MaxValue, exitCode: 1);

        string output = stringBuilderConsole.Output;
        Assert.Contains(ExpectedCounts(2, 1, 0), GetAssemblySummaryLine(output, assemblyA));
        Assert.Contains(ExpectedCounts(5, 0, 2), GetAssemblySummaryLine(output, assemblyB));
    }

    // Ported from the dotnet/sdk TerminalTestReporterTests: when an assembly's tests were retried, the per-assembly
    // summary appends a "/r{N}" segment so the user can tell the final counts came from retries. Attempt 1 fails the
    // test; attempt 2 (a new instance id under the same execution id) passes it, so the final tally is 1 passed with
    // 1 retried.
    [TestMethod]
    public void AssemblyRunCompleted_WhenTestsWereRetried_ShowsRetriedCount()
    {
        var stringBuilderConsole = new StringBuilderConsole();
        var terminalReporter = new TerminalTestReporter(stringBuilderConsole, new TerminalTestReporterOptions
        {
            AnsiMode = AnsiMode.NoAnsi,
            ShowProgress = () => false,
            ShowAssembly = true,
            ShowAssemblyStartAndComplete = true,
        });

        terminalReporter.TestExecutionStarted(DateTimeOffset.MinValue, workerCount: 1, isDiscovery: false, isHelp: false, isRetry: true);

        string assembly = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"C:\repo\Flaky.Tests.dll" : "/repo/Flaky.Tests.dll";
        const string executionId = "exec-flaky";

        // Attempt 1: register the first instance and report a failure.
        terminalReporter.AssemblyRunStarted(assembly, "net9.0", "x64", executionId, instanceId: "inst-1");
        ReportOrchestratorTest(terminalReporter, assembly, executionId, instanceId: "inst-1", testUid: "flaky-1", TestOutcome.Fail);

        // Attempt 2: a new instance id triggers a retry; the failing test now passes.
        terminalReporter.AssemblyRunStarted(assembly, "net9.0", "x64", executionId, instanceId: "inst-2");
        ReportOrchestratorTest(terminalReporter, assembly, executionId, instanceId: "inst-2", testUid: "flaky-1", TestOutcome.Passed);

        terminalReporter.AssemblyRunCompleted(executionId, exitCode: 0, outputData: null, errorData: null);

        string assemblyLine = GetAssemblySummaryLine(stringBuilderConsole.Output, assembly);
        Assert.Contains(ExpectedCounts(1, 0, 0, retried: 1), assemblyLine);
    }

    // Companion to the test above driving the FULL lifecycle to validate the two retry-specific renderings the
    // dotnet/sdk orchestrator acceptance test RunTestProjectWithWithRetryFeature_ShouldSucceed asserts:
    //   1) each per-test result line is annotated with "(try N)" so retried attempts are distinguishable, and
    //   2) the run summary's total line is suffixed with "(+N retried)".
    // The in-process host never retries (isRetry stays false, TryCount stays 1), so neither rendering appears there.
    [TestMethod]
    public void TestExecutionCompleted_WhenTestsWereRetried_AnnotatesTryNumberAndSummaryRetriedCount()
    {
        var stringBuilderConsole = new StringBuilderConsole();
        var terminalReporter = new TerminalTestReporter(stringBuilderConsole, new TerminalTestReporterOptions
        {
            AnsiMode = AnsiMode.NoAnsi,
            ShowProgress = () => false,
            ShowPassedTests = () => true,
            ShowAssembly = true,
            ShowAssemblyStartAndComplete = true,
        });

        terminalReporter.TestExecutionStarted(DateTimeOffset.MinValue, workerCount: 1, isDiscovery: false, isHelp: false, isRetry: true);

        string assembly = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"C:\repo\Flaky.Tests.dll" : "/repo/Flaky.Tests.dll";
        const string executionId = "exec-flaky";

        // Attempt 1 fails the test...
        terminalReporter.AssemblyRunStarted(assembly, "net9.0", "x64", executionId, instanceId: "inst-1");
        ReportOrchestratorTest(terminalReporter, assembly, executionId, instanceId: "inst-1", testUid: "flaky-1", TestOutcome.Fail);

        // ...attempt 2 (new instance id) retries and passes it.
        terminalReporter.AssemblyRunStarted(assembly, "net9.0", "x64", executionId, instanceId: "inst-2");
        ReportOrchestratorTest(terminalReporter, assembly, executionId, instanceId: "inst-2", testUid: "flaky-1", TestOutcome.Passed);

        terminalReporter.AssemblyRunCompleted(executionId, exitCode: 0, outputData: null, errorData: null);
        terminalReporter.TestExecutionCompleted(DateTimeOffset.MaxValue, exitCode: 0);

        string output = stringBuilderConsole.Output;

        // 1) Per-test "(try N)" annotation: the failing first attempt is "(try 1)", the passing retry is "(try 2)".
        string tryOne = string.Format(CultureInfo.CurrentCulture, TerminalResources.Try, 1);
        string tryTwo = string.Format(CultureInfo.CurrentCulture, TerminalResources.Try, 2);
        Assert.Contains($"({tryOne})", output);
        Assert.Contains($"({tryTwo})", output);
        Assert.DoesNotContain($"({string.Format(CultureInfo.CurrentCulture, TerminalResources.Try, 3)})", output);

        // 2) Summary total line carries the "(+1 retried)" suffix.
        Assert.Contains($"{TerminalResources.TotalLowercase}: 1 (+1 {TerminalResources.Retried})", output);

        // The retry also surfaces in the per-assembly "(try N) Running tests from" banner.
        Assert.Contains($"({tryTwo}) {TerminalResources.RunningTestsFrom}", output);
    }

    // Orchestrator discovery (dotnet test --list-tests across N assemblies): each assembly gets a
    // "Discovered N tests in assembly - <link>" header with its test names listed, and the run ends with a
    // "Discovered M tests in K assemblies." total. The in-process host (ShowAssembly off) keeps its own
    // "Test discovery summary: found N test(s)" format, covered by the existing discovery tests.
    [TestMethod]
    public void AppendTestDiscoverySummary_ForOrchestrator_PrintsPerAssemblyDiscoveredCountsAndTotal()
    {
        var stringBuilderConsole = new StringBuilderConsole();
        TerminalTestReporter terminalReporter = CreateOrchestratorReporter(stringBuilderConsole);
        terminalReporter.TestExecutionStarted(DateTimeOffset.MinValue, workerCount: 2, isDiscovery: true, isHelp: false, isRetry: false);

        string assemblyA = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"C:\repo\A.Tests.dll" : "/repo/A.Tests.dll";
        string assemblyB = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"C:\repo\B.Tests.dll" : "/repo/B.Tests.dll";

        terminalReporter.AssemblyRunStarted(assemblyA, "net9.0", "x64", executionId: "exec-A", instanceId: "inst-A");
        terminalReporter.AssemblyRunStarted(assemblyB, "net9.0", "x64", executionId: "exec-B", instanceId: "inst-B");

        terminalReporter.TestDiscovered("exec-A", "A.Test1");
        terminalReporter.TestDiscovered("exec-A", "A.Test2");
        terminalReporter.TestDiscovered("exec-B", "B.Test1");

        terminalReporter.AssemblyRunCompleted("exec-A");
        terminalReporter.AssemblyRunCompleted("exec-B");
        terminalReporter.TestExecutionCompleted(DateTimeOffset.MaxValue, exitCode: 0);

        string output = stringBuilderConsole.Output;

        // Per-assembly discovered-count headers and the listed test names.
        Assert.Contains(string.Format(CultureInfo.CurrentCulture, TerminalResources.DiscoveredTestsInAssembly, 2), output);
        Assert.Contains(string.Format(CultureInfo.CurrentCulture, TerminalResources.DiscoveredTestsInAssembly, 1), output);
        Assert.Contains("A.Test1", output);
        Assert.Contains("B.Test1", output);

        // Run-level total across both assemblies.
        Assert.Contains(string.Format(CultureInfo.CurrentCulture, TerminalResources.DiscoveredTestsSummary, 3, 2), output);

        // The in-process-only "Test discovery summary: found N test(s)" wording must NOT appear for the orchestrator.
        Assert.DoesNotContain(string.Format(CultureInfo.CurrentCulture, TerminalResources.TestDiscoverySummarySingular, 3), output);
    }

    [TestMethod]
    public void AssemblyRunCompleted_WhenKnownAssemblyFails_PrintsExecutableSummary()
    {
        var stringBuilderConsole = new StringBuilderConsole();
        TerminalTestReporter terminalReporter = CreateOrchestratorReporter(stringBuilderConsole);
        terminalReporter.TestExecutionStarted(DateTimeOffset.MinValue, workerCount: 1, isDiscovery: false, isHelp: false, isRetry: false);

        string assembly = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"C:\repo\Failing.dll" : "/repo/Failing.dll";
        terminalReporter.AssemblyRunStarted(assembly, "net9.0", "x64", "exec-1", "inst-1");
        ReportOrchestratorTest(terminalReporter, assembly, "exec-1", "inst-1", "t-1", TestOutcome.Fail);

        terminalReporter.AssemblyRunCompleted("exec-1", exitCode: 3, outputData: "the stdout", errorData: "the stderr");

        string output = stringBuilderConsole.Output;
        Assert.Contains($"{TerminalResources.ExitCode}: 3", output);
        Assert.Contains("the stdout", output);
        Assert.Contains("the stderr", output);
        Assert.IsFalse(terminalReporter.HasHandshakeFailure);
    }

    [TestMethod]
    public void AssemblyRunCompleted_WhenKnownAssemblySucceeds_DoesNotPrintExecutableSummary()
    {
        var stringBuilderConsole = new StringBuilderConsole();

        // Keep the default ShowAssemblyStartAndComplete: true so the reporter DOES print the per-assembly summary
        // line. Otherwise a zero-exit run writes nothing at all and the DoesNotContain assertion below passes
        // vacuously instead of verifying that the executable-summary block is specifically suppressed on success.
        TerminalTestReporter terminalReporter = CreateOrchestratorReporter(stringBuilderConsole);
        terminalReporter.TestExecutionStarted(DateTimeOffset.MinValue, workerCount: 1, isDiscovery: false, isHelp: false, isRetry: false);

        string assembly = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"C:\repo\Passing.dll" : "/repo/Passing.dll";
        terminalReporter.AssemblyRunStarted(assembly, "net9.0", "x64", "exec-1", "inst-1");
        ReportOrchestratorTest(terminalReporter, assembly, "exec-1", "inst-1", "t-1", TestOutcome.Passed);

        terminalReporter.AssemblyRunCompleted("exec-1", exitCode: 0, outputData: "ignored", errorData: "ignored");

        // The per-assembly summary line is printed (proving the run produced output)...
        string assemblyLine = GetAssemblySummaryLine(stringBuilderConsole.Output, assembly);
        Assert.Contains(ExpectedCounts(1, 0, 0), assemblyLine);

        // ...but on success the executable summary (exit code + captured output) must not be printed.
        Assert.DoesNotContain($"{TerminalResources.ExitCode}:", stringBuilderConsole.Output);
    }

    // Drives the dotnet/sdk acceptance scenario RunMTPProjectThatCrashesWithExitCodeNonZero_ShouldFail_WithSameExitCode:
    // an assembly whose tests all pass but whose process exits non-zero (a crash / explicit non-zero exit) is a run
    // failure. The run-summary verdict must escalate to "Failed!" even though failed-test count is zero — but only
    // AFTER the zero-tests branch, so a legitimately empty project is unaffected (covered separately).
    [TestMethod]
    public void TestExecutionCompleted_WhenAssemblyExitsNonZeroButTestsPassed_ReportsFailedVerdict()
    {
        var stringBuilderConsole = new StringBuilderConsole();
        TerminalTestReporter terminalReporter = CreateOrchestratorReporter(stringBuilderConsole);
        terminalReporter.TestExecutionStarted(DateTimeOffset.MinValue, workerCount: 1, isDiscovery: false, isHelp: false, isRetry: false);

        string assembly = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"C:\repo\Crashy.dll" : "/repo/Crashy.dll";
        terminalReporter.AssemblyRunStarted(assembly, "net9.0", "x64", "exec-1", "inst-1");
        ReportOrchestratorTest(terminalReporter, assembly, "exec-1", "inst-1", "t-1", TestOutcome.Passed);

        // The process exits 47 even though the single test passed -> Success is false.
        terminalReporter.AssemblyRunCompleted("exec-1", exitCode: 47, outputData: null, errorData: null);
        terminalReporter.TestExecutionCompleted(DateTimeOffset.MaxValue, exitCode: 47);

        string output = stringBuilderConsole.Output;

        // The run verdict escalates to "Failed!" (not "Passed!") because the assembly process failed.
        Assert.Contains($"{TerminalResources.TestRunSummary} {TerminalResources.Failed}!", output);
        Assert.DoesNotContain($"{TerminalResources.TestRunSummary} {TerminalResources.Passed}!", output);

        // The summary surfaces the failed-process count on a dedicated "error: 1" line.
        Assert.Contains($"{TerminalResources.Error}: 1", output);
    }

    [TestMethod]
    public void TestExecutionCompleted_WhenHandshakeFailures_PrintsRecapAndFailsRun()
    {
        var stringBuilderConsole = new StringBuilderConsole();
        TerminalTestReporter terminalReporter = CreateOrchestratorReporter(stringBuilderConsole, showAssemblyStartAndComplete: false);
        terminalReporter.TestExecutionStarted(DateTimeOffset.MinValue, workerCount: 2, isDiscovery: false, isHelp: false, isRetry: false);

        // Two assemblies fail to handshake (their execution ids were never registered). The assembly paths are not
        // observable here: an unregistered completion is recorded with assemblyPath: string.Empty.
        terminalReporter.AssemblyRunCompleted("never-A", exitCode: 1, outputData: null, errorData: "A failed");
        terminalReporter.AssemblyRunCompleted("never-B", exitCode: 2, outputData: null, errorData: "B failed");

        Assert.IsTrue(terminalReporter.HasHandshakeFailure);

        terminalReporter.TestExecutionCompleted(DateTimeOffset.MaxValue, exitCode: 1);

        string output = stringBuilderConsole.Output;

        // The end-of-run recap header is printed and the captured failure output is surfaced.
        Assert.Contains(TerminalResources.HandshakeFailuresHeader, output);
        Assert.Contains("A failed", output);
        Assert.Contains("B failed", output);

        // With every assembly failing to handshake and zero tests, the summary verdict is "Failed!", not the
        // benign "Zero tests ran" (dotnet/sdk#51608).
        Assert.Contains($"{TerminalResources.TestRunSummary} {TerminalResources.Failed}!", output);
    }

    // dotnet/sdk#51952: an assembly that DOES handshake but then exits non-zero without any failed test (a crash,
    // Environment.FailFast, a hang-dump kill, an option rejected after the handshake, ...) has its process output
    // printed inline when it completes, which is easily lost in the middle of a large multi-assembly run. The
    // end-of-run summary must re-print an "Errored assemblies:" recap with the captured output so it is discoverable.
    [TestMethod]
    public void TestExecutionCompleted_WhenAssemblyErroredWithoutFailedTests_ReprintsErroredAssemblyRecap()
    {
        var stringBuilderConsole = new StringBuilderConsole();
        TerminalTestReporter terminalReporter = CreateOrchestratorReporter(stringBuilderConsole, showAssemblyStartAndComplete: false);
        terminalReporter.TestExecutionStarted(DateTimeOffset.MinValue, workerCount: 1, isDiscovery: false, isHelp: false, isRetry: false);

        string assembly = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"C:\repo\Crashy.dll" : "/repo/Crashy.dll";
        terminalReporter.AssemblyRunStarted(assembly, "net9.0", "x64", "exec-1", "inst-1");
        ReportOrchestratorTest(terminalReporter, assembly, "exec-1", "inst-1", "t-1", TestOutcome.Passed);

        // The single test passed but the process exits non-zero -> error category (FailedTests == 0).
        terminalReporter.AssemblyRunCompleted("exec-1", exitCode: 42, outputData: "boom stdout", errorData: "boom stderr");

        // It is NOT a handshake failure (the assembly was registered).
        Assert.IsFalse(terminalReporter.HasHandshakeFailure);

        terminalReporter.TestExecutionCompleted(DateTimeOffset.MaxValue, exitCode: 42);

        string output = stringBuilderConsole.Output;

        // The end-of-run recap header is printed and identifies the errored assembly with its captured output.
        Assert.Contains(TerminalResources.ErroredAssembliesHeader, output);
        string recap = output[output.IndexOf(TerminalResources.ErroredAssembliesHeader, StringComparison.Ordinal)..];
        Assert.Contains("Crashy.dll", recap);
        Assert.Contains($"{TerminalResources.ExitCode}: 42", recap);
        Assert.Contains("boom stdout", recap);
        Assert.Contains("boom stderr", recap);
    }

    // Guard for the recap's scope: an assembly that exits non-zero BECAUSE a test failed must not be added to the
    // "Errored assemblies:" recap - those failures are already reported per-test, and re-dumping every failing
    // assembly's process output at the end would be noise. The recap is reserved for unexplained process errors.
    [TestMethod]
    public void TestExecutionCompleted_WhenAssemblyExitedNonZeroWithFailedTests_DoesNotReprintErroredAssemblyRecap()
    {
        var stringBuilderConsole = new StringBuilderConsole();
        TerminalTestReporter terminalReporter = CreateOrchestratorReporter(stringBuilderConsole, showAssemblyStartAndComplete: false);
        terminalReporter.TestExecutionStarted(DateTimeOffset.MinValue, workerCount: 1, isDiscovery: false, isHelp: false, isRetry: false);

        string assembly = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"C:\repo\Failing.dll" : "/repo/Failing.dll";
        terminalReporter.AssemblyRunStarted(assembly, "net9.0", "x64", "exec-1", "inst-1");
        ReportOrchestratorTest(terminalReporter, assembly, "exec-1", "inst-1", "t-1", TestOutcome.Fail);

        terminalReporter.AssemblyRunCompleted("exec-1", exitCode: 1, outputData: null, errorData: null);
        terminalReporter.TestExecutionCompleted(DateTimeOffset.MaxValue, exitCode: 1);

        // No "Errored assemblies:" recap: the failure is already reported through the failed test.
        Assert.DoesNotContain(TerminalResources.ErroredAssembliesHeader, stringBuilderConsole.Output);
    }

    [TestMethod]
    public void AssemblyRunStarted_AfterRetry_RendersLatestAttemptCounts()
    {
        var stringBuilderConsole = new StringBuilderConsole();
        TerminalTestReporter terminalReporter = CreateOrchestratorReporter(stringBuilderConsole);
        terminalReporter.TestExecutionStarted(DateTimeOffset.MinValue, workerCount: 1, isDiscovery: false, isHelp: false, isRetry: true);

        string assembly = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"C:\repo\Flaky.dll" : "/repo/Flaky.dll";

        terminalReporter.AssemblyRunStarted(assembly, "net9.0", "x64", "exec-1", "inst-1");
        terminalReporter.AssemblyRunStarted(assembly, "net9.0", "x64", "exec-1", "inst-2");

        // Re-registering the same instance id is a no-op (not a new attempt).
        terminalReporter.AssemblyRunStarted(assembly, "net9.0", "x64", "exec-1", "inst-2");

        ReportOrchestratorTest(terminalReporter, assembly, "exec-1", "inst-2", "t-1", TestOutcome.Passed);
        terminalReporter.AssemblyRunCompleted("exec-1", exitCode: 0, outputData: null, errorData: null);

        // The per-assembly counts block reflects the latest attempt's single pass. (The "/r" segment tracks
        // RetriedFailedTests - tests that failed then passed on retry - which is 0 here, not the attempt count.)
        string assemblyLine = GetAssemblySummaryLine(stringBuilderConsole.Output, assembly);
        Assert.Contains(ExpectedCounts(1, 0, 0), assemblyLine);
    }

    [TestMethod]
    public void TestExecutionCompleted_WhenShowSlowestTestsSet_PrintsFlatSlowestSectionRankedByDuration()
    {
        var stringBuilderConsole = new StringBuilderConsole();
        var terminalReporter = new TerminalTestReporter(stringBuilderConsole, static () => false, new TerminalTestReporterOptions
        {
            AnsiMode = AnsiMode.NoAnsi,
            ShowProgress = () => false,
            SlowestTestsCount = 2,
        });

        terminalReporter.TestExecutionStarted(DateTimeOffset.MinValue, 1, isDiscovery: false, isHelp: false, isRetry: false);
        string assembly = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"C:\work\assembly.dll" : "/mnt/work/assembly.dll";
        terminalReporter.AssemblyRunStarted(assembly, "net9.0", "x64", "0", "0");

        // A failed slowest test and a passed medium test should surface (all outcomes are eligible); the fast skipped
        // test falls outside the requested top-2.
        terminalReporter.TestCompleted("0", testNodeUid: "SlowFailedTest", "SlowFailedTest", TestOutcome.Fail, TimeSpan.FromSeconds(10),
            informativeMessage: null, errorMessage: "boom", exception: null, expected: null, actual: null, standardOutput: null, errorOutput: null);
        terminalReporter.TestCompleted("0", testNodeUid: "MediumPassedTest", "MediumPassedTest", TestOutcome.Passed, TimeSpan.FromSeconds(5),
            informativeMessage: null, errorMessage: null, exception: null, expected: null, actual: null, standardOutput: null, errorOutput: null);
        terminalReporter.TestCompleted("0", testNodeUid: "FastSkippedTest", "FastSkippedTest", TestOutcome.Skipped, TimeSpan.FromSeconds(1),
            informativeMessage: null, errorMessage: null, exception: null, expected: null, actual: null, standardOutput: null, errorOutput: null);

        terminalReporter.AssemblyRunCompleted("0");
        terminalReporter.TestExecutionCompleted(DateTimeOffset.MaxValue, exitCode: null);

        string output = stringBuilderConsole.Output;
        int headerIndex = output.IndexOf(TerminalResources.SlowestTests, StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, headerIndex, $"Expected a slowest-tests section. Output:{Environment.NewLine}{output}");

        string slowestSection = output.Substring(headerIndex);
        int slowIndex = slowestSection.IndexOf("SlowFailedTest", StringComparison.Ordinal);
        int mediumIndex = slowestSection.IndexOf("MediumPassedTest", StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, slowIndex, "Slowest test should be listed.");
        Assert.IsGreaterThanOrEqualTo(0, mediumIndex, "Second-slowest test should be listed.");
        Assert.IsLessThan(mediumIndex, slowIndex, "Tests should be listed from slowest to fastest.");
        Assert.IsFalse(slowestSection.Contains("FastSkippedTest", StringComparison.Ordinal), "The fastest test should not appear in the top-2 slowest section.");
    }

    [TestMethod]
    public void TestExecutionCompleted_WhenShowSlowestTestsNotSet_DoesNotPrintSlowestSection()
    {
        var stringBuilderConsole = new StringBuilderConsole();
        var terminalReporter = new TerminalTestReporter(stringBuilderConsole, static () => false, new TerminalTestReporterOptions
        {
            AnsiMode = AnsiMode.NoAnsi,
            ShowProgress = () => false,
        });

        terminalReporter.TestExecutionStarted(DateTimeOffset.MinValue, 1, isDiscovery: false, isHelp: false, isRetry: false);
        string assembly = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"C:\work\assembly.dll" : "/mnt/work/assembly.dll";
        terminalReporter.AssemblyRunStarted(assembly, "net9.0", "x64", "0", "0");
        terminalReporter.TestCompleted("0", testNodeUid: "SomeTest", "SomeTest", TestOutcome.Passed, TimeSpan.FromSeconds(10),
            informativeMessage: null, errorMessage: null, exception: null, expected: null, actual: null, standardOutput: null, errorOutput: null);
        terminalReporter.AssemblyRunCompleted("0");
        terminalReporter.TestExecutionCompleted(DateTimeOffset.MaxValue, exitCode: null);

        Assert.IsFalse(stringBuilderConsole.Output.Contains(TerminalResources.SlowestTests, StringComparison.Ordinal));
    }

    // Verifies the reporter's multi-assembly rendering path: when SlowestTestsCount is set and more than one
    // assembly is registered (as the dotnet test orchestrator does), each assembly gets its own slowest-tests
    // sub-list. This exercises the shared reporter directly; note that wiring `dotnet test --show-slowest-tests`
    // to set SlowestTestsCount is owned by the dotnet/sdk CLI (TerminalReporterContract.props excludes the MTP
    // options provider) and is out of scope for this repo.
    [TestMethod]
    public void TestExecutionCompleted_WithMultipleAssemblies_WhenShowSlowestTestsSet_PrintsPerAssemblySlowest()
    {
        var stringBuilderConsole = new StringBuilderConsole();
        var terminalReporter = new TerminalTestReporter(stringBuilderConsole, new TerminalTestReporterOptions
        {
            AnsiMode = AnsiMode.NoAnsi,
            ShowProgress = () => false,
            ShowAssembly = true,
            ShowAssemblyStartAndComplete = false,
            SlowestTestsCount = 1,
        });

        terminalReporter.TestExecutionStarted(DateTimeOffset.MinValue, workerCount: 2, isDiscovery: false, isHelp: false, isRetry: false);

        string assemblyA = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"C:\repo\A.Tests.dll" : "/repo/A.Tests.dll";
        string assemblyB = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"C:\repo\B.Tests.dll" : "/repo/B.Tests.dll";
        terminalReporter.AssemblyRunStarted(assemblyA, "net9.0", "x64", executionId: "exec-A", instanceId: "inst-A");
        terminalReporter.AssemblyRunStarted(assemblyB, "net9.0", "x64", executionId: "exec-B", instanceId: "inst-B");

        ReportOrchestratorTestWithDuration(terminalReporter, assemblyA, "exec-A", "inst-A", "a-slow", TestOutcome.Passed, TimeSpan.FromSeconds(9));
        ReportOrchestratorTestWithDuration(terminalReporter, assemblyA, "exec-A", "inst-A", "a-fast", TestOutcome.Passed, TimeSpan.FromSeconds(7));
        ReportOrchestratorTestWithDuration(terminalReporter, assemblyB, "exec-B", "inst-B", "b-slow", TestOutcome.Passed, TimeSpan.FromSeconds(6));
        ReportOrchestratorTestWithDuration(terminalReporter, assemblyB, "exec-B", "inst-B", "b-fast", TestOutcome.Passed, TimeSpan.FromSeconds(2));

        terminalReporter.AssemblyRunCompleted(executionId: "exec-A", exitCode: 0, outputData: null, errorData: null);
        terminalReporter.AssemblyRunCompleted(executionId: "exec-B", exitCode: 0, outputData: null, errorData: null);
        terminalReporter.TestExecutionCompleted(DateTimeOffset.MaxValue, exitCode: 0);

        string output = stringBuilderConsole.Output;
        int headerIndex = output.IndexOf(TerminalResources.SlowestTests, StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, headerIndex, $"Expected a slowest-tests section. Output:{Environment.NewLine}{output}");

        string slowestSection = output.Substring(headerIndex);
        // Durations are chosen so a-fast (7s) is globally slower than b-slow (6s): a merged global ranking would
        // surface both A tests and drop b-slow, so asserting b-slow is present and a-fast is absent proves the
        // ranking is scoped per assembly (each assembly contributes only its own top-1).
        Assert.Contains("a-slow", slowestSection);
        Assert.Contains("b-slow", slowestSection);
        Assert.IsFalse(slowestSection.Contains("a-fast", StringComparison.Ordinal), "Faster test of assembly A should not appear in its top-1 slowest.");
        Assert.IsFalse(slowestSection.Contains("b-fast", StringComparison.Ordinal), "Faster test of assembly B should not appear in its top-1 slowest.");
    }

    // Regression test for the retry case: a timed first attempt followed by a retry that reports no timing must
    // drop the stale first-attempt duration instead of keeping it ranked in the slowest section.
    [TestMethod]
    public void TestExecutionCompleted_WhenTimedTestRetriedWithoutTiming_DropsStaleDurationFromSlowest()
    {
        var stringBuilderConsole = new StringBuilderConsole();
        var terminalReporter = new TerminalTestReporter(stringBuilderConsole, new TerminalTestReporterOptions
        {
            AnsiMode = AnsiMode.NoAnsi,
            ShowProgress = () => false,
            ShowAssembly = true,
            ShowAssemblyStartAndComplete = false,
            SlowestTestsCount = 5,
        });

        terminalReporter.TestExecutionStarted(DateTimeOffset.MinValue, workerCount: 1, isDiscovery: false, isHelp: false, isRetry: true);

        string assembly = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"C:\repo\Flaky.Tests.dll" : "/repo/Flaky.Tests.dll";
        const string executionId = "exec-flaky";

        // Attempt 1: the flaky test fails with a large (10s) reported duration, and a stable test passes quickly.
        terminalReporter.AssemblyRunStarted(assembly, "net9.0", "x64", executionId, instanceId: "inst-1");
        ReportOrchestratorTestWithDuration(terminalReporter, assembly, executionId, "inst-1", "flaky", TestOutcome.Fail, TimeSpan.FromSeconds(10));
        ReportOrchestratorTestWithDuration(terminalReporter, assembly, executionId, "inst-1", "stable", TestOutcome.Passed, TimeSpan.FromSeconds(1));

        // Attempt 2 (new instance id): the retry passes but reports no timing.
        terminalReporter.AssemblyRunStarted(assembly, "net9.0", "x64", executionId, instanceId: "inst-2");
        ReportOrchestratorTestWithDuration(terminalReporter, assembly, executionId, "inst-2", "flaky", TestOutcome.Passed, duration: null);

        terminalReporter.AssemblyRunCompleted(executionId, exitCode: 0, outputData: null, errorData: null);
        terminalReporter.TestExecutionCompleted(DateTimeOffset.MaxValue, exitCode: 0);

        string output = stringBuilderConsole.Output;
        int headerIndex = output.IndexOf(TerminalResources.SlowestTests, StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, headerIndex, $"Expected a slowest-tests section. Output:{Environment.NewLine}{output}");

        string slowestSection = output.Substring(headerIndex);
        Assert.Contains("stable", slowestSection);
        Assert.IsFalse(slowestSection.Contains("flaky", StringComparison.Ordinal), "The retried test's stale first-attempt duration should have been dropped once the retry reported no timing.");
    }

    // Regression test for the retry-replacement behavior: when the same uid is reported across two attempts with
    // different durations, only the latest attempt's duration must be ranked (not the earlier attempt's).
    [TestMethod]
    public void TestExecutionCompleted_WhenTestRetriedWithDifferentDuration_RanksOnlyLatestAttempt()
    {
        var stringBuilderConsole = new StringBuilderConsole();
        var terminalReporter = new TerminalTestReporter(stringBuilderConsole, new TerminalTestReporterOptions
        {
            AnsiMode = AnsiMode.NoAnsi,
            ShowProgress = () => false,
            ShowAssembly = true,
            ShowAssemblyStartAndComplete = false,
            SlowestTestsCount = 5,
        });

        terminalReporter.TestExecutionStarted(DateTimeOffset.MinValue, workerCount: 1, isDiscovery: false, isHelp: false, isRetry: true);

        string assembly = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"C:\repo\Flaky.Tests.dll" : "/repo/Flaky.Tests.dll";
        const string executionId = "exec-flaky";

        // Attempt 1: the flaky test fails taking 10s; another test passes taking 5s.
        terminalReporter.AssemblyRunStarted(assembly, "net9.0", "x64", executionId, instanceId: "inst-1");
        ReportOrchestratorTestWithDuration(terminalReporter, assembly, executionId, "inst-1", "flaky", TestOutcome.Fail, TimeSpan.FromSeconds(10));
        ReportOrchestratorTestWithDuration(terminalReporter, assembly, executionId, "inst-1", "other", TestOutcome.Passed, TimeSpan.FromSeconds(5));

        // Attempt 2 (new instance id): the retry passes quickly (2s).
        terminalReporter.AssemblyRunStarted(assembly, "net9.0", "x64", executionId, instanceId: "inst-2");
        ReportOrchestratorTestWithDuration(terminalReporter, assembly, executionId, "inst-2", "flaky", TestOutcome.Passed, TimeSpan.FromSeconds(2));

        terminalReporter.AssemblyRunCompleted(executionId, exitCode: 0, outputData: null, errorData: null);
        terminalReporter.TestExecutionCompleted(DateTimeOffset.MaxValue, exitCode: 0);

        string output = stringBuilderConsole.Output;
        int headerIndex = output.IndexOf(TerminalResources.SlowestTests, StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, headerIndex, $"Expected a slowest-tests section. Output:{Environment.NewLine}{output}");

        string slowestSection = output.Substring(headerIndex);
        // The stale 10s first attempt must not be ranked; the latest attempt's 2s wins, so 'other' (5s) ranks above
        // 'flaky' (2s). If the first attempt leaked, 'flaky' would rank first and the 10s duration would appear.
        Assert.IsFalse(slowestSection.Contains("10s", StringComparison.Ordinal), "The first attempt's 10s duration must not be ranked after the retry replaced it.");
        int otherIndex = slowestSection.IndexOf("other", StringComparison.Ordinal);
        int flakyIndex = slowestSection.IndexOf("flaky", StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, otherIndex, "Expected 'other' to be listed.");
        Assert.IsGreaterThanOrEqualTo(0, flakyIndex, "Expected 'flaky' to be listed.");
        Assert.IsLessThan(flakyIndex, otherIndex, "'other' (5s) should rank above the retried 'flaky' (2s), proving only the latest attempt's duration is used.");
    }

    private static TerminalTestReporter CreateOrchestratorReporter(StringBuilderConsole console, bool showAssemblyStartAndComplete = true)
        => new(console, new TerminalTestReporterOptions
        {
            AnsiMode = AnsiMode.NoAnsi,
            ShowProgress = () => false,
            ShowAssembly = true,
            ShowAssemblyStartAndComplete = showAssemblyStartAndComplete,
        });

    private static void ReportOrchestratorTest(TerminalTestReporter reporter, string assembly, string executionId, string instanceId, string testUid, TestOutcome outcome)
        => reporter.TestCompleted(
            assembly,
            targetFramework: "net9.0",
            architecture: "x64",
            executionId,
            instanceId,
            testNodeUid: testUid,
            displayName: testUid,
            informativeMessage: null,
            outcome,
            duration: TimeSpan.FromMilliseconds(1),
            exceptions: null,
            expected: null,
            actual: null,
            standardOutput: null,
            errorOutput: null);

    private static void ReportOrchestratorTestWithDuration(TerminalTestReporter reporter, string assembly, string executionId, string instanceId, string testUid, TestOutcome outcome, TimeSpan? duration)
        => reporter.TestCompleted(
            assembly,
            targetFramework: "net9.0",
            architecture: "x64",
            executionId,
            instanceId,
            testNodeUid: testUid,
            displayName: testUid,
            informativeMessage: null,
            outcome,
            duration,
            exceptions: null,
            expected: null,
            actual: null,
            standardOutput: null,
            errorOutput: null);

    private static string GetAssemblySummaryLine(string output, string assemblyPath)
        => Array.Find(
               output.Split('\n'),
               line => line.Contains(assemblyPath, StringComparison.Ordinal) && line.Contains("[+", StringComparison.Ordinal))
           ?? throw new InvalidOperationException(
               $"Expected output to contain a per-assembly summary line for '{assemblyPath}', but it did not. Full output:{Environment.NewLine}{output}");

    // The reporter renders the per-assembly counts with CultureInfo.CurrentCulture, so build the expected bracket the
    // same way; this keeps the assertion correct under cultures that use non-Latin digit shapes.
    private static string ExpectedCounts(int passed, int failed, int skipped)
        => $"[+{passed.ToString(CultureInfo.CurrentCulture)}/x{failed.ToString(CultureInfo.CurrentCulture)}/?{skipped.ToString(CultureInfo.CurrentCulture)}]";

    private static string ExpectedCounts(int passed, int failed, int skipped, int retried)
        => $"[+{passed.ToString(CultureInfo.CurrentCulture)}/x{failed.ToString(CultureInfo.CurrentCulture)}/?{skipped.ToString(CultureInfo.CurrentCulture)}/r{retried.ToString(CultureInfo.CurrentCulture)}]";

    [TestMethod]
    public void TerminalTestReporter_WhenReusedAcrossSessions_DoesNotLeakArtifactsOrCancelledState()
    {
        // Reproduces the HotReload reuse case: the same reporter instance runs multiple sessions. After a session
        // completes, the per-run state (artifacts, cancellation) must be reset so a later session neither re-prints
        // the previous session's artifacts nor stays stuck in the aborted state.
        string assembly = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"C:\work\assembly.dll" : "/mnt/work/assembly.dll";
        string folder = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"C:\work\" : "/mnt/work/";
        string firstSessionArtifact = $"{folder}first-session-artifact.txt";

        var stringBuilderConsole = new StringBuilderConsole();
        var terminalReporter = new TerminalTestReporter(stringBuilderConsole, static () => false, new TerminalTestReporterOptions
        {
            ShowPassedTests = () => false,
            AnsiMode = AnsiMode.NoAnsi,
            ShowProgress = () => false,
        });

        // First session: produces an artifact and is cancelled.
        terminalReporter.TestExecutionStarted(DateTimeOffset.MinValue, workerCount: 1, isDiscovery: false, isHelp: false, isRetry: false);
        terminalReporter.AssemblyRunStarted(assembly, "net8.0", "x64", "0", "0");
        terminalReporter.TestCompleted("0", testNodeUid: "T1", "T1", TestOutcome.Passed, TimeSpan.FromSeconds(1),
            informativeMessage: null, errorMessage: null, exception: null, expected: null, actual: null, standardOutput: null, errorOutput: null);
        terminalReporter.ArtifactAdded(outOfProcess: false, assembly: assembly, targetFramework: "net8.0", architecture: "x64", executionId: "0", testName: null, firstSessionArtifact);
        terminalReporter.StartCancelling();
        terminalReporter.AssemblyRunCompleted("0");
        terminalReporter.TestExecutionCompleted(DateTimeOffset.MaxValue, exitCode: null);

        // Second session on the SAME reporter: no artifacts, not cancelled.
        terminalReporter.TestExecutionStarted(DateTimeOffset.MinValue, workerCount: 1, isDiscovery: false, isHelp: false, isRetry: false);
        terminalReporter.AssemblyRunStarted(assembly, "net8.0", "x64", "0", "0");
        int outputLengthBeforeSecondSummary = stringBuilderConsole.Output.Length;
        terminalReporter.TestCompleted("0", testNodeUid: "T2", "T2", TestOutcome.Passed, TimeSpan.FromSeconds(1),
            informativeMessage: null, errorMessage: null, exception: null, expected: null, actual: null, standardOutput: null, errorOutput: null);
        terminalReporter.AssemblyRunCompleted("0");
        terminalReporter.TestExecutionCompleted(DateTimeOffset.MaxValue, exitCode: null);

        string secondSessionOutput = stringBuilderConsole.Output.Substring(outputLengthBeforeSecondSummary);

        // The first session's artifact must not be re-printed in the second session's summary.
        Assert.DoesNotContain(firstSessionArtifact, secondSessionOutput);

        // The second session is a clean pass, so its summary must not be marked as failed/aborted.
        Assert.DoesNotContain(TerminalResources.Aborted, secondSessionOutput);
        Assert.Contains("  failed: 0", secondSessionOutput);
    }

    [TestMethod]
    public void SimpleTerminal_UsesWindowWidthNotBufferWidth()
    {
        // Arrange - Create a console where BufferWidth and WindowWidth are different
        var console = new TestConsoleWithDifferentBufferAndWindowWidth
        {
            BufferWidth = 4096,
            WindowWidth = 120,
        };

        var terminal = new NonAnsiTerminal(console);

        // Assert - Width should use WindowWidth, not BufferWidth
        Assert.AreEqual(120, terminal.Width);
    }

    [TestMethod]
    public void AnsiTerminal_UsesWindowWidthNotBufferWidth()
    {
        // Arrange - Create a console where BufferWidth and WindowWidth are different
        var console = new TestConsoleWithDifferentBufferAndWindowWidth
        {
            BufferWidth = 4096,
            WindowWidth = 120,
        };

        var terminal = new AnsiTerminal(console);

        // Assert - Width should use WindowWidth, not BufferWidth
        Assert.AreEqual(120, terminal.Width);
    }

    /// <summary>
    /// Locks in the PR #8348 fix for issue #6753: when the progress state has not changed between
    /// two refresh ticks (same <c>ProgressId</c> + <c>ProgressVersion</c>), the renderer must only
    /// rewrite the duration cell instead of erasing and re-emitting the whole progress line.
    ///
    /// Before the fix the optimization was guarded by a <c>&amp;&amp; false</c> leftover, which made every
    /// 500 ms tick fall through to the full re-render branch (<c>CSI K</c> + counters + assembly name).
    /// This test fails if that branch is ever disabled again.
    /// </summary>
    [TestMethod]
    public void AnsiTerminal_ProgressFrame_OnlyUpdatesDuration_WhenProgressVersionUnchanged()
    {
        string targetFramework = "net8.0";
        string architecture = "x64";
        string assembly = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"C:\work\assembly.dll" : "/mnt/work/assembly.dll";

        var stringBuilderConsole = new StringBuilderConsole();
        var stopwatchFactory = new StopwatchFactory();
        var terminalReporter = new TerminalTestReporter(stringBuilderConsole, static () => false, new TerminalTestReporterOptions
        {
            ShowPassedTests = () => true,
            AnsiMode = AnsiMode.ForceAnsi,

            // Intentionally do NOT enable ShowActiveTests: the optimization is per-line and we keep
            // the rendered frame to a single line (the assembly progress) to make assertions simple.
            ShowActiveTests = false,
            ShowProgress = () => true,
        })
        {
            CreateStopwatch = stopwatchFactory.CreateStopwatch,
        };

        // Gate the refresher thread so renders happen one at a time, on our cue, with deterministic
        // duration values. Without this, the 500 ms timer would race with our assertions.
        var renderGate = new AutoResetEvent(initialState: false);
        var renderDone = new AutoResetEvent(initialState: false);
        terminalReporter.OnProgressStartUpdate += (sender, args) => renderGate.WaitOne();
        terminalReporter.OnProgressStopUpdate += (sender, args) => renderDone.Set();

        terminalReporter.TestExecutionStarted(DateTimeOffset.MinValue, 1, isDiscovery: false, isHelp: false, isRetry: false);
        terminalReporter.AssemblyRunStarted(assembly, targetFramework, architecture, "0", "0");

        // Pick a starting elapsed value whose rendered form ("1s") has the same length as the value
        // we will use for the second tick ("2s"). The duration-only path only fires when the rendered
        // duration string has the same length as the one rendered in the previous frame.
        stopwatchFactory.AddTime(TimeSpan.FromSeconds(1));

        // First tick: nothing was rendered yet, so this is the full frame.
        int beforeFirstRender = stringBuilderConsole.Output.Length;
        renderGate.Set();
        renderDone.WaitOne();
        string firstRender = stringBuilderConsole.Output[beforeFirstRender..];

        // Sanity: the first render is the full frame (counters + assembly name + duration).
        Assert.Contains("assembly.dll", firstRender);
        Assert.Contains("(1s)", firstRender);

        // Advance the clock by 1 second without touching any progress state. The worker version is
        // unchanged, so the next render should take the "same Id + Version → duration-only" path.
        stopwatchFactory.AddTime(TimeSpan.FromSeconds(1));

        int beforeSecondRender = stringBuilderConsole.Output.Length;
        renderGate.Set();
        renderDone.WaitOne();
        string secondRender = stringBuilderConsole.Output[beforeSecondRender..];

        // The duration-only path writes only the new duration with cursor positioning; it must not
        // re-emit the counters, the assembly name, or a CSI K erase-in-line.
        Assert.Contains("(2s)", secondRender);
        Assert.DoesNotContain("assembly.dll", secondRender);
        Assert.DoesNotContain("(1s)", secondRender);
        Assert.DoesNotContain($"{AnsiCodes.CSI}{AnsiCodes.EraseInLine}", secondRender);
        Assert.DoesNotContain("✓", secondRender);
        Assert.Contains(AnsiCodes.SetCursorHorizontal(250), secondRender);

        // Note: we deliberately do not stop the reporter here. The refresher thread is a background
        // thread that is currently blocked in OnProgressStartUpdate; calling StopShowingProgress
        // (which Joins the thread) would deadlock. The existing tests in this file follow the same
        // pattern - the thread dies with the test process.
    }

    /// <summary>
    /// Reproduces the bug from issue #7240: when Console.BufferWidth > Console.WindowWidth,
    /// the ANSI cursor positioning places timings off-screen because it was using BufferWidth
    /// (capped to 250) instead of WindowWidth.
    ///
    /// Before fix: cursor goes to column 242 (= MaxColumn 250 - 8), off-screen for 120-col window.
    /// After fix:  cursor goes to column 112 (= WindowWidth 120 - 8), visible in window.
    /// </summary>
    [TestMethod]
    public void AnsiTerminal_ProgressFrame_UseWindowWidthForCursorPositioning_WhenBufferWidthIsLarger()
    {
        string targetFramework = "net8.0";
        string architecture = "x64";
        string assembly = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"C:\work\assembly.dll" : "/mnt/work/assembly.dll";

        // Console with BufferWidth=4096 but WindowWidth=120, mimicking the bug scenario.
        var stringBuilderConsole = new StringBuilderConsoleWithCustomWidths(bufferWidth: 4096, windowWidth: 120);
        var stopwatchFactory = new StopwatchFactory();
        var terminalReporter = new TerminalTestReporter(stringBuilderConsole, static () => false, new TerminalTestReporterOptions
        {
            ShowPassedTests = () => true,
            AnsiMode = AnsiMode.ForceAnsi,
            ShowActiveTests = true,
            ShowProgress = () => true,
        })
        {
            CreateStopwatch = stopwatchFactory.CreateStopwatch,
        };

        var startHandle = new AutoResetEvent(initialState: false);
        var stopHandle = new AutoResetEvent(initialState: false);

        terminalReporter.OnProgressStartUpdate += (sender, args) => startHandle.WaitOne();
        terminalReporter.OnProgressStopUpdate += (sender, args) => stopHandle.Set();

        DateTimeOffset startTime = DateTimeOffset.MinValue;
        terminalReporter.TestExecutionStarted(startTime, 1, isDiscovery: false, isHelp: false, isRetry: false);
        terminalReporter.AssemblyRunStarted(assembly, targetFramework, architecture, "0", "0");

        terminalReporter.TestInProgress(executionId: "0", testNodeUid: "Test1", displayName: "Test1");
        stopwatchFactory.AddTime(TimeSpan.FromMinutes(1) + TimeSpan.FromSeconds(31));

        terminalReporter.TestCompleted("0", testNodeUid: "Test1", "Test1", TestOutcome.Passed, TimeSpan.FromSeconds(10),
            informativeMessage: null, errorMessage: null, exception: null, expected: null, actual: null, standardOutput: null, errorOutput: null);

        string output = stringBuilderConsole.Output;
        startHandle.Set();
        stopHandle.WaitOne();

        string escapedOutput = ShowEscape(output)!;

        // With WindowWidth=120, cursor for "(1m 31s)" (8 chars) should be at column 120-8=112.
        // Before the fix, BufferWidth=4096 was capped to MaxColumn=250, giving column 250-8=242.
        Assert.Contains("␛[112G(1m 31s)", escapedOutput,
            "Cursor should be positioned at column 112 (WindowWidth=120 minus duration length), not at 242 (MaxColumn=250 minus duration length)");
        Assert.DoesNotContain("␛[242G", escapedOutput,
            "Cursor must NOT be positioned at column 242 which would happen if BufferWidth (4096, capped to 250) was used instead of WindowWidth");
    }

    internal class TestConsoleWithDifferentBufferAndWindowWidth : IConsole
    {
        public int BufferHeight { get; set; } = 300;

        public int BufferWidth { get; set; } = 4096;

        public int WindowHeight { get; set; } = 30;

        public int WindowWidth { get; set; } = 120;

        public bool IsOutputRedirected => false;

        public event ConsoleCancelEventHandler? CancelKeyPress = (sender, e) => { };

        public void Clear() => throw new NotImplementedException();

        public ConsoleColor GetForegroundColor() => ConsoleColor.White;

        public void SetForegroundColor(ConsoleColor color)
        {
        }

        public void Write(string? value)
        {
        }

        public void Write(char value)
        {
        }

        public void WriteLine()
        {
        }

        public void WriteLine(string? value)
        {
        }
    }

    /// <summary>
    /// A StringBuilderConsole variant that captures output and allows custom Buffer/Window dimensions.
    /// </summary>
    internal sealed class StringBuilderConsoleWithCustomWidths : IConsole
    {
        private readonly StringBuilder _output = new();

        public StringBuilderConsoleWithCustomWidths(int bufferWidth, int windowWidth)
        {
            BufferWidth = bufferWidth;
            WindowWidth = windowWidth;
        }

        public int BufferHeight => int.MaxValue;

        public int BufferWidth { get; }

        public int WindowHeight => int.MaxValue;

        public int WindowWidth { get; }

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
