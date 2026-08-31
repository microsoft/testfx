// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Text.Json;

using Microsoft.Testing.Extensions.CtrfReport;
using Microsoft.Testing.Extensions.UnitTests.Helpers;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHost;

using Moq;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class CtrfReportGeneratorLifecycleTests
{
    private readonly Mock<IFileSystem> _fileSystemMock = new();
    private readonly Mock<IEnvironment> _environmentMock = new();
    private readonly Mock<ITestFramework> _testFrameworkMock = new();
    private readonly Mock<IConfiguration> _configurationMock = new();
    private readonly Mock<ITestApplicationModuleInfo> _testApplicationModuleInfoMock = new();

    [TestMethod]
    public async Task IsEnabledAsync_ReturnsTrue_WhenOptionIsSetAsync()
    {
        GeneratorTestContext context = CreateGenerator(optionIsSet: true);

        Assert.IsTrue(await context.Generator.IsEnabledAsync().ConfigureAwait(false));
    }

    [TestMethod]
    public async Task IsEnabledAsync_ReturnsFalse_WhenOptionIsNotSetAsync()
    {
        GeneratorTestContext context = CreateGenerator(optionIsSet: false);

        Assert.IsFalse(await context.Generator.IsEnabledAsync().ConfigureAwait(false));
    }

    [TestMethod]
    public async Task OnTestSessionFinishingAsync_WithoutPriorStart_ThrowsUnreachableExceptionAsync()
    {
        GeneratorTestContext context = CreateGenerator(optionIsSet: true);

        // UnreachableException is an internal, per-assembly polyfill on non-NETCOREAPP TFMs, so asserting on the
        // generic type parameter would compare against this test assembly's copy and fail due to type identity
        // mismatch across assemblies. Assert on the full type name instead.
        Exception exception = await Assert.ThrowsAsync<Exception>(
            () => context.Generator.OnTestSessionFinishingAsync(new TestSessionContextStub())).ConfigureAwait(false);

        Assert.AreEqual(typeof(UnreachableException).FullName, exception.GetType().FullName);
    }

    [TestMethod]
    public async Task FullLifecycle_ConsumesTestNodesAndPublishesArtifactAsync()
    {
        GeneratorTestContext context = CreateGenerator(optionIsSet: true);
        var sessionContext = new TestSessionContextStub();

        await context.Generator.OnTestSessionStartingAsync(sessionContext).ConfigureAwait(false);
        await context.Generator.ConsumeAsync(
            null!,
            CreateMessage("passed", new PassedTestNodeStateProperty()),
            CancellationToken.None).ConfigureAwait(false);
        await context.Generator.ConsumeAsync(
            null!,
            CreateMessage("failed", new FailedTestNodeStateProperty()),
            CancellationToken.None).ConfigureAwait(false);
        await context.Generator.ConsumeAsync(
            null!,
            Mock.Of<IData>(),
            CancellationToken.None).ConfigureAwait(false);
        await context.Generator.OnTestSessionFinishingAsync(sessionContext).ConfigureAwait(false);

        Assert.HasCount(1, context.PublishedArtifacts);
        SessionFileArtifact artifact = context.PublishedArtifacts[0];
        Assert.AreEqual(sessionContext.SessionUid.Value, artifact.SessionUid.Value);
        Assert.EndsWith(".json", artifact.FileInfo.FullName, StringComparison.Ordinal);

        using var document = JsonDocument.Parse(context.ReportStream.GetUtf8Content());
        JsonElement summary = document.RootElement.GetProperty("results").GetProperty("summary");
        Assert.AreEqual(2, summary.GetProperty("tests").GetInt32());
        Assert.AreEqual(1, summary.GetProperty("passed").GetInt32());
        Assert.AreEqual(1, summary.GetProperty("failed").GetInt32());
    }

    [TestMethod]
    public async Task OnTestSessionFinishingAsync_WithoutWarning_PublishesArtifactWithoutOutputAsync()
    {
        GeneratorTestContext context = CreateGenerator(optionIsSet: true);
        var sessionContext = new TestSessionContextStub();

        await context.Generator.OnTestSessionStartingAsync(sessionContext).ConfigureAwait(false);
        await context.Generator.OnTestSessionFinishingAsync(sessionContext).ConfigureAwait(false);

        Assert.HasCount(1, context.PublishedArtifacts);
        context.OutputDeviceMock.Verify(
            outputDevice => outputDevice.DisplayAsync(
                It.IsAny<IOutputDeviceDataProducer>(),
                It.IsAny<IOutputDeviceData>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private GeneratorTestContext CreateGenerator(bool optionIsSet)
    {
        var reportStream = new MemoryFileStream();
        var messageBusMock = new Mock<IMessageBus>();
        var outputDeviceMock = new Mock<IOutputDevice>();
        var loggerFactoryMock = new Mock<ILoggerFactory>();
        var testApplicationProcessExitCodeMock = new Mock<ITestApplicationProcessExitCode>();
        List<SessionFileArtifact> publishedArtifacts = [];

        _ = _fileSystemMock.Setup(fileSystem => fileSystem.ExistFile(It.IsAny<string>())).Returns(false);
        _ = _fileSystemMock
            .Setup(fileSystem => fileSystem.NewFileStream(It.IsAny<string>(), It.IsAny<FileMode>()))
            .Returns(reportStream);
        _ = _configurationMock.SetupGet(configuration => configuration[It.IsAny<string>()]).Returns(string.Empty);
        _ = _environmentMock.SetupGet(environment => environment.MachineName).Returns("MachineName");
        _ = _environmentMock
            .Setup(environment => environment.GetEnvironmentVariable(It.IsAny<string>()))
            .Returns("user");
        _ = _testApplicationModuleInfoMock
            .Setup(moduleInfo => moduleInfo.GetCurrentTestApplicationFullPath())
            .Returns("TestAppPath");
        _ = _testFrameworkMock.SetupGet(testFramework => testFramework.Uid).Returns("fake-uid");
        _ = _testFrameworkMock.SetupGet(testFramework => testFramework.Version).Returns("0.0.0");
        _ = _testFrameworkMock.SetupGet(testFramework => testFramework.DisplayName).Returns("Fake");
        _ = messageBusMock
            .Setup(messageBus => messageBus.PublishAsync(It.IsAny<IDataProducer>(), It.IsAny<IData>()))
            .Callback<IDataProducer, IData>((_, data) => publishedArtifacts.Add((SessionFileArtifact)data))
            .Returns(Task.CompletedTask);
        _ = loggerFactoryMock
            .Setup(loggerFactory => loggerFactory.CreateLogger(It.IsAny<string>()))
            .Returns(Mock.Of<ILogger>());
        _ = testApplicationProcessExitCodeMock
            .Setup(exitCode => exitCode.GetProcessExitCode())
            .Returns(0);

        Dictionary<string, string[]> options = optionIsSet
            ? new() { [CtrfReportGeneratorCommandLine.CtrfReportOptionName] = [] }
            : [];

        ServiceProvider serviceProvider = new();
        serviceProvider.AddService(_configurationMock.Object);
        serviceProvider.AddService(new TestCommandLineOptions(options));
        serviceProvider.AddService(_fileSystemMock.Object);
        serviceProvider.AddService(_testApplicationModuleInfoMock.Object);
        serviceProvider.AddService(messageBusMock.Object);
        serviceProvider.AddService(Mock.Of<IClock>(clock => clock.UtcNow == DateTimeOffset.UtcNow));
        serviceProvider.AddService(_environmentMock.Object);
        serviceProvider.AddService(outputDeviceMock.Object);
        serviceProvider.AllowTestAdapterFrameworkRegistration = true;
        serviceProvider.AddService(_testFrameworkMock.Object);
        serviceProvider.AddService(testApplicationProcessExitCodeMock.Object);
        serviceProvider.AddService(loggerFactoryMock.Object);

        return new(
            new CtrfReportGenerator(serviceProvider),
            outputDeviceMock,
            publishedArtifacts,
            reportStream);
    }

    private static TestNodeUpdateMessage CreateMessage(string uid, IProperty state)
        => new(
            new SessionUid("source-session"),
            new TestNode
            {
                Uid = uid,
                DisplayName = uid,
                Properties = new PropertyBag(state),
            });

    private sealed class GeneratorTestContext(
        CtrfReportGenerator generator,
        Mock<IOutputDevice> outputDeviceMock,
        List<SessionFileArtifact> publishedArtifacts,
        MemoryFileStream reportStream)
    {
        public CtrfReportGenerator Generator { get; } = generator;

        public Mock<IOutputDevice> OutputDeviceMock { get; } = outputDeviceMock;

        public List<SessionFileArtifact> PublishedArtifacts { get; } = publishedArtifacts;

        public MemoryFileStream ReportStream { get; } = reportStream;
    }

    private sealed class TestSessionContextStub : ITestSessionContext
    {
        public SessionUid SessionUid { get; } = new("finishing-session");

        public CancellationToken CancellationToken { get; } = CancellationToken.None;
    }

    private sealed class MemoryFileStream : IFileStream
    {
        private readonly MemoryStream _stream = new();

        Stream IFileStream.Stream => _stream;

        string IFileStream.Name => string.Empty;

        public string GetUtf8Content() => Encoding.UTF8.GetString(_stream.ToArray());

        void IDisposable.Dispose() => _stream.Dispose();

#if NETCOREAPP
        ValueTask IAsyncDisposable.DisposeAsync() => _stream.DisposeAsync();
#endif
    }
}
