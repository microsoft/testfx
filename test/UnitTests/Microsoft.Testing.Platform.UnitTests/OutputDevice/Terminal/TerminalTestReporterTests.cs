// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.OutputDevice.Terminal;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class TerminalTestReporterTests
{
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
        Assert.IsFalse(terminal.Output.Contains(" :0"));
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
        var terminalReporter = new TerminalTestReporter(assembly, targetFramework, architecture, stringBuilderConsole, new CTRLPlusCCancellationTokenSource(), new TerminalTestReporterOptions
        {
            ShowPassedTests = () => true,

            // Like --no-ansi in commandline, should disable ANSI altogether.
            UseAnsi = false,

            ShowProgress = () => false,
        });

        DateTimeOffset startTime = DateTimeOffset.MinValue;
        DateTimeOffset endTime = DateTimeOffset.MaxValue;
        terminalReporter.TestExecutionStarted(startTime, 1, isDiscovery: false);

        string folder = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"C:\work\" : "/mnt/work/";
        string folderNoSlash = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"C:\work" : "/mnt/work";
        string folderLink = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"C:/work/" : "mnt/work/";
        string folderLinkNoSlash = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"C:/work" : "mnt/work";

        terminalReporter.AssemblyRunStarted();
        string standardOutput = "Hello!";
        string errorOutput = "Oh no!";

        terminalReporter.TestCompleted(testNodeUid: "PassedTest1", "PassedTest1", TestOutcome.Passed, TimeSpan.FromSeconds(10),
            informativeMessage: null, errorMessage: null, exception: null, expected: null, actual: null, standardOutput, errorOutput);
        terminalReporter.TestCompleted(testNodeUid: "SkippedTest1", "SkippedTest1", TestOutcome.Skipped, TimeSpan.FromSeconds(10),
            informativeMessage: null, errorMessage: null, exception: null, expected: null, actual: null, standardOutput, errorOutput);
        // timed out + canceled + failed should all report as failed in summary
        terminalReporter.TestCompleted(testNodeUid: "TimedoutTest1", "TimedoutTest1", TestOutcome.Timeout, TimeSpan.FromSeconds(10),
            informativeMessage: null, errorMessage: null, exception: null, expected: null, actual: null, standardOutput, errorOutput);
        terminalReporter.TestCompleted(testNodeUid: "CanceledTest1", "CanceledTest1", TestOutcome.Canceled, TimeSpan.FromSeconds(10),
            informativeMessage: null, errorMessage: null, exception: null, expected: null, actual: null, standardOutput, errorOutput);
        terminalReporter.TestCompleted(testNodeUid: "FailedTest1", "FailedTest1", TestOutcome.Fail, TimeSpan.FromSeconds(10),
            informativeMessage: null, errorMessage: "Tests failed", exception: new StackTraceException(@$"   at FailingTest() in {folder}codefile.cs:line 10"), expected: "ABC", actual: "DEF", standardOutput, errorOutput);
        terminalReporter.ArtifactAdded(outOfProcess: true, testName: null, @$"{folder}artifact1.txt");
        terminalReporter.ArtifactAdded(outOfProcess: false, testName: null, @$"{folder}artifact2.txt");
        terminalReporter.AssemblyRunCompleted();
        terminalReporter.TestExecutionCompleted(endTime);

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
        var terminalReporter = new TerminalTestReporter(assembly, targetFramework, architecture, stringBuilderConsole, new CTRLPlusCCancellationTokenSource(), new TerminalTestReporterOptions
        {
            ShowPassedTests = () => true,
            // Like if we autodetect that we are in CI (e.g. by looking at TF_BUILD, and we don't disable ANSI.
            UseAnsi = true,
            UseCIAnsi = true,
            ForceAnsi = true,

            ShowProgress = () => false,
        });

        DateTimeOffset startTime = DateTimeOffset.MinValue;
        DateTimeOffset endTime = DateTimeOffset.MaxValue;
        terminalReporter.TestExecutionStarted(startTime, 1, isDiscovery: false);

        string folder = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"C:\work\" : "/mnt/work/";
        string folderNoSlash = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"C:\work" : "/mnt/work";
        string folderLink = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"C:/work/" : "mnt/work/";
        string folderLinkNoSlash = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"C:/work" : "mnt/work";

        terminalReporter.AssemblyRunStarted();
        string standardOutput = "Hello!";
        string errorOutput = "Oh no!";

        terminalReporter.TestCompleted(testNodeUid: "PassedTest1", "PassedTest1", TestOutcome.Passed, TimeSpan.FromSeconds(10),
            informativeMessage: null, errorMessage: null, exception: null, expected: null, actual: null, standardOutput, errorOutput);
        terminalReporter.TestCompleted(testNodeUid: "SkippedTest1", "SkippedTest1", TestOutcome.Skipped, TimeSpan.FromSeconds(10),
            informativeMessage: null, errorMessage: null, exception: null, expected: null, actual: null, standardOutput, errorOutput);
        // timed out + canceled + failed should all report as failed in summary
        terminalReporter.TestCompleted(testNodeUid: "TimedoutTest1", "TimedoutTest1", TestOutcome.Timeout, TimeSpan.FromSeconds(10),
            informativeMessage: null, errorMessage: null, exception: null, expected: null, actual: null, standardOutput, errorOutput);
        terminalReporter.TestCompleted(testNodeUid: "CanceledTest1", "CanceledTest1", TestOutcome.Canceled, TimeSpan.FromSeconds(10),
            informativeMessage: null, errorMessage: null, exception: null, expected: null, actual: null, standardOutput, errorOutput);
        terminalReporter.TestCompleted(testNodeUid: "FailedTest1", "FailedTest1", TestOutcome.Fail, TimeSpan.FromSeconds(10),
            informativeMessage: null, errorMessage: "Tests failed", exception: new StackTraceException(@$"   at FailingTest() in {folder}codefile.cs:line 10"), expected: "ABC", actual: "DEF", standardOutput, errorOutput);
        terminalReporter.ArtifactAdded(outOfProcess: true, testName: null, @$"{folder}artifact1.txt");
        terminalReporter.ArtifactAdded(outOfProcess: false, testName: null, @$"{folder}artifact2.txt");
        terminalReporter.AssemblyRunCompleted();
        terminalReporter.TestExecutionCompleted(endTime);

        string output = stringBuilderConsole.Output;

        string expected = $"""
            ␛[32mpassed␛[m PassedTest1␛[90m ␛[90m(10s 000ms)␛[m
            ␛[90m  Standard output
            ␛[90m    Hello!
            ␛[90m  Error output
            ␛[90m    Oh no!
            ␛[m␛[33mskipped␛[m SkippedTest1␛[90m ␛[90m(10s 000ms)␛[m
            ␛[90m  Standard output
            ␛[90m    Hello!
            ␛[90m  Error output
            ␛[90m    Oh no!
            ␛[m␛[31mfailed (canceled)␛[m TimedoutTest1␛[90m ␛[90m(10s 000ms)␛[m
            ␛[90m  Standard output
            ␛[90m    Hello!
            ␛[90m  Error output
            ␛[90m    Oh no!
            ␛[m␛[31mfailed (canceled)␛[m CanceledTest1␛[90m ␛[90m(10s 000ms)␛[m
            ␛[90m  Standard output
            ␛[90m    Hello!
            ␛[90m  Error output
            ␛[90m    Oh no!
            ␛[m␛[31mfailed␛[m FailedTest1␛[90m ␛[90m(10s 000ms)␛[m
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
        var terminalReporter = new TerminalTestReporter(assembly, targetFramework, architecture, stringBuilderConsole, new CTRLPlusCCancellationTokenSource(), new TerminalTestReporterOptions
        {
            ShowPassedTests = () => true,
            // Like if we autodetect that we are in ANSI capable terminal.
            UseAnsi = true,
            UseCIAnsi = false,
            ForceAnsi = true,

            ShowProgress = () => false,
        });

        DateTimeOffset startTime = DateTimeOffset.MinValue;
        DateTimeOffset endTime = DateTimeOffset.MaxValue;
        terminalReporter.TestExecutionStarted(startTime, 1, isDiscovery: false);

        string folder = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"C:\work\" : "/mnt/work/";
        string folderNoSlash = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"C:\work" : "/mnt/work";
        string folderLink = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"C:/work/" : "mnt/work/";
        string folderLinkNoSlash = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"C:/work" : "mnt/work";

        terminalReporter.AssemblyRunStarted();
        string standardOutput = "Hello!";
        string errorOutput = "Oh no!";

        terminalReporter.TestCompleted(testNodeUid: "PassedTest1", "PassedTest1", TestOutcome.Passed, TimeSpan.FromSeconds(10),
            informativeMessage: null, errorMessage: null, exception: null, expected: null, actual: null, standardOutput, errorOutput);
        terminalReporter.TestCompleted(testNodeUid: "SkippedTest1", "SkippedTest1", TestOutcome.Skipped, TimeSpan.FromSeconds(10),
            informativeMessage: null, errorMessage: null, exception: null, expected: null, actual: null, standardOutput, errorOutput);
        // timed out + canceled + failed should all report as failed in summary
        terminalReporter.TestCompleted(testNodeUid: "TimedoutTest1", "TimedoutTest1", TestOutcome.Timeout, TimeSpan.FromSeconds(10),
            informativeMessage: null, errorMessage: null, exception: null, expected: null, actual: null, standardOutput, errorOutput);
        terminalReporter.TestCompleted(testNodeUid: "CanceledTest1", "CanceledTest1", TestOutcome.Canceled, TimeSpan.FromSeconds(10),
            informativeMessage: null, errorMessage: null, exception: null, expected: null, actual: null, standardOutput, errorOutput);
        terminalReporter.TestCompleted(testNodeUid: "FailedTest1", "FailedTest1", TestOutcome.Fail, TimeSpan.FromSeconds(10),
            informativeMessage: null, errorMessage: "Tests failed", exception: new StackTraceException(@$"   at FailingTest() in {folder}codefile.cs:line 10"), expected: "ABC", actual: "DEF", standardOutput, errorOutput);
        terminalReporter.ArtifactAdded(outOfProcess: true, testName: null, @$"{folder}artifact1.txt");
        terminalReporter.ArtifactAdded(outOfProcess: false, testName: null, @$"{folder}artifact2.txt");
        terminalReporter.AssemblyRunCompleted();
        terminalReporter.TestExecutionCompleted(endTime);

        string output = stringBuilderConsole.Output;

        string expected = $"""
            ␛[32mpassed␛[m PassedTest1␛[90m ␛[90m(10s 000ms)␛[m
            ␛[90m  Standard output
                Hello!
              Error output
                Oh no!
            ␛[m␛[33mskipped␛[m SkippedTest1␛[90m ␛[90m(10s 000ms)␛[m
            ␛[90m  Standard output
                Hello!
              Error output
                Oh no!
            ␛[m␛[31mfailed (canceled)␛[m TimedoutTest1␛[90m ␛[90m(10s 000ms)␛[m
            ␛[90m  Standard output
                Hello!
              Error output
                Oh no!
            ␛[m␛[31mfailed (canceled)␛[m CanceledTest1␛[90m ␛[90m(10s 000ms)␛[m
            ␛[90m  Standard output
                Hello!
              Error output
                Oh no!
            ␛[m␛[31mfailed␛[m FailedTest1␛[90m ␛[90m(10s 000ms)␛[m
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
        var terminalReporter = new TerminalTestReporter(assembly, targetFramework, architecture, stringBuilderConsole, new CTRLPlusCCancellationTokenSource(), new TerminalTestReporterOptions
        {
            ShowPassedTests = () => true,
            // Like if we autodetect that we are in ANSI capable terminal.
            UseAnsi = true,
            UseCIAnsi = false,
            ForceAnsi = true,

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
        terminalReporter.TestExecutionStarted(startTime, 1, isDiscovery: false);

        string folder = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"C:\work\" : "/mnt/work/";
        string folderNoSlash = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"C:\work" : "/mnt/work";
        string folderLink = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"C:/work/" : "mnt/work/";
        string folderLinkNoSlash = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"C:/work" : "mnt/work";

        terminalReporter.AssemblyRunStarted();
        string standardOutput = "Hello!";
        string errorOutput = "Oh no!";

        // Note: Add 1ms to make the order of the progress frame deterministic.
        // Otherwise all tests that run for 1m31s could show in any order.
        terminalReporter.TestInProgress(testNodeUid: "PassedTest1", displayName: "PassedTest1");
        stopwatchFactory.AddTime(TimeSpan.FromMilliseconds(1));
        terminalReporter.TestInProgress(testNodeUid: "SkippedTest1", displayName: "SkippedTest1");
        stopwatchFactory.AddTime(TimeSpan.FromMilliseconds(1));
        terminalReporter.TestInProgress(testNodeUid: "InProgressTest1", displayName: "InProgressTest1");
        stopwatchFactory.AddTime(TimeSpan.FromMinutes(1));
        terminalReporter.TestInProgress(testNodeUid: "InProgressTest2", displayName: "InProgressTest2");
        stopwatchFactory.AddTime(TimeSpan.FromSeconds(30));
        terminalReporter.TestInProgress(testNodeUid: "InProgressTest3", displayName: "InProgressTest3");
        stopwatchFactory.AddTime(TimeSpan.FromSeconds(1));

        terminalReporter.TestCompleted(testNodeUid: "PassedTest1", "PassedTest1", TestOutcome.Passed, TimeSpan.FromSeconds(10),
            informativeMessage: null, errorMessage: null, exception: null, expected: null, actual: null, standardOutput, errorOutput);
        terminalReporter.TestCompleted(testNodeUid: "SkippedTest1", "SkippedTest1", TestOutcome.Skipped, TimeSpan.FromSeconds(10),
            informativeMessage: null, errorMessage: null, exception: null, expected: null, actual: null, standardOutput, errorOutput);

        string output = stringBuilderConsole.Output;
        startHandle.Set();
        stopHandle.WaitOne();

        // Note: On MacOS the busy indicator is not rendered.
        bool useBusyIndicator = !RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
        string busyIndicatorString = useBusyIndicator ? "␛]9;4;3;␛\\" : string.Empty;

        // Note: The progress is drawn after each completed event.
        string expected = $"""
            {busyIndicatorString}␛[?25l␛[32mpassed␛[m PassedTest1␛[90m ␛[90m(10s 000ms)␛[m
            ␛[90m  Standard output
                Hello!
              Error output
                Oh no!
            ␛[m
            [␛[32m✓1␛[m/␛[31mx0␛[m/␛[33m↓0␛[m] assembly.dll (net8.0|x64)␛[2147483640G(1m 31s)
              SkippedTest1␛[2147483640G(1m 31s)
              InProgressTest1␛[2147483640G(1m 31s)
              InProgressTest2␛[2147483643G(31s)
              InProgressTest3␛[2147483644G(1s)
            ␛[7F
            ␛[J␛[33mskipped␛[m SkippedTest1␛[90m ␛[90m(10s 000ms)␛[m
            ␛[90m  Standard output
                Hello!
              Error output
                Oh no!
            ␛[m
            [␛[32m✓1␛[m/␛[31mx0␛[m/␛[33m↓1␛[m] assembly.dll (net8.0|x64)␛[2147483640G(1m 31s)
              InProgressTest1␛[2147483640G(1m 31s)
              InProgressTest2␛[2147483643G(31s)
              InProgressTest3␛[2147483644G(1s)
            
            """;

        Assert.AreEqual(expected, ShowEscape(output));
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

        public int BufferWidth => int.MinValue;

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

    private class StackTraceException : Exception
    {
        public StackTraceException(string stackTrace) => StackTrace = stackTrace;

        public override string? StackTrace { get; }
    }
}
