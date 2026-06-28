// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.JUnitReport;
using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Services;

using Moq;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class JUnitReportEngineApiTests
{
    [TestMethod]
    public void XmlSafeText_RemainsInternalAndSanitizesInvalidCharacters()
        => Assert.AreEqual("a\uFFFDb", JUnitReportEngine.XmlSafeText("a\u0001b"));

    [TestMethod]
    public async Task GenerateReportAsync_UsesReportEngineContextDependencies()
    {
        using var stream = new MemoryFileStream();
        var fileSystemMock = new Mock<IFileSystem>();
        var moduleInfoMock = new Mock<ITestApplicationModuleInfo>();
        var environmentMock = new Mock<IEnvironment>();
        var commandLineOptionsMock = new Mock<ICommandLineOptions>();
        var configurationMock = new Mock<IConfiguration>();
        var clockMock = new Mock<IClock>();
        var testFrameworkMock = new Mock<ITestFramework>();

        _ = fileSystemMock.Setup(x => x.NewFileStream(It.IsAny<string>(), FileMode.Create)).Returns(stream);
        _ = fileSystemMock.Setup(x => x.ExistFile(It.IsAny<string>())).Returns(false);
        _ = moduleInfoMock.Setup(x => x.GetCurrentTestApplicationFullPath()).Returns("My.Test.Module.dll");
        _ = configurationMock.SetupGet(x => x[It.IsAny<string>()]).Returns("/results");
        _ = testFrameworkMock.SetupGet(x => x.Uid).Returns("fake-uid");
        _ = testFrameworkMock.SetupGet(x => x.Version).Returns("1.2.3");
        _ = testFrameworkMock.SetupGet(x => x.DisplayName).Returns("Fake");

        var engine = new JUnitReportEngine(new(
            fileSystemMock.Object,
            moduleInfoMock.Object,
            environmentMock.Object,
            commandLineOptionsMock.Object,
            configurationMock.Object,
            clockMock.Object,
            testFrameworkMock.Object,
            DateTimeOffset.UtcNow,
            0,
            CancellationToken.None));

        (string fileName, string? warning) = await engine.GenerateReportAsync([], new Dictionary<string, TestResultCapture.ParentChainEntry>());

        // The production code combines the results directory with the file name via Path.Combine,
        // so the separator is platform-specific ('/' on Unix, '\' on Windows).
        string expectedPrefix = "/results" + Path.DirectorySeparatorChar;

        Assert.IsNull(warning);
        Assert.StartsWith(expectedPrefix, fileName);
        Assert.EndsWith(".xml", fileName);
        fileSystemMock.Verify(x => x.NewFileStream(It.Is<string>(path => path.StartsWith(expectedPrefix) && path.EndsWith(".tmp")), FileMode.Create), Times.Once);
        fileSystemMock.Verify(x => x.MoveFile(It.Is<string>(path => path.StartsWith(expectedPrefix) && path.EndsWith(".tmp")), fileName, overwrite: true), Times.Once);
    }

    private sealed class MemoryFileStream : IFileStream
    {
        public MemoryFileStream() => Stream = new MemoryStream();

        public MemoryStream Stream { get; }

        Stream IFileStream.Stream => Stream;

        string IFileStream.Name => string.Empty;

        void IDisposable.Dispose() => Stream.Dispose();

#if NETCOREAPP
        ValueTask IAsyncDisposable.DisposeAsync() => Stream.DisposeAsync();
#endif
    }
}
