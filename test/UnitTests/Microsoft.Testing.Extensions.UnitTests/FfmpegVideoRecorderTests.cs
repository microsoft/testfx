// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Reflection;

using Microsoft.Testing.Extensions.VideoRecorder;
using Microsoft.Testing.Platform.Helpers;

using Moq;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class FfmpegVideoRecorderTests
{
    private static readonly FieldInfo SegmentListPathField =
        typeof(FfmpegVideoRecorder).GetField("_segmentListPath", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Could not resolve FfmpegVideoRecorder._segmentListPath.");

    private static readonly PropertyInfo SegmentDirectoryProperty =
        typeof(FfmpegVideoRecorder).GetProperty(nameof(FfmpegVideoRecorder.SegmentDirectory))
        ?? throw new InvalidOperationException("Could not resolve FfmpegVideoRecorder.SegmentDirectory.");

    [TestMethod]
    public void ReadSegments_DefaultState_ReturnsEmpty()
    {
        FfmpegVideoRecorder recorder = CreateRecorder(Path.GetTempPath());

        IReadOnlyList<VideoSegment> segments = recorder.ReadSegments();

        Assert.IsEmpty(segments);
    }

    [TestMethod]
    public void ReadSegments_MissingListFile_ReturnsEmpty()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            FfmpegVideoRecorder recorder = CreateInitializedRecorder(directory, Path.Combine(directory, "missing.csv"));

            IReadOnlyList<VideoSegment> segments = recorder.ReadSegments();

            Assert.IsEmpty(segments);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    [DoNotParallelize]
    public void ReadSegments_RelativeEntry_CombinesDirectoryAndParsesInvariantNumbers()
    {
        string directory = CreateTemporaryDirectory();
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        try
        {
            string segmentPath = Path.Combine(directory, "relative.mp4");
            string listPath = Path.Combine(directory, "segments.csv");
            File.WriteAllText(segmentPath, "x");
            File.WriteAllText(listPath, "relative.mp4,1.25,2.5");
            FfmpegVideoRecorder recorder = CreateInitializedRecorder(directory, listPath);
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");

            IReadOnlyList<VideoSegment> segments = recorder.ReadSegments();

            Assert.HasCount(1, segments);
            Assert.AreEqual(segmentPath, segments[0].Path);
            Assert.AreEqual(1.25, segments[0].StartSeconds);
            Assert.AreEqual(2.5, segments[0].EndSeconds);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void ReadSegments_RootedEntry_PreservesAbsolutePath()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            string segmentPath = Path.Combine(directory, "rooted.mp4");
            string listPath = Path.Combine(directory, "segments.csv");
            File.WriteAllText(segmentPath, "x");
            File.WriteAllText(listPath, $"{segmentPath},3,4");
            FfmpegVideoRecorder recorder = CreateInitializedRecorder(directory, listPath);

            IReadOnlyList<VideoSegment> segments = recorder.ReadSegments();

            Assert.HasCount(1, segments);
            Assert.AreEqual(segmentPath, segments[0].Path);
            Assert.AreEqual(3, segments[0].StartSeconds);
            Assert.AreEqual(4, segments[0].EndSeconds);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void ReadSegments_MixedValidAndMalformedRows_ReturnsValidRowsInListOrder()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            string firstSegmentPath = Path.Combine(directory, "later.mp4");
            string secondSegmentPath = Path.Combine(directory, "earlier.mp4");
            string listPath = Path.Combine(directory, "segments.csv");
            File.WriteAllText(firstSegmentPath, "x");
            File.WriteAllText(secondSegmentPath, "x");
            File.WriteAllLines(
                listPath,
                [
                    "later.mp4,8,9",
                    "too-few-columns.mp4,1",
                    "invalid-start.mp4,not-a-number,3",
                    "invalid-end.mp4,3,not-a-number",
                    "earlier.mp4,1,2",
                ]);
            FfmpegVideoRecorder recorder = CreateInitializedRecorder(directory, listPath);

            IReadOnlyList<VideoSegment> segments = recorder.ReadSegments();

            Assert.HasCount(2, segments);
            Assert.AreEqual(firstSegmentPath, segments[0].Path);
            Assert.AreEqual(8, segments[0].StartSeconds);
            Assert.AreEqual(9, segments[0].EndSeconds);
            Assert.AreEqual(secondSegmentPath, segments[1].Path);
            Assert.AreEqual(1, segments[1].StartSeconds);
            Assert.AreEqual(2, segments[1].EndSeconds);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void ReadSegments_MissingSegmentFile_OmitsEntry()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            string listPath = Path.Combine(directory, "segments.csv");
            File.WriteAllText(listPath, "missing.mp4,1,2");
            FfmpegVideoRecorder recorder = CreateInitializedRecorder(directory, listPath);

            IReadOnlyList<VideoSegment> segments = recorder.ReadSegments();

            Assert.IsEmpty(segments);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void ReadSegments_EmptySegmentFile_OmitsEntry()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            string segmentPath = Path.Combine(directory, "empty.mp4");
            string listPath = Path.Combine(directory, "segments.csv");
            File.WriteAllBytes(segmentPath, []);
            File.WriteAllText(listPath, "empty.mp4,1,2");
            FfmpegVideoRecorder recorder = CreateInitializedRecorder(directory, listPath);

            IReadOnlyList<VideoSegment> segments = recorder.ReadSegments();

            Assert.IsEmpty(segments);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static FfmpegVideoRecorder CreateInitializedRecorder(string directory, string segmentListPath)
    {
        FfmpegVideoRecorder recorder = CreateRecorder(directory);
        SegmentDirectoryProperty.SetValue(recorder, directory);
        SegmentListPathField.SetValue(recorder, segmentListPath);
        return recorder;
    }

    private static FfmpegVideoRecorder CreateRecorder(string outputDirectory)
        => new(
            new VideoRecorderOptions
            {
                FfmpegPath = Path.Combine(outputDirectory, "missing-ffmpeg"),
                OutputDirectory = outputDirectory,
            },
            outputDirectory,
            Mock.Of<IClock>(),
            log: null,
            warn: null);

    private static string CreateTemporaryDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"{nameof(FfmpegVideoRecorderTests)}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }
}
