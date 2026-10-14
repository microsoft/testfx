// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.IO.Pipes;

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Extensions.TestHostControllers;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Hosts;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.Telemetry;
using Microsoft.Testing.Platform.TestHost;
using Microsoft.Testing.Platform.TestHostControllers;
using Microsoft.Testing.Platform.UnitTests.Helpers;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

/// <summary>
/// Runs the shipping controller host with fake processes and a uniquely owned local protocol peer.
/// No executable, port, process-global environment or runtime setting is changed.
/// </summary>
[UnsupportedOSPlatform("browser")]
internal sealed class ControllerHostFixture : IDisposable
{
    public static readonly TimeSpan OperationLimit = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan CleanupLimit = TimeSpan.FromSeconds(30);
    private readonly CancellationTokenSource _peerLifetime = new(OperationLimit);
    private readonly TaskCompletionSource<bool> _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<bool> _exited = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly ConcurrentQueue<Task> _workers = new();
    private readonly int _reportedExitCode;
    private readonly int _unfilteredExitCode;
    private readonly bool _sendCompletion;
    private bool _hasExited;
    private Task? _peer;
    private Task<int>? _run;

    public ControllerHostFixture(
        bool customLauncher = false,
        bool validEnvironment = true,
        bool telemetry = false,
        bool ownsCoverage = false,
        string? lineThreshold = null,
        long coveredLines = 90,
        int osExitCode = 0,
        int reportedExitCode = 0,
        int? unfilteredExitCode = null,
        bool authoritativeExitCode = true,
        bool sendCompletion = true)
    {
        _reportedExitCode = reportedExitCode;
        _unfilteredExitCode = unfilteredExitCode ?? reportedExitCode;
        _sendCompletion = sendCompletion;
        Environment.SetupGet(x => x.ProcessId).Returns(42);
        Environment.Setup(x => x.GetEnvironmentVariable(It.IsAny<string>())).Returns((string?)null);
        Environment.Setup(x => x.GetEnvironmentVariables()).Returns(new Hashtable
        {
            ["TF_BUILD"] = "false",
            ["CUSTOM"] = "inherited-fake",
        });
        Cancellation = new(environment: Environment.Object);
        LoggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(new NopLogger());
        Mock<IClock> clock = new();
        clock.SetupGet(x => x.UtcNow).Returns(new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero));
        Mock<ITask> tasks = CreateTrackedTask();
        Mock<IConfiguration> configuration = new();
        configuration.SetupGet(x => x[PlatformConfigurationConstants.PlatformCurrentWorkingDirectory]).Returns("controller-workspace");
        configuration.SetupGet(x => x[PlatformConfigurationConstants.PlatformTestHostControllersManagerSingleConnectionNamedPipeServerWaitConnectionTimeoutSeconds]).Returns("10");
        Mock<ITestApplicationModuleInfo> module = new();
        module.Setup(x => x.GetCurrentExecutableInfo()).Returns(new ExecutableInfo("fake-host", ["--filter", "name with spaces"], "workspace"));
        Mock<ITelemetryInformation> telemetryInformation = new();
        telemetryInformation.SetupGet(x => x.IsEnabled).Returns(telemetry);
        Telemetry.Setup(x => x.LogEventAsync(It.IsAny<string>(), It.IsAny<IDictionary<string, object>>(), It.IsAny<CancellationToken>()))
            .Callback<string, IDictionary<string, object>, CancellationToken>((name, values, _) => TelemetryEvents.Enqueue((name, new Dictionary<string, object>(values))))
            .Returns(Task.CompletedTask);

