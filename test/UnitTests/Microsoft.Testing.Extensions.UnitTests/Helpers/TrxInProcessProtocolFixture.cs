// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;

using Microsoft.Testing.Extensions.TrxReport.Abstractions;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.ArtifactPostProcessing;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Extensions.TestHostControllers;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHost;
using Microsoft.Testing.Platform.TestHostControllers;
using Microsoft.Testing.Platform.Tools;

using Moq;

using TestSessionContext = Microsoft.Testing.Platform.Services.TestSessionContext;

namespace Microsoft.Testing.Extensions.UnitTests.Helpers;

internal sealed class TrxInProcessProtocolFixture
{
    private readonly CancellationTokenSource _lifetime;
    private readonly RunningTrxTask _task = new();
    private readonly TestHostControllersManager _controllers = new();
    private readonly ServiceProvider _controllerServices;
    private readonly TrxReportGenerator _generator;
    private readonly object _logSync = new();
    private bool _generatorDisposed;

    public TrxInProcessProtocolFixture(CancellationToken cancellationToken)
    {
        _lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _lifetime.CancelAfter(TimeSpan.FromMinutes(1));
        Memory = new TrxBehaviorFixture();
        ResultsDirectory = Path.Combine(Memory.Root, "résultats-東京-😀");
        ReportPath = Path.Combine(ResultsDirectory, "rapport-été-東京-😀.trx");
        ModulePath = Path.Combine(ResultsDirectory, "Suite-東京.dll");
        Memory.Options[TrxReportGeneratorCommandLine.TrxReportFileNameOptionName] = ["rapport-été-東京-😀.trx"];
        Memory.Configuration.SetupGet(value => value[PlatformConfigurationConstants.PlatformResultDirectory]).Returns(ResultsDirectory);
        Memory.Module.Setup(value => value.GetCurrentTestApplicationFullPath()).Returns(ModulePath);
        Memory.Framework.SetupGet(value => value.Uid).Returns("adapter.équipe.東京");
        Memory.Framework.SetupGet(value => value.Version).Returns("7.8.9-preview.4");
        Memory.EnvironmentValues["UserName"] = "zoë";
        Memory.EnvironmentValues["TESTINGPLATFORM_TRX_TESTRUN_ID"] = "a41e114b-2a25-4c12-9395-5def20458c6c";
        Memory.Environment.SetupGet(value => value.NewLine).Returns("\n");
        Memory.Logger.Setup(value => value.Log(
            It.IsAny<LogLevel>(), It.IsAny<string>(), It.IsAny<Exception>(), It.IsAny<Func<string, Exception?, string>>()))
            .Callback<LogLevel, string, Exception?, Func<string, Exception?, string>>((level, message, error, _) => RecordLog(level, message, error));
        Memory.Logger.Setup(value => value.LogAsync(
            It.IsAny<LogLevel>(), It.IsAny<string>(), It.IsAny<Exception>(), It.IsAny<Func<string, Exception?, string>>()))
            .Callback<LogLevel, string, Exception?, Func<string, Exception?, string>>((level, message, error, _) => RecordLog(level, message, error))
            .Returns(Task.CompletedTask);
        Memory.FileSystem.Setup(value => value.DeleteFile(It.IsAny<string>()))
            .Callback<string>(path =>
            {
                DeleteAttempts.Add(path);
                Memory.Events.Add("delete-attempt:" + path);
                if (KeepSidecarOnDelete && Path.GetFileName(path).StartsWith("trx-stream-", StringComparison.Ordinal))
                {
                    throw new IOException("Controlled child sidecar cleanup failure.");
                }

                Memory.Events.Add("delete:" + path);
                Memory.Files.Remove(path);
                Memory.ExistingFiles.Remove(path);
            });

        Memory.Services.ReplaceService<ITask>(_task);
        _controllerServices = Memory.Services;
        var childServices = (ServiceProvider)Memory.Services.Clone();
        childServices.AllowTestAdapterFrameworkRegistration = true;
        var childOptions = new Dictionary<string, string[]>(Memory.Options)
        {
            [PlatformCommandLineProvider.TestHostControllerPIDOptionKey] = ["42"],
        };
        childServices.ReplaceService<ICommandLineOptions>(new TestCommandLineOptions(childOptions));

        var host = new Mock<ITestHostManager>(MockBehavior.Strict);
        var builder = new Mock<IArtifactPostProcessingApplicationBuilder>(MockBehavior.Strict);
        builder.SetupGet(value => value.TestHost).Returns(host.Object);
        builder.SetupGet(value => value.TestHostControllers).Returns(_controllers);
        builder.SetupGet(value => value.CommandLine).Returns(Mock.Of<ICommandLineManager>());
        builder.SetupGet(value => value.Tools).Returns(Mock.Of<IToolsManager>());
        builder.SetupGet(value => value.ArtifactPostProcessing).Returns(Mock.Of<IArtifactPostProcessingManager>());
        Func<IServiceProvider, ITestHostApplicationLifetime>? lifecycleFactory = null;
        CompositeExtensionFactory<TrxReportGenerator>? consumerFactory = null;
        CompositeExtensionFactory<TrxReportGenerator>? sessionFactory = null;
        host.Setup(value => value.AddTestHostApplicationLifetime(It.IsAny<Func<IServiceProvider, ITestHostApplicationLifetime>>()))
            .Callback((Func<IServiceProvider, ITestHostApplicationLifetime> factory) => lifecycleFactory = factory);
        host.Setup(value => value.AddDataConsumer(It.IsAny<CompositeExtensionFactory<TrxReportGenerator>>()))
            .Callback((CompositeExtensionFactory<TrxReportGenerator> factory) => consumerFactory = factory);
        host.Setup(value => value.AddTestSessionLifetimeHandler(It.IsAny<CompositeExtensionFactory<TrxReportGenerator>>()))
            .Callback((CompositeExtensionFactory<TrxReportGenerator> factory) => sessionFactory = factory);

        builder.Object.AddTrxReportProvider();

        Lifecycle = Assert.IsInstanceOfType<TrxTestApplicationLifecycleCallbacks>(lifecycleFactory!(childServices));
        childServices.AddService(Lifecycle);
        _generator = Assert.IsInstanceOfType<TrxReportGenerator>(((ICompositeExtensionFactory)consumerFactory!).GetInstance(childServices));
        ChildConsumer = _generator;
        SessionLifetime = Assert.IsInstanceOfType<ITestSessionLifetimeHandler>(((ICompositeExtensionFactory)sessionFactory!).GetInstance(childServices));
        Assert.AreSame<IExtension>(ChildConsumer, SessionLifetime, "Use both roles returned by the registered composite factory.");
        Session = new TestSessionContext(Token);
    }

