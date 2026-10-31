// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.VideoRecorder;
using Microsoft.Testing.Extensions.VideoRecorder.Resources;
using Microsoft.Testing.Platform.Helpers;

using Moq;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class FfmpegVideoRecorderPublicFailureTests
{
    [TestMethod]
    public async Task UnavailableRecorder_RepeatedStartAndCanceledOperations_RemainInert()
    {
        var warnings = new List<string>();
        var logs = new List<string>();
        var clock = new Mock<IClock>(MockBehavior.Strict);
        var recorder = new FfmpegVideoRecorder(
            new VideoRecorderOptions { FfmpegPath = "\0missing-ffmpeg" },
            "\0output",
            clock.Object,
            logs.Add,
            warnings.Add);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        recorder.Start();
        recorder.Start();
        await recorder.StopAsync(cancellation.Token);
        string? result = await recorder.ConcatAsync(
            [new VideoSegment("first.mp4", 0, 4), new VideoSegment("second.mp4", 4, 8)],
            "\0result.mp4",
            "\0chapters.txt",
            cancellation.Token);

        Assert.AreSequenceEqual([VideoRecorderResources.FfmpegNotFound, VideoRecorderResources.FfmpegNotFound], warnings);
        Assert.IsNull(result);
        Assert.IsFalse(recorder.IsAvailable);
        Assert.IsNull(recorder.RecordingStartUtc);
        Assert.IsNull(recorder.SegmentDirectory);
        Assert.IsEmpty(recorder.ReadSegments());
        Assert.IsEmpty(logs);
        clock.VerifyNoOtherCalls();
    }

    [TestMethod]
    [DataRow(VideoRecorderFormat.Mp4H264, "mp4", DisplayName = "MP4 startup validation failure")]
    [DataRow(VideoRecorderFormat.WebMVp9, "webm", DisplayName = "WebM startup validation failure")]
    public async Task Start_InvalidOutputDirectory_ReportsEachFailureWithoutLaunching(VideoRecorderFormat format, string extension)
    {
        var warnings = new List<string>();
        var logs = new List<string>();
        var clock = new Mock<IClock>(MockBehavior.Strict);
        // This existing file is resolved but never executed. The invalid output path fails before Process is constructed.
        string configuredFile = typeof(FfmpegVideoRecorder).Assembly.Location;
        var recorder = new FfmpegVideoRecorder(
            new VideoRecorderOptions { FfmpegPath = configuredFile, Format = format },
            "\0output",
            clock.Object,
            logs.Add,
            warnings.Add);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        recorder.Start();
        recorder.Start();
        await recorder.StopAsync(cancellation.Token);

        Assert.IsTrue(recorder.IsAvailable, "The configured assembly must resolve so Start exercises output-path failure, not executable lookup failure.");
        Assert.HasCount(2, warnings);
        string[] warningParts = VideoRecorderResources.FailedToStartRecording.Split(["{0}"], StringSplitOptions.None);
        foreach (string warning in warnings)
        {
            Assert.StartsWith(warningParts[0], warning);
            Assert.EndsWith(warningParts[1], warning);

            Assert.IsGreaterThan(warningParts[0].Length + warningParts[1].Length, warning.Length, "The warning must retain the failure detail.");
        }

        Assert.IsEmpty(logs, "No launch message or process work is reached after invalid-directory validation fails.");
        Assert.IsNull(recorder.RecordingStartUtc);
        Assert.IsNull(recorder.SegmentDirectory);
        Assert.AreEqual(extension, recorder.SegmentExtension);
        Assert.IsEmpty(recorder.ReadSegments());
        clock.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task ConcatAsync_BeforeRecording_IgnoresOutputArgumentsAndReturnsNull()
    {
        var logs = new List<string>();
        var warnings = new List<string>();
        var recorder = new FfmpegVideoRecorder(
            new VideoRecorderOptions { FfmpegPath = typeof(FfmpegVideoRecorder).Assembly.Location },
            "\0output",
            Mock.Of<IClock>(),
            logs.Add,
            warnings.Add);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        string? result = await recorder.ConcatAsync(
            [new VideoSegment("first.mp4", 0, 4), new VideoSegment("second.mp4", 4, 8)],
            "\0result.mp4",
            "\0chapters.txt",
            cancellation.Token);

        Assert.IsTrue(recorder.IsAvailable);
        Assert.IsNull(result);
        Assert.IsEmpty(logs);
        Assert.IsEmpty(warnings);
        Assert.IsNull(recorder.SegmentDirectory);
        Assert.IsNull(recorder.RecordingStartUtc);
    }
}
