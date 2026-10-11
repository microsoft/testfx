// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

using Microsoft.Testing.Extensions.VideoRecorder;
using Microsoft.Testing.Extensions.VideoRecorder.Resources;
using Microsoft.Testing.Platform.Helpers;

using Moq;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class FfmpegVideoRecorderBehaviorTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(VideoRecorderFormat.Mp4H264, -2, null, 1, "mp4")]
    [DataRow(VideoRecorderFormat.Mp4H264, 0, "   ", 1, "mp4")]
    [DataRow(VideoRecorderFormat.Mp4H264, 4, "  -an  ", 4, "mp4")]
    [DataRow(VideoRecorderFormat.WebMVp9, 7, " -b:v 2M ", 7, "webm")]
    public void BuildSegmentArguments_InputOverride_UsesEncoderKeyframesAndTrimmedOutputOptions(
        VideoRecorderFormat format, int segmentSeconds, string? extra, int expectedSeconds, string extension)
    {
        string directory = TestContext.TestTempDirectory!;
        string listPath = Path.Combine(directory, "segments.csv");
        var options = new VideoRecorderOptions
        {
            Format = format,
            InputArgumentsOverride = "-f lavfi -i testsrc=size=320x240",
            SegmentLengthSeconds = segmentSeconds,
            ExtraRecorderArguments = extra,
        };
        FfmpegVideoRecorder recorder = CreateRecorder(options, directory);
        typeof(FfmpegVideoRecorder).GetProperty(nameof(FfmpegVideoRecorder.SegmentExtension))!.SetValue(recorder, extension);

        string arguments = InvokeArguments(recorder, directory, listPath);

        string encoder = format == VideoRecorderFormat.WebMVp9
            ? "-c:v libvpx-vp9 -b:v 0 -crf 32 -deadline realtime -cpu-used 5"
            : "-c:v libx264 -preset veryfast -crf 28";
        string expectedExtra = string.IsNullOrWhiteSpace(extra) ? string.Empty : extra!.Trim() + " ";
        string expected = $"-y -f lavfi -i testsrc=size=320x240 {encoder} -pix_fmt yuv420p -force_key_frames \"expr:gte(t,n_forced*{expectedSeconds})\" "
            + $"-f segment -segment_time {expectedSeconds} -segment_format {extension} -reset_timestamps 1 "
            + $"-segment_list \"{listPath}\" -segment_list_type csv {expectedExtra}\"{Path.Combine(directory, $"seg_%05d.{extension}")}\"";
        Assert.AreEqual(expected, arguments);
    }

    [TestMethod]
    public void BuildSegmentArguments_DefaultScreenInput_IncludesEvenPixelCrop()
    {
        // Screen-source argument construction never queries desktop windows or launches ffmpeg.
        string directory = TestContext.TestTempDirectory!;
        var options = new VideoRecorderOptions { FrameRate = 23, Source = VideoCaptureSource.Screen };
        FfmpegVideoRecorder recorder = CreateRecorder(options, directory);
        string input = (string)typeof(FfmpegVideoRecorder)
            .GetMethod("BuildDefaultInput", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(recorder, null)!;

        string arguments = InvokeArguments(recorder, directory, Path.Combine(directory, "segments.csv"));

        Assert.StartsWith($"-y {input} -vf \"crop=trunc(iw/2)*2:trunc(ih/2)*2\" -c:v libx264", arguments);
        Assert.Contains("-framerate 23", input);
    }

    [TestMethod]
    public async Task Start_MissingExecutable_WarnsWithoutCreatingRecordingState()
    {
        string directory = TestContext.TestTempDirectory!;
        var warnings = new List<string>();
        FfmpegVideoRecorder recorder = CreateRecorder(new VideoRecorderOptions(), directory, warnings.Add);

        recorder.Start();
        await recorder.StopAsync(TestContext.CancellationToken);

        Assert.AreSequenceEqual(new[] { VideoRecorderResources.FfmpegNotFound }, warnings);
        Assert.IsNull(recorder.RecordingStartUtc);
        Assert.IsNull(recorder.SegmentDirectory);
        Assert.IsEmpty(Directory.GetFileSystemEntries(directory));
    }

    [TestMethod]
    public async Task Start_OutputDirectoryIsAFile_FailsBeforeProcessLaunchAndResetsState()
    {
        string directory = TestContext.TestTempDirectory!;
        string executable = Path.Combine(directory, "configured-ffmpeg");
        string blockedOutput = Path.Combine(directory, "not-a-directory");
        File.WriteAllText(executable, "not executed");
        File.WriteAllText(blockedOutput, "keep");
        var warnings = new List<string>();
        var logs = new List<string>();
        var options = new VideoRecorderOptions { FfmpegPath = executable, Format = VideoRecorderFormat.WebMVp9 };
        var recorder = new FfmpegVideoRecorder(options, blockedOutput, Mock.Of<IClock>(), logs.Add, warnings.Add);

        recorder.Start();
        recorder.Start();
        await recorder.StopAsync(TestContext.CancellationToken);

        Assert.HasCount(2, warnings);
        Assert.IsTrue(warnings.All(message => message.Length > 0));
        Assert.IsEmpty(logs, "Directory creation must fail before process launch is logged.");
        Assert.AreEqual("keep", File.ReadAllText(blockedOutput));
        Assert.AreEqual("webm", recorder.SegmentExtension);
        Assert.IsNull(recorder.RecordingStartUtc);
        Assert.IsNull(recorder.SegmentDirectory);
        Assert.IsEmpty(recorder.ReadSegments());
    }

    [TestMethod]
    [DataRow(true, true, true)]
    [DataRow(false, false, true)]
    [DataRow(false, true, false)]
    public async Task ConcatAsync_MissingPrerequisite_ReturnsNullWithoutWritingFiles(bool missingExecutable, bool hasSegments, bool hasDirectory)
    {
        string directory = TestContext.TestTempDirectory!;
        string executable = Path.Combine(directory, "configured-ffmpeg");
        if (!missingExecutable)
        {
            File.WriteAllText(executable, "not executed");
        }

        FfmpegVideoRecorder recorder = CreateRecorder(new VideoRecorderOptions { FfmpegPath = executable }, directory);
        if (hasDirectory)
        {
            typeof(FfmpegVideoRecorder).GetProperty(nameof(FfmpegVideoRecorder.SegmentDirectory))!.SetValue(recorder, directory);
        }

        IReadOnlyList<VideoSegment> segments = hasSegments ? [new VideoSegment("segment.mp4", 0, 1)] : [];
        string[] before = Directory.GetFileSystemEntries(directory);

        string? result = await recorder.ConcatAsync(segments, "result.mp4", null, CancellationToken.None);

        Assert.IsNull(result);
        Assert.AreSequenceEqual(before, Directory.GetFileSystemEntries(directory));
    }

    [TestMethod]
    public async Task ConcatAsync_ListDirectoryIsAFile_LogsFailureAndLeavesExistingFilesIntact()
    {
        string directory = TestContext.TestTempDirectory!;
        string executable = Path.Combine(directory, "configured-ffmpeg");
        string blockedListDirectory = Path.Combine(directory, "not-a-directory");
        File.WriteAllText(executable, "not executed");
        File.WriteAllText(blockedListDirectory, "keep");
        var logs = new List<string>();
        var recorder = new FfmpegVideoRecorder(
            new VideoRecorderOptions { FfmpegPath = executable }, directory, Mock.Of<IClock>(), logs.Add, null);
        typeof(FfmpegVideoRecorder).GetProperty(nameof(FfmpegVideoRecorder.SegmentDirectory))!.SetValue(recorder, blockedListDirectory);

        string? result = await recorder.ConcatAsync([new VideoSegment("segment.mp4", 0, 1)], "result.mp4", null, CancellationToken.None);

        Assert.IsNull(result);
        Assert.StartsWith("Failed to concatenate segments into 'result.mp4':", Assert.ContainsSingle(logs));
        Assert.AreEqual("keep", File.ReadAllText(blockedListDirectory));
        Assert.IsFalse(File.Exists(Path.Combine(directory, "result.mp4")));
    }

    [TestMethod]
    public void OutputDiagnostics_FilterErrorsTrimHistoryAndIgnoreEndOfStream()
    {
        string directory = TestContext.TestTempDirectory!;
        var logs = new List<string>();
        var recorder = new FfmpegVideoRecorder(
            new VideoRecorderOptions { FfmpegPath = Path.Combine(directory, "missing-ffmpeg") },
            directory, Mock.Of<IClock>(), logs.Add, null);
        Assert.AreEqual("No ffmpeg output was captured.", recorder.DescribeLastFfmpegError());

        FeedOutput(recorder, "old ERROR");
        for (int i = 1; i <= 8; i++)
        {
            FeedOutput(recorder, $"frame {i}");
        }

        Assert.AreEqual("frame 8", recorder.DescribeLastFfmpegError(), "The oldest error must be evicted from the eight-line history.");
        FeedOutput(recorder, "Permission DENIED");
        FeedOutput(recorder, "Could not open input");
        FeedOutput(recorder, "Failed to encode");
        FeedOutput(recorder, "ERROR while closing");
        FeedOutput(recorder, null);
        Assert.AreEqual("Permission DENIED | Could not open input | Failed to encode | ERROR while closing", recorder.DescribeLastFfmpegError());
        Assert.HasCount(13, logs);
        Assert.AreEqual("[ffmpeg] ERROR while closing", logs[logs.Count - 1]);

        typeof(FfmpegVideoRecorder).GetMethod("ClearRecentFfmpegOutput", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(recorder, null);
        Assert.AreEqual("No ffmpeg output was captured.", recorder.DescribeLastFfmpegError());
    }

    [TestMethod]
    public void PruneSegments_DeletesOnlyNamedFilesAndToleratesMissingEntries()
    {
        string directory = TestContext.TestTempDirectory!;
        string removed = Path.Combine(directory, "old.mp4");
        string retained = Path.Combine(directory, "current.mp4");
        File.WriteAllText(removed, "old");
        File.WriteAllText(retained, "current");

        FfmpegVideoRecorder.PruneSegments(
            [new VideoSegment(removed, 0, 1), new VideoSegment(removed, 0, 1), new VideoSegment(Path.Combine(directory, "missing.mp4"), 1, 2)]);

        Assert.IsFalse(File.Exists(removed));
        Assert.AreEqual("current", File.ReadAllText(retained));
    }

    private static FfmpegVideoRecorder CreateRecorder(VideoRecorderOptions options, string directory, Action<string>? warn = null)
    {
        options.FfmpegPath ??= Path.Combine(directory, "missing-ffmpeg");
        return new FfmpegVideoRecorder(options, directory, Mock.Of<IClock>(), null, warn);
    }

    private static string InvokeArguments(FfmpegVideoRecorder recorder, string directory, string listPath)
        => (string)typeof(FfmpegVideoRecorder).GetMethod("BuildSegmentArguments", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(recorder, [directory, listPath])!;

    private static void FeedOutput(FfmpegVideoRecorder recorder, string? line)
    {
        var args = (DataReceivedEventArgs)typeof(DataReceivedEventArgs)
            .GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, [typeof(string)], null)!.Invoke([line]);
        typeof(FfmpegVideoRecorder).GetMethod("OnFfmpegOutput", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(recorder, [recorder, args]);
    }
}
