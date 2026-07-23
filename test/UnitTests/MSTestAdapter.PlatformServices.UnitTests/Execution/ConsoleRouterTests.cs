// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

using Moq;

using TestFramework.ForTestingMSTest;

using ITestMethod = Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface.ObjectModel.ITestMethod;

namespace MSTestAdapter.PlatformServices.UnitTests.Execution;

public class ConsoleRouterTests : TestContainer
{
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);

    private readonly Mock<ITestMethod> _testMethod = new();

    private TestContextImplementation CreateTestContext()
        => new(_testMethod.Object, null, new Dictionary<string, object?>(), null, null);

    private static Func<TestOutputCaptureMode> Mode(TestOutputCaptureMode mode) => () => mode;

    public void ConsoleOutRouter_InLiveMode_WritesToBothTestContextAndOriginalConsole()
    {
        var original = new StringWriter();
        TestContextImplementation testContext = CreateTestContext();
        var router = new ConsoleOutRouter(original, Mode(TestOutputCaptureMode.Live));

        using (TestContextImplementation.SetCurrentTestContext(testContext))
        {
            router.Write("hello");
            router.Write(' ');
            router.Write("world".ToCharArray(), 0, 5);
        }

        // Captured into the test result and echoed live to the original console.
        testContext.GetAndClearOutput().Should().Be("hello world");
        original.ToString().Should().Be("hello world");
    }

    public void ConsoleOutRouter_InResultMode_WritesOnlyToTestContext()
    {
        var original = new StringWriter();
        TestContextImplementation testContext = CreateTestContext();
        var router = new ConsoleOutRouter(original, Mode(TestOutputCaptureMode.Result));

        using (TestContextImplementation.SetCurrentTestContext(testContext))
        {
            router.Write("hello");
        }

        testContext.GetAndClearOutput().Should().Be("hello");
        original.ToString().Should().BeEmpty();
    }

    public void ConsoleOutRouter_InNoneMode_PassesThroughWithoutCapturing()
    {
        var original = new StringWriter();
        TestContextImplementation testContext = CreateTestContext();
        var router = new ConsoleOutRouter(original, Mode(TestOutputCaptureMode.None));

        using (TestContextImplementation.SetCurrentTestContext(testContext))
        {
            router.Write("hello");
        }

        // None does not capture even when a test is running; output flows straight to the console.
        testContext.GetAndClearOutput().Should().BeNull();
        original.ToString().Should().Be("hello");
    }

    public void ConsoleErrorRouter_InLiveMode_WritesToBothTestContextAndOriginalConsole()
    {
        var original = new StringWriter();
        TestContextImplementation testContext = CreateTestContext();
        var router = new ConsoleErrorRouter(original, Mode(TestOutputCaptureMode.Live));

        using (TestContextImplementation.SetCurrentTestContext(testContext))
        {
            router.Write("boom");
        }

        testContext.GetAndClearError().Should().Be("boom");
        original.ToString().Should().Be("boom");
    }

    public void ConsoleRouter_WithoutTestContext_WritesToOriginalConsoleOnly()
    {
        var original = new StringWriter();
        TestContextImplementation testContext = CreateTestContext();
        var router = new ConsoleOutRouter(original, Mode(TestOutputCaptureMode.Live));

        // No current test context -> passthrough to the original console, nothing captured.
        router.Write("outside");

        original.ToString().Should().Be("outside");
        testContext.GetAndClearOutput().Should().BeNull();
    }

    public void ConsoleOutRouter_HonorsModeChangeBetweenWrites()
    {
        var original = new StringWriter();
        TestContextImplementation testContext = CreateTestContext();
        TestOutputCaptureMode mode = TestOutputCaptureMode.Result;
        var router = new ConsoleOutRouter(original, () => mode);

        using (TestContextImplementation.SetCurrentTestContext(testContext))
        {
            router.Write("a");
            mode = TestOutputCaptureMode.Live;
            router.Write("b");
        }

        // Both writes are captured; only the second (Live) is echoed live.
        testContext.GetAndClearOutput().Should().Be("ab");
        original.ToString().Should().Be("b");
    }

    public void TraceTextWriter_InLiveMode_WritesToBothTestContextAndConsole()
    {
        var console = new StringWriter();
        TestContextImplementation testContext = CreateTestContext();
        var writer = new TraceTextWriter(console, Mode(TestOutputCaptureMode.Live));

        using (TestContextImplementation.SetCurrentTestContext(testContext))
        {
            writer.Write("trace-line");
        }

        testContext.GetAndClearTrace().Should().Be("trace-line");
        console.ToString().Should().Be("trace-line");
    }

    public void TraceTextWriter_InResultMode_CapturesWithoutEcho()
    {
        var console = new StringWriter();
        TestContextImplementation testContext = CreateTestContext();
        var writer = new TraceTextWriter(console, Mode(TestOutputCaptureMode.Result));

        using (TestContextImplementation.SetCurrentTestContext(testContext))
        {
            writer.Write("trace-line");
        }

        testContext.GetAndClearTrace().Should().Be("trace-line");
        console.ToString().Should().BeEmpty();
    }

    public void TraceTextWriter_InNoneMode_DoesNotCapture()
    {
        var console = new StringWriter();
        TestContextImplementation testContext = CreateTestContext();
        var writer = new TraceTextWriter(console, Mode(TestOutputCaptureMode.None));

        using (TestContextImplementation.SetCurrentTestContext(testContext))
        {
            writer.Write("trace-line");
        }

        // None leaves trace to the default listeners; our writer neither captures nor echoes.
        testContext.GetAndClearTrace().Should().BeNull();
        console.ToString().Should().BeEmpty();
    }

    public async Task LiveOutput_ConcurrentTestContextAndConsoleWrites_DoNotInvertConsoleWriterLocks()
    {
        TextWriter previousConsoleOut = Console.Out;

        MSTestSettings.PopulateSettings(
            """
            <RunSettings>
              <MSTestV2>
                <CaptureTraceOutput>Live</CaptureTraceOutput>
              </MSTestV2>
            </RunSettings>
            """,
            null,
            null);

        try
        {
            var capturedWriter = new LockCycleTextWriter();
            LockCycleResult capturedWriterResult = await RunLockCycleScenarioAsync(
                capturedWriter,
                capturedWriter,
                testContext => testContext.WriteLine("test-context"));
            capturedWriterResult.LockInversionObserved.Should().BeTrue(
                "the controlled writer should reproduce the captured/current console lock inversion");

            capturedWriter = new LockCycleTextWriter();
            var standardOutput = new ReentrantConsoleStream();
            TextWriter liveOutputWriter = UnitTestRunner.CreateLiveOutputWriter(
                standardOutput,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

            LockCycleResult dedicatedWriterResult = await RunLockCycleScenarioAsync(
                capturedWriter,
                liveOutputWriter,
                testContext => testContext.WriteLine("test-context"));

            dedicatedWriterResult.LockInversionObserved.Should().BeFalse(
                "TestContext live output must not hold the captured console writer while the standard output stream consults Console.Out");
            dedicatedWriterResult.TestContextMessages.Should().Contain("test-context");
            dedicatedWriterResult.TestContextOutput.Should().Contain("console-logger");
            capturedWriter.Output.Should().Contain("console-logger").And.Contain("standard-output-write");
            standardOutput.Output.Should().NotStartWith("\uFEFF").And.Contain("test-context");
        }
        finally
        {
            Console.SetOut(previousConsoleOut);
            TestContextImplementation.ConfigureLiveOutputWriter(previousConsoleOut);
            MSTestSettings.Reset();
        }
    }

    public async Task LiveTrace_ConcurrentTraceAndConsoleWrites_DoNotInvertConsoleWriterLocks()
    {
        TextWriter previousConsoleOut = Console.Out;
        MSTestSettings.PopulateSettings(
            """
            <RunSettings>
              <MSTestV2>
                <CaptureTraceOutput>Live</CaptureTraceOutput>
              </MSTestV2>
            </RunSettings>
            """,
            null,
            null);

        try
        {
            var capturedWriter = new LockCycleTextWriter(reenterCurrentConsoleOnFirstWrite: true);
            var capturedTraceWriter = new TraceTextWriter(capturedWriter, Mode(TestOutputCaptureMode.Live));
            LockCycleResult capturedWriterResult = await RunLockCycleScenarioAsync(
                capturedWriter,
                capturedWriter,
                _ => capturedTraceWriter.Write("trace"));
            capturedWriterResult.LockInversionObserved.Should().BeTrue(
                "the controlled writer should reproduce the trace/current console lock inversion");

            capturedWriter = new LockCycleTextWriter();
            var standardOutput = new ReentrantConsoleStream();
            TextWriter liveOutputWriter = UnitTestRunner.CreateLiveOutputWriter(
                standardOutput,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            var dedicatedTraceWriter = new TraceTextWriter(liveOutputWriter, Mode(TestOutputCaptureMode.Live));

            LockCycleResult dedicatedWriterResult = await RunLockCycleScenarioAsync(
                capturedWriter,
                liveOutputWriter,
                _ => dedicatedTraceWriter.Write("trace"));

            dedicatedWriterResult.LockInversionObserved.Should().BeFalse(
                "trace live output must not hold the captured console writer while the standard output stream consults Console.Out");
            dedicatedWriterResult.TraceOutput.Should().Contain("trace");
            capturedWriter.Output.Should().Contain("console-logger").And.Contain("standard-output-write");
            standardOutput.Output.Should().NotStartWith("\uFEFF").And.Contain("trace");
        }
        finally
        {
            Console.SetOut(previousConsoleOut);
            TestContextImplementation.ConfigureLiveOutputWriter(previousConsoleOut);
            MSTestSettings.Reset();
        }
    }

    private async Task<LockCycleResult> RunLockCycleScenarioAsync(
        LockCycleTextWriter capturedWriter,
        TextWriter liveOutputWriter,
        Action<TestContextImplementation> liveWrite)
    {
        TestContextImplementation.ConfigureLiveOutputWriter(liveOutputWriter);
        TestContextImplementation testContext = CreateTestContext();
        Console.SetOut(new ConsoleOutRouter(capturedWriter, capturedWriter.EnterCurrentWriter));

        try
        {
            using (TestContextImplementation.SetCurrentTestContext(testContext))
            {
                var liveOutputWrite = Task.Run(() => liveWrite(testContext));

                var firstWriterTimeout = Task.Delay(WaitTimeout);
                Task firstWriterEntered = await Task.WhenAny(
                    capturedWriter.CapturedWriterEntered,
                    capturedWriter.CurrentWriterEntered,
                    firstWriterTimeout);
                firstWriterEntered.Should().NotBeSameAs(
                    firstWriterTimeout,
                    "one output path should enter a console writer before the timeout");

                var consoleLoggerWrite = Task.Run(() => Console.Write("console-logger"));
                capturedWriter.ReleaseCurrentWriter();

                var allWrites = Task.WhenAll(liveOutputWrite, consoleLoggerWrite);
                Task completedTask = await Task.WhenAny(allWrites, Task.Delay(WaitTimeout));
                completedTask.Should().BeSameAs(allWrites, "both controlled output paths should complete");
                await allWrites;
            }

            return new(
                capturedWriter.LockInversionObserved,
                testContext.GetDiagnosticMessages(),
                testContext.GetAndClearOutput(),
                testContext.GetAndClearTrace());
        }
        finally
        {
            capturedWriter.ReleaseAll();
        }
    }

    private sealed record LockCycleResult(
        bool LockInversionObserved,
        string? TestContextMessages,
        string? TestContextOutput,
        string? TraceOutput);

    private sealed class LockCycleTextWriter : TextWriter
    {
        private readonly bool _reenterCurrentConsoleOnFirstWrite;
        private readonly object _capturedWriterLock = new();
        private readonly StringBuilder _output = new();
        private readonly TaskCompletionSource<object?> _capturedWriterEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<object?> _currentWriterEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<object?> _releaseCurrentWriter = new(TaskCreationOptions.RunContinuationsAsynchronously);

        private int _capturedWriterOwnerThreadId;
        private int _currentWriterEntryCount;
        private int _lockInversionObserved;
        private int _writeReentryStarted;

        internal LockCycleTextWriter(bool reenterCurrentConsoleOnFirstWrite = false)
            => _reenterCurrentConsoleOnFirstWrite = reenterCurrentConsoleOnFirstWrite;

        public override Encoding Encoding => Encoding.UTF8;

        internal Task CapturedWriterEntered => _capturedWriterEntered.Task;

        internal Task CurrentWriterEntered => _currentWriterEntered.Task;

        internal bool LockInversionObserved => Volatile.Read(ref _lockInversionObserved) == 1;

        internal string Output
        {
            get
            {
                lock (_capturedWriterLock)
                {
                    return _output.ToString();
                }
            }
        }

        internal TestOutputCaptureMode EnterCurrentWriter()
        {
            if (Interlocked.Increment(ref _currentWriterEntryCount) == 1)
            {
                _currentWriterEntered.TrySetResult(null);
                _releaseCurrentWriter.Task.GetAwaiter().GetResult();
            }

            return TestOutputCaptureMode.Live;
        }

        internal void ReleaseCurrentWriter()
            => _releaseCurrentWriter.TrySetResult(null);

        internal void ReleaseAll()
        {
            _currentWriterEntered.TrySetResult(null);
            _releaseCurrentWriter.TrySetResult(null);
        }

        public override void Write(string? value)
        {
            if (_reenterCurrentConsoleOnFirstWrite
                && Interlocked.CompareExchange(ref _writeReentryStarted, 1, 0) == 0)
            {
                lock (_capturedWriterLock)
                {
                    Volatile.Write(ref _capturedWriterOwnerThreadId, Environment.CurrentManagedThreadId);
                    try
                    {
                        _capturedWriterEntered.TrySetResult(null);
                        _currentWriterEntered.Task.GetAwaiter().GetResult();
                        Console.Write("stream-writer-reentry");
                        _output.Append(value);
                    }
                    finally
                    {
                        Volatile.Write(ref _capturedWriterOwnerThreadId, 0);
                    }
                }

                return;
            }

            if (!Monitor.TryEnter(_capturedWriterLock))
            {
                if (Volatile.Read(ref _capturedWriterOwnerThreadId) != 0)
                {
                    Volatile.Write(ref _lockInversionObserved, 1);
                    return;
                }

                lock (_capturedWriterLock)
                {
                    _output.Append(value);
                }

                return;
            }

            try
            {
                _output.Append(value);
            }
            finally
            {
                Monitor.Exit(_capturedWriterLock);
            }
        }

        public override void WriteLine(string? value)
        {
            lock (_capturedWriterLock)
            {
                Volatile.Write(ref _capturedWriterOwnerThreadId, Environment.CurrentManagedThreadId);
                try
                {
                    _capturedWriterEntered.TrySetResult(null);
                    _currentWriterEntered.Task.GetAwaiter().GetResult();

                    // Models the Unix StreamWriter/ConsolePal path consulting the current Console.Out
                    // while the originally captured synchronized writer is still held.
                    Console.Write("stream-writer-reentry");
                    _output.AppendLine(value);
                }
                finally
                {
                    Volatile.Write(ref _capturedWriterOwnerThreadId, 0);
                }
            }
        }
    }

    private sealed class ReentrantConsoleStream : Stream
    {
        private readonly MemoryStream _output = new();

        internal string Output
        {
            get
            {
                lock (_output)
                {
                    return Encoding.UTF8.GetString(_output.ToArray());
                }
            }
        }

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set
            {
                _ = value;
                throw new NotSupportedException();
            }
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
            => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin)
            => throw new NotSupportedException();

        public override void SetLength(long value)
            => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
        {
            // Models ConsolePal.WriteFromConsoleStream consulting the current Console.Out.
            Console.Write("standard-output-write");

            lock (_output)
            {
                _output.Write(buffer, offset, count);
            }
        }
    }
}