        ConfigureExtension(Handler, "lifetime");
        ConfigureExtension(Provider, "environment");
        ConfigureExtension(Completion, "completion");
        Handler.Setup(x => x.BeforeTestHostProcessStartAsync(It.IsAny<CancellationToken>()))
            .Callback(() => Events.Enqueue("before-process"))
            .Returns(Task.CompletedTask);
        Handler.Setup(x => x.OnTestHostProcessStartedAsync(It.IsAny<ITestHostProcessInformation>(), It.IsAny<CancellationToken>()))
            .Callback<ITestHostProcessInformation, CancellationToken>((info, _) =>
            {
                StartedInformation = info;
                Events.Enqueue("process-started");
                _started.TrySetResult(true);
            })
            .Returns(Task.CompletedTask);
        Handler.Setup(x => x.OnTestHostProcessExitedAsync(It.IsAny<ITestHostProcessInformation>(), It.IsAny<CancellationToken>()))
            .Callback<ITestHostProcessInformation, CancellationToken>((info, _) =>
            {
                ExitedInformation = info;
                Events.Enqueue("process-exited");
            })
            .Returns(async () =>
            {
                if (ownsCoverage)
                {
                    await Bus.PublishAsync(Handler.As<IDataProducer>().Object, new TestCoverageMessage(
                        new SessionUid("controller-session"), CoverageScope.Overall, CoverageMetric.Line, coveredLines, 100, "controller-collector"));
                }
            });
        Handler.As<IDisposable>().Setup(x => x.Dispose()).Callback(() => Events.Enqueue("dispose-lifetime"));
        if (ownsCoverage)
        {
            Handler.As<IDataProducer>().SetupGet(x => x.DataTypesProduced).Returns([typeof(TestCoverageMessage)]);
        }

        Provider.Setup(x => x.UpdateAsync(It.IsAny<IEnvironmentVariables>()))
            .Callback<IEnvironmentVariables>(values =>
            {
                Events.Enqueue("update-environment");
                values.SetVariable(new EnvironmentVariable("TF_BUILD", "false", isSecret: false, isLocked: false));
                values.SetVariable(new EnvironmentVariable("CUSTOM", "provider-value", isSecret: false, isLocked: false));
            })
            .Returns(Task.CompletedTask);
        Provider.Setup(x => x.ValidateTestHostEnvironmentVariablesAsync(It.IsAny<IReadOnlyEnvironmentVariables>()))
            .Callback(() => Events.Enqueue("validate-environment"))
            .ReturnsAsync(validEnvironment ? ValidationResult.Valid() : ValidationResult.Invalid("provider rejected"));
        Provider.As<IDisposable>().Setup(x => x.Dispose()).Callback(() => Events.Enqueue("dispose-environment"));
        Completion.Setup(x => x.OnRunCompletedAsync(It.IsAny<int>(), It.IsAny<IReadOnlyList<SessionFileArtifact>>(), It.IsAny<CancellationToken>()))
            .Callback<int, IReadOnlyList<SessionFileArtifact>, CancellationToken>((code, artifacts, _) =>
            {
                CompletionExitCode = code;
                CompletionArtifacts = artifacts.ToArray();
                Events.Enqueue("run-completed");
            })
            .Returns(Task.CompletedTask);
        Output.Setup(x => x.DisplayAsync(It.IsAny<IOutputDeviceDataProducer>(), It.IsAny<IOutputDeviceData>(), It.IsAny<CancellationToken>()))
            .Callback<IOutputDeviceDataProducer, IOutputDeviceData, CancellationToken>((_, data, _) => OutputMessages.Enqueue(data))
            .Returns(Task.CompletedTask);
        Output.Setup(x => x.DisplayAfterSessionEndRunAsync(It.IsAny<CancellationToken>()))
            .Callback(() => Events.Enqueue("output-finalized"))
            .Returns(Task.CompletedTask);
        Mock<IStopPoliciesService> stopPolicies = new();
        stopPolicies.Setup(x => x.ExecuteAbortCallbacksAsync()).Callback(() => Events.Enqueue("abort")).Returns(Task.CompletedTask);
        Dictionary<string, string[]> optionValues = [];
        if (lineThreshold is not null)
        {
            optionValues[PlatformCommandLineProvider.CoverageThresholdLineOptionKey] = [lineThreshold];
        }

