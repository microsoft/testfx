// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

using Moq;

using TestFramework.ForTestingMSTest;

using ITestMethod = Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface.ObjectModel.ITestMethod;

namespace MSTestAdapter.PlatformServices.UnitTests.Services;

public sealed class TestContextLiveOutputScopeMutationTests : TestContainer
{
    private readonly Mock<ITestMethod> _testMethod = new();

    public TestContextLiveOutputScopeMutationTests()
        => SetCaptureMode("Live");

    public void WriteIsMirroredLiveWhileTheScopeOfTheContextIsActive()
    {
        var liveWriter = new RecordingWriter();
        using (TestContextImplementation.SetLiveOutputWriterForTesting(liveWriter))
        {
            TestContextImplementation context = CreateContext();
            using (TestContextImplementation.SetCurrentTestContext(context))
            {
                context.Write("inside");
            }
        }

        liveWriter.Calls.Should().Equal("Write(inside)");
    }

    public void WriteWithoutConfiguredLiveWriterOnlyCapturesTheMessage()
    {
        using (TestContextImplementation.SetLiveOutputWriterForTesting(null!))
        {
            TestContextImplementation context = CreateContext();
            using (TestContextImplementation.SetCurrentTestContext(context))
            {
                context.Write("captured");
                context.WriteLine("-and-more");
            }

            string? diagnosticMessages = context.GetDiagnosticMessages();
            diagnosticMessages.Should().NotBeNull();
            diagnosticMessages!.Replace("\r\n", "\n").Should().Be(
                """
                captured-and-more

                """.Replace("\r\n", "\n"));
        }
    }

    public void WriteIsNotMirroredLiveUnlessTheCaptureModeIsLive()
    {
        var liveWriter = new RecordingWriter();
        using (TestContextImplementation.SetLiveOutputWriterForTesting(liveWriter))
        {
            TestContextImplementation context = CreateContext();
            using (TestContextImplementation.SetCurrentTestContext(context))
            {
                context.Write("live-1");

                foreach (string mode in new[] { "Result", "None" })
                {
                    SetCaptureMode(mode);
                    context.Write($"not-live-{mode}");
                }

                SetCaptureMode("Live");
                context.WriteLine("live-2");
            }

            string? diagnosticMessages = context.GetDiagnosticMessages();
            diagnosticMessages.Should().NotBeNull();
            diagnosticMessages!.Replace("\r\n", "\n").Should().Be(
                """
                live-1not-live-Resultnot-live-Nonelive-2

                """.Replace("\r\n", "\n"));
        }

        liveWriter.Calls.Should().Equal("Write(live-1)", "WriteLine(live-2)");
    }

    public void WriteThroughExecutionContextCapturedBeforeTheScopeEndedIsNotMirrored()
    {
        var liveWriter = new RecordingWriter();
        using (TestContextImplementation.SetLiveOutputWriterForTesting(liveWriter))
        {
            TestContextImplementation context = CreateContext();
            ExecutionContext capturedContext;
            using (TestContextImplementation.SetCurrentTestContext(context))
            {
                capturedContext = ExecutionContext.Capture()!;
                context.Write("inside");
            }

            ExecutionContext.Run(capturedContext, static state => ((TestContextImplementation)state!).Write("late"), context);

            context.GetDiagnosticMessages().Should().Be("insidelate");
        }

        liveWriter.Calls.Should().Equal("Write(inside)");
    }

    protected override void Dispose(bool disposing)
    {
        try
        {
            MSTestSettings.Reset();
        }
        finally
        {
            base.Dispose(disposing);
        }
    }

    private static void SetCaptureMode(string mode)
        => MSTestSettings.PopulateSettings(
            $"""
            <RunSettings>
              <MSTestV2>
                <CaptureTraceOutput>{mode}</CaptureTraceOutput>
              </MSTestV2>
            </RunSettings>
            """,
            null,
            null);

    private TestContextImplementation CreateContext()
        => new(_testMethod.Object, null, new Dictionary<string, object?>(), null, null);

    private sealed class RecordingWriter : TextWriter
    {
        public override Encoding Encoding => Encoding.UTF8;

        public List<string> Calls { get; } = [];

        public override void Write(string? value) => Calls.Add($"Write({value})");

        public override void WriteLine(string? value) => Calls.Add($"WriteLine({value})");
    }
}
