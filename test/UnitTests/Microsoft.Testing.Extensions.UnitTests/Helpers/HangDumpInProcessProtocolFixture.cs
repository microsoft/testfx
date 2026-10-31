// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;

using Microsoft.Testing.Extensions.Diagnostics;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Extensions.TestHostControllers;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHost;
using Microsoft.Testing.Platform.TestHostControllers;

using Moq;

using TestSessionContext = Microsoft.Testing.Platform.Services.TestSessionContext;

namespace Microsoft.Testing.Extensions.UnitTests.Helpers;

internal sealed class HangDumpInProcessProtocolFixture
{
    private readonly CancellationTokenSource _lifetime;
    private readonly ServiceProvider _controllerServices = new();
    private readonly ServiceProvider _childServices = new();
    private readonly TestHostControllersManager _controllers = new();
    private readonly Mock<ILoggerFactory> _controllerLoggerFactory = new(MockBehavior.Strict);
    private readonly ConcurrentQueue<Task> _transportTasks = new();
    private readonly ConcurrentQueue<Task> _operations = new();
    private readonly ConcurrentDictionary<Task, bool> _observedOperations = new();
    private readonly ICompositeExtensionFactory _consumerFactory;
    private readonly ICompositeExtensionFactory _sessionFactory;
    private HangDumpProcessLifetimeHandler? _controller;
    private HangDumpActivityIndicator? _child;
    private bool _closed;

    public HangDumpInProcessProtocolFixture(CancellationToken cancellationToken, bool traceEnabled = true)
    {
        _lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _lifetime.CancelAfter(TimeSpan.FromMinutes(1));
        TraceEnabled = traceEnabled;
        var options = new TestCommandLineOptions(new()
        {
            [HangDumpCommandLineProvider.HangDumpOptionName] = [],
            [HangDumpCommandLineProvider.HangDumpTimeoutOptionName] = ["1h"],
            [HangDumpCommandLineProvider.HangDumpTypeOptionName] = ["None"],
        });

        // Never consult the process environment, including an inherited CI deadline. Any
        // unexpected environment/process/filesystem work fails at its strict mock boundary.
        ControllerEnvironment.Setup(value => value.GetEnvironmentVariable("TESTINGPLATFORM_DEADLINE")).Returns((string?)null);
        ChildEnvironment.Setup(value => value.GetEnvironmentVariable(HangDumpEnvironmentVariableProvider.PipeNameEnvironmentVariableName))
            .Returns(() => PipeName);
        ConfigureEnvironment(ControllerEnvironment);
        ConfigureEnvironment(ChildEnvironment);
        ControllerClock.SetupGet(value => value.UtcNow).Returns(() => Now);
        ChildClock.SetupGet(value => value.UtcNow).Returns(() => Now);
        ConfigureLogger(ControllerLogger, "controller", traceEnabled);
        ConfigureLogger(ChildLogger, "child", traceEnabled);
        _controllerLoggerFactory.Setup(value => value.CreateLogger(It.IsAny<string>()))
            .Returns(ControllerLogger.Object);
        var childLoggerFactory = new Mock<ILoggerFactory>(MockBehavior.Strict);
        childLoggerFactory.Setup(value => value.CreateLogger(typeof(HangDumpActivityIndicator).FullName!))
            .Returns(ChildLogger.Object);
        Configuration.SetupGet(value => value[PlatformConfigurationConstants.PlatformResultDirectory])
            .Returns("task-owned-virtual-hang-results");
        ProcessInformation.SetupGet(value => value.HasExitedGracefully).Returns(() => GracefulExit);
        ProcessInformation.SetupGet(value => value.ExitCode).Returns(() => ExitCode);
        Scheduler.Setup(value => value.Run(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()))
            .Returns((Func<Task> callback, CancellationToken token) =>
            {
                Task task = Task.Run(callback, token);
                _transportTasks.Enqueue(task);
                return task;
            });

        _controllerServices.AddServices(
        [
            options, ControllerEnvironment.Object, ControllerClock.Object, _controllerLoggerFactory.Object,
            Configuration.Object, ProcessHandler.Object, FileSystem.Object, Output.Object, MessageBus.Object, Scheduler.Object,
        ]);
        _childServices.AddServices(
        [
            options, ChildEnvironment.Object, ChildClock.Object, childLoggerFactory.Object, Scheduler.Object,
        ]);

        var host = new Mock<ITestHostManager>(MockBehavior.Strict);
        var commands = new Mock<ICommandLineManager>(MockBehavior.Strict);
        var builder = new Mock<ITestApplicationBuilder>(MockBehavior.Strict);
        builder.SetupGet(value => value.TestHostControllers).Returns(_controllers);
        builder.SetupGet(value => value.TestHost).Returns(host.Object);
        builder.SetupGet(value => value.CommandLine).Returns(commands.Object);
        commands.Setup(value => value.AddProvider(It.IsAny<Func<ICommandLineOptionsProvider>>()));
        CompositeExtensionFactory<HangDumpActivityIndicator>? consumerFactory = null;
        CompositeExtensionFactory<HangDumpActivityIndicator>? sessionFactory = null;
        host.Setup(value => value.AddDataConsumer(It.IsAny<CompositeExtensionFactory<HangDumpActivityIndicator>>()))
            .Callback((CompositeExtensionFactory<HangDumpActivityIndicator> factory) => consumerFactory = factory);
        host.Setup(value => value.AddTestSessionLifetimeHandler(It.IsAny<CompositeExtensionFactory<HangDumpActivityIndicator>>()))
            .Callback((CompositeExtensionFactory<HangDumpActivityIndicator> factory) => sessionFactory = factory);

        builder.Object.AddHangDumpProvider();

        Assert.IsNotNull(consumerFactory);
        Assert.AreSame(consumerFactory, sessionFactory);
        _consumerFactory = (ICompositeExtensionFactory)consumerFactory;
        _sessionFactory = (ICompositeExtensionFactory)sessionFactory!;
    }

