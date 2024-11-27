// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.OutputDevice.Terminal;

namespace Microsoft.Testing.Platform.UnitTests;

[TestGroup]
public sealed class TerminalTestReporterTests : TestBase
{
    public TerminalTestReporterTests(ITestExecutionContext testExecutionContext)
        : base(testExecutionContext)
    {
    }

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
        Assert.Contains("    at Microsoft.Testing.Platform.UnitTests.TerminalTestReporterTests.AppendStackFrameFormatsStackTraceLineCorrectly()", terminal.Output);
#endif
        // Line number without the respective file
        Assert.That(!terminal.Output.ToString().Contains(" :0"));
    }

    public void StackTraceRegexCapturesLines()
    {
        string[] stackTraceLines = """
                                       at System.Text.RegularExpressions.RegexRunner.<CheckTimeout>g__ThrowRegexTimeout|25_0()
                                       at System.Text.RegularExpressions.Generated.<RegexGenerator_g>F06D33C3F8C8C3FD257C1A1967E3A3BAC4BE9C8EC41CC9366C764C2205C68F0CE__GetFrameRegex_1.RunnerFactory.Runner.TryMatchAtCurrentPosition(ReadOnlySpan`1) in /_/artifacts/obj/Microsoft.Testing.Platform/Release/net8.0/System.Text.RegularExpressions.Generator/System.Text.RegularExpressions.Generator.RegexGenerator/RegexGenerator.g.cs:line 639
                                       at System.Text.RegularExpressions.Generated.<RegexGenerator_g>F06D33C3F8C8C3FD257C1A1967E3A3BAC4BE9C8EC41CC9366C764C2205C68F0CE__GetFrameRegex_1.RunnerFactory.Runner.Scan(ReadOnlySpan`1) in /_/artifacts/obj/Microsoft.Testing.Platform/Release/net8.0/System.Text.RegularExpressions.Generator/System.Text.RegularExpressions.Generator.RegexGenerator/RegexGenerator.g.cs:line 537
                                       at System.Text.RegularExpressions.Regex.ScanInternal(RegexRunnerMode, Boolean, String, Int32, RegexRunner, ReadOnlySpan`1, Boolean)
                                       at System.Text.RegularExpressions.Regex.RunSingleMatch(RegexRunnerMode, Int32, String, Int32, Int32, Int32)
                                       at System.Text.RegularExpressions.Regex.Match(String)
                                       at Microsoft.Testing.Platform.OutputDevice.Terminal.TerminalTestReporter.AppendStackFrame(ITerminal, String) in /_/src/Platform/Microsoft.Testing.Platform/OutputDevice/Terminal/TerminalTestReporter.cs:line 650
                                       at Microsoft.Testing.Platform.OutputDevice.Terminal.TerminalTestReporter.FormatStackTrace(ITerminal, FlatException[], Int32) in /_/src/Platform/Microsoft.Testing.Platform/OutputDevice/Terminal/TerminalTestReporter.cs:line 601
                                       at Microsoft.Testing.Platform.OutputDevice.Terminal.TerminalTestReporter.RenderTestCompleted(ITerminal , String , String, String, String , TestOutcome, TimeSpan, FlatException[] , String, String, String, String) in /_/src/Platform/Microsoft.Testing.Platform/OutputDevice/Terminal/TerminalTestReporter.cs:line 517
                                       at Microsoft.Testing.Platform.OutputDevice.Terminal.TerminalTestReporter.<>c__DisplayClass27_0.<TestCompleted>b__0(ITerminal terminal) in /_/src/Platform/Microsoft.Testing.Platform/OutputDevice/Terminal/TerminalTestReporter.cs:line 439
                                       at Microsoft.Testing.Platform.OutputDevice.Terminal.TestProgressStateAwareTerminal.WriteToTerminal(Action`1) in /_/src/Platform/Microsoft.Testing.Platform/OutputDevice/Terminal/TestProgressStateAwareTerminal.cs:line 129
                                       at Microsoft.Testing.Platform.OutputDevice.Terminal.TerminalTestReporter.TestCompleted(String , String, String, String, String , TestOutcome, TimeSpan, FlatException[] , String, String, String, String) in /_/src/Platform/Microsoft.Testing.Platform/OutputDevice/Terminal/TerminalTestReporter.cs:line 439
                                       at Microsoft.Testing.Platform.OutputDevice.Terminal.TerminalTestReporter.TestCompleted(String , String, String, String, String , TestOutcome, TimeSpan, String, Exception, String, String, String, String) in /_/src/Platform/Microsoft.Testing.Platform/OutputDevice/Terminal/TerminalTestReporter.cs:line 386
                                       at Microsoft.Testing.Platform.OutputDevice.TerminalOutputDevice.ConsumeAsync(IDataProducer, IData, CancellationToken) in /_/src/Platform/Microsoft.Testing.Platform/OutputDevice/TerminalOutputDevice.cs:line 458
                                       at Microsoft.Testing.Platform.Messages.AsyncConsumerDataProcessor.ConsumeAsync() in /_/src/Platform/Microsoft.Testing.Platform/Messages/ChannelConsumerDataProcessor.cs:line 74
                                       at Microsoft.Testing.Platform.Messages.AsyncConsumerDataProcessor.DrainDataAsync() in /_/src/Platform/Microsoft.Testing.Platform/Messages/ChannelConsumerDataProcessor.cs:line 146
                                       at Microsoft.Testing.Platform.Messages.AsynchronousMessageBus.DrainDataAsync() in /_/src/Platform/Microsoft.Testing.Platform/Messages/AsynchronousMessageBus.cs:line 177
                                       at Microsoft.Testing.Platform.Messages.MessageBusProxy.DrainDataAsync() in /_/src/Platform/Microsoft.Testing.Platform/Messages/MessageBusProxy.cs:line 39
                                       at Microsoft.Testing.Platform.Hosts.CommonTestHost.NotifyTestSessionEndAsync(SessionUid, BaseMessageBus, ServiceProvider, CancellationToken) in /_/src/Platform/Microsoft.Testing.Platform/Hosts/CommonTestHost.cs:line 192
                                       at Microsoft.Testing.Platform.Hosts.CommonTestHost.ExecuteRequestAsync(IPlatformOutputDevice, ITestSessionContext, ServiceProvider, BaseMessageBus, ITestFramework, ClientInfo) in /_/src/Platform/Microsoft.Testing.Platform/Hosts/CommonTestHost.cs:line 133
                                       at Microsoft.Testing.Platform.Hosts.ConsoleTestHost.InternalRunAsync() in /_/src/Platform/Microsoft.Testing.Platform/Hosts/ConsoleTestHost.cs:line 85
                                       at Microsoft.Testing.Platform.Hosts.ConsoleTestHost.InternalRunAsync() in /_/src/Platform/Microsoft.Testing.Platform/Hosts/ConsoleTestHost.cs:line 117
                                       at Microsoft.Testing.Platform.Hosts.CommonTestHost.RunTestAppAsync(CancellationToken) in /_/src/Platform/Microsoft.Testing.Platform/Hosts/CommonTestHost.cs:line 106
                                       at Microsoft.Testing.Platform.Hosts.CommonTestHost.RunAsync() in /_/src/Platform/Microsoft.Testing.Platform/Hosts/CommonTestHost.cs:line 34
                                       at Microsoft.Testing.Platform.Hosts.CommonTestHost.RunAsync() in /_/src/Platform/Microsoft.Testing.Platform/Hosts/CommonTestHost.cs:line 72
                                       at Microsoft.Testing.Platform.Builder.TestApplication.RunAsync() in /_/src/Platform/Microsoft.Testing.Platform/Builder/TestApplication.cs:line 244
                                       at TestingPlatformEntryPoint.Main(String[]) in /_/TUnit.TestProject/obj/Release/net8.0/osx-x64/TestPlatformEntryPoint.cs:line 16
                                       at TestingPlatformEntryPoint.<Main>(String[])
                                    """.Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries);

        Regex regex = TerminalTestReporter.GetFrameRegex();
        foreach (string stackTraceLine in stackTraceLines)
        {
            Match match = regex.Match(stackTraceLine);
            Assert.IsTrue(match.Success);
            Assert.IsTrue(match.Groups["code"].Success);

            bool hasFileAndLine = char.IsDigit(stackTraceLine.Last());
            Assert.That(match.Groups["file"].Success == hasFileAndLine);
            Assert.That(match.Groups["line"].Success == hasFileAndLine);
        }
    }