    public TrxBehaviorFixture Memory { get; }

    public string ResultsDirectory { get; }

    public string ReportPath { get; }

    public string ModulePath { get; }

    public CancellationToken Token => _lifetime.Token;

    public TrxTestApplicationLifecycleCallbacks Lifecycle { get; }

    public IDataConsumer ChildConsumer { get; }

    public ITestSessionLifetimeHandler SessionLifetime { get; }

    public TestSessionContext Session { get; }

    public ITestHostProcessLifetimeHandler Controller { get; private set; } = null!;

    public IDataConsumer ControllerConsumer { get; private set; } = null!;

    public bool KeepSidecarOnDelete { get; set; }

    public List<string> DeleteAttempts { get; } = [];

    public string SidecarPath => Assert.ContainsSingle(Memory.Files.Keys.Where(
        path => Path.GetFileName(path).StartsWith("trx-stream-", StringComparison.Ordinal)));

    public XDocument Report
    {
        get
        {
            using var stream = new MemoryStream(Memory.GetBytes(ReportPath), writable: false);
            return XDocument.Load(stream);
        }
    }

    public async Task BindAsync()
    {
        TestHostControllerConfiguration configuration = await _controllers.BuildAsync(_controllerServices);
        Assert.IsTrue(configuration.RequireProcessRestart);
        Controller = Assert.ContainsSingle(configuration.LifetimeHandlers);
        ControllerConsumer = Assert.ContainsSingle(configuration.DataConsumer);
        Assert.AreSame<IExtension>(Controller, ControllerConsumer);
        Assert.IsTrue(await Controller.IsEnabledAsync());
        Assert.IsTrue(await Lifecycle.IsEnabledAsync());
        Assert.IsTrue(await ChildConsumer.IsEnabledAsync());

        await Controller.BeforeTestHostProcessStartAsync(Token);

        // The provider observes the effective name after binding. Transfer only its task-owned
        // value into the mocked child environment; never set a process environment variable.
        ITestHostEnvironmentVariableProvider provider = Assert.ContainsSingle(configuration.EnvironmentVariableProviders);
        var variables = new EnvironmentVariables(Memory.LoggerFactory.Object) { CurrentProvider = provider };
        await provider.UpdateAsync(variables);
        Assert.IsTrue(variables.TryGetVariable("TRXNAMEDPIPENAME", out OwnedEnvironmentVariable? pipe));
        Assert.AreSame(provider, pipe.Owner);
        Assert.IsTrue(pipe.IsLocked);
        Assert.IsFalse(pipe.IsSecret);
        Assert.IsNotNull(pipe.Value);
        Assert.IsNotEmpty(pipe.Value);
        Assert.IsTrue((await provider.ValidateTestHostEnvironmentVariablesAsync(variables)).IsValid);
        Memory.EnvironmentValues["TRXNAMEDPIPENAME"] = pipe.Value;
    }

