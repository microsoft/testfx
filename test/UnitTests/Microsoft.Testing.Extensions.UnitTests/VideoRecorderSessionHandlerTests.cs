// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
public sealed class VideoRecorderSessionHandlerTests
{
    [TestMethod]
    public async Task ConsumeAsync_ExecutionCompleted_RemovesInFlightTestWithoutRecordingOutcome()
    {
        var options = new VideoRecorderOptions
        {
            FfmpegPath = Path.Combine(Path.GetTempPath(), "missing-ffmpeg"),
            OutputDirectory = Path.GetTempPath(),
        };
        var commandLineOptions = new TestCommandLineOptions(new()
        {
            [VideoRecorderCommandLineProvider.EnableOptionName] = [],
        });
        var handler = new VideoRecorderSessionHandler(
            options,
            Mock.Of<IConfiguration>(),
            commandLineOptions,
            Mock.Of<IMessageBus>(),
            Mock.Of<IOutputDevice>(),
            Mock.Of<IClock>(),
            Mock.Of<ILogger<VideoRecorderSessionHandler>>());

        await handler.ConsumeAsync(null!, CreateUpdate(InProgressTestNodeStateProperty.CachedInstance), CancellationToken.None);
        Assert.AreEqual(1, GetCollectionCount(handler, "_inFlight"));

        await handler.ConsumeAsync(null!, CreateUpdate(TestNodeExecutionCompletedProperty.CachedInstance), CancellationToken.None);

        Assert.AreEqual(0, GetCollectionCount(handler, "_inFlight"));
        Assert.AreEqual(0, GetCollectionCount(handler, "_testRecords"));
    }

    private static TestNodeUpdateMessage CreateUpdate(IProperty property)
        => new(
            new SessionUid("session"),
            new TestNode
            {
                Uid = "uid",
                DisplayName = "DroppedTest",
                Properties = new PropertyBag(property),
            });

    private static int GetCollectionCount(VideoRecorderSessionHandler handler, string fieldName)
    {
        object collection = typeof(VideoRecorderSessionHandler)
            .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(handler)!;
        return (int)collection.GetType().GetProperty("Count")!.GetValue(collection)!;
    }
}
