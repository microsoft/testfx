// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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

namespace Microsoft.Testing.Extensions.UnitTests.Helpers;

internal sealed class ReportBehaviorFixture
{
    public static readonly DateTimeOffset Start = new(2026, 8, 31, 12, 0, 0, TimeSpan.Zero);
    private readonly TaskCompletionSource<object?> _writerCompleted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Func<Task>? _writer;

    public ReportBehaviorFixture(Dictionary<string, string[]>? options = null)
    {
        Configuration.SetupGet(value => value[PlatformConfigurationConstants.PlatformResultDirectory]).Returns("results");
        Clock.SetupGet(value => value.UtcNow).Returns(Start);
        Environment.SetupGet(value => value.ProcessId).Returns(42);
        Environment.SetupGet(value => value.MachineName).Returns("agent");
        Module.Setup(value => value.GetCurrentTestApplicationFullPath()).Returns("tests.dll");
        Framework.SetupGet(value => value.Uid).Returns("fw");
        Framework.SetupGet(value => value.Version).Returns("1.2.3");
        Framework.SetupGet(value => value.DisplayName).Returns("Test Framework");
        Framework.Setup(value => value.IsEnabledAsync()).ReturnsAsync(true);
        LoggerFactory.Setup(value => value.CreateLogger(It.IsAny<string>())).Returns(Logger.Object);
        FileSystem.Setup(value => value.CreateDirectory(It.IsAny<string>())).Returns<string>(Path.GetFullPath);
        FileSystem.Setup(value => value.ExistFile(It.IsAny<string>())).Returns<string>(path => InputFiles.ContainsKey(path) || ExistingPaths.Contains(path));
        FileSystem.Setup(value => value.NewFileStream(It.IsAny<string>(), It.IsAny<FileMode>()))
            .Returns<string, FileMode>((path, _) => CreateOutput(path));
        FileSystem.Setup(value => value.NewFileStream(It.IsAny<string>(), FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
            .Returns<string, FileMode, FileAccess, FileShare>((path, _, _, _) => CreateOutput(path));
        FileSystem.Setup(value => value.NewFileStream(It.IsAny<string>(), FileMode.Append, FileAccess.Write, FileShare.Read))
            .Returns<string, FileMode, FileAccess, FileShare>((path, _, _, _) => CreateOutput(path));
        FileSystem.Setup(value => value.NewFileStream(It.IsAny<string>(), FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            .Returns<string, FileMode, FileAccess, FileShare>((path, _, _, _) => new MemoryFile(Encoding.UTF8.GetBytes(InputFiles[path])));
        FileSystem.Setup(value => value.MoveFile(It.IsAny<string>(), It.IsAny<string>(), true))
            .Callback<string, string, bool>((source, destination, _) => WrittenFiles[destination] = WrittenFiles[source]);
        MessageBus.Setup(value => value.PublishAsync(It.IsAny<IDataProducer>(), It.IsAny<IData>()))
            .Callback<IDataProducer, IData>((_, data) =>
            {
                Artifacts.Add(data);
                Events.Add("artifact");
            })
            .Returns(Task.CompletedTask);
        Output.Setup(value => value.DisplayAsync(It.IsAny<IOutputDeviceDataProducer>(), It.IsAny<IOutputDeviceData>(), It.IsAny<CancellationToken>()))
            .Callback<IOutputDeviceDataProducer, IOutputDeviceData, CancellationToken>((_, data, _) =>
            {
                Warnings.Add(Assert.IsInstanceOfType<WarningMessageOutputDeviceData>(data).Message);
                Events.Add("warning");
            })
            .Returns(Task.CompletedTask);
        TaskService.Setup(value => value.RunLongRunning(It.IsAny<Func<Task>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<Func<Task>, string, CancellationToken>((writer, _, _) => _writer = writer)
            .Returns(_writerCompleted.Task);
        TaskService.Setup(value => value.Delay(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Returns(new TaskCompletionSource<object?>().Task);
        Services.AllowTestAdapterFrameworkRegistration = true;
        Services.AddServices(
        [
            Configuration.Object, new TestCommandLineOptions(options ?? []), FileSystem.Object, Clock.Object,
            Environment.Object, Module.Object, Framework.Object, LoggerFactory.Object, MessageBus.Object,
            Output.Object, Mock.Of<ITestApplicationProcessExitCode>(), TaskService.Object,
        ]);
    }

    public ServiceProvider Services { get; } = new();

    public Mock<IConfiguration> Configuration { get; } = new();

    public Mock<IClock> Clock { get; } = new();

    public Mock<IEnvironment> Environment { get; } = new();

    public Mock<ITestApplicationModuleInfo> Module { get; } = new();

    public Mock<ITestFramework> Framework { get; } = new();

    public Mock<ILoggerFactory> LoggerFactory { get; } = new();

    public Mock<ILogger> Logger { get; } = new();

    public Mock<IFileSystem> FileSystem { get; } = new();

    public Mock<IMessageBus> MessageBus { get; } = new();

    public Mock<IOutputDevice> Output { get; } = new();

    public Mock<ITask> TaskService { get; } = new();

    public Dictionary<string, string> InputFiles { get; } = [];

    public HashSet<string> ExistingPaths { get; } = [];

    public Dictionary<string, MemoryFile> WrittenFiles { get; } = [];

    public List<IData> Artifacts { get; } = [];

    public List<string> Warnings { get; } = [];

    public List<string> Events { get; } = [];

    public async Task FinishAsync(ITestSessionLifetimeHandler generator, ITestSessionContext session)
    {
        Task finishing = generator.OnTestSessionFinishingAsync(session);
        if (_writer is not null)
        {
            try
            {
                await _writer();
                _writerCompleted.TrySetResult(null);
            }
            catch (Exception exception)
            {
                _writerCompleted.TrySetException(exception);
            }
        }

        await finishing;
    }

    private MemoryFile CreateOutput(string path)
    {
        var file = new MemoryFile();
        WrittenFiles[path] = file;
        return file;
    }

    internal sealed class Session(CancellationToken cancellationToken = default) : ITestSessionContext
    {
        public SessionUid SessionUid { get; } = new("report-session");

        public CancellationToken CancellationToken { get; } = cancellationToken;
    }

    internal sealed class MemoryFile(byte[]? initial = null) : IFileStream
    {
        private readonly MemoryStream _stream = initial is null ? new() : new(initial);

        public Stream Stream => _stream;

        public string Name => "memory";

        public bool IsDisposed { get; private set; }

        public string Text => Encoding.UTF8.GetString(_stream.ToArray());

        public void Dispose()
        {
            IsDisposed = true;
            _stream.Dispose();
        }

#if NETCOREAPP
        public ValueTask DisposeAsync()
        {
            Dispose();
            return default;
        }
#endif
    }
}
