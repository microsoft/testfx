// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Logging;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class FileLoggerInformationTests
{
    [TestMethod]
    public void FileLoggerInformation_LegacyRecordPreservesEveryFieldAcrossEqualityAndCopies()
    {
        FileInfo originalFile = new("retained.diag");
        FileLoggerInformation original = new(true, originalFile, LogLevel.Debug);
        FileLoggerInformation equal = new(true, originalFile, LogLevel.Debug);
        Assert.AreEqual(original, equal);
        Assert.AreNotEqual(original, original with { SynchronousWrite = false });
        Assert.AreNotEqual(original, original with { LogLevel = LogLevel.Error });
        Assert.AreNotEqual(original, original with { LogFile = new FileInfo("retained.diag") });
        Assert.AreEqual(
            "FileLoggerInformation { SynchronousWrite = True, LogFile = retained.diag, LogLevel = Debug }",
            original.ToString());
        FileInfo replacementFile = new("replacement.diag");
        FileLoggerInformation replacement = original with { SynchronousWrite = false, LogFile = replacementFile, LogLevel = LogLevel.Critical };

        Assert.IsTrue(original.SynchronousWrite);
        Assert.AreSame(originalFile, original.LogFile);
        Assert.AreEqual(LogLevel.Debug, original.LogLevel);
        Assert.IsFalse(replacement.SynchronousWrite);
        Assert.AreSame(replacementFile, replacement.LogFile);
        Assert.AreEqual(LogLevel.Critical, replacement.LogLevel);
        Dictionary<FileLoggerInformation, string> snapshots = new() { [original] = "retained configuration" };
        Assert.AreEqual("retained configuration", snapshots[equal]);
    }
}