        Options = new TestCommandLineOptions(optionValues);
        Services.AddServices(
        [
            Environment.Object, LoggerFactory.Object, clock.Object, tasks.Object, configuration.Object,
            module.Object, ProcessHandler.Object, telemetryInformation.Object, Telemetry.Object,
            Cancellation, Bus, new ProxyOutputDevice(Output.Object, null), stopPolicies.Object, Options,
            new TestCoverageCapabilities(), new TestCoverageResult(),
            Handler.Object, Provider.Object, Completion.Object,
        ]);
        Process.SetupGet(x => x.Id).Returns(606);
        Process.SetupGet(x => x.HasExited).Returns(() => Volatile.Read(ref _hasExited));
        Process.SetupGet(x => x.ExitCode).Returns(osExitCode);
        Process.Setup(x => x.WaitForExitAsync(It.IsAny<CancellationToken>())).Returns(_exited.Task);
        Process.Setup(x => x.Dispose()).Callback(() => Events.Enqueue("dispose-process"));
        ProcessHandler.Setup(x => x.Start(It.IsAny<ProcessStartInfo>()))
            .Callback<ProcessStartInfo>(info =>
            {
                StartInfo = info;
                Events.Enqueue("launch");
                StartPeer(info.EnvironmentVariables[EnvironmentVariableConstants.TESTINGPLATFORM_TESTHOSTCONTROLLER_PIPENAME + "_42"]!);
            })
            .Returns(Process.Object);
        ConfigureExtension(Launcher, "custom-launcher");
        Handle.SetupGet(x => x.Identifier).Returns("fake-remote-host");
        Handle.SetupGet(x => x.HasExited).Returns(() => Volatile.Read(ref _hasExited));
        Handle.SetupGet(x => x.ExitCode).Returns(osExitCode);
        Handle.Setup(x => x.WaitForExitAsync(It.IsAny<CancellationToken>())).Returns(_exited.Task);
        Handle.Setup(x => x.Dispose()).Callback(() => Events.Enqueue("dispose-handle"));
        Handle.As<ITestHostHandleExitCodePolicy>().SetupGet(x => x.IsExitCodeAuthoritative).Returns(authoritativeExitCode);
        Launcher.Setup(x => x.LaunchTestHostAsync(It.IsAny<TestHostLaunchContext>(), It.IsAny<CancellationToken>()))
            .Callback<TestHostLaunchContext, CancellationToken>((context, _) =>
            {
                LaunchContext = context;
                Events.Enqueue("launch");
                StartPeer(context.EnvironmentVariables[EnvironmentVariableConstants.TESTINGPLATFORM_TESTHOSTCONTROLLER_PIPENAME + "_42"]!);
            })
            .ReturnsAsync(Handle.Object);
        TestHostControllerConfiguration hostConfiguration = new([Provider.Object], [Handler.Object], [], customLauncher ? Launcher.Object : null, requireProcessRestart: true)
        {
            RunCompletionHandlers = [Completion.Object],
        };
        Host = new(hostConfiguration, Services, passiveNode: null, Environment.Object, LoggerFactory.Object, clock.Object);
    }

    public Mock<IEnvironment> Environment { get; } = new();

    public Mock<IProcess> Process { get; } = new();

    public Mock<IProcessHandler> ProcessHandler { get; } = new();

    public Mock<ITestHostLauncher> Launcher { get; } = new();

    public Mock<ITestHostHandle> Handle { get; } = new();

    public Mock<ITestHostProcessLifetimeHandler> Handler { get; } = new();

    public Mock<ITestHostEnvironmentVariableProvider> Provider { get; } = new();

    public Mock<ITestHostControllerRunCompletionHandler> Completion { get; } = new();

    public Mock<IPlatformOutputDevice> Output { get; } = new();

    public Mock<ILoggerFactory> LoggerFactory { get; } = new();

    public Mock<ITelemetryCollector> Telemetry { get; } = new();

    public CTRLPlusCCancellationTokenSource Cancellation { get; }

    public ServiceProvider Services { get; } = new();

    public MessageBusProxy Bus { get; } = new();

    public TestCommandLineOptions Options { get; }

    public TestHostControllersTestHost Host { get; }

    public ConcurrentQueue<string> Events { get; } = new();

    public ConcurrentQueue<IOutputDeviceData> OutputMessages { get; } = new();

    public ConcurrentQueue<(string Name, IDictionary<string, object> Values)> TelemetryEvents { get; } = new();

    public ProcessStartInfo? StartInfo { get; private set; }

    public TestHostLaunchContext? LaunchContext { get; private set; }

    public ITestHostProcessInformation? StartedInformation { get; private set; }

    public ITestHostProcessInformation? ExitedInformation { get; private set; }

    public int? CompletionExitCode { get; private set; }

    public SessionFileArtifact[] CompletionArtifacts { get; private set; } = [];

    public Task<int> RunAsync()
    {
        _run = Host.RunAsync();
        return _run;
    }

    public void Dispose() => DisposeAsync().GetAwaiter().GetResult();

    public async Task DisposeAsync()
    {
        _started.TrySetResult(true);
        _exited.TrySetResult(true);
        Volatile.Write(ref _hasExited, true);
        Cancellation.Cancel();
#if NETCOREAPP
        await _peerLifetime.CancelAsync();
#else
        _peerLifetime.Cancel();
#endif
        if (_run is { IsCompleted: false })
        {
            await _run.TimeoutAfterAsync(CleanupLimit);
        }

        if (_peer is not null)
        {
            await _peer.TimeoutAfterAsync(CleanupLimit);
        }

        await Task.WhenAll(_workers.ToArray()).TimeoutAfterAsync(CleanupLimit);
        Host.Dispose();
        Bus.Dispose();
        Cancellation.Dispose();
        _peerLifetime.Dispose();
    }

    private static void ConfigureExtension<T>(Mock<T> extension, string uid)
        where T : class, IExtension
    {
        extension.SetupGet(x => x.Uid).Returns(uid);
        extension.SetupGet(x => x.DisplayName).Returns(uid);
        extension.SetupGet(x => x.Description).Returns("Controller fixture extension.");
        extension.SetupGet(x => x.Version).Returns("1.2.3");
        extension.Setup(x => x.IsEnabledAsync()).ReturnsAsync(true);
    }

    private Mock<ITask> CreateTrackedTask()
    {
        Mock<ITask> tasks = new();
        tasks.Setup(x => x.Run(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()))
            .Returns<Func<Task>, CancellationToken>((action, token) => Track(Task.Run(action, token)));
        tasks.Setup(x => x.RunLongRunning(It.IsAny<Func<Task>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<Func<Task>, string, CancellationToken>((action, _, token) => Track(Task.Run(action, token)));
        tasks.Setup(x => x.WhenAll(It.IsAny<Task[]>())).Returns<Task[]>(Task.WhenAll);
        return tasks;
    }

    private Task Track(Task task)
    {
        _workers.Enqueue(task);
        return task;
    }

    private void StartPeer(string pipeName) => _peer = ExchangeAsync(pipeName);

    private async Task ExchangeAsync(string pipeName)
    {
        try
        {
            using NamedPipeClientStream stream = new(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            await stream.ConnectAsync(_peerLifetime.Token);
            await SendAsync(stream, 2, BitConverter.GetBytes(606));
            await _started.Task.TimeoutAfterAsync(OperationLimit);
            if (_sendCompletion)
            {
                using MemoryStream payload = new();
                using BinaryWriter writer = new(payload, Encoding.UTF8, leaveOpen: true);
                writer.Write(_reportedExitCode);
                writer.Write(_unfilteredExitCode);
                writer.Write(1);
                WriteString(writer, "controller-session");
                WriteString(writer, Path.Combine(Path.GetTempPath(), "controller-summary.trx"));
                WriteString(writer, "child summary");
                writer.Write(1);
                WriteString(writer, "child artifact");
                writer.Write(1);
                WriteString(writer, "microsoft.testing.trx");
                await SendAsync(stream, 1, payload.ToArray());
            }

            Volatile.Write(ref _hasExited, true);
            _exited.TrySetResult(true);
            Process.Raise(x => x.Exited += null!, EventArgs.Empty);
        }
        catch (OperationCanceledException) when (_peerLifetime.IsCancellationRequested)
        {
        }
        catch (IOException) when (_peerLifetime.IsCancellationRequested || _run?.IsFaulted == true)
        {
        }
    }

    private async Task SendAsync(Stream stream, int id, byte[] payload)
    {
        byte[] frame = [.. BitConverter.GetBytes(payload.Length + sizeof(int)), .. BitConverter.GetBytes(id), .. payload];
        await stream.WriteAsync(frame, 0, frame.Length, _peerLifetime.Token);
        await stream.FlushAsync(_peerLifetime.Token);
        byte[] response = new byte[8];
        int received = 0;
        while (received < response.Length)
        {
            int count = await stream.ReadAsync(response, received, response.Length - received, _peerLifetime.Token);
            if (count == 0)
            {
                throw new EndOfStreamException("Controller disconnected before acknowledging a request.");
            }

            received += count;
        }

        if (BitConverter.ToInt32(response, 0) != 4 || BitConverter.ToInt32(response, 4) != 0)
        {
            throw new InvalidDataException("Controller returned an unexpected protocol response.");
        }
    }

    private static void WriteString(BinaryWriter writer, string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }
}
