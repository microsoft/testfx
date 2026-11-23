// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;
using Microsoft.VisualStudio.TestTools.UnitTesting.Logging;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution;

public sealed class UnitTestRunnerMutationTests : TestContainer
{
    private readonly IPlatformServiceProvider _previousProvider = PlatformServiceProvider.Instance;
    private readonly TestablePlatformServiceProvider _provider = new();

    public UnitTestRunnerMutationTests()
    {
        PlatformServiceProvider.Instance = _provider;
    }

    protected override void Dispose(bool disposing)
    {
        TestRun.SetCurrent(null);
        MSTestSettings.Reset();
        PlatformServiceProvider.Instance = _previousProvider;
        base.Dispose(disposing);
    }

    public void ConstructorRegistersALoggerHandlerThatWritesToTheStandardOutputOfTheCurrentTestContext()
    {
        const string Probe = "logger-handler-probe";
        using TestContextImplementation testContext = CreateTestContext();
        int deliveriesBefore = LogAndCountDeliveries(testContext, Probe);

        _ = new UnitTestRunner(CreateSettings(TestOutputCaptureMode.None), []);

        LogAndCountDeliveries(testContext, Probe).Should().Be(
            deliveriesBefore + 1,
            "every runner adds one handler to Logger.OnLogMessage");
    }

    public void ConstructorInstallsOutputRoutingThatCapturesConsoleAndTraceOutputIntoTheCurrentTestContext()
    {
        using var sandbox = new OutputRoutingSandbox();
        using TestContextImplementation testContext = CreateTestContext();

        _ = new UnitTestRunner(CreateSettings(TestOutputCaptureMode.Result), []);

        sandbox.IsInstalled.Should().BeTrue("the runner remembers that output routing is installed");
        TraceListener[] addedListeners = sandbox.GetAddedListeners();
        addedListeners.Should().ContainSingle("one trace listener routes Trace and Debug output").Which.Should().BeOfType<TextWriterTraceListener>();
        sandbox.CurrentLiveOutputWriter.Should().NotBeNull().And.NotBeSameAs(sandbox.LiveOutputWriterSentinel, "the dedicated live output writer replaces the previous one");

        using (TestContextImplementation.SetCurrentTestContext(testContext))
        {
            Console.Out.Write("captured-out");
            Console.Error.Write("captured-error");
            addedListeners[0].Write("captured-trace");
        }

        testContext.GetAndClearOutput().Should().Be("captured-out", "Console.Out is routed to the test context");
        testContext.GetAndClearError().Should().Be("captured-error", "Console.Error is routed to the test context");
        testContext.GetAndClearTrace().Should().Be("captured-trace", "the trace listener is routed to the test context");
    }

    public void ConstructorInstallsOutputRoutingOnlyOnce()
    {
        using var sandbox = new OutputRoutingSandbox();
        _ = new UnitTestRunner(CreateSettings(TestOutputCaptureMode.Result), []);
        TextWriter outputAfterFirstRunner = Console.Out;
        TextWriter errorAfterFirstRunner = Console.Error;
        TextWriter? liveOutputWriterAfterFirstRunner = sandbox.CurrentLiveOutputWriter;

        _ = new UnitTestRunner(CreateSettings(TestOutputCaptureMode.Result), []);

        sandbox.IsInstalled.Should().BeTrue();
        sandbox.GetAddedListeners().Should().ContainSingle("a second runner must not register a second trace listener");
        Console.Out.Should().BeSameAs(outputAfterFirstRunner, "a second runner must not wrap Console.Out again");
        Console.Error.Should().BeSameAs(errorAfterFirstRunner, "a second runner must not wrap Console.Error again");
        sandbox.CurrentLiveOutputWriter.Should().BeSameAs(liveOutputWriterAfterFirstRunner, "a second runner must not replace the live output writer");
    }

    public void ConstructorDoesNotInstallOutputRoutingWhenOutputCaptureIsDisabled()
    {
        using var sandbox = new OutputRoutingSandbox();

        _ = new UnitTestRunner(CreateSettings(TestOutputCaptureMode.None), []);

        sandbox.IsInstalled.Should().BeFalse("nothing is installed when output capture is disabled");
        Console.Out.Should().BeSameAs(sandbox.OriginalOut);
        Console.Error.Should().BeSameAs(sandbox.OriginalError);
        sandbox.GetAddedListeners().Should().BeEmpty();
        sandbox.CurrentLiveOutputWriter.Should().BeSameAs(sandbox.LiveOutputWriterSentinel);
    }

    public void ConstructorDoesNotInstallOutputRoutingWhenItIsAlreadyInstalled()
    {
        using var sandbox = new OutputRoutingSandbox();
        sandbox.IsInstalled = true;

        _ = new UnitTestRunner(CreateSettings(TestOutputCaptureMode.Result), []);

        sandbox.IsInstalled.Should().BeTrue();
        Console.Out.Should().BeSameAs(sandbox.OriginalOut);
        Console.Error.Should().BeSameAs(sandbox.OriginalError);
        sandbox.GetAddedListeners().Should().BeEmpty();
        sandbox.CurrentLiveOutputWriter.Should().BeSameAs(sandbox.LiveOutputWriterSentinel);
    }

