// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.UnitTests.Helpers;
using Microsoft.Testing.Extensions.VideoRecorder;
using Microsoft.Testing.Extensions.VideoRecorder.Resources;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.OutputDevice;

using static Microsoft.Testing.Extensions.UnitTests.Helpers.VideoRecorderBehaviorContext;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class VideoRecorderOptionBehaviorTests
{
    [TestMethod]
    public async Task Constructor_MixedCaseOverrides_ChangePersistenceGranularityAndReadyHint()
    {
        var options = new VideoRecorderOptions
        {
            PersistMode = VideoRecorderPersistenceMode.OnFailure,
            Granularity = VideoCaptureGranularity.PerTest,
            Source = VideoCaptureSource.Screen,
            IncludeChapters = true,
            MaxRetainedDuration = TimeSpan.FromSeconds(11),
            ExtraRecorderArguments = "-configured",
        };
        var context = new VideoRecorderBehaviorContext(options, new()
        {
            [VideoRecorderCommandLineProvider.EnableOptionName] = ["AlWaYs"],
            [VideoRecorderCommandLineProvider.SourceOptionName] = ["WiNdOw"],
            [VideoRecorderCommandLineProvider.GranularityOptionName] = ["SeSsIoN"],
            [VideoRecorderCommandLineProvider.ArgsOptionName] = [" -vf scale=1024:-1 "],
            [VideoRecorderCommandLineProvider.MaxDurationOptionName] = [" +42 "],
            [VideoRecorderCommandLineProvider.ChaptersOptionName] = ["OfF"],
        })
        {
            ConcatResult = ArtifactPath("configured-session.mp4"),
        };

        await context.Handler.OnTestSessionStartingAsync(context.Session.Object);
        await context.ConsumeAsync("passed", "Passed test", PassedTestNodeStateProperty.CachedInstance, Timing(1, 3));
        context.Segments = [new VideoSegment("test.mp4", 0, 4), new VideoSegment("rest-of-session.mp4", 4, 8)];
        await context.Handler.OnTestSessionFinishingAsync(context.Session.Object);

        Assert.AreEqual(VideoRecorderPersistenceMode.Always, options.PersistMode);
        Assert.AreEqual(VideoCaptureSource.Window, options.Source);
        Assert.AreEqual(VideoCaptureGranularity.PerSession, options.Granularity);
        Assert.AreEqual(" -vf scale=1024:-1 ", options.ExtraRecorderArguments, "The session handler must not trim or tokenize recorder arguments.");
        Assert.AreEqual(TimeSpan.FromSeconds(42), options.MaxRetainedDuration);
        Assert.IsFalse(options.IncludeChapters);
        FormattedTextOutputDeviceData ready = Assert.IsInstanceOfType<FormattedTextOutputDeviceData>(Assert.ContainsSingle(context.OutputMessages).Data);
        Assert.AreEqual(
            string.Format(
                CultureInfo.CurrentCulture,
                VideoRecorderResources.RecorderReady,
                "mock-ffmpeg",
                VideoCaptureGranularity.PerSession,
                VideoRecorderPersistenceMode.Always,
                VideoRecorderResources.HintPerSession),
            ready.Text);
        ConcatCall call = Assert.ContainsSingle(context.Concats);
        Assert.AreSequenceEqual(context.Segments, call.Segments, "CLI options must change actual persistence and granularity, not merely the ready message.");
        Assert.IsNull(call.MetadataPath);
        (IDataProducer producer, SessionFileArtifact artifact) = Assert.ContainsSingle(context.Published);
        Assert.AreSame(context.Handler, producer);
        Assert.AreEqual(SessionId, artifact.SessionUid);
        Assert.AreEqual(ArtifactPath("configured-session.mp4"), artifact.FileInfo.FullName);
        Assert.AreEqual(VideoRecorderResources.ArtifactSessionDisplayName, artifact.DisplayName);
        Assert.AreEqual(VideoRecorderResources.ArtifactSessionDescription, artifact.Description);
    }

    [TestMethod]
    [DataRow("0", DisplayName = "Zero duration preserves configured limit")]
    [DataRow("-1", DisplayName = "Negative duration preserves configured limit")]
    [DataRow("1.5", DisplayName = "Fractional duration preserves configured limit")]
    [DataRow("not-a-number", DisplayName = "Nonnumeric duration preserves configured limit")]
    [DataRow("2147483648", DisplayName = "Overflow duration preserves configured limit")]
    [DataRow("", DisplayName = "Empty duration preserves configured limit")]
    public void Constructor_InvalidDurationOverride_PreservesConfiguredLimit(string duration)
    {
        var options = new VideoRecorderOptions { MaxRetainedDuration = TimeSpan.FromSeconds(37) };
        var context = new VideoRecorderBehaviorContext(options, new()
        {
            [VideoRecorderCommandLineProvider.EnableOptionName] = ["always"],
            [VideoRecorderCommandLineProvider.MaxDurationOptionName] = [duration],
        });
        var validOptions = new VideoRecorderOptions { MaxRetainedDuration = TimeSpan.FromSeconds(37) };
        _ = new VideoRecorderBehaviorContext(validOptions, new()
        {
            [VideoRecorderCommandLineProvider.EnableOptionName] = [],
            [VideoRecorderCommandLineProvider.MaxDurationOptionName] = ["43"],
        });

        Assert.AreEqual(TimeSpan.FromSeconds(37), options.MaxRetainedDuration);
        Assert.AreEqual(TimeSpan.FromSeconds(43), validOptions.MaxRetainedDuration, "The positive control proves duration overrides are actually applied.");
        Assert.AreEqual(VideoRecorderPersistenceMode.Always, options.PersistMode, "An invalid duration must not suppress independent valid overrides.");
        context.Recorder.VerifyNoOtherCalls();
    }

    [TestMethod]
    [DataRow("1", 1, DisplayName = "Minimum positive duration is accepted")]
    [DataRow("2147483647", int.MaxValue, DisplayName = "Maximum integer duration is accepted")]
    public void Constructor_PositiveDurationOverride_AcceptsIntegerBoundaries(string duration, int expectedSeconds)
    {
        var options = new VideoRecorderOptions { MaxRetainedDuration = TimeSpan.FromSeconds(37) };

        _ = new VideoRecorderBehaviorContext(options, new()
        {
            [VideoRecorderCommandLineProvider.EnableOptionName] = [],
            [VideoRecorderCommandLineProvider.MaxDurationOptionName] = [duration],
        });

        Assert.AreEqual(TimeSpan.FromSeconds(expectedSeconds), options.MaxRetainedDuration);
    }
}
