// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

using Moq;

using TestFramework.ForTestingMSTest;

using ITestMethod = Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface.ObjectModel.ITestMethod;

namespace MSTestAdapter.PlatformServices.UnitTests.Services;

public sealed class TestContextOutputMutationTests : TestContainer
{
    private readonly Mock<ITestMethod> _testMethod = new();

    public TestContextOutputMutationTests()
        => MSTestSettings.PopulateSettings(
            """
            <RunSettings>
              <MSTestV2>
                <CaptureTraceOutput>Live</CaptureTraceOutput>
              </MSTestV2>
            </RunSettings>
            """,
            null,
            null);

    public void WriteMessageIsMirroredLiveWithoutLineTerminator()
    {
        var liveWriter = new RecordingWriter();
        using (TestContextImplementation.SetLiveOutputWriterForTesting(liveWriter))
        {
            TestContextImplementation context = CreateContext();
            using (TestContextImplementation.SetCurrentTestContext(context))
            {
                context.Write("first");
                context.Write("second");
            }

            context.GetDiagnosticMessages().Should().Be("firstsecond");
        }

        liveWriter.Calls.Should().Equal("Write(first)", "Write(second)");
    }

    public void WriteWithFormatIsMirroredLiveWithoutLineTerminator()
    {
        var liveWriter = new RecordingWriter();
        using (TestContextImplementation.SetLiveOutputWriterForTesting(liveWriter))
        {
            TestContextImplementation context = CreateContext();
            using (TestContextImplementation.SetCurrentTestContext(context))
            {
                context.Write("value-{0}", 1);
                context.Write("{0}+{1}", "a", "b");
            }

            context.GetDiagnosticMessages().Should().Be("value-1a+b");
        }

        liveWriter.Calls.Should().Equal("Write(value-1)", "Write(a+b)");
    }

    public void WriteLineWithFormatIsMirroredLiveWithLineTerminator()
    {
        var liveWriter = new RecordingWriter();
        using (TestContextImplementation.SetLiveOutputWriterForTesting(liveWriter))
        {
            TestContextImplementation context = CreateContext();
            using (TestContextImplementation.SetCurrentTestContext(context))
            {
                context.WriteLine("{0}!", 5);
                context.WriteLine("{0}?", "six");
            }

            context.GetDiagnosticMessages().Should().StartWith("5!").And.Contain("six?");
        }

        liveWriter.Calls.Should().Equal("WriteLine(5!)", "WriteLine(six?)");
    }

    public void LiveMirroringKeepsTheKindOfEachWriteInOrder()
    {
        var liveWriter = new RecordingWriter();
        using (TestContextImplementation.SetLiveOutputWriterForTesting(liveWriter))
        {
            TestContextImplementation context = CreateContext();
            using (TestContextImplementation.SetCurrentTestContext(context))
            {
                context.Write("a");
                context.WriteLine("b");
                context.Write("{0}", "c");
                context.WriteLine("{0}", "d");
            }
        }

        liveWriter.Calls.Should().Equal("Write(a)", "WriteLine(b)", "Write(c)", "WriteLine(d)");
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
