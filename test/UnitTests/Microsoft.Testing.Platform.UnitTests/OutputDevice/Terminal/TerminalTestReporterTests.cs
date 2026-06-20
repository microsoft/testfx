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

        // No test ran, so the run verdict is the red "Zero tests ran" (runFailed also includes HasHandshakeFailure).
        Assert.Contains(TerminalResources.ZeroTestsRan, output);

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

    private static string GetAssemblySummaryLine(string output, string assemblyPath)
    {
        foreach (string line in output.Split('\n'))
        {
            if (line.Contains(assemblyPath, StringComparison.Ordinal) && line.Contains("[+", StringComparison.Ordinal))
            {
                return line;
            }
        }

        throw new InvalidOperationException(
            $"Expected output to contain a per-assembly summary line for '{assemblyPath}', but it did not. Full output:{Environment.NewLine}{output}");
    }

    // The reporter renders the per-assembly counts with CultureInfo.CurrentCulture, so build the expected bracket the
    // same way; this keeps the assertion correct under cultures that use non-Latin digit shapes.
    private static string ExpectedCounts(int passed, int failed, int skipped)
        => $"[+{passed.ToString(CultureInfo.CurrentCulture)}/x{failed.ToString(CultureInfo.CurrentCulture)}/?{skipped.ToString(CultureInfo.CurrentCulture)}]";

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