    public bool TraceEnabled { get; }

    public DateTimeOffset Now { get; } = new(2026, 10, 30, 12, 34, 56, TimeSpan.Zero);

    public bool GracefulExit { get; set; } = true;

    public int ExitCode { get; set; }

    public CancellationToken Token => _lifetime.Token;

    public string? PipeName { get; private set; }

    public string? ReversePipeName { get; private set; }

    public ITestHostProcessLifetimeHandler Controller { get; private set; } = null!;

    public IDataConsumer Consumer { get; private set; } = null!;

    public ITestSessionLifetimeHandler SessionLifetime { get; private set; } = null!;

    public Mock<IEnvironment> ControllerEnvironment { get; } = new(MockBehavior.Strict);

    public Mock<IEnvironment> ChildEnvironment { get; } = new(MockBehavior.Strict);

    public Mock<IClock> ControllerClock { get; } = new(MockBehavior.Strict);

    public Mock<IClock> ChildClock { get; } = new(MockBehavior.Strict);

    public Mock<ILogger> ControllerLogger { get; } = new(MockBehavior.Strict);

    public Mock<ILogger> ChildLogger { get; } = new(MockBehavior.Strict);

    public Mock<IConfiguration> Configuration { get; } = new(MockBehavior.Strict);

    public Mock<IProcessHandler> ProcessHandler { get; } = new(MockBehavior.Strict);

    public Mock<ITestHostProcessInformation> ProcessInformation { get; } = new(MockBehavior.Strict);

    public Mock<IFileSystem> FileSystem { get; } = new(MockBehavior.Strict);

    public Mock<IOutputDevice> Output { get; } = new(MockBehavior.Strict);

    public Mock<IMessageBus> MessageBus { get; } = new(MockBehavior.Strict);

    public Mock<ITask> Scheduler { get; } = new(MockBehavior.Strict);

    public ConcurrentQueue<(string Role, LogLevel Level, string Message, Exception? Error)> Logs { get; } = new();

    public Func<string, Task>? ControllerLogAsync { get; set; }

    public Func<string, Task>? ChildLogAsync { get; set; }

