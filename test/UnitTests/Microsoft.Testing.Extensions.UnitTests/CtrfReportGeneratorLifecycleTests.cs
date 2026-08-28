// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

using Microsoft.Testing.Extensions.CtrfReport;
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

// These tests exercise the shared session lifecycle orchestration in ReportGeneratorBase
// (IsEnabledAsync, ConsumeAsync, OnTestSessionStartingAsync/FinishingAsync) through its
// simplest concrete subclass, CtrfReportGenerator. The report-content generation itself is
// already covered by CtrfReportEngineTests; here we only need GenerateReportAsync to run,
// so the filesystem is stubbed to avoid touching disk.
[TestClass]
public class CtrfReportGeneratorLifecycleTests
{
    private readonly Mock<IFileSystem> _fileSystemMock = new();
    private readonly Mock<IEnvironment> _environmentMock = new();
    private readonly Mock<ITestFramework> _testFrameworkMock = new();
    private readonly Mock<IConfiguration> _configurationMock = new();
    private readonly Mock<ITestApplicationModuleInfo> _testApplicationModuleInfoMock = new();

    [TestMethod]
    public async Task IsEnabledAsync_ReturnsTrue_WhenOptionIsSetAsync()
    {
        CtrfReportGenerator generator = CreateGenerator(optionIsSet: true, out _, out _);

        Assert.IsTrue(await generator.IsEnabledAsync());
    }

    [TestMethod]
    public async Task IsEnabledAsync_ReturnsFalse_WhenOptionIsNotSetAsync()
    {
        CtrfReportGenerator generator = CreateGenerator(optionIsSet: false, out _, out _);

        Assert.IsFalse(await generator.IsEnabledAsync());
    }

    [TestMethod]
    public async Task OnTestSessionFinishingAsync_WithoutPriorStart_ThrowsAsync()
    {
        CtrfReportGenerator generator = CreateGenerator(optionIsSet: true, out _, out _);

        await Assert.ThrowsExactlyAsync<UnreachableException>(
            () => generator.OnTestSessionFinishingAsync(new TestSessionContextStub()));
    }

