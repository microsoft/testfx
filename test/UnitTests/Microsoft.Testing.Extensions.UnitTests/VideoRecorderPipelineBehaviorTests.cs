// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.UnitTests.Helpers;
using Microsoft.Testing.Extensions.VideoRecorder;
using Microsoft.Testing.Extensions.VideoRecorder.Resources;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Logging;

using Moq;

using static Microsoft.Testing.Extensions.UnitTests.Helpers.VideoRecorderBehaviorContext;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class VideoRecorderPipelineBehaviorTests
{
    [TestMethod]
    public async Task Finish_NullClipResult_ContinuesToNextTestWithoutPublishingMissingClip()
    {
        var first = new VideoSegment("first.mp4", 0, 4);
        var second = new VideoSegment("second.mp4", 4, 8);
        var context = new VideoRecorderBehaviorContext { Segments = [first, second] };
        string outputPath = ArtifactPath("second-only.mp4");
        context.Recorder.Setup(recorder => recorder.ConcatAsync(
            It.IsAny<IReadOnlyList<VideoSegment>>(), It.IsAny<string>(), null, CancellationToken.None))
            .Callback<IReadOnlyList<VideoSegment>, string, string?, CancellationToken>((segments, fileName, metadata, token) =>
                context.Concats.Add(new ConcatCall([.. segments], fileName, metadata, token)))
            .ReturnsAsync(() => context.Concats.Count == 1 ? null : outputPath);
        await context.Handler.OnTestSessionStartingAsync(context.Session.Object);
        await context.ConsumeAsync("first", "Missing clip", PassedTestNodeStateProperty.CachedInstance, Timing(0, 3));
        await context.ConsumeAsync("second", "Kept clip", PassedTestNodeStateProperty.CachedInstance, Timing(4, 7));

        await context.Handler.OnTestSessionFinishingAsync(context.Session.Object);

        Assert.HasCount(2, context.Concats);
        Assert.AreSequenceEqual([first], context.Concats[0].Segments);
        Assert.AreSequenceEqual([second], context.Concats[1].Segments);
        (IDataProducer producer, SessionFileArtifact artifact) = Assert.ContainsSingle(context.Published);
        Assert.AreSame(context.Handler, producer);
        Assert.AreEqual(SessionId, artifact.SessionUid);
        Assert.AreEqual(outputPath, artifact.FileInfo.FullName);
        Assert.AreEqual(
            string.Format(CultureInfo.CurrentCulture, VideoRecorderResources.ArtifactPerTestDisplayName, "Kept clip"),
            artifact.DisplayName);
        Assert.AreEqual(
            string.Format(CultureInfo.CurrentCulture, VideoRecorderResources.ArtifactPerTestDescription, "Kept clip"),
            artifact.Description);
    }

    [TestMethod]
    [DataRow("failed", true)]
    [DataRow("error", true)]
    [DataRow("timeout", true)]
    [DataRow("passed", false)]
    [DataRow("skipped", false)]
    public async Task Consume_FinalOutcome_DrivesFailureOnlySessionPersistence(string outcome, bool keep)
    {
        var context = new VideoRecorderBehaviorContext(new VideoRecorderOptions
        {
            Granularity = VideoCaptureGranularity.PerSession,
            PersistMode = VideoRecorderPersistenceMode.OnFailure,
            IncludeChapters = false,
        })
        {
            ConcatResult = ArtifactPath("outcome-session.mp4"),
        };
        await context.Handler.OnTestSessionStartingAsync(context.Session.Object);
        // Pruning sees an empty finalized snapshot during consumption; no real segment files are involved.
        await context.ConsumeAsync("test", "Outcome test", State(outcome), Timing(1, 3));
        context.Segments = [new VideoSegment("first.mp4", 0, 4), new VideoSegment("outside-test.mp4", 4, 8)];

        await context.Handler.OnTestSessionFinishingAsync(context.Session.Object);

        Assert.HasCount(keep ? 1 : 0, context.Concats);
        if (keep)
        {
            Assert.AreSequenceEqual(context.Segments, context.Concats[0].Segments, "Per-session persistence keeps the whole recording, not only the failed test's overlap.");
            Assert.IsNull(context.Concats[0].MetadataPath);
            AssertSessionArtifact(context, ArtifactPath("outcome-session.mp4"));
        }
        else
        {
            Assert.IsEmpty(context.Published);
        }
    }

    [TestMethod]
    [DataRow("passed")]
    [DataRow("skipped")]
    public async Task Consume_SupersededFailures_DoNotKeepSuccessfulSession(string finalOutcome)
    {
        var context = new VideoRecorderBehaviorContext(new VideoRecorderOptions
        {
            Granularity = VideoCaptureGranularity.PerSession,
            PersistMode = VideoRecorderPersistenceMode.OnFailure,
            IncludeChapters = false,
        })
        {
            ConcatResult = ArtifactPath("must-not-exist.mp4"),
        };
        await context.Handler.OnTestSessionStartingAsync(context.Session.Object);
        await context.ConsumeAsync("retry", "Retry test", InProgressTestNodeStateProperty.CachedInstance);
        TestNodeStateProperty[] attempts = [new FailedTestNodeStateProperty("retry"), new ErrorTestNodeStateProperty("retry"), new TimeoutTestNodeStateProperty("retry")];
        for (int i = 0; i < attempts.Length; i++)
        {
            await context.ConsumeAsync("retry", "Retry test", attempts[i], Timing(i, i + 1), new RetryAttemptProperty(i + 1, isSuperseded: true));
        }

        await context.ConsumeAsync("retry", "Retry test", State(finalOutcome), Timing(3, 7), new RetryAttemptProperty(4, isSuperseded: false));
        context.Segments = [new VideoSegment("first.mp4", 0, 4), new VideoSegment("second.mp4", 4, 8)];

        await context.Handler.OnTestSessionFinishingAsync(context.Session.Object);

        Assert.IsEmpty(context.Concats, "Superseded failures must not mark the session as failed.");
        Assert.IsEmpty(context.Published);
        Assert.HasCount(1, context.OutputMessages, "A successful recording with discarded retry failures must not emit an empty-recording warning.");
    }

    [TestMethod]
    public async Task Consume_FinalRetryFailure_KeepsFootageFromFirstObservedAttempt()
    {
        var context = new VideoRecorderBehaviorContext(new VideoRecorderOptions
        {
            PersistMode = VideoRecorderPersistenceMode.OnFailure,
            IncludeChapters = false,
        })
        {
            ConcatResult = ArtifactPath("final-failure.mp4"),
        };
        await context.Handler.OnTestSessionStartingAsync(context.Session.Object);
        context.Now = Epoch.AddSeconds(1);
        await context.ConsumeAsync("retry", "Retried failure", InProgressTestNodeStateProperty.CachedInstance);
        context.Now = Epoch.AddSeconds(5);
        await context.ConsumeAsync("retry", "Retried failure", InProgressTestNodeStateProperty.CachedInstance);
        await context.ConsumeAsync("retry", "Retried failure", new ErrorTestNodeStateProperty("retry"), new RetryAttemptProperty(1, isSuperseded: true));
        context.Now = Epoch.AddSeconds(7);
        await context.ConsumeAsync("retry", "Retried failure", new FailedTestNodeStateProperty("final"), new RetryAttemptProperty(2, isSuperseded: false));
        context.Segments = [new VideoSegment("first-attempt.mp4", 0, 4), new VideoSegment("final-attempt.mp4", 4, 8)];

        await context.Handler.OnTestSessionFinishingAsync(context.Session.Object);

        ConcatCall call = Assert.ContainsSingle(context.Concats);
        Assert.AreSequenceEqual(context.Segments, call.Segments);
        Assert.HasCount(1, context.Published);
        Assert.AreEqual(ArtifactPath("final-failure.mp4"), context.Published[0].Artifact.FileInfo.FullName);
    }

    [TestMethod]
    public async Task Consume_CanceledTerminal_DoesNotRemoveObservedStartOrAddRecord()
    {
        var context = new VideoRecorderBehaviorContext { ConcatResult = ArtifactPath("uncanceled-outcome.mp4") };
        await context.Handler.OnTestSessionStartingAsync(context.Session.Object);
        context.Now = Epoch.AddSeconds(1);
        await context.ConsumeAsync("test", "Preserved start", InProgressTestNodeStateProperty.CachedInstance);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        context.Now = Epoch.AddSeconds(5);

        OperationCanceledException exception = await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => context.Handler.ConsumeAsync(
            null!, Update("test", "Canceled failure", new FailedTestNodeStateProperty("ignored")), cancellation.Token));
        context.Now = Epoch.AddSeconds(7);
        await context.ConsumeAsync("test", "Preserved start", PassedTestNodeStateProperty.CachedInstance);
        context.Segments = [new VideoSegment("before-cancellation.mp4", 0, 4), new VideoSegment("after-cancellation.mp4", 4, 8)];
        await context.Handler.OnTestSessionFinishingAsync(context.Session.Object);

        Assert.AreEqual(cancellation.Token, exception.CancellationToken);
        ConcatCall call = Assert.ContainsSingle(context.Concats);
        Assert.AreSequenceEqual(context.Segments, call.Segments);
        Assert.HasCount(1, context.Published, "A rejected terminal message must not leave a phantom record.");
    }

    [TestMethod]
    public async Task Consume_InterleavedSameNameTests_KeepIndependentObservedTimelines()
    {
        var context = new VideoRecorderBehaviorContext();
        string[] outputPaths = [ArtifactPath("alpha.mp4"), ArtifactPath("beta.mp4")];
        context.Recorder.Setup(recorder => recorder.ConcatAsync(
            It.IsAny<IReadOnlyList<VideoSegment>>(), It.IsAny<string>(), null, CancellationToken.None))
            .Callback<IReadOnlyList<VideoSegment>, string, string?, CancellationToken>((segments, fileName, metadata, token) =>
                context.Concats.Add(new ConcatCall([.. segments], fileName, metadata, token)))
            .ReturnsAsync(() => outputPaths[context.Concats.Count - 1]);
        await context.Handler.OnTestSessionStartingAsync(context.Session.Object);
        context.Now = Epoch.AddSeconds(1);
        await context.ConsumeAsync("alpha", "Shared display", InProgressTestNodeStateProperty.CachedInstance);
        context.Now = Epoch.AddSeconds(5);
        await context.ConsumeAsync("beta", "Shared display", InProgressTestNodeStateProperty.CachedInstance);
        context.Now = Epoch.AddSeconds(6);
        await context.ConsumeAsync("alpha", "Shared display", InProgressTestNodeStateProperty.CachedInstance);
        context.Now = Epoch.AddSeconds(7);
        await context.ConsumeAsync("alpha", "Shared display", PassedTestNodeStateProperty.CachedInstance);
        context.Now = Epoch.AddSeconds(11);
        await context.ConsumeAsync("beta", "Shared display", PassedTestNodeStateProperty.CachedInstance);
        var first = new VideoSegment("early.mp4", 0, 4);
        var middle = new VideoSegment("middle.mp4", 4, 8);
        var last = new VideoSegment("late.mp4", 8, 12);
        context.Segments = [first, middle, last];

        await context.Handler.OnTestSessionFinishingAsync(context.Session.Object);

        Assert.HasCount(2, context.Concats);
        Assert.AreSequenceEqual([first, middle], context.Concats[0].Segments);
        Assert.AreSequenceEqual([middle, last], context.Concats[1].Segments);
        Assert.AreSequenceEqual(outputPaths, context.Published.Select(published => published.Artifact.FileInfo.FullName));
        foreach ((IDataProducer producer, SessionFileArtifact artifact) in context.Published)
        {
            Assert.AreSame(context.Handler, producer);
            Assert.AreEqual(SessionId, artifact.SessionUid);
        }
    }

    [TestMethod]
    public async Task Consume_ExecutionCompletedWithFailure_RemovesStartWithoutRecordingOutcome()
    {
        var context = new VideoRecorderBehaviorContext { ConcatResult = ArtifactPath("after-completion.mp4") };
        await context.Handler.OnTestSessionStartingAsync(context.Session.Object);
        context.Now = Epoch.AddSeconds(1);
        await context.ConsumeAsync("test", "Completed node", InProgressTestNodeStateProperty.CachedInstance);
        context.Now = Epoch.AddSeconds(3);
        await context.ConsumeAsync(
            "test", "Completed node", TestNodeExecutionCompletedProperty.CachedInstance, new FailedTestNodeStateProperty("must be ignored"));
        context.Now = Epoch.AddSeconds(9);
        await context.ConsumeAsync("test", "Later outcome", PassedTestNodeStateProperty.CachedInstance);
        var retained = new VideoSegment("later.mp4", 8, 12);
        context.Segments = [new VideoSegment("original-start.mp4", 0, 4), new VideoSegment("middle.mp4", 4, 8), retained];

        await context.Handler.OnTestSessionFinishingAsync(context.Session.Object);

        ConcatCall call = Assert.ContainsSingle(context.Concats);
        Assert.AreSequenceEqual([retained], call.Segments);
        Assert.HasCount(1, context.Published);
    }

    [TestMethod]
    [DataRow("unrelated-data")]
    [DataRow("missing-state")]
    [DataRow("discovered")]
    public async Task Consume_NonOutcomeUpdates_PreserveObservedStartWithoutAddingRecord(string updateKind)
    {
        var context = new VideoRecorderBehaviorContext { ConcatResult = ArtifactPath("observed-start.mp4") };
        await context.Handler.OnTestSessionStartingAsync(context.Session.Object);
        context.Now = Epoch.AddSeconds(1);
        await context.ConsumeAsync("test", "Observed start", InProgressTestNodeStateProperty.CachedInstance);
        context.Now = Epoch.AddSeconds(5);
        if (updateKind == "unrelated-data")
        {
            await context.Handler.ConsumeAsync(null!, Mock.Of<IData>(), CancellationToken.None);
        }
        else if (updateKind == "missing-state")
        {
            await context.ConsumeAsync("test", "Observed start");
        }
        else
        {
            await context.ConsumeAsync("test", "Observed start", DiscoveredTestNodeStateProperty.CachedInstance);
        }

        context.Now = Epoch.AddSeconds(7);
        await context.ConsumeAsync("test", "Observed start", PassedTestNodeStateProperty.CachedInstance);
        context.Segments = [new VideoSegment("original-start.mp4", 0, 4), new VideoSegment("end.mp4", 4, 8)];

        await context.Handler.OnTestSessionFinishingAsync(context.Session.Object);

        ConcatCall call = Assert.ContainsSingle(context.Concats);
        Assert.AreSequenceEqual(context.Segments, call.Segments);
        Assert.HasCount(1, context.Published);
    }

    [TestMethod]
    public async Task Consume_TerminalWithoutStartOrTiming_UsesCurrentClockForBothBounds()
    {
        var context = new VideoRecorderBehaviorContext { ConcatResult = ArtifactPath("instant.mp4") };
        await context.Handler.OnTestSessionStartingAsync(context.Session.Object);
        context.Now = Epoch.AddSeconds(6);
        await context.ConsumeAsync("test", "Unobserved test", PassedTestNodeStateProperty.CachedInstance);
        var containing = new VideoSegment("contains-now.mp4", 4, 8);
        context.Segments = [new VideoSegment("before-now.mp4", 0, 4), containing, new VideoSegment("after-now.mp4", 8, 12)];

        await context.Handler.OnTestSessionFinishingAsync(context.Session.Object);

        Assert.AreSequenceEqual([containing], Assert.ContainsSingle(context.Concats).Segments);
        Assert.HasCount(1, context.Published);
    }

    [TestMethod]
    [DataRow(4d, 8d, 2, DisplayName = "Strict segment boundaries")]
    [DataRow(-2d, 2d, 1, DisplayName = "Start before recording is clamped")]
    [DataRow(6d, 2d, 2, DisplayName = "Reversed timing clamps end to start")]
    [DataRow(4d, 4d, -1, DisplayName = "Instant at boundary has no overlap")]
    [DataRow(6d, 6d, 2, DisplayName = "Instant inside segment retains containing footage")]
    [DataRow(-4d, -1d, -1, DisplayName = "Wholly before recording has no overlap")]
    public async Task Finish_TimingPartitions_SelectOnlyStrictlyOverlappingSegments(double start, double end, int expectedIndex)
    {
        var context = new VideoRecorderBehaviorContext
        {
            Segments =
            [
                new VideoSegment("before-recording.mp4", -4, 0),
                new VideoSegment("first.mp4", 0, 4),
                new VideoSegment("middle.mp4", 4, 8),
                new VideoSegment("last.mp4", 8, 12),
            ],
            ConcatResult = ArtifactPath("timed.mp4"),
        };
        await context.Handler.OnTestSessionStartingAsync(context.Session.Object);
        await context.ConsumeAsync("test", "Timing partition", PassedTestNodeStateProperty.CachedInstance, Timing(start, end));

        await context.Handler.OnTestSessionFinishingAsync(context.Session.Object);

        if (expectedIndex < 0)
        {
            Assert.IsEmpty(context.Concats);
            Assert.IsEmpty(context.Published);
            (LogLevel level, string text) = Assert.ContainsSingle(context.Logs);
            Assert.AreEqual(LogLevel.Trace, level);
            Assert.AreEqual("No segments survived for test 'Timing partition' (pruned by the rolling buffer); skipping its clip.", text);
        }
        else
        {
            Assert.AreSequenceEqual([context.Segments[expectedIndex]], Assert.ContainsSingle(context.Concats).Segments);
            Assert.HasCount(1, context.Published);
            Assert.IsEmpty(context.Logs);
        }
    }

    [TestMethod]
    public async Task Consume_RollingCap_WarnsOnceOnlyAfterRunningTestReleasesFootage()
    {
        var context = new VideoRecorderBehaviorContext(new VideoRecorderOptions
        {
            PersistMode = VideoRecorderPersistenceMode.Always,
            MaxRetainedDuration = TimeSpan.FromSeconds(30),
        })
        {
            // File.Exists rejects the NUL path, so exercising pruning cannot delete a real file.
            Segments = [new VideoSegment("\0rolling-segment.mp4", 50, 70)],
        };
        await context.Handler.OnTestSessionStartingAsync(context.Session.Object);
        context.Now = Epoch.AddSeconds(60);
        await context.ConsumeAsync("running", "Running test", InProgressTestNodeStateProperty.CachedInstance);
        context.Now = Epoch.AddSeconds(100);

        await context.ConsumeAsync("other", "Other test", PassedTestNodeStateProperty.CachedInstance, Timing(90, 100));
        Assert.IsEmpty(context.Logs, "A running test still needs the old segment; the cap must not drop it or warn yet.");

        await context.ConsumeAsync("running", "Running test", TestNodeExecutionCompletedProperty.CachedInstance);
        await context.ConsumeAsync("running", "Running test", TestNodeExecutionCompletedProperty.CachedInstance);

        (LogLevel level, string text) = Assert.ContainsSingle(context.Logs);
        Assert.AreEqual(LogLevel.Warning, level);
        string cap = string.Format(CultureInfo.CurrentCulture, VideoRecorderResources.CapDurationDescription, TimeSpan.FromSeconds(30));
        Assert.AreEqual(string.Format(CultureInfo.CurrentCulture, VideoRecorderResources.RecordingCapReached, cap), text);
        context.Recorder.Verify(recorder => recorder.ReadSegments(), Times.Exactly(3));
        Assert.IsEmpty(context.Concats);
        Assert.IsEmpty(context.Published);
    }

    [TestMethod]
    public async Task Finish_AlwaysSessionWithoutResults_PublishesWholeRecordingWithoutChapters()
    {
        VideoRecorderBehaviorContext context = CreateChapterContext();
        await context.Handler.OnTestSessionStartingAsync(context.Session.Object);

        await context.Handler.OnTestSessionFinishingAsync(context.Session.Object);

        ConcatCall call = Assert.ContainsSingle(context.Concats);
        Assert.AreSequenceEqual(context.Segments, call.Segments);
        Assert.IsNull(call.MetadataPath);
        context.Recorder.VerifyGet(recorder => recorder.SegmentDirectory, Times.Once(), "Only cleanup should inspect the directory when there are no chapter records.");
        AssertSessionArtifact(context, ArtifactPath("session.mp4"));
    }

    [TestMethod]
    public async Task Finish_ChaptersWithoutSegmentDirectory_StillPublishesSession()
    {
        VideoRecorderBehaviorContext context = CreateChapterContext();
        await context.Handler.OnTestSessionStartingAsync(context.Session.Object);
        await context.ConsumeAsync("test", "Chapter test", PassedTestNodeStateProperty.CachedInstance, Timing(1, 7));

        await context.Handler.OnTestSessionFinishingAsync(context.Session.Object);

        ConcatCall call = Assert.ContainsSingle(context.Concats);
        Assert.AreSequenceEqual(context.Segments, call.Segments);
        Assert.IsNull(call.MetadataPath);
        Assert.IsEmpty(context.Logs);
        context.Recorder.VerifyGet(recorder => recorder.SegmentDirectory, Times.Exactly(2));
        AssertSessionArtifact(context, ArtifactPath("session.mp4"));
    }

    [TestMethod]
    public async Task Finish_ChapterRecordsOutsideSurvivingVideo_DoNotCreateMetadata()
    {
        VideoRecorderBehaviorContext context = CreateChapterContext();
        context.SegmentDirectory = "\0segments";
        context.Segments = [new VideoSegment("surviving.mp4", 10, 20)];
        await context.Handler.OnTestSessionStartingAsync(context.Session.Object);
        await context.ConsumeAsync("before", "Before surviving video", PassedTestNodeStateProperty.CachedInstance, Timing(-4, -1));
        await context.ConsumeAsync("after", "After surviving video", PassedTestNodeStateProperty.CachedInstance, Timing(20, 25));

        await context.Handler.OnTestSessionFinishingAsync(context.Session.Object);

        ConcatCall call = Assert.ContainsSingle(context.Concats);
        Assert.AreSequenceEqual(context.Segments, call.Segments);
        Assert.IsNull(call.MetadataPath);
        Assert.IsEmpty(context.Logs, "Out-of-range records must be filtered before attempting a metadata write to the invalid sentinel directory.");
        AssertSessionArtifact(context, ArtifactPath("session.mp4"));
    }

    [TestMethod]
    public async Task Finish_ChapterWriteFailure_LogsAndPublishesVideoWithoutMetadata()
    {
        VideoRecorderBehaviorContext context = CreateChapterContext();
        context.SegmentDirectory = "\0segments";
        await context.Handler.OnTestSessionStartingAsync(context.Session.Object);
        await context.ConsumeAsync("test", "Chapter test", PassedTestNodeStateProperty.CachedInstance, Timing(1, 7));

        await context.Handler.OnTestSessionFinishingAsync(context.Session.Object);

        (LogLevel level, string text) = Assert.ContainsSingle(context.Logs);
        Assert.AreEqual(LogLevel.Trace, level);
        const string Prefix = "Failed to write chapter metadata: ";
        Assert.StartsWith(Prefix, text);
        Assert.IsGreaterThan(Prefix.Length, text.Length, "The best-effort diagnostic must retain the filesystem validation error.");
        ConcatCall call = Assert.ContainsSingle(context.Concats);
        Assert.AreSequenceEqual(context.Segments, call.Segments);
        Assert.IsNull(call.MetadataPath);
        AssertSessionArtifact(context, ArtifactPath("session.mp4"));
    }

    [TestMethod]
    public async Task Finish_NullSessionConcatResult_DoesNotPublishArtifact()
    {
        var context = new VideoRecorderBehaviorContext(new VideoRecorderOptions
        {
            Granularity = VideoCaptureGranularity.PerSession,
            PersistMode = VideoRecorderPersistenceMode.Always,
            IncludeChapters = false,
        })
        {
            Segments = [new VideoSegment("first.mp4", 0, 4), new VideoSegment("second.mp4", 4, 8)],
        };
        await context.Handler.OnTestSessionStartingAsync(context.Session.Object);

        await context.Handler.OnTestSessionFinishingAsync(context.Session.Object);

        Assert.AreSequenceEqual(context.Segments, Assert.ContainsSingle(context.Concats).Segments);
        Assert.IsEmpty(context.Published);
        context.Recorder.VerifyGet(recorder => recorder.SegmentDirectory, Times.Once());
    }

    [TestMethod]
    [DataRow(null, DisplayName = "Null test display name")]
    [DataRow("", DisplayName = "Empty test display name")]
    [DataRow(" \t ", DisplayName = "Whitespace test display name")]
    public async Task Finish_UnnamedTests_UseTimestampedFallbackFileName(string? displayName)
    {
        var context = new VideoRecorderBehaviorContext
        {
            SegmentExtension = "webm",
            Segments = [new VideoSegment("first.webm", 0, 4), new VideoSegment("second.webm", 4, 8)],
        };
        await context.Handler.OnTestSessionStartingAsync(context.Session.Object);
        await context.ConsumeAsync("test", displayName, PassedTestNodeStateProperty.CachedInstance, Timing(1, 7));

        await context.Handler.OnTestSessionFinishingAsync(context.Session.Object);

        ConcatCall call = Assert.ContainsSingle(context.Concats);
        Assert.AreSequenceEqual(context.Segments, call.Segments);
        AssertFileName(call.FileName, "recording_20261012_131415_123_", ".webm");
        Assert.IsEmpty(context.Published);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task Finish_WindowsTestName_SanitizesWindowsSeparatorsAndPreservesArtifactIdentity()
        => await AssertNamedClipAsync("Suite_Case_variant_child_quoted_end");

    [TestMethod]
    [OSCondition(OperatingSystems.Linux)]
    public async Task Finish_LinuxTestName_PreservesUnixLegalCharactersAndArtifactIdentity()
        => await AssertNamedClipAsync("Suite:Case\\variant_child\"quoted_end");

    private static VideoRecorderBehaviorContext CreateChapterContext()
        => new(new VideoRecorderOptions
        {
            Granularity = VideoCaptureGranularity.PerSession,
            PersistMode = VideoRecorderPersistenceMode.Always,
            IncludeChapters = true,
        })
        {
            Segments = [new VideoSegment("first.mp4", 0, 4), new VideoSegment("second.mp4", 4, 8)],
            ConcatResult = ArtifactPath("session.mp4"),
        };

    private static async Task AssertNamedClipAsync(string sanitizedName)
    {
        const string OriginalName = "Suite:Case\\variant/child\"quoted\0end";
        var context = new VideoRecorderBehaviorContext
        {
            SegmentExtension = "webm",
            Segments = [new VideoSegment("first.webm", 0, 4), new VideoSegment("second.webm", 4, 8)],
            ConcatResult = ArtifactPath("named.webm"),
        };
        await context.Handler.OnTestSessionStartingAsync(context.Session.Object);
        await context.ConsumeAsync("test", OriginalName, PassedTestNodeStateProperty.CachedInstance, Timing(1, 7));

        await context.Handler.OnTestSessionFinishingAsync(context.Session.Object);

        ConcatCall call = Assert.ContainsSingle(context.Concats);
        Assert.AreSequenceEqual(context.Segments, call.Segments);
        AssertFileName(call.FileName, sanitizedName + "_20261012_131415_123_", ".webm");
        (IDataProducer producer, SessionFileArtifact artifact) = Assert.ContainsSingle(context.Published);
        Assert.AreSame(context.Handler, producer);
        Assert.AreEqual(SessionId, artifact.SessionUid);
        Assert.AreEqual(ArtifactPath("named.webm"), artifact.FileInfo.FullName);
        Assert.AreEqual(
            string.Format(CultureInfo.CurrentCulture, VideoRecorderResources.ArtifactPerTestDisplayName, OriginalName),
            artifact.DisplayName);
        Assert.AreEqual(
            string.Format(CultureInfo.CurrentCulture, VideoRecorderResources.ArtifactPerTestDescription, OriginalName),
            artifact.Description);
    }

    private static void AssertFileName(string fileName, string prefix, string extension)
    {
        Assert.StartsWith(prefix, fileName);
        Assert.EndsWith(extension, fileName);
        Assert.AreEqual(prefix.Length + 8 + extension.Length, fileName.Length);
        Assert.IsTrue(fileName.Substring(prefix.Length, 8).All(Uri.IsHexDigit), "The suffix must be the eight hexadecimal characters used for clip identity.");
    }

    private static void AssertSessionArtifact(VideoRecorderBehaviorContext context, string outputPath)
    {
        (IDataProducer producer, SessionFileArtifact artifact) = Assert.ContainsSingle(context.Published);
        Assert.AreSame(context.Handler, producer);
        Assert.AreEqual(SessionId, artifact.SessionUid);
        Assert.AreEqual(outputPath, artifact.FileInfo.FullName);
        Assert.AreEqual(VideoRecorderResources.ArtifactSessionDisplayName, artifact.DisplayName);
        Assert.AreEqual(VideoRecorderResources.ArtifactSessionDescription, artifact.Description);
    }

    private static TestNodeStateProperty State(string outcome)
        => outcome switch
        {
            "failed" => new FailedTestNodeStateProperty("failed"),
            "error" => new ErrorTestNodeStateProperty("error"),
            "timeout" => new TimeoutTestNodeStateProperty("timeout"),
            "passed" => PassedTestNodeStateProperty.CachedInstance,
            "skipped" => SkippedTestNodeStateProperty.CachedInstance,
            _ => throw new ArgumentOutOfRangeException(nameof(outcome)),
        };
}