    public async Task ConnectChildAsync()
    {
        await Lifecycle.BeforeRunAsync(Token);
        await Controller.OnTestHostProcessStartedAsync(Mock.Of<ITestHostProcessInformation>(MockBehavior.Strict), Token);
    }

    public async Task StartSessionAsync()
    {
        Memory.Clock.SetupGet(value => value.UtcNow).Returns(TrxBehaviorFixture.Start.AddSeconds(1));
        await SessionLifetime.OnTestSessionStartingAsync(Session);
    }

    public async Task CloseChildAsync(int exitCode)
    {
        DisposeGenerator();
        await Lifecycle.AfterRunAsync(exitCode, Token);
        Lifecycle.Dispose();
    }

    public void AddCorruptTail(string sidecarPath)
    {
        byte[] prefix = Memory.GetBytes(sidecarPath);
        Memory.ExistingFiles[sidecarPath] = [.. prefix, 1, 0, 0, 0, 1];
        Memory.Files.Remove(sidecarPath);
    }

    public async Task CloseAsync()
    {
        try
        {
            DisposeGenerator();
        }
        finally
        {
            try
            {
                _lifetime.Cancel();
                Lifecycle.Dispose();
            }
            finally
            {
                try
                {
                    (Controller as IDisposable)?.Dispose();
                }
                finally
                {
                    try
                    {
                        await _task.DrainAsync();
                        Memory.Environment.Verify(value => value.Exit(It.IsAny<int>()), Times.Never);
                        Memory.Environment.Verify(value => value.FailFast(It.IsAny<string>(), It.IsAny<Exception>()), Times.Never);
                        Memory.Environment.Verify(value => value.FailFast(It.IsAny<string>()), Times.Never);
                    }
                    finally
                    {
                        Memory.Dispose();
                        _lifetime.Dispose();
                    }
                }
            }
        }
    }

    private void DisposeGenerator()
    {
        if (!_generatorDisposed)
        {
            // This public shutdown path seals the producer queue and joins its real writer.
            // Inspect virtual file bytes only after it has returned.
            _generator.Dispose();
            _generatorDisposed = true;
        }
    }

    private void RecordLog(LogLevel level, string message, Exception? error)
    {
        lock (_logSync)
        {
            Memory.Logs.Add((level, message, error));
        }
    }

    private sealed class RunningTrxTask : ITask
    {
        private readonly ConcurrentQueue<Task> _work = new();
        private readonly ConcurrentQueue<CancellationTokenSource> _delayTokens = new();
        private readonly CancellationTokenSource _shutdown = new();

        public Task Run(Action action) => Track(Task.Run(action));

        public Task Run(Func<Task> function, CancellationToken cancellationToken) => Track(Task.Run(function, cancellationToken));

        public Task<T> Run<T>(Func<Task<T>?> function, CancellationToken cancellationToken) => Track(Task.Run(function, cancellationToken));

        public Task RunLongRunning(Func<Task> action, string name, CancellationToken cancellationToken)
            => Track(Task.Factory.StartNew(action, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap());

        public Task WhenAll(params Task[] tasks) => Task.WhenAll(tasks);

        public Task Delay(int millisecondDelay) => Delay(TimeSpan.FromMilliseconds(millisecondDelay), CancellationToken.None);

        public Task Delay(TimeSpan timeSpan, CancellationToken cancellationToken)
        {
            var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _shutdown.Token);
            _delayTokens.Enqueue(linked);
            return Track(Task.Delay(timeSpan, linked.Token));
        }

        public async Task DrainAsync()
        {
            _shutdown.Cancel();
            Task[] tasks = _work.ToArray();
            Task joined = Task.WhenAll(tasks);
            using var timeout = new CancellationTokenSource();
            Task guard = Task.Delay(TimeSpan.FromMinutes(1), timeout.Token);
            try
            {
                if (await Task.WhenAny(joined, guard) != joined)
                {
                    throw new TimeoutException("Task-owned TRX transport and writer delegates did not finish during cleanup.");
                }

                try
                {
                    await joined;
                }
                catch (OperationCanceledException) when (tasks.All(task => !task.IsFaulted))
                {
                    // Pending connection/timeout guards are canceled by this fixture's shutdown.
                    // Actual delegate faults must still fail cleanup.
                }
            }
            finally
            {
                timeout.Cancel();
                foreach (CancellationTokenSource source in _delayTokens)
                {
                    source.Dispose();
                }

                _shutdown.Dispose();
            }
        }

        private TTask Track<TTask>(TTask task)
            where TTask : Task
        {
            _work.Enqueue(task);
            return task;
        }
    }
}