#if NET7_0_OR_GREATER
    public void AOTStackTraceRegexCapturesLines()
    {
        string[] stackTraceLines = """
                                       at BenchmarkTest.ExceptionThrower.<RandomSwitcher>d__7.MoveNext() + 0x42f
                                    --- End of stack trace from previous location ---
                                       at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
                                       at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
                                       at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
                                       at BenchmarkTest.ExceptionThrower.<Nested4>d__5.MoveNext() + 0x9d
                                    --- End of stack trace from previous location ---
                                       at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
                                       at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
                                       at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
                                       at BenchmarkTest.ExceptionThrower.<RandomSwitcher>d__7.MoveNext() + 0x341
                                    --- End of stack trace from previous location ---
                                       at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
                                       at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
                                       at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
                                       at BenchmarkTest.ExceptionThrower.<Nested1>d__2.MoveNext() + 0x9d
                                    --- End of stack trace from previous location ---
                                       at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
                                       at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
                                       at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
                                       at BenchmarkTest.ExceptionThrower.<RandomSwitcher>d__7.MoveNext() + 0x17f
                                    --- End of stack trace from previous location ---
                                       at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
                                       at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
                                       at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
                                       at BenchmarkTest.ExceptionThrower.<Nested3>d__4.MoveNext() + 0x9d
                                    --- End of stack trace from previous location ---
                                       at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
                                       at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
                                       at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
                                       at BenchmarkTest.ExceptionThrower.<RandomSwitcher>d__7.MoveNext() + 0x2ab
                                    --- End of stack trace from previous location ---
                                       at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
                                       at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
                                       at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
                                       at BenchmarkTest.ExceptionThrower.<Nested1>d__2.MoveNext() + 0x9d
                                    --- End of stack trace from previous location ---
                                       at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
                                       at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
                                       at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
                                       at BenchmarkTest.ExceptionThrower.<RandomSwitcher>d__7.MoveNext() + 0x17f
                                    --- End of stack trace from previous location ---
                                       at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
                                       at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
                                       at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
                                       at BenchmarkTest.ExceptionThrower.<Nested4>d__5.MoveNext() + 0x9d
                                    --- End of stack trace from previous location ---
                                       at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
                                       at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
                                       at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
                                       at BenchmarkTest.ExceptionThrower.<RandomSwitcher>d__7.MoveNext() + 0x341
                                    --- End of stack trace from previous location ---
                                       at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
                                       at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
                                       at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
                                       at BenchmarkTest.ExceptionThrower.<Nested2>d__3.MoveNext() + 0x9d
                                    --- End of stack trace from previous location ---
                                       at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
                                       at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
                                       at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
                                       at BenchmarkTest.ExceptionThrower.<RandomSwitcher>d__7.MoveNext() + 0x215
                                    --- End of stack trace from previous location ---
                                       at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
                                       at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
                                       at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
                                       at BenchmarkTest.ExceptionThrower.<Nested3>d__4.MoveNext() + 0x9d
                                    --- End of stack trace from previous location ---
                                       at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
                                       at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
                                       at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
                                       at BenchmarkTest.ExceptionThrower.<RandomSwitcher>d__7.MoveNext() + 0x2ab
                                    --- End of stack trace from previous location ---
                                       at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
                                       at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
                                       at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
                                       at BenchmarkTest.ExceptionThrower.<Nested3>d__4.MoveNext() + 0x9d
                                    --- End of stack trace from previous location ---
                                       at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
                                       at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
                                       at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
                                       at BenchmarkTest.ExceptionThrower.<RandomSwitcher>d__7.MoveNext() + 0x2ab
                                    --- End of stack trace from previous location ---
                                       at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
                                       at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
                                       at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
                                       at BenchmarkTest.ExceptionThrower.<Nested1>d__2.MoveNext() + 0x9d
                                    --- End of stack trace from previous location ---
                                       at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
                                       at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
                                       at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
                                       at BenchmarkTest.ExceptionThrower.<RandomSwitcher>d__7.MoveNext() + 0x17f
                                    --- End of stack trace from previous location ---
                                       at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
                                       at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
                                       at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
                                       at BenchmarkTest.ExceptionThrower.<Nested2>d__3.MoveNext() + 0x9d
                                    --- End of stack trace from previous location ---
                                       at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
                                       at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
                                       at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
                                       at BenchmarkTest.ExceptionThrower.<RandomSwitcher>d__7.MoveNext() + 0x215
                                    --- End of stack trace from previous location ---
                                       at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
                                       at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
                                       at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
                                       at BenchmarkTest.ExceptionThrower.<Nested4>d__5.MoveNext() + 0x9d
                                    --- End of stack trace from previous location ---
                                       at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
                                       at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
                                       at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
                                       at BenchmarkTest.ExceptionThrower.<RandomSwitcher>d__7.MoveNext() + 0x341
                                    --- End of stack trace from previous location ---
                                       at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
                                       at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
                                       at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
                                       at BenchmarkTest.ExceptionThrower.<Nested4>d__5.MoveNext() + 0x9d
                                    --- End of stack trace from previous location ---
                                       at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
                                       at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
                                       at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
                                       at BenchmarkTest.ExceptionThrower.<RandomSwitcher>d__7.MoveNext() + 0x341
                                    --- End of stack trace from previous location ---
                                       at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
                                       at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
                                       at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
                                       at BenchmarkTest.ExceptionThrower.<Nested1>d__2.MoveNext() + 0x9d
                                    --- End of stack trace from previous location ---
                                       at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
                                       at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
                                       at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
                                       at BenchmarkTest.ExceptionThrower.<RandomSwitcher>d__7.MoveNext() + 0x17f
                                    --- End of stack trace from previous location ---
                                       at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
                                       at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
                                       at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
                                       at BenchmarkTest.ExceptionThrower.<Nested1>d__2.MoveNext() + 0x9d
                                    --- End of stack trace from previous location ---
                                       at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
                                       at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
                                       at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
                                       at BenchmarkTest.ExceptionThrower.<RandomSwitcher>d__7.MoveNext() + 0x17f
                                    --- End of stack trace from previous location ---
                                       at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
                                       at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
                                       at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
                                       at BenchmarkTest.ExceptionThrower.<Nested1>d__2.MoveNext() + 0x9d
                                    --- End of stack trace from previous location ---
                                       at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
                                       at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
                                       at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
                                       at BenchmarkTest.ExceptionThrower.<RandomSwitcher>d__7.MoveNext() + 0x17f
                                    --- End of stack trace from previous location ---
                                       at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
                                       at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
                                       at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
                                       at BenchmarkTest.ExceptionThrower.<Nested2>d__3.MoveNext() + 0x9d
                                    --- End of stack trace from previous location ---
                                       at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
                                       at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
                                       at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
                                       at BenchmarkTest.ExceptionThrower.<RandomSwitcher>d__7.MoveNext() + 0x215
                                    --- End of stack trace from previous location ---
                                       at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
                                       at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
                                       at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
                                       at BenchmarkTest.ExceptionThrower.<Nested3>d__4.MoveNext() + 0x9d
                                    --- End of stack trace from previous location ---
                                       at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
                                       at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
                                       at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
                                       at BenchmarkTest.ExceptionThrower.<RandomSwitcher>d__7.MoveNext() + 0x2ab
                                    --- End of stack trace from previous location ---
                                       at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
                                       at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
                                       at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
                                       at BenchmarkTest.ExceptionThrower.<Nested1>d__2.MoveNext() + 0x9d
                                    --- End of stack trace from previous location ---
                                       at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
                                       at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
                                       at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
                                       at BenchmarkTest.ExceptionThrower.<RandomSwitcher>d__7.MoveNext() + 0x17f
                                    --- End of stack trace from previous location ---
                                       at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
                                       at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
                                       at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
                                       at BenchmarkTest.ExceptionThrower.<Nested3>d__4.MoveNext() + 0x9d
                                    --- End of stack trace from previous location ---
                                       at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
                                       at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
                                       at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
                                       at BenchmarkTest.ExceptionThrower.<RandomSwitcher>d__7.MoveNext() + 0x2ab
                                    --- End of stack trace from previous location ---
                                       at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
                                       at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
                                       at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
                                       at BenchmarkTest.ExceptionThrower.<Nested1>d__2.MoveNext() + 0x9d
                                    --- End of stack trace from previous location ---
                                       at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
                                       at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
                                       at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
                                       at BenchmarkTest.ExceptionThrower.<GetExceptionWithStacktrace>d__1.MoveNext() + 0xad
                                       at BenchmarkTest.ExceptionThrower.<RandomSwitcher>d__7.MoveNext() + 0x42f
                                    --- End of stack trace from previous location ---
                                       at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
                                       at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
                                       at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
                                       at BenchmarkTest.ExceptionThrower.<Nested4>d__5.MoveNext() + 0x9d
                                    --- End of stack trace from previous location ---
                                       at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
                                       at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
                                       at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
                                       at BenchmarkTest.ExceptionThrower.<RandomSwitcher>d__7.MoveNext() + 0x341
                                    --- End of stack trace from previous location ---
                                       at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
                                       at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
                                       at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
                                       at BenchmarkTest.ExceptionThrower.<Nested1>d__2.MoveNext() + 0x9d
                                    --- End of stack trace from previous location ---
                                       at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
                                       at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
                                       at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
                                       at BenchmarkTest.ExceptionThrower.<RandomSwitcher>d__7.MoveNext() + 0x17f
                                    --- End of stack trace from previous location ---
                                       at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
                                       at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
                                       at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
                                       at BenchmarkTest.ExceptionThrower.<Nested3>d__4.MoveNext() + 0x9d
                                    --- End of stack trace from previous location ---
                                       at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
                                       at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
                                       at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
                                       at BenchmarkTest.ExceptionThrower.<RandomSwitcher>d__7.MoveNext() + 0x2ab
                                    --- End of stack trace from previous location ---
                                       at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
                                       at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
                                       at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
                                       at BenchmarkTest.ExceptionThrower.<Nested1>d__2.MoveNext() + 0x9d
                                    --- End of stack trace from previous location ---
                                       at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
                                       at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
                                       at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
                                       at BenchmarkTest.ExceptionThrower.<RandomSwitcher>d__7.MoveNext() + 0x17f
                                    --- End of stack trace from previous location ---
                                       at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
                                       at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
                                       at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
                                       at BenchmarkTest.ExceptionThrower.<Nested4>d__5.MoveNext() + 0x9d
                                    --- End of stack trace from previous location ---
                                       at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
                                       at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
                                       at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
                                       at BenchmarkTest.ExceptionThrower.<RandomSwitcher>d__7.MoveNext() + 0x341
                                    --- End of stack trace from previous location ---
                                       at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
                                       at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
                                       at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
                                       at BenchmarkTest.ExceptionThrower.<Nested2>d__3.MoveNext() + 0x9d
                                    --- End of stack trace from previous location ---
                                       at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
                                       at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
                                       at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
                                       at BenchmarkTest.ExceptionThrower.<RandomSwitcher>d__7.MoveNext() + 0x215
                                    --- End of stack trace from previous location ---
                                       at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
                                       at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
                                       at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
                                       at BenchmarkTest.ExceptionThrower.<Nested3>d__4.MoveNext() + 0x9d
                                    --- End of stack trace from previous location ---
                                       at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
                                       at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
                                       at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
                                       at BenchmarkTest.ExceptionThrower.<RandomSwitcher>d__7.MoveNext() + 0x2ab
                                    --- End of stack trace from previous location ---
                                       at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
                                       at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
                                       at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
                                       at BenchmarkTest.ExceptionThrower.<Nested3>d__4.MoveNext() + 0x9d
                                    --- End of stack trace from previous location ---
                                       at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
                                       at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
                                       at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
                                       at BenchmarkTest.ExceptionThrower.<RandomSwitcher>d__7.MoveNext() + 0x2ab
                                    --- End of stack trace from previous location ---
                                       at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
                                       at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
                                       at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
                                       at BenchmarkTest.ExceptionThrower.<Nested1>d__2.MoveNext() + 0x9d
                                    --- End of stack trace from previous location ---
                                       at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
                                       at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
                                       at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
                                       at BenchmarkTest.ExceptionThrower.<RandomSwitcher>d__7.MoveNext() + 0x17f
                                    --- End of stack trace from previous location ---
                                       at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
                                       at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
                                       at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
                                       at BenchmarkTest.ExceptionThrower.<Nested2>d__3.MoveNext() + 0x9d
                                    --- End of stack trace from previous location ---
                                       at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
                                       at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
                                       at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
                                       at BenchmarkTest.ExceptionThrower.<RandomSwitcher>d__7.MoveNext() + 0x215
                                    --- End of stack trace from previous location ---
                                       at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
                                       at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
                                       at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
                                       at BenchmarkTest.ExceptionThrower.<Nested4>d__5.MoveNext() + 0x9d
                                    --- End of stack trace from previous location ---
                                       at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
                                       at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
                                       at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
                                       at BenchmarkTest.ExceptionThrower.<RandomSwitcher>d__7.MoveNext() + 0x341
                                    --- End of stack trace from previous location ---
                                       at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
                                       at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
                                       at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
                                       at BenchmarkTest.ExceptionThrower.<Nested4>d__5.MoveNext() + 0x9d
                                    --- End of stack trace from previous location ---
                                       at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
                                       at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
                                       at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
                                       at BenchmarkTest.ExceptionThrower.<RandomSwitcher>d__7.MoveNext() + 0x341
                                    --- End of stack trace from previous location ---
                                       at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
                                       at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
                                       at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
                                       at BenchmarkTest.ExceptionThrower.<Nested1>d__2.MoveNext() + 0x9d
                                    --- End of stack trace from previous location ---
                                       at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
                                       at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
                                       at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
                                       at BenchmarkTest.ExceptionThrower.<RandomSwitcher>d__7.MoveNext() + 0x17f
                                    --- End of stack trace from previous location ---
                                       at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
                                       at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
                                       at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
                                       at BenchmarkTest.ExceptionThrower.<Nested1>d__2.MoveNext() + 0x9d
                                    --- End of stack trace from previous location ---
                                       at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
                                       at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
                                       at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
                                       at BenchmarkTest.ExceptionThrower.<RandomSwitcher>d__7.MoveNext() + 0x17f
                                    --- End of stack trace from previous location ---
                                       at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
                                       at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
                                       at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
                                       at BenchmarkTest.ExceptionThrower.<Nested1>d__2.MoveNext() + 0x9d
                                    --- End of stack trace from previous location ---
                                       at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
                                       at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
                                       at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
                                       at BenchmarkTest.ExceptionThrower.<RandomSwitcher>d__7.MoveNext() + 0x17f
                                    --- End of stack trace from previous location ---
                                       at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
                                       at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
                                       at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
                                       at BenchmarkTest.ExceptionThrower.<Nested2>d__3.MoveNext() + 0x9d
                                    --- End of stack trace from previous location ---
                                       at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
                                       at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
                                       at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
                                       at BenchmarkTest.ExceptionThrower.<RandomSwitcher>d__7.MoveNext() + 0x215
                                    --- End of stack trace from previous location ---
                                       at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
                                       at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
                                       at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
                                       at BenchmarkTest.ExceptionThrower.<Nested3>d__4.MoveNext() + 0x9d
                                    --- End of stack trace from previous location ---
                                       at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
                                       at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
                                       at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
                                       at BenchmarkTest.ExceptionThrower.<RandomSwitcher>d__7.MoveNext() + 0x2ab
                                    --- End of stack trace from previous location ---
                                       at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
                                       at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
                                       at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
                                       at BenchmarkTest.ExceptionThrower.<Nested1>d__2.MoveNext() + 0x9d
                                    --- End of stack trace from previous location ---
                                       at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
                                       at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
                                       at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
                                       at BenchmarkTest.ExceptionThrower.<RandomSwitcher>d__7.MoveNext() + 0x17f
                                    --- End of stack trace from previous location ---
                                       at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
                                       at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
                                       at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
                                       at BenchmarkTest.ExceptionThrower.<Nested3>d__4.MoveNext() + 0x9d
                                    --- End of stack trace from previous location ---
                                       at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
                                       at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
                                       at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
                                       at BenchmarkTest.ExceptionThrower.<RandomSwitcher>d__7.MoveNext() + 0x2ab
                                    --- End of stack trace from previous location ---
                                       at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
                                       at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
                                       at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
                                       at BenchmarkTest.ExceptionThrower.<Nested1>d__2.MoveNext() + 0x9d
                                    --- End of stack trace from previous location ---
                                       at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw() + 0x20
                                       at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task) + 0xb2
                                       at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task, ConfigureAwaitOptions) + 0x4b
                                       at BenchmarkTest.ExceptionThrower.<GetExceptionWithStacktrace>d__1.MoveNext() + 0xad
                                    """.Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries);

        Regex regex = TerminalTestReporter.GetAOTFrameRegex();
        foreach (string stackTraceLine in stackTraceLines.Where(line =>
                     !line.StartsWith("--- ", StringComparison.Ordinal)))
        {
            Match match = regex.Match(stackTraceLine);
            Assert.IsTrue(match.Success);
            Assert.IsTrue(match.Groups["code"].Success);

            Assert.IsFalse(match.Groups["file"].Success);
            Assert.IsFalse(match.Groups["line"].Success);
        }
    }
