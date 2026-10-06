// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;

using Moq;

namespace Microsoft.Testing.Platform.MSBuild.UnitTests;

[TestClass]
public sealed class MSBuildCompatibilityHelperTests
{
    [TestMethod]
    public void SupportsMultiLine_WithCurrentMSBuildVersion_ReturnsTrue()
        => Assert.IsTrue(MSBuildCompatibilityHelper.SupportsMultiLine());

    [TestMethod]
    public void SupportsTerminalLoggerWithExtendedMessages_WithCurrentMSBuildVersion_ReturnsTrue()
        => Assert.IsTrue(MSBuildCompatibilityHelper.SupportsTerminalLoggerWithExtendedMessages());

    [TestMethod]
    public void TryWriteExtendedMessage_WhenSupported_LogsExtendedMessageAndReturnsTrue()
    {
        List<BuildMessageEventArgs> messages = [];
        Mock<IBuildEngine> engine = new();
        engine.Setup(x => x.LogMessageEvent(It.IsAny<BuildMessageEventArgs>()))
            .Callback<BuildMessageEventArgs>(messages.Add);
        Dictionary<string, string?> metadata = new() { ["source"] = "test", ["empty"] = null };

        bool written = MSBuildCompatibilityHelper.TryWriteExtendedMessage(engine.Object, "TestType", "Test message", metadata);

        Assert.IsTrue(written);
        ExtendedBuildMessageEventArgs logged = Assert.IsInstanceOfType<ExtendedBuildMessageEventArgs>(Assert.ContainsSingle(messages));
        Assert.AreEqual("TestType", logged.ExtendedType);
        Assert.AreEqual("Test message", logged.Message);
        Assert.AreEqual(MessageImportance.High, logged.Importance);
        Assert.AreSame(metadata, logged.ExtendedMetadata);
    }
}
