// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.VideoRecorder;
using Microsoft.Testing.Platform.Helpers;

using Moq;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class VideoRecorderFactoryTests
{
    [TestMethod]
    public void IsNativeConfigurationSupported_CompatibleSessionConfiguration_ReturnsTrue()
    {
        VideoRecorderOptions options = CreateCompatibleOptions();

        Assert.IsTrue(VideoRecorderFactory.IsNativeConfigurationSupported(options));
    }

    [TestMethod]
    [DataRow("explicit-ffmpeg")]
    [DataRow("per-test")]
    [DataRow("chapters")]
    [DataRow("rolling-buffer")]
    [DataRow("window")]
    [DataRow("webm")]
    [DataRow("extra-arguments")]
    [DataRow("input-override")]
    public void IsNativeConfigurationSupported_UnsupportedRequirement_ReturnsFalse(string requirement)
    {
        VideoRecorderOptions options = CreateCompatibleOptions();
        switch (requirement)
        {
            case "explicit-ffmpeg":
                options.FfmpegPath = "ffmpeg.exe";
                break;
            case "per-test":
                options.Granularity = VideoCaptureGranularity.PerTest;
                break;
            case "chapters":
                options.IncludeChapters = true;
                break;
            case "rolling-buffer":
                options.MaxRetainedDuration = TimeSpan.FromMinutes(1);
                break;
            case "window":
                options.Source = VideoCaptureSource.Window;
                break;
            case "webm":
                options.Format = VideoRecorderFormat.WebMVp9;
                break;
            case "extra-arguments":
                options.ExtraRecorderArguments = "-b:v 2M";
                break;
            case "input-override":
                options.InputArgumentsOverride = "-f gdigrab -i desktop";
                break;
            default:
                Assert.Fail($"Unknown requirement '{requirement}'.");
                break;
        }

        Assert.IsFalse(VideoRecorderFactory.IsNativeConfigurationSupported(options));
    }

    [TestMethod]
    public void Create_DefaultPerTestConfiguration_ReturnsFfmpegRecorder()
    {
        IVideoRecorder recorder = VideoRecorderFactory.Create(
            new VideoRecorderOptions(),
            Path.GetTempPath(),
            Mock.Of<IClock>(),
            log: null,
            warn: null);

        Assert.IsInstanceOfType<FfmpegVideoRecorder>(recorder);
    }

    private static VideoRecorderOptions CreateCompatibleOptions()
        => new()
        {
            Format = VideoRecorderFormat.Mp4H264,
            Granularity = VideoCaptureGranularity.PerSession,
            Source = VideoCaptureSource.Screen,
            IncludeChapters = false,
            MaxRetainedDuration = null,
        };
}