    [TestMethod]
    public async Task FullLifecycle_PublishesArtifact_AfterConsumingTestNodesAsync()
    {
        CapturingMessageBus messageBus = new();
        CtrfReportGenerator generator = CreateGenerator(optionIsSet: true, out _, out _, messageBus);

        var sessionContext = new TestSessionContextStub();
        await generator.OnTestSessionStartingAsync(sessionContext);

        await generator.ConsumeAsync(null!, CreateMessage("u1", "Ns.T1", new PassedTestNodeStateProperty()), CancellationToken.None);
        await generator.ConsumeAsync(null!, CreateMessage("u2", "Ns.T2", new FailedTestNodeStateProperty()), CancellationToken.None);

        // Non-TestNodeUpdateMessage data must be ignored rather than throwing.
        await generator.ConsumeAsync(null!, new IgnoredData(), CancellationToken.None);

        await generator.OnTestSessionFinishingAsync(sessionContext);

        Assert.HasCount(1, messageBus.PublishedArtifacts);
        SessionFileArtifact artifact = messageBus.PublishedArtifacts[0];
        Assert.AreEqual(sessionContext.SessionUid.Value, artifact.SessionUid.Value);
        Assert.IsTrue(artifact.FileInfo.FullName.EndsWith(".json", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task OnTestSessionFinishingAsync_DoesNotDisplayWarning_WhenReportGeneratesSuccessfullyAsync()
    {
        // ReportGeneratorBase forwards CtrfReportEngine.GenerateReportAsync's Warning (if any) to the
        // output device. When the report generates cleanly (no captured test results is a valid,
        // warning-free scenario for CTRF), no warning should reach the output device, and the
        // artifact should still be published.
        CapturingOutputDevice outputDevice = new();
        CapturingMessageBus messageBus = new();
        CtrfReportGenerator generator = CreateGenerator(optionIsSet: true, out _, out _, messageBus, outputDevice);

        var sessionContext = new TestSessionContextStub();
        await generator.OnTestSessionStartingAsync(sessionContext);
        await generator.OnTestSessionFinishingAsync(sessionContext);

        Assert.IsEmpty(outputDevice.Lines);
        Assert.HasCount(1, messageBus.PublishedArtifacts);
    }

    private CtrfReportGenerator CreateGenerator(
        bool optionIsSet,
        out CapturingMessageBus messageBus,
        out CapturingOutputDevice outputDevice)
        => CreateGenerator(optionIsSet, out messageBus, out outputDevice, new CapturingMessageBus(), new CapturingOutputDevice());

    private CtrfReportGenerator CreateGenerator(
        bool optionIsSet,
        out CapturingMessageBus messageBus,
        out CapturingOutputDevice outputDevice,
        CapturingMessageBus? messageBusOverride = null,
        CapturingOutputDevice? outputDeviceOverride = null)
    {
        messageBus = messageBusOverride ?? new CapturingMessageBus();
        outputDevice = outputDeviceOverride ?? new CapturingOutputDevice();

        _ = _fileSystemMock.Setup(x => x.ExistFile(It.IsAny<string>())).Returns(false);
        _ = _fileSystemMock.Setup(x => x.NewFileStream(It.IsAny<string>(), It.IsAny<FileMode>()))
            .Returns(() => new MemoryFileStream());
        _ = _configurationMock.SetupGet(x => x[It.IsAny<string>()]).Returns(string.Empty);
        _ = _environmentMock.SetupGet(x => x.MachineName).Returns("MachineName");
        _ = _environmentMock.Setup(x => x.GetEnvironmentVariable(It.IsAny<string>())).Returns("user");
        _ = _testApplicationModuleInfoMock.Setup(x => x.GetCurrentTestApplicationFullPath()).Returns("TestAppPath");
        _ = _testFrameworkMock.SetupGet(x => x.Uid).Returns("fake-uid");
        _ = _testFrameworkMock.SetupGet(x => x.Version).Returns("0.0.0");
        _ = _testFrameworkMock.SetupGet(x => x.DisplayName).Returns("Fake");

        var commandLineOptions = new FakeCommandLineOptions(optionIsSet ? [CtrfReportGeneratorCommandLine.CtrfReportOptionName] : []);

        ServiceProvider serviceProvider = new();
        serviceProvider.AddService(_configurationMock.Object);
        serviceProvider.AddService(commandLineOptions);
        serviceProvider.AddService(_fileSystemMock.Object);
        serviceProvider.AddService(_testApplicationModuleInfoMock.Object);
        serviceProvider.AddService(messageBus);
        serviceProvider.AddService(new FixedClock(DateTimeOffset.UtcNow));
        serviceProvider.AddService(_environmentMock.Object);
        serviceProvider.AddService(outputDevice);
        serviceProvider.AllowTestAdapterFrameworkRegistration = true;
        serviceProvider.AddService(_testFrameworkMock.Object);
        serviceProvider.AddService(new FakeTestApplicationProcessExitCode());
        serviceProvider.AddService(new StubLoggerFactory());

        return new CtrfReportGenerator(serviceProvider);
    }

    private static TestNodeUpdateMessage CreateMessage(string uid, string fullyQualifiedName, IProperty state)
    {
        PropertyBag propertyBag = new();
        propertyBag.Add(state);
        propertyBag.Add(new SerializableKeyValuePairStringProperty("vstest.TestCase.FullyQualifiedName", fullyQualifiedName));

        return new TestNodeUpdateMessage(new SessionUid("session"), new TestNode
        {
            Uid = uid,
            DisplayName = fullyQualifiedName,
            Properties = propertyBag,
        });
    }

    private sealed class IgnoredData : IData
    {
        public string DisplayName => "ignored";

        public string? Description => "ignored";
    }

    private sealed class FakeCommandLineOptions(string[] setOptions) : ICommandLineOptions
    {
        private readonly HashSet<string> _setOptions = [.. setOptions];

        public bool IsOptionSet(string optionName) => _setOptions.Contains(optionName);

        public bool TryGetOptionArgumentList(string optionName, [NotNullWhen(true)] out string[]? arguments)
        {
            arguments = null;
            return false;
        }
    }

    private sealed class CapturingOutputDevice : IOutputDevice
    {
        public List<string> Lines { get; } = [];

        public Task DisplayAsync(IOutputDeviceDataProducer producer, IOutputDeviceData data, CancellationToken cancellationToken)
        {
            Lines.Add(((TextOutputDeviceData)data).Text);
            return Task.CompletedTask;
        }
    }

    private sealed class CapturingMessageBus : IMessageBus
    {
        public List<SessionFileArtifact> PublishedArtifacts { get; } = [];

        public Task PublishAsync(IDataProducer dataProducer, IData data)
        {
            if (data is SessionFileArtifact artifact)
            {
                PublishedArtifacts.Add(artifact);
            }

            return Task.CompletedTask;
        }
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; } = now;
    }

    private sealed class TestSessionContextStub : ITestSessionContext
    {
        public SessionUid SessionUid { get; } = new("session");

        public CancellationToken CancellationToken { get; } = CancellationToken.None;
    }

    private sealed class FakeTestApplicationProcessExitCode : ITestApplicationProcessExitCode
    {
        public bool HasTestAdapterTestSessionFailure => false;

        public string? TestAdapterTestSessionFailureErrorMessage => null;

        public Type[] DataTypesConsumed { get; } = [];

        public string Uid => nameof(FakeTestApplicationProcessExitCode);

        public string Version => "1.0.0";

        public string DisplayName => Uid;

        public string Description => Uid;

        public Task<bool> IsEnabledAsync() => Task.FromResult(true);

        public Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SetTestAdapterTestSessionFailureAsync(string errorMessage, CancellationToken cancellationToken) => Task.CompletedTask;

        public int GetProcessExitCode() => 0;

        public Statistics GetStatistics() => new();
    }

    private sealed class StubLoggerFactory : ILoggerFactory
    {
        public ILogger CreateLogger(string categoryName) => new NullLogger();

        private sealed class NullLogger : ILogger
        {
            public bool IsEnabled(LogLevel logLevel) => false;

            public void Log<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
            }

            public Task LogAsync<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                => Task.CompletedTask;
        }
    }

    private sealed class MemoryFileStream : IFileStream
    {
        private readonly MemoryStream _stream = new();

        Stream IFileStream.Stream => _stream;

        string IFileStream.Name => string.Empty;

        void IDisposable.Dispose() => _stream.Dispose();

#if NETCOREAPP
        ValueTask IAsyncDisposable.DisposeAsync() => _stream.DisposeAsync();
#endif
    }
}