    public TaskCompletionSource<bool> ControllerAccepted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public TaskCompletionSource<bool> ControllerWaitEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public TaskCompletionSource<bool> ChildReverseAccepting { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public bool AllTrackedTasksCompleted => _operations.All(task => task.IsCompleted) && _transportTasks.All(task => task.IsCompleted);

    public int TrackedTransportTaskCount => _transportTasks.Count;

    public string[] ControllerMessages => [.. Logs.Where(value => value.Role == "controller").Select(value => value.Message)];

    public string[] ChildMessages => [.. Logs.Where(value => value.Role == "child").Select(value => value.Message)];

    public async Task BindAsync()
    {
        TestHostControllerConfiguration configuration = await _controllers.BuildAsync(_controllerServices);
        Assert.IsTrue(configuration.RequireProcessRestart);
        Assert.IsEmpty(configuration.DataConsumer);
        Controller = Assert.ContainsSingle(configuration.LifetimeHandlers);
        _controller = Assert.IsInstanceOfType<HangDumpProcessLifetimeHandler>(Controller);
        Assert.IsTrue(await Controller.IsEnabledAsync());
        await Controller.BeforeTestHostProcessStartAsync(Token);

        ITestHostEnvironmentVariableProvider provider = Assert.ContainsSingle(configuration.EnvironmentVariableProviders);
        Assert.IsTrue(await provider.IsEnabledAsync());
        var variables = new EnvironmentVariables(_controllerLoggerFactory.Object) { CurrentProvider = provider };
        await provider.UpdateAsync(variables);
        Assert.IsTrue(variables.TryGetVariable(HangDumpEnvironmentVariableProvider.PipeNameEnvironmentVariableName, out OwnedEnvironmentVariable? pipe));
        Assert.AreSame(provider, pipe.Owner);
        Assert.IsTrue(pipe.IsLocked);
        Assert.IsFalse(pipe.IsSecret);
        Assert.IsNotNull(pipe.Value);
        Assert.IsNotEmpty(pipe.Value);
        PipeName = pipe.Value;
        Assert.IsTrue((await provider.ValidateTestHostEnvironmentVariablesAsync(variables)).IsValid);

        // Materialize the registered host roles only after the actual bound endpoint is handed
        // to the mocked child environment. No hidden IPC interface is referenced by these tests.
        Consumer = Assert.IsInstanceOfType<IDataConsumer>(_consumerFactory.GetInstance(_childServices));
        SessionLifetime = Assert.IsInstanceOfType<ITestSessionLifetimeHandler>(_sessionFactory.GetInstance(_childServices));
        Assert.AreSame<IExtension>(Consumer, SessionLifetime);
        _child = Assert.IsInstanceOfType<HangDumpActivityIndicator>(Consumer);
        Assert.IsTrue(await Consumer.IsEnabledAsync());
        Assert.IsTrue(await SessionLifetime.IsEnabledAsync());
        Assert.AreSequenceEqual(new[] { "Hang dump timeout setup 01:00:00." },
            Logs.Where(value => value.Level == LogLevel.Information).Select(value => value.Message));
    }

    public Task StartControllerAsync(CancellationToken? cancellationToken = null)
        => TrackOperation(Task.Run(
            () => Controller.OnTestHostProcessStartedAsync(ProcessInformation.Object, cancellationToken ?? Token),
            CancellationToken.None));

    public Task StartChildAsync(CancellationToken? cancellationToken = null)
        => TrackOperation(Task.Run(
            () => SessionLifetime.OnTestSessionStartingAsync(new TestSessionContext(cancellationToken ?? Token)),
            CancellationToken.None));

    public async Task ConnectAsync()
    {
        Task controller = StartControllerAsync();
        Task child = StartChildAsync();
        await AwaitAsync(Task.WhenAll(controller, child));
    }

    public async Task AwaitAsync(Task operation)
    {
        try
        {
            await BoundAsync(operation);
        }
        finally
        {
            if (operation.IsCompleted)
            {
                _observedOperations.TryAdd(operation, true);
            }
        }
    }

    public void AssertReadyHandshake()
    {
        Assert.IsNotNull(PipeName);
        Assert.IsNotNull(ReversePipeName);
        Assert.IsNotEmpty(ReversePipeName);
        Assert.AreNotEqual(PipeName, ReversePipeName);
        Assert.Contains($"Waiting for connection to {PipeName}", ControllerMessages);
        Assert.Contains($"Wait for test host connection to the server pipe '{PipeName}'", ControllerMessages);
        Assert.Contains($"Consumer pipe name received '{ReversePipeName}'", ControllerMessages);
        Assert.Contains($"Connected to the test host server pipe '{ReversePipeName}'", ControllerMessages);
        Assert.Contains($"Connecting to the process lifetime handler {PipeName}", ChildMessages);
        Assert.Contains("Connected to the process lifetime handler", ChildMessages);
        Assert.Contains($"Send consumer pipe name to the test controller '{ReversePipeName}'", ChildMessages);
        Assert.Contains("Test host controller connected", ChildMessages);
        Assert.HasCount(1, ControllerMessages.Where(message => message.StartsWith("Consumer pipe name received '", StringComparison.Ordinal)));
        Assert.HasCount(1, ChildMessages.Where(message => message == "Test host controller connected"));
        AssertNoDumpWork();
    }

    public void AssertSignalCount(int count)
    {
        int tracedCount = TraceEnabled ? count : 0;
        Assert.HasCount(tracedCount, ControllerMessages.Where(message => message == $"Activity signal received by the test host '{Now}'"));
        Assert.HasCount(tracedCount, ChildMessages.Where(message => message == "Activity signal sent."));
        Assert.AreSequenceEqual(
            Enumerable.Range(0, tracedCount).SelectMany(_ => new[] { "controller", "child" }),
            Logs.Where(value => value.Message.StartsWith("Activity signal received by the test host '", StringComparison.Ordinal)
                || value.Message == "Activity signal sent.").Select(value => value.Role),
            "Each sent confirmation must follow its actual controller acknowledgement.");
    }

    public void AssertNoDumpWork()
    {
        ProcessHandler.VerifyNoOtherCalls();
        ProcessInformation.VerifyGet(value => value.PID, Times.Never);
        FileSystem.VerifyNoOtherCalls();
        Configuration.VerifyGet(value => value[PlatformConfigurationConstants.PlatformResultDirectory], Times.Never);
        Configuration.VerifyNoOtherCalls();
        Assert.IsEmpty(Output.Invocations);
        Assert.IsEmpty(MessageBus.Invocations);
        ControllerEnvironment.Verify(value => value.GetEnvironmentVariable("TESTINGPLATFORM_DEADLINE"), Times.Once);
        ControllerEnvironment.Verify(value => value.GetEnvironmentVariable("TESTINGPLATFORM_DEADLINE_DUMP_MARGIN"), Times.Never);
        ChildEnvironment.Verify(value => value.GetEnvironmentVariable(HangDumpEnvironmentVariableProvider.PipeNameEnvironmentVariableName), Times.Once);
        Assert.IsEmpty(Logs.Where(value => value.Level == LogLevel.Error), "A transport error is not successful lifecycle coverage.");
        Assert.IsEmpty(ControllerMessages.Where(message => message.StartsWith("Hang dump deadline setup ", StringComparison.Ordinal)
            || message.Contains("Taking hang dump.", StringComparison.Ordinal)));
        VerifyNoEnvironmentTermination(ControllerEnvironment);
        VerifyNoEnvironmentTermination(ChildEnvironment);
    }

    public async Task ShutdownAsync(bool useAsyncDisposal)
    {
        _lifetime.Cancel();
        try
        {
            if (_child is not null)
            {
#if NETCOREAPP
                if (useAsyncDisposal)
                {
                    await _child.DisposeAsync();
                }
                else
#endif
                {
                    _child.Dispose();
                }
            }

            await DrainOperationsAsync();
        }
        finally
        {
            try
            {
                if (_controller is not null)
                {
#if NETCOREAPP
                    if (useAsyncDisposal)
                    {
                        await _controller.DisposeAsync();
                    }
                    else
#endif
                    {
                        _controller.Dispose();
                    }
                }
            }
            finally
            {
                await DrainTransportAsync();
            }
        }
    }

    public async Task CloseAsync()
    {
        if (_closed)
        {
            return;
        }

        try
        {
            await ShutdownAsync(useAsyncDisposal: false);
            Assert.IsTrue(AllTrackedTasksCompleted, "No task-owned pipe or lifecycle delegate may survive cleanup.");
            AssertNoDumpWork();
        }
        finally
        {
            _closed = true;
            _lifetime.Dispose();
        }
    }

    private static void ConfigureEnvironment(Mock<IEnvironment> environment)
    {
        environment.SetupGet(value => value.NewLine).Returns("\n");
        environment.Setup(value => value.Exit(It.IsAny<int>())).Throws(new AssertFailedException("Unexpected transport process exit."));
        environment.Setup(value => value.FailFast(It.IsAny<string>())).Throws(new AssertFailedException("Unexpected transport failure."));
        environment.Setup(value => value.FailFast(It.IsAny<string>(), It.IsAny<Exception>()))
            .Throws(new AssertFailedException("Unexpected transport failure."));
    }

    private static void VerifyNoEnvironmentTermination(Mock<IEnvironment> environment)
    {
        environment.Verify(value => value.Exit(It.IsAny<int>()), Times.Never);
        environment.Verify(value => value.FailFast(It.IsAny<string>()), Times.Never);
        environment.Verify(value => value.FailFast(It.IsAny<string>(), It.IsAny<Exception>()), Times.Never);
    }

    private static async Task BoundAsync(Task operation)
    {
        using var cancellation = new CancellationTokenSource();
        Task guard = Task.Delay(TimeSpan.FromSeconds(30), cancellation.Token);
        try
        {
            if (await Task.WhenAny(operation, guard) != operation)
            {
                throw new TimeoutException("The task-owned HangDump lifecycle or transport delegate did not complete.");
            }

            await operation;
        }
        finally
        {
            cancellation.Cancel();
            try
            {
                await guard;
            }
            catch (OperationCanceledException)
            {
                // This is a canceled failure guard, never a domain timer or an ordering oracle.
            }
        }
    }

    private void ConfigureLogger(Mock<ILogger> logger, string role, bool traceEnabled)
    {
        logger.Setup(value => value.IsEnabled(It.IsAny<LogLevel>()))
            .Returns<LogLevel>(level => traceEnabled || level != LogLevel.Trace);
        logger.Setup(value => value.Log(
            It.IsAny<LogLevel>(), It.IsAny<string>(), It.IsAny<Exception?>(), It.IsAny<Func<string, Exception?, string>>()))
            .Callback<LogLevel, string, Exception?, Func<string, Exception?, string>>(
                (level, message, error, _) => RecordLog(role, level, message, error));
        logger.Setup(value => value.LogAsync(
            It.IsAny<LogLevel>(), It.IsAny<string>(), It.IsAny<Exception?>(), It.IsAny<Func<string, Exception?, string>>()))
            .Returns<LogLevel, string, Exception?, Func<string, Exception?, string>>((level, message, error, _) =>
            {
                RecordLog(role, level, message, error);
                return (role == "controller" ? ControllerLogAsync : ChildLogAsync)?.Invoke(message) ?? Task.CompletedTask;
            });
    }

    private void RecordLog(string role, LogLevel level, string message, Exception? error)
    {
        Logs.Enqueue((role, level, message, error));
        if (role == "controller" && message.StartsWith("Client connected to ", StringComparison.Ordinal))
        {
            ControllerAccepted.TrySetResult(true);
        }

        if (role == "controller" && message.StartsWith("Wait for test host connection to the server pipe '", StringComparison.Ordinal))
        {
            ControllerWaitEntered.TrySetResult(true);
        }

        if (role == "child" && message.StartsWith("Hang dump pipe name: '", StringComparison.Ordinal))
        {
            const string Prefix = "Hang dump pipe name: '";
            ReversePipeName = message.Substring(Prefix.Length, message.Length - Prefix.Length - 1);
        }

        if (role == "child" && message.StartsWith("Waiting for connection for the pipe name ", StringComparison.Ordinal))
        {
            ChildReverseAccepting.TrySetResult(true);
        }
    }

    private Task TrackOperation(Task operation)
    {
        _operations.Enqueue(operation);
        return operation;
    }

    private async Task DrainOperationsAsync()
    {
        Task[] operations = _operations.ToArray();
        try
        {
            await BoundAsync(Task.WhenAll(operations));
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested && operations.All(task => !task.IsFaulted))
        {
            // A failed assertion may leave a prepared handshake that needs lifetime cancellation.
        }
        catch (Exception) when (operations.Any(task => task.IsFaulted)
            && operations.Where(task => task.IsFaulted).All(task => _observedOperations.ContainsKey(task)))
        {
            // The test already awaited and asserted these original injected lifecycle failures.
        }

        Assert.IsTrue(operations.All(task => task.IsCompleted));
    }

    private async Task DrainTransportAsync()
    {
        while (true)
        {
            Task[] tasks = _transportTasks.ToArray();
            try
            {
                await BoundAsync(Task.WhenAll(tasks));
            }
            catch (OperationCanceledException) when (_lifetime.IsCancellationRequested && tasks.All(task => !task.IsFaulted))
            {
                // A never-connected accept task observes the fixture's canceled lifetime.
            }

            Assert.IsTrue(tasks.All(task => task.IsCompleted));
            if (tasks.Length == _transportTasks.Count)
            {
                return;
            }
        }
    }
}
