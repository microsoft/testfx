// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.TrxReport.Abstractions;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;

using Moq;

using TestSessionContext = Microsoft.Testing.Platform.Services.TestSessionContext;

namespace Microsoft.Testing.Extensions.UnitTests.Helpers;

internal sealed class TrxBehaviorFixture : IDisposable
{
    public static readonly DateTimeOffset Start = new(2026, 10, 30, 11, 12, 13, TimeSpan.Zero);
    private TrxReportGenerator? _generator;

    public TrxBehaviorFixture()
    {
        Options[TrxReportGeneratorCommandLine.TrxReportOptionName] = [];
        Options[TrxReportGeneratorCommandLine.TrxReportFileNameOptionName] = ["logical.trx"];
        CommandLine = new TestCommandLineOptions(Options);
        Configuration.SetupGet(value => value[PlatformConfigurationConstants.PlatformResultDirectory]).Returns(Root);
        Clock.SetupGet(value => value.UtcNow).Returns(Start);
        Environment.SetupGet(value => value.MachineName).Returns("agent");
        Environment.SetupGet(value => value.ProcessId).Returns(42);
        Environment.Setup(value => value.GetEnvironmentVariable(It.IsAny<string>()))
            .Returns<string>(name => EnvironmentValues.TryGetValue(name, out string? value) ? value : null);
        Module.Setup(value => value.GetCurrentTestApplicationFullPath()).Returns(Path.Combine(Root, "Suite.dll"));
        Module.Setup(value => value.GetCurrentExecutableInfo()).Returns(new ExecutableInfo("not-launched.exe", [], Root));
        Framework.SetupGet(value => value.Uid).Returns("fixture.adapter");
        Framework.SetupGet(value => value.Version).Returns("1.2.3");
        Framework.SetupGet(value => value.DisplayName).Returns("Fixture framework");
        Framework.SetupGet(value => value.Description).Returns("Fixture description");
        Framework.Setup(value => value.IsEnabledAsync()).ReturnsAsync(true);
        Capability.SetupGet(value => value.IsSupported).Returns(true);
        Capabilities = new TestFrameworkCapabilities(Capability.Object);
        LoggerFactory.Setup(value => value.CreateLogger(It.IsAny<string>())).Returns(Logger.Object);
        Logger.Setup(value => value.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        Logger.Setup(value => value.Log(
            It.IsAny<LogLevel>(), It.IsAny<string>(), It.IsAny<Exception>(), It.IsAny<Func<string, Exception?, string>>()))
            .Callback<LogLevel, string, Exception?, Func<string, Exception?, string>>((level, message, error, _) => Logs.Add((level, message, error)));
        Logger.Setup(value => value.LogAsync(
            It.IsAny<LogLevel>(), It.IsAny<string>(), It.IsAny<Exception>(), It.IsAny<Func<string, Exception?, string>>()))
            .Callback<LogLevel, string, Exception?, Func<string, Exception?, string>>((level, message, error, _) => Logs.Add((level, message, error)))
            .Returns(Task.CompletedTask);
        FileSystem.Setup(value => value.ExistDirectory(It.IsAny<string>())).Returns(true);
        FileSystem.Setup(value => value.CreateDirectory(It.IsAny<string>())).Returns<string>(path => path);
        FileSystem.Setup(value => value.ExistFile(It.IsAny<string>()))
            .Returns<string>(path => Files.ContainsKey(path) || ExistingFiles.ContainsKey(path));
        FileSystem.Setup(value => value.NewFileStream(It.IsAny<string>(), FileMode.Create))
            .Returns<string, FileMode>((path, _) =>
            {
                BeforeReportCreate?.Invoke();
                return CreateFile(path);
            });
        FileSystem.Setup(value => value.NewFileStream(It.IsAny<string>(), FileMode.Create, FileAccess.Write, FileShare.Read))
            .Returns<string, FileMode, FileAccess, FileShare>((path, _, _, _) => CreateFile(path));
        FileSystem.Setup(value => value.NewFileStream(It.IsAny<string>(), FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            .Returns<string, FileMode, FileAccess, FileShare>((path, _, _, _) =>
                new VirtualFile(path, new MemoryStream(GetBytes(path), writable: false), Events, "dispose-read:"));
        FileSystem.Setup(value => value.DeleteFile(It.IsAny<string>()))
            .Callback<string>(path =>
            {
                Events.Add("delete:" + path);
                Files.Remove(path);
                ExistingFiles.Remove(path);
            });
        FileSystem.Setup(value => value.CopyFile(It.IsAny<string>(), It.IsAny<string>(), false))
            .Callback<string, string, bool>((source, destination, _) =>
            {
                CopyFailure?.Invoke(source);
                ExistingFiles[destination] = GetBytes(source);
                Copies.Add((source, destination));
            });
        MessageBus.Setup(value => value.PublishAsync(It.IsAny<IDataProducer>(), It.IsAny<IData>()))
            .Callback<IDataProducer, IData>((producer, artifact) =>
            {
                Publications.Add((producer, artifact));
                Events.Add("publish");
            })
            .Returns(Task.CompletedTask);
        Output.Setup(value => value.DisplayAsync(
            It.IsAny<IOutputDeviceDataProducer>(), It.IsAny<IOutputDeviceData>(), It.IsAny<CancellationToken>()))
            .Callback<IOutputDeviceDataProducer, IOutputDeviceData, CancellationToken>((producer, data, token) =>
            {
                Displays.Add((producer, data, token));
                Events.Add("display");
            })
            .Returns(Task.CompletedTask);
        Services.AllowTestAdapterFrameworkRegistration = true;
        Services.AddServices(
        [
            CommandLine, Configuration.Object, Clock.Object, Environment.Object, Module.Object, Framework.Object,
            Capabilities, LoggerFactory.Object, FileSystem.Object, MessageBus.Object, Output.Object, ExitCode.Object, Scheduler,
        ]);
    }

    public string Root { get; } = Path.Combine(Path.GetTempPath(), "virtual-trx-" + Guid.NewGuid().ToString("N"));

    public string ReportPath => Path.Combine(Root, "logical.trx");

    public Dictionary<string, string[]> Options { get; } = [];

    public TestCommandLineOptions CommandLine { get; }

    public ServiceProvider Services { get; } = new();

    public Mock<IConfiguration> Configuration { get; } = new();

    public Mock<IClock> Clock { get; } = new();

    public Mock<IEnvironment> Environment { get; } = new();

    public Dictionary<string, string?> EnvironmentValues { get; } = [];

    public Mock<ITestApplicationModuleInfo> Module { get; } = new();

    public Mock<ITestFramework> Framework { get; } = new();

    public Mock<ITrxReportCapability> Capability { get; } = new();

    public ITestFrameworkCapabilities Capabilities { get; set; }

    public Mock<ITestApplicationProcessExitCode> ExitCode { get; } = new();

    public Mock<ILogger> Logger { get; } = new();

    public Mock<ILoggerFactory> LoggerFactory { get; } = new();

    public Mock<IFileSystem> FileSystem { get; } = new();

    public Mock<IMessageBus> MessageBus { get; } = new();

    public Mock<IOutputDevice> Output { get; } = new();

    public DeferredTrxTask Scheduler { get; } = new();

    public Dictionary<string, VirtualFile> Files { get; } = [];

    public Dictionary<string, byte[]> ExistingFiles { get; } = [];

    public List<string> Events { get; } = [];

    public List<(LogLevel Level, string Message, Exception? Error)> Logs { get; } = [];

    public List<(IDataProducer Producer, IData Data)> Publications { get; } = [];

    public List<(IOutputDeviceDataProducer Producer, IOutputDeviceData Data, CancellationToken Token)> Displays { get; } = [];

    public List<(string Source, string Destination)> Copies { get; } = [];

    public Action<string>? CopyFailure { get; set; }

    public Action? BeforeReportCreate { get; set; }

    public TrxReportGenerator CreateGenerator(TrxTestApplicationLifecycleCallbacks? lifecycle = null)
    {
        Assert.IsNull(_generator, "Each fixture owns one session consumer.");
        _generator = new(
            Configuration.Object, CommandLine, FileSystem.Object, Module.Object, MessageBus.Object, Clock.Object,
            Environment.Object, Output.Object, Framework.Object, Capabilities, ExitCode.Object, Scheduler, lifecycle, LoggerFactory.Object.CreateLogger<TrxReportGenerator>());
        return _generator;
    }

    public byte[] GetBytes(string path) => Files.TryGetValue(path, out VirtualFile? file) ? file.Bytes : ExistingFiles[path];

    public XDocument Report
    {
        get
        {
            using var stream = new MemoryStream(GetBytes(ReportPath), writable: false);
            return XDocument.Load(stream);
        }
    }

    public Task FinishAsync(TrxReportGenerator generator, CancellationToken cancellationToken = default)
        => FinishAsync(generator, new TestSessionContext(cancellationToken));

    public async Task FinishAsync(TrxReportGenerator generator, ITestSessionContext session)
    {
        Task finishing = generator.OnTestSessionFinishingAsync(session);
        if (Scheduler.HasWriter)
        {
            await Scheduler.DrainAsync();
        }

        await finishing;
    }

    public void Dispose()
    {
        // Failed assertions must not turn a controlled, never-scheduled task into a real hang.
        Scheduler.ReleaseJoin();
        _generator?.Dispose();
        foreach (VirtualFile file in Files.Values)
        {
            file.Dispose();
        }
    }

    private VirtualFile CreateFile(string path)
    {
        var file = new VirtualFile(path, new MemoryStream(), Events);
        Files[path] = file;
        Events.Add("create:" + path);
        return file;
    }

    internal sealed class VirtualFile(string name, Stream stream, List<string> events, string disposePrefix = "dispose:") : IFileStream
    {
        public Stream Stream { get; } = stream;

        public string Name { get; } = name;

        public byte[] Bytes => ((MemoryStream)Stream).ToArray();

        public bool IsDisposed { get; private set; }

        public void Dispose()
        {
            if (!IsDisposed)
            {
                IsDisposed = true;
                events.Add(disposePrefix + Name);
                Stream.Dispose();
            }
        }

#if NETCOREAPP
        public ValueTask DisposeAsync()
        {
            Dispose();
            return default;
        }
#endif
    }

    internal sealed class DeferredTrxTask : ITask
    {
        private readonly TaskCompletionSource<bool> _writerCompleted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _timeout = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private Func<Task>? _writer;

        public bool HasWriter => _writer is not null;

        public string? WriterName { get; private set; }

        public CancellationToken WriterToken { get; private set; }

        public Func<TimeSpan, CancellationToken, Task>? DelayOverride { get; set; }

        public List<(TimeSpan Delay, CancellationToken Token)> Delays { get; } = [];

        public Task RunLongRunning(Func<Task> action, string name, CancellationToken cancellationToken)
        {
            Assert.IsNull(_writer, "One sidecar owns exactly one writer.");
            _writer = action;
            WriterName = name;
            WriterToken = cancellationToken;
            return _writerCompleted.Task;
        }

        public async Task DrainAsync()
        {
            Func<Task> writer = _writer ?? throw new InvalidOperationException("No writer was registered.");
            _writer = null;
            try
            {
                await writer();
                _writerCompleted.TrySetResult(true);
            }
            catch (Exception exception)
            {
                _writerCompleted.TrySetException(exception);
                throw;
            }
        }

        public void ReleaseJoin() => _writerCompleted.TrySetResult(true);

        public void FaultJoin(Exception exception) => _writerCompleted.TrySetException(exception);

        public Task Delay(TimeSpan timeSpan, CancellationToken cancellationToken)
        {
            Delays.Add((timeSpan, cancellationToken));
            return DelayOverride?.Invoke(timeSpan, cancellationToken) ?? _timeout.Task;
        }

        public Task Run(Action action) => throw new NotSupportedException("Real background scheduling is forbidden.");

        public Task Run(Func<Task> function, CancellationToken cancellationToken) => throw new NotSupportedException("Real background scheduling is forbidden.");

        public Task<T> Run<T>(Func<Task<T>?> function, CancellationToken cancellationToken) => throw new NotSupportedException("Real background scheduling is forbidden.");

        public Task WhenAll(params Task[] tasks) => Task.WhenAll(tasks);

        public Task Delay(int millisecondDelay) => throw new NotSupportedException("Real delays are forbidden.");
    }
}
