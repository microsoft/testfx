// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

using Microsoft.Testing.Extensions.UnitTests.Helpers;
using Microsoft.Testing.Extensions.VideoRecorder;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.TestHost;

using Moq;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class VideoRecorderSessionHandlerSegmentPruningTests
{
    private static readonly Type FailedWindowType =
        typeof(VideoRecorderSessionHandler).GetNestedType("FailedWindow", BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Could not resolve FailedWindow.");

    private static readonly ConstructorInfo FailedWindowConstructor =
        FailedWindowType.GetConstructor([typeof(double), typeof(double), FailedWindowType])
        ?? throw new InvalidOperationException("Could not resolve the FailedWindow constructor.");

    private static readonly MethodInfo OverlapsAnyFailedWindowMethod =
        typeof(VideoRecorderSessionHandler).GetMethod(
            "OverlapsAnyFailedWindow",
            BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("Could not resolve OverlapsAnyFailedWindow.");

    private static readonly MethodInfo TryPruneOldSegmentsMethod =
        typeof(VideoRecorderSessionHandler).GetMethod(
            "TryPruneOldSegments",
            BindingFlags.NonPublic | BindingFlags.Instance)
        ?? throw new InvalidOperationException("Could not resolve TryPruneOldSegments.");

    [TestMethod]
    [DynamicData(nameof(GetOverlapCases))]
    public void OverlapsAnyFailedWindow_WindowsAndSegment_ReturnsExpectedResult(double[] failedWindows, bool expected)
    {
        var segment = new VideoSegment("segment.mp4", 10, 20);
        object? failedWindow = null;
        for (int i = 0; i + 1 < failedWindows.Length; i += 2)
        {
            failedWindow = FailedWindowConstructor.Invoke([failedWindows[i], failedWindows[i + 1], failedWindow]);
        }

        bool actual = (bool)OverlapsAnyFailedWindowMethod.Invoke(null, [segment, failedWindow])!;

        Assert.AreEqual(expected, actual);
    }

    public static IEnumerable<object[]> GetOverlapCases()
    {
        yield return [Array.Empty<double>(), false];
        yield return [new double[] { 20, 30 }, false];
        yield return [new double[] { 0, 10 }, false];
        yield return [new double[] { 15, 25 }, true];
        yield return [new double[] { 0, 30 }, true];
        yield return [new double[] { 0, 5, 15, 25 }, true];
        yield return [new double[] { 0, 5, 99 }, false];
    }

    [TestMethod]
    public async Task TryPruneOldSegments_OnFailurePassedOnly_DeletesAllFinalizedSegmentsWithoutWarning()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            DateTimeOffset recordingStart = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
            DateTimeOffset now = recordingStart.AddSeconds(100);
            IReadOnlyList<VideoSegment> currentSegments = [];
            VideoRecorderSessionHandler handler = CreateHandler(
                VideoRecorderPersistenceMode.OnFailure,
                maxRetainedDuration: null,
                directory,
                recordingStart,
                () => now,
                () => currentSegments,
                out _,
                out Mock<ILogger<VideoRecorderSessionHandler>> logger);

            await handler.ConsumeAsync(
                null!,
                CreateUpdate(PassedTestNodeStateProperty.CachedInstance, recordingStart.AddSeconds(15), recordingStart.AddSeconds(25)),
                CancellationToken.None);

            VideoSegment first = CreateSegment(directory, "passed-first.tmp", 0, 10);
            VideoSegment second = CreateSegment(directory, "passed-second.tmp", 10, 20);
            currentSegments = [first, second];

            InvokeTryPruneOldSegments(handler);

            Assert.IsFalse(File.Exists(first.Path));
            Assert.IsFalse(File.Exists(second.Path));
            VerifyWarningCount(logger, Times.Never());
        }
        finally
        {
            DeleteTemporaryDirectory(directory);
        }
    }

    [TestMethod]
    public async Task TryPruneOldSegments_OnFailureWithFailedWindow_DeletesOnlyNonOverlappingSegments()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            DateTimeOffset recordingStart = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
            DateTimeOffset now = recordingStart.AddSeconds(100);
            IReadOnlyList<VideoSegment> currentSegments = [];
            VideoRecorderSessionHandler handler = CreateHandler(
                VideoRecorderPersistenceMode.OnFailure,
                maxRetainedDuration: null,
                directory,
                recordingStart,
                () => now,
                () => currentSegments,
                out _,
                out Mock<ILogger<VideoRecorderSessionHandler>> logger);

            await handler.ConsumeAsync(
                null!,
                CreateUpdate(new FailedTestNodeStateProperty("failure"), recordingStart.AddSeconds(15), recordingStart.AddSeconds(25)),
                CancellationToken.None);

            VideoSegment beforeFailure = CreateSegment(directory, "before-failure.tmp", 0, 10);
            VideoSegment overlapsFailureStart = CreateSegment(directory, "overlaps-failure-start.tmp", 10, 20);
            VideoSegment overlapsFailureEnd = CreateSegment(directory, "overlaps-failure-end.tmp", 20, 30);
            VideoSegment afterFailure = CreateSegment(directory, "after-failure.tmp", 30, 40);
            currentSegments = [beforeFailure, overlapsFailureStart, overlapsFailureEnd, afterFailure];

            InvokeTryPruneOldSegments(handler);

            Assert.IsFalse(File.Exists(beforeFailure.Path));
            Assert.IsTrue(File.Exists(overlapsFailureStart.Path));
            Assert.IsTrue(File.Exists(overlapsFailureEnd.Path));
            Assert.IsFalse(File.Exists(afterFailure.Path));
            VerifyWarningCount(logger, Times.Never());
        }
        finally
        {
            DeleteTemporaryDirectory(directory);
        }
    }

    [TestMethod]
    public async Task TryPruneOldSegments_CappedFailedFootage_DeletesAtCutoffAndKeepsNewerFootage()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            DateTimeOffset recordingStart = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
            DateTimeOffset now = recordingStart.AddSeconds(100);
            IReadOnlyList<VideoSegment> currentSegments = [];
            VideoRecorderSessionHandler handler = CreateHandler(
                VideoRecorderPersistenceMode.OnFailure,
                TimeSpan.FromSeconds(30),
                directory,
                recordingStart,
                () => now,
                () => currentSegments,
                out _,
                out Mock<ILogger<VideoRecorderSessionHandler>> logger);

            await handler.ConsumeAsync(
                null!,
                CreateUpdate(new FailedTestNodeStateProperty("failure"), recordingStart, recordingStart.AddSeconds(100)),
                CancellationToken.None);

            VideoSegment atCutoff = CreateSegment(directory, "failed-at-cutoff.tmp", 60, 70);
            VideoSegment afterCutoff = CreateSegment(directory, "failed-after-cutoff.tmp", 70, 71);
            currentSegments = [atCutoff, afterCutoff];

            InvokeTryPruneOldSegments(handler);

            Assert.IsFalse(File.Exists(atCutoff.Path));
            Assert.IsTrue(File.Exists(afterCutoff.Path));
            VerifyWarningCount(logger, Times.Once());
        }
        finally
        {
            DeleteTemporaryDirectory(directory);
        }
    }

    [TestMethod]
    public async Task TryPruneOldSegments_CapDropsOnRepeatedPasses_WarnsExactlyOnce()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            DateTimeOffset recordingStart = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
            DateTimeOffset now = recordingStart.AddSeconds(100);
            IReadOnlyList<VideoSegment> currentSegments = [];
            VideoRecorderSessionHandler handler = CreateHandler(
                VideoRecorderPersistenceMode.OnFailure,
                TimeSpan.FromSeconds(30),
                directory,
                recordingStart,
                () => now,
                () => currentSegments,
                out _,
                out Mock<ILogger<VideoRecorderSessionHandler>> logger);

            await handler.ConsumeAsync(
                null!,
                CreateUpdate(new FailedTestNodeStateProperty("failure"), recordingStart, recordingStart.AddSeconds(100)),
                CancellationToken.None);

            VideoSegment firstPass = CreateSegment(directory, "failed-first-pass.tmp", 0, 10);
            currentSegments = [firstPass];
            InvokeTryPruneOldSegments(handler);

            VideoSegment secondPass = CreateSegment(directory, "failed-second-pass.tmp", 10, 20);
            currentSegments = [secondPass];
            InvokeTryPruneOldSegments(handler);

            Assert.IsFalse(File.Exists(firstPass.Path));
            Assert.IsFalse(File.Exists(secondPass.Path));
            VerifyWarningCount(logger, Times.Once());
        }
        finally
        {
            DeleteTemporaryDirectory(directory);
        }
    }

    [TestMethod]
    public void TryPruneOldSegments_AlwaysWithCap_DeletesAtCutoffAndKeepsNewerSegment()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            DateTimeOffset recordingStart = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
            DateTimeOffset now = recordingStart.AddSeconds(100);
            IReadOnlyList<VideoSegment> currentSegments = [];
            VideoRecorderSessionHandler handler = CreateHandler(
                VideoRecorderPersistenceMode.Always,
                TimeSpan.FromSeconds(30),
                directory,
                recordingStart,
                () => now,
                () => currentSegments,
                out _,
                out Mock<ILogger<VideoRecorderSessionHandler>> logger);

            VideoSegment atCutoff = CreateSegment(directory, "always-at-cutoff.tmp", 60, 70);
            VideoSegment afterCutoff = CreateSegment(directory, "always-after-cutoff.tmp", 70, 71);
            currentSegments = [atCutoff, afterCutoff];

            InvokeTryPruneOldSegments(handler);

            Assert.IsFalse(File.Exists(atCutoff.Path));
            Assert.IsTrue(File.Exists(afterCutoff.Path));
            VerifyWarningCount(logger, Times.Once());
        }
        finally
        {
            DeleteTemporaryDirectory(directory);
        }
    }

    [TestMethod]
    public async Task TryPruneOldSegments_InFlightWatermark_DeletesBoundaryAndProtectsCurrentAndLaterSegments()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            DateTimeOffset recordingStart = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
            DateTimeOffset now = recordingStart.AddSeconds(30);
            IReadOnlyList<VideoSegment> currentSegments = [];
            VideoRecorderSessionHandler handler = CreateHandler(
                VideoRecorderPersistenceMode.OnFailure,
                maxRetainedDuration: null,
                directory,
                recordingStart,
                () => now,
                () => currentSegments,
                out _,
                out Mock<ILogger<VideoRecorderSessionHandler>> logger);

            await handler.ConsumeAsync(
                null!,
                CreateUpdate(InProgressTestNodeStateProperty.CachedInstance, recordingStart, recordingStart),
                CancellationToken.None);
            now = recordingStart.AddSeconds(100);

            VideoSegment atWatermark = CreateSegment(directory, "at-watermark.tmp", 20, 30);
            VideoSegment crossesWatermark = CreateSegment(directory, "crosses-watermark.tmp", 30, 31);
            VideoSegment afterWatermark = CreateSegment(directory, "after-watermark.tmp", 31, 40);
            currentSegments = [atWatermark, crossesWatermark, afterWatermark];

            InvokeTryPruneOldSegments(handler);

            Assert.IsFalse(File.Exists(atWatermark.Path));
            Assert.IsTrue(File.Exists(crossesWatermark.Path));
            Assert.IsTrue(File.Exists(afterWatermark.Path));
            VerifyWarningCount(logger, Times.Never());
        }
        finally
        {
            DeleteTemporaryDirectory(directory);
        }
    }

    [TestMethod]
    public void TryPruneOldSegments_AlwaysWithoutCap_DoesNotReadOrDeleteSegments()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            DateTimeOffset recordingStart = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
            DateTimeOffset now = recordingStart.AddSeconds(100);
            VideoSegment segment = CreateSegment(directory, "unbounded-always.tmp", 0, 10);
            IReadOnlyList<VideoSegment> currentSegments = [segment];
            VideoRecorderSessionHandler handler = CreateHandler(
                VideoRecorderPersistenceMode.Always,
                maxRetainedDuration: null,
                directory,
                recordingStart,
                () => now,
                () => currentSegments,
                out Mock<IVideoRecorder> recorder,
                out Mock<ILogger<VideoRecorderSessionHandler>> logger);

            InvokeTryPruneOldSegments(handler);

            recorder.Verify(instance => instance.ReadSegments(), Times.Never());
            Assert.IsTrue(File.Exists(segment.Path));
            VerifyWarningCount(logger, Times.Never());
        }
        finally
        {
            DeleteTemporaryDirectory(directory);
        }
    }

    private static VideoRecorderSessionHandler CreateHandler(
        VideoRecorderPersistenceMode persistMode,
        TimeSpan? maxRetainedDuration,
        string outputDirectory,
        DateTimeOffset recordingStart,
        Func<DateTimeOffset> getUtcNow,
        Func<IReadOnlyList<VideoSegment>> getSegments,
        out Mock<IVideoRecorder> recorder,
        out Mock<ILogger<VideoRecorderSessionHandler>> logger)
    {
        var options = new VideoRecorderOptions
        {
            OutputDirectory = outputDirectory,
            PersistMode = persistMode,
            MaxRetainedDuration = maxRetainedDuration,
        };
        var commandLineOptions = new TestCommandLineOptions(new()
        {
            [VideoRecorderCommandLineProvider.EnableOptionName] = [],
        });
        var clock = new Mock<IClock>();
        clock.SetupGet(instance => instance.UtcNow).Returns(getUtcNow);
        recorder = new Mock<IVideoRecorder>();
        recorder.SetupGet(instance => instance.RecordingStartUtc).Returns(recordingStart);
        recorder.Setup(instance => instance.ReadSegments()).Returns(getSegments);
        logger = new Mock<ILogger<VideoRecorderSessionHandler>>();

        return new VideoRecorderSessionHandler(
            options,
            Mock.Of<IConfiguration>(),
            commandLineOptions,
            Mock.Of<IMessageBus>(),
            Mock.Of<IOutputDevice>(),
            clock.Object,
            logger.Object,
            recorder.Object);
    }

    private static TestNodeUpdateMessage CreateUpdate(
        TestNodeStateProperty state,
        DateTimeOffset start,
        DateTimeOffset end)
        => new(
            new SessionUid("session"),
            new TestNode
            {
                Uid = Guid.NewGuid().ToString("N"),
                DisplayName = "Pruning test",
                Properties = new PropertyBag(
                    state,
                    new TimingProperty(new TimingInfo(start, end, end - start))),
            });

    private static VideoSegment CreateSegment(string directory, string fileName, double startSeconds, double endSeconds)
    {
        string path = Path.Combine(directory, fileName);
        File.WriteAllText(path, "segment data");
        return new VideoSegment(path, startSeconds, endSeconds);
    }

    private static string CreateTemporaryDirectory()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            $"{nameof(VideoRecorderSessionHandlerSegmentPruningTests)}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static void DeleteTemporaryDirectory(string directory)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static void InvokeTryPruneOldSegments(VideoRecorderSessionHandler handler)
        => TryPruneOldSegmentsMethod.Invoke(handler, parameters: null);

    private static void VerifyWarningCount(Mock<ILogger<VideoRecorderSessionHandler>> logger, Times times)
        => logger.Verify(
            instance => instance.Log(
                LogLevel.Warning,
                It.IsAny<string>(),
                null,
                It.IsAny<Func<string, Exception?, string>>()),
            times);
}
