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
}
