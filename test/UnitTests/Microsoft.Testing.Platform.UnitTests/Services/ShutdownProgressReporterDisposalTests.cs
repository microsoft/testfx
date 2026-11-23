// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class ShutdownProgressReporterDisposalTests
{
    [TestMethod]
    public void Track_AfterDispose_DoesNotRecordTheWorkEvenWhileTheHandleIsStillOpen()
    {
        using var cts = new CancellationTokenSource();
        ShutdownProgressReporter reporter = CreateReporter(cts, new Mock<ITask>());
        reporter.Dispose();

        using IDisposable handle = reporter.Track("uid-1", "Display 1", "Phase 1");

        Assert.IsEmpty(reporter.Snapshot());
    }

    [TestMethod]
    public void Dispose_UnregistersTheCancellationCallbackSoALaterCancellationStartsNoWatchdog()
    {
        using var cts = new CancellationTokenSource();
        Mock<ITask> task = new();
        ShutdownProgressReporter reporter = CreateReporter(cts, task);
        reporter.Dispose();

        // With the registration still alive this would run the callback on the disposed stop source, which throws.
        cts.Cancel();

        task.Verify(t => t.Run(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static ShutdownProgressReporter CreateReporter(CancellationTokenSource cts, Mock<ITask> task)
    {
        Mock<ITestApplicationCancellationTokenSource> cancellationSource = new();
        cancellationSource.SetupGet(x => x.CancellationToken).Returns(cts.Token);
        Mock<ILoggerFactory> loggerFactory = new();
        loggerFactory.Setup(f => f.CreateLogger(It.IsAny<string>())).Returns(new Mock<ILogger>().Object);
        Mock<IClock> clock = new();
        clock.SetupGet(c => c.UtcNow).Returns(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));
        return new ShutdownProgressReporter(
            cancellationSource.Object,
            new Mock<IOutputDevice>().Object,
            loggerFactory.Object,
            clock.Object,
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(1),
            task.Object);
    }
}
