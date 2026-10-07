// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

using Microsoft.Build.Framework;

using Moq;

namespace Microsoft.Testing.Platform.MSBuild.UnitTests;

[TestClass]
[DoNotParallelize] // Tests temporarily replace the helper's process-wide caches.
public sealed class MSBuildCompatibilityHelperTests
{
    // The pinned MSBuild package is newer than the 17.10 feature thresholds.
    [TestMethod]
    public void SupportsMultiLine_WithCurrentMSBuildVersion_ReturnsTrue()
        => Assert.IsTrue(MSBuildCompatibilityHelper.SupportsMultiLine());

    [TestMethod]
    public void SupportsTerminalLoggerWithExtendedMessages_WithCurrentMSBuildVersion_ReturnsTrue()
        => Assert.IsTrue(MSBuildCompatibilityHelper.SupportsTerminalLoggerWithExtendedMessages());

    [TestMethod]
    [DataRow("17.9.999", false)]
    [DataRow("17.10.0", true)]
    [DataRow("17.10.1", true)]
    public void SupportsMultiLine_AtVersionBoundary_ReturnsExpectedSupport(string version, bool expectedSupport)
        => WithMSBuildVersion(version, () => Assert.AreEqual(expectedSupport, MSBuildCompatibilityHelper.SupportsMultiLine()));

    [TestMethod]
    [DataRow("17.9.999", false)]
    [DataRow("17.10.0", true)]
    [DataRow("17.10.1", true)]
    public void SupportsTerminalLoggerWithExtendedMessages_AtVersionBoundary_ReturnsExpectedSupport(string version, bool expectedSupport)
        => WithMSBuildVersion(version, () => Assert.AreEqual(expectedSupport, MSBuildCompatibilityHelper.SupportsTerminalLoggerWithExtendedMessages()));

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

    [TestMethod]
    public void TryWriteExtendedMessage_WhenUnsupported_DoesNotLogAndReturnsFalse()
        => WithMSBuildVersion("17.9.999", () =>
        {
            Mock<IBuildEngine> engine = new();
            Dictionary<string, string?> metadata = new() { ["source"] = "test" };

            bool written = MSBuildCompatibilityHelper.TryWriteExtendedMessage(engine.Object, "TestType", "Test message", metadata);

            Assert.IsFalse(written);
            engine.Verify(x => x.LogMessageEvent(It.IsAny<BuildMessageEventArgs>()), Times.Never);
        });

    private static void WithMSBuildVersion(string version, Action action)
    {
        FieldInfo versionField = GetCacheField("s_msBuildVersion");
        FieldInfo multilineField = GetCacheField("s_supportsMultiline");
        FieldInfo extendedMessagesField = GetCacheField("s_supportsTerminalLoggerWithExtendedMessages");
        object? originalVersion = versionField.GetValue(null);
        object? originalMultiline = multilineField.GetValue(null);
        object? originalExtendedMessages = extendedMessagesField.GetValue(null);

        try
        {
            versionField.SetValue(null, new Version(version));
            multilineField.SetValue(null, null);
            extendedMessagesField.SetValue(null, null);
            action();
        }
        finally
        {
            versionField.SetValue(null, originalVersion);
            multilineField.SetValue(null, originalMultiline);
            extendedMessagesField.SetValue(null, originalExtendedMessages);
        }
    }

    private static FieldInfo GetCacheField(string name)
    {
        FieldInfo? field = typeof(MSBuildCompatibilityHelper).GetField(name, BindingFlags.Static | BindingFlags.NonPublic);
        Assert.IsNotNull(field, $"Expected MSBuildCompatibilityHelper cache field '{name}'.");

        return field;
    }
}