    public void ConstructorKeepsTheTestRunCancellationTokenThatIsAlreadyRegistered()
    {
        var existingToken = new TestRunCancellationToken();
        _provider.TestRunCancellationToken = existingToken;

        _ = new UnitTestRunner(CreateSettings(TestOutputCaptureMode.None), []);

        _provider.TestRunCancellationToken.Should().BeSameAs(existingToken);
    }

    public void ConstructorRegistersATestRunCancellationTokenWhenNoneIsRegistered()
    {
        _provider.TestRunCancellationToken = null;

        _ = new UnitTestRunner(CreateSettings(TestOutputCaptureMode.None), []);

        Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.TestRunCancellationToken? token = _provider.TestRunCancellationToken;
        token.Should().NotBeNull();
    }

    public void ConstructorPublishesThePlannedTestsOfTheRunThroughTestRun()
    {
        TestRun.SetCurrent(null);
        UnitTestElement[] tests =
        [
            CreateElement("First", "Namespace.ClassA"),
            CreateElement("Second", "Namespace.ClassB"),
        ];

        _ = new UnitTestRunner(CreateSettings(TestOutputCaptureMode.None), tests);

        TestRun.Current.PlannedTests.Select(test => (test.FullyQualifiedTestClassName, test.TestName))
            .Should().Equal(("Namespace.ClassA", "First"), ("Namespace.ClassB", "Second"));
    }

    public void CreateLiveOutputWriterDoesNotWrapAnEncodingThatHasNoPreamble()
    {
        using var standardOutput = new MemoryStream();
        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        TextWriter writer = UnitTestRunner.CreateLiveOutputWriter(standardOutput, encoding);

        writer.Encoding.Should().BeSameAs(encoding);
    }

    public void CreateLiveOutputWriterLeavesTheStandardOutputStreamOpenWhenTheWriterIsDisposed()
    {
        using var standardOutput = new MemoryStream();
        TextWriter writer = UnitTestRunner.CreateLiveOutputWriter(standardOutput, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write("live");

        writer.Dispose();

        standardOutput.CanWrite.Should().BeTrue("the writer does not own the standard output stream");
        new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetString(standardOutput.ToArray()).Should().Be("live");
    }

    private static TestContextImplementation CreateTestContext()
        => new(null, null, new Dictionary<string, object?>(), null, null);

    private static UnitTestElement CreateElement(string methodName, string fullClassName)
        => new(new TestMethod(methodName, fullClassName, "TestAssembly", null));

    private static MSTestSettings CreateSettings(TestOutputCaptureMode captureMode)
    {
        var settings = new MSTestSettings();
        typeof(MSTestSettings)
            .GetProperty(nameof(MSTestSettings.OutputCaptureMode), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .SetValue(settings, captureMode);
        return settings;
    }

    private static int LogAndCountDeliveries(TestContextImplementation testContext, string message)
    {
        using (TestContextImplementation.SetCurrentTestContext(testContext))
        {
            Logger.LogMessage(message);
        }

        string output = testContext.GetAndClearOutput() ?? string.Empty;
        return (output.Length - output.Replace(message, string.Empty).Length) / message.Length;
    }

    private sealed class OutputRoutingSandbox : IDisposable
    {
        private static readonly FieldInfo InstalledField = typeof(UnitTestRunner).GetField("s_outputRoutingInstalled", BindingFlags.Static | BindingFlags.NonPublic)!;
        private static readonly FieldInfo LiveOutputWriterField = typeof(TestContextImplementation).GetField("s_liveOutputWriter", BindingFlags.Static | BindingFlags.NonPublic)!;

        private readonly TraceListener[] _originalListeners = Trace.Listeners.Cast<TraceListener>().ToArray();
        private readonly TextWriter _processOut = Console.Out;
        private readonly TextWriter _processError = Console.Error;
        private readonly StringWriter _sentinelOut = new();
        private readonly StringWriter _sentinelError = new();
        private readonly bool _wasInstalled;
        private readonly IDisposable _liveOutputWriterScope;

        public OutputRoutingSandbox()
        {
            // Plain writers stand in for the console so that output can only be routed by the runner itself: a router
            // that an earlier test left installed on the process-wide streams would otherwise hide a missing installation.
            Console.SetOut(_sentinelOut);
            Console.SetError(_sentinelError);
            OriginalOut = Console.Out;
            OriginalError = Console.Error;
            _wasInstalled = IsInstalled;
            IsInstalled = false;
            _liveOutputWriterScope = TestContextImplementation.SetLiveOutputWriterForTesting(LiveOutputWriterSentinel);
        }

        public TextWriter OriginalOut { get; }

        public TextWriter OriginalError { get; }

        public TextWriter LiveOutputWriterSentinel { get; } = new StringWriter();

        public TextWriter? CurrentLiveOutputWriter => (TextWriter?)LiveOutputWriterField.GetValue(null);

        public bool IsInstalled
        {
            get => (bool)InstalledField.GetValue(null)!;
            set => InstalledField.SetValue(null, value);
        }

        public TraceListener[] GetAddedListeners()
            => Trace.Listeners.Cast<TraceListener>().Except(_originalListeners).ToArray();

        public void Dispose()
        {
            foreach (TraceListener listener in GetAddedListeners())
            {
                Trace.Listeners.Remove(listener);
            }

            Console.SetOut(_processOut);
            Console.SetError(_processError);
            _sentinelOut.Dispose();
            _sentinelError.Dispose();
            _liveOutputWriterScope.Dispose();
            LiveOutputWriterSentinel.Dispose();
            IsInstalled = _wasInstalled;
        }
    }
}
