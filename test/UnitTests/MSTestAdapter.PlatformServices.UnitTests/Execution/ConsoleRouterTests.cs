// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

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

    public void ConsoleOutRouter_WhenEchoLive_WritesToBothTestContextAndOriginalConsole()
    {
        var original = new StringWriter();
        TestContextImplementation testContext = CreateTestContext();
        var router = new ConsoleOutRouter(original, echoLive: true);

        using (TestContextImplementation.SetCurrentTestContext(testContext))
        {
            router.Write("hello");
            router.Write(' ');
            router.Write("world".ToCharArray(), 0, 5);
        }

        // Captured into the test result.
        testContext.GetAndClearOutput().Should().Be("hello world");

        // And echoed live to the original console.
        original.ToString().Should().Be("hello world");
    }

    public void ConsoleOutRouter_WhenNotEchoLive_WritesOnlyToTestContext()
    {
        var original = new StringWriter();
        TestContextImplementation testContext = CreateTestContext();
        var router = new ConsoleOutRouter(original, echoLive: false);

        using (TestContextImplementation.SetCurrentTestContext(testContext))
        {
            router.Write("hello");
        }

        testContext.GetAndClearOutput().Should().Be("hello");
        original.ToString().Should().BeEmpty();
    }

    public void ConsoleErrorRouter_WhenEchoLive_WritesToBothTestContextAndOriginalConsole()
    {
        var original = new StringWriter();
        TestContextImplementation testContext = CreateTestContext();
        var router = new ConsoleErrorRouter(original, echoLive: true);

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
        var router = new ConsoleOutRouter(original, echoLive: true);

        // No current test context -> passthrough to the original console, nothing captured.
        router.Write("outside");

        original.ToString().Should().Be("outside");
        testContext.GetAndClearOutput().Should().BeNull();
    }

    public void ConsoleOutRouter_WhenWrappingAnotherRouter_DoesNotEchoToAvoidDoubleCapture()
    {
        var real = new StringWriter();
        TestContextImplementation testContext = CreateTestContext();

        // Simulate the process being reused: an inner router is already installed, and a new outer router
        // wraps it. Echoing to the inner router while a test context is set would capture the output twice.
        var inner = new ConsoleOutRouter(real, echoLive: true);
        var outer = new ConsoleOutRouter(inner, echoLive: true);

        using (TestContextImplementation.SetCurrentTestContext(testContext))
        {
            outer.Write("once");
        }

        // Captured exactly once, and not echoed into the inner router (which would re-capture).
        testContext.GetAndClearOutput().Should().Be("once");
        real.ToString().Should().BeEmpty();
    }

    public void TraceTextWriter_WhenLiveEchoTargetProvided_WritesToBothTestContextAndTarget()
    {
        var target = new StringWriter();
        TestContextImplementation testContext = CreateTestContext();
        var writer = new TraceTextWriter(target);

        using (TestContextImplementation.SetCurrentTestContext(testContext))
        {
            writer.Write("trace-line");
        }

        testContext.GetAndClearTrace().Should().Be("trace-line");
        target.ToString().Should().Be("trace-line");
    }

    public void TraceTextWriter_WhenNoLiveEchoTarget_WritesOnlyToTestContext()
    {
        TestContextImplementation testContext = CreateTestContext();
        var writer = new TraceTextWriter(liveEchoTarget: null);

        using (TestContextImplementation.SetCurrentTestContext(testContext))
        {
            writer.Write("trace-line");
        }

        testContext.GetAndClearTrace().Should().Be("trace-line");
    }

    public void TraceTextWriter_WhenTargetIsRouter_DoesNotEchoToAvoidDoubleCapture()
    {
        var real = new StringWriter();
        TestContextImplementation testContext = CreateTestContext();
        var router = new ConsoleOutRouter(real, echoLive: true);
        var writer = new TraceTextWriter(router);

        using (TestContextImplementation.SetCurrentTestContext(testContext))
        {
            writer.Write("trace-line");
        }

        // Captured into the trace buffer once; not routed through the console router.
        testContext.GetAndClearTrace().Should().Be("trace-line");
        testContext.GetAndClearOutput().Should().BeNull();
        real.ToString().Should().BeEmpty();
    }
}