#endif

    public void OutputFormattingIsCorrect()
    {
        var stringBuilderConsole = new StringBuilderConsole();
        var terminalReporter = new TerminalTestReporter(stringBuilderConsole, new TerminalTestReporterOptions
        {
            ShowPassedTests = () => true,
            UseAnsi = true,
            ForceAnsi = true,

            ShowAssembly = false,
            ShowAssemblyStartAndComplete = false,
            ShowProgress = () => false,
        });

        DateTimeOffset startTime = DateTimeOffset.MinValue;
        DateTimeOffset endTime = DateTimeOffset.MaxValue;
        terminalReporter.TestExecutionStarted(startTime, 1, isDiscovery: false);

        string targetFramework = "net8.0";
        string architecture = "x64";
        string assembly = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"C:\work\assembly.dll" : "/mnt/work/assembly.dll";
        string folder = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"C:\work\" : "/mnt/work/";
        string folderNoSlash = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"C:\work" : "/mnt/work";
        string folderLink = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"C:/work/" : "mnt/work/";
        string folderLinkNoSlash = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"C:/work" : "mnt/work";

        terminalReporter.AssemblyRunStarted(assembly, targetFramework, architecture, executionId: null);
        string standardOutput = "Hello!";
        string errorOutput = "Oh no!";

        terminalReporter.TestCompleted(assembly, targetFramework, architecture, executionId: null, "PassedTest1", TestOutcome.Passed, TimeSpan.FromSeconds(10),
            errorMessage: null, exception: null, expected: null, actual: null, standardOutput, errorOutput);
        terminalReporter.TestCompleted(assembly, targetFramework, architecture, executionId: null, "SkippedTest1", TestOutcome.Skipped, TimeSpan.FromSeconds(10),
            errorMessage: null, exception: null, expected: null, actual: null, standardOutput, errorOutput);
        // timed out + canceled + failed should all report as failed in summary
        terminalReporter.TestCompleted(assembly, targetFramework, architecture, executionId: null, "TimedoutTest1", TestOutcome.Timeout, TimeSpan.FromSeconds(10),
            errorMessage: null, exception: null, expected: null, actual: null, standardOutput, errorOutput);
        terminalReporter.TestCompleted(assembly, targetFramework, architecture, executionId: null, "CanceledTest1", TestOutcome.Canceled, TimeSpan.FromSeconds(10),
            errorMessage: null, exception: null, expected: null, actual: null, standardOutput, errorOutput);
        terminalReporter.TestCompleted(assembly, targetFramework, architecture, executionId: null, "FailedTest1", TestOutcome.Fail, TimeSpan.FromSeconds(10),
            errorMessage: "Tests failed", exception: new StackTraceException(@$"   at FailingTest() in {folder}codefile.cs:line 10"), expected: "ABC", actual: "DEF", standardOutput, errorOutput);
        terminalReporter.ArtifactAdded(outOfProcess: true, assembly, targetFramework, architecture, executionId: null, testName: null, @$"{folder}artifact1.txt");
        terminalReporter.ArtifactAdded(outOfProcess: false, assembly, targetFramework, architecture, executionId: null, testName: null, @$"{folder}artifact2.txt");
        terminalReporter.AssemblyRunCompleted(assembly, targetFramework, architecture, executionId: null, exitCode: null, outputData: null, errorData: null);
        terminalReporter.TestExecutionCompleted(endTime);

        string output = stringBuilderConsole.Output;

        string expected = $"""
            ␛[92mpassed␛[m PassedTest1␛[90m ␛[90m(10s 000ms)␛[m
            ␛[90m  Standard output
                Hello!
              Error output
                Oh no!
            ␛[m␛[93mskipped␛[m SkippedTest1␛[90m ␛[90m(10s 000ms)␛[m
            ␛[90m  Standard output
                Hello!
              Error output
                Oh no!
            ␛[m␛[91mfailed (canceled)␛[m TimedoutTest1␛[90m ␛[90m(10s 000ms)␛[m
            ␛[90m  Standard output
                Hello!
              Error output
                Oh no!
            ␛[m␛[91mfailed (canceled)␛[m CanceledTest1␛[90m ␛[90m(10s 000ms)␛[m
            ␛[90m  Standard output
                Hello!
              Error output
                Oh no!
            ␛[m␛[91mfailed␛[m FailedTest1␛[90m ␛[90m(10s 000ms)␛[m
            ␛[91m  Tests failed
            ␛[m␛[91m  Expected
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
            ␛[91mTest run summary: Failed!␛[90m - ␛[m␛[90m␛]8;;file:///{folderLinkNoSlash}␛\{folder}assembly.dll␛]8;;␛\␛[m (net8.0|x64)
            ␛[m  total: 5
            ␛[91m  failed: 3
            ␛[m  succeeded: 1
              skipped: 1
              duration: 3652058d 23h 59m 59s 999ms

            """;

        Assert.AreEqual(expected, ShowEscape(output));
    }

    private static string? ShowEscape(string? text)
    {
        string visibleEsc = "\x241b";
        return text?.Replace(AnsiCodes.Esc, visibleEsc);
    }

    internal class StringBuilderConsole : IConsole
    {
        private readonly StringBuilder _output = new();

        public int BufferHeight => int.MaxValue;

        public int BufferWidth => int.MinValue;

        public bool IsOutputRedirected => throw new NotImplementedException();

        public string Output => _output.ToString();

        public event ConsoleCancelEventHandler? CancelKeyPress = (sender, e) => { };

        public void Clear() => throw new NotImplementedException();

        public ConsoleColor GetBackgroundColor() => throw new NotImplementedException();

        public ConsoleColor GetForegroundColor() => ConsoleColor.White;

        public void SetBackgroundColor(ConsoleColor color) => throw new NotImplementedException();

        public void SetForegroundColor(ConsoleColor color)
        {
            // do nothing
        }

        public void Write(string format, object?[]? args) => throw new NotImplementedException();

        public void Write(string? value) => _output.Append(value);

        public void Write(char value) => _output.Append(value);

        public void WriteLine() => _output.AppendLine();

        public void WriteLine(string? value) => _output.AppendLine(value);

        public void WriteLine(object? value) => throw new NotImplementedException();

        public void WriteLine(string format, object? arg0) => throw new NotImplementedException();

        public void WriteLine(string format, object? arg0, object? arg1) => throw new NotImplementedException();

        public void WriteLine(string format, object? arg0, object? arg1, object? arg2) => throw new NotImplementedException();

        public void WriteLine(string format, object?[]? args) => throw new NotImplementedException();
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
