// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.Diagnostics.Helpers;
using Microsoft.Testing.Extensions.Diagnostics.Resources;
using Microsoft.Testing.Extensions.HangDump.Serializers;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Extensions.TestHostControllers;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.IPC.Models;
using Microsoft.Testing.Platform.IPC.Serializers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;

#if NETCOREAPP
using Microsoft.Diagnostics.NETCore.Client;
#endif

namespace Microsoft.Testing.Extensions.Diagnostics;

[UnsupportedOSPlatform("browser")]
[UnsupportedOSPlatform("ios")]
[UnsupportedOSPlatform("tvos")]
[UnsupportedOSPlatform("wasi")]
internal sealed class HangDumpProcessLifetimeHandler : ITestHostProcessLifetimeHandler, IOutputDeviceDataProducer, IDataProducer,
#if NETCOREAPP
    IAsyncDisposable,
#endif
    IDisposable
{
    private readonly IMessageBus _messageBus;
    private readonly OutputDeviceWriter _outputDisplay;
    private readonly ICommandLineOptions _commandLineOptions;
    private readonly ITask _task;
    private readonly IEnvironment _environment;
    private readonly IConfiguration _configuration;
    private readonly IProcessHandler _processHandler;
    private readonly IClock _clock;
    private readonly PipeNameDescription _pipeNameDescription;
    private readonly bool _traceEnabled;
    private readonly ILogger<HangDumpProcessLifetimeHandler> _logger;
    private readonly ManualResetEventSlim _waitConsumerPipeName = new(false);
    private readonly List<string> _dumpFiles = [];

    // Guards the "take the dump only once" gate (_dumpTaken) together with publishing the running
    // dump task (_activityIndicatorTask), so disposal always observes and awaits the winning dump.
#if NET9_0_OR_GREATER
    private readonly Lock _dumpLock = new();
#else
    private readonly object _dumpLock = new();
#endif

    private TimeSpan? _activityTimerValue;
    private Timer? _activityTimer;
    private DateTimeOffset? _deadlineDumpAt;
    private Timer? _deadlineTimer;

    /// <summary>
    /// <see cref="Timer"/> throws for due times above ~49.7 days (its internal limit is
    /// <see cref="uint.MaxValue"/> milliseconds).
    /// </summary>
    private static readonly TimeSpan MaxTimerDueTime = TimeSpan.FromMilliseconds(uint.MaxValue - 1);

    /// <summary>
    /// Upper bound for the optional in-progress-test query before taking a dump. A connected but
    /// wedged host never answers the request/reply, and the application token is not cancelled while
    /// the run is still "in progress" (which is exactly when the deadline dump fires), so an unbounded
    /// query would block the dump and kill indefinitely and consume the whole dump margin. The query
    /// is issued once per dump of the tree, not once per process, so this is the total worst case for
    /// the whole tree and stays a small slice of the default 30s dump margin however many processes
    /// are dumped; the healthy path answers in milliseconds.
    /// </summary>
    private static readonly TimeSpan InProgressTestsQueryTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan BestEffortDiagnosticsTimeout = TimeSpan.FromSeconds(1);

    private int _dumpTaken;
    private Task? _waitConnectionTask;
    private Task? _activityIndicatorTask;
    private NamedPipeServer? _singleConnectionNamedPipeServer;
    private string _dumpType = "Full";
    private string? _dumpFileNamePattern;
    private ITestHostProcessInformation? _testHostProcessInformation;
    private NamedPipeClient? _namedPipeClient;

    // Cancels the pipe handshake in OnTestHostProcessStartedAsync when a dump wins the race. Read and
    // written under _dumpLock, and null whenever no handshake is in flight. Cancelled outside the lock,
    // because cancellation runs the waiters' continuations inline and those continue into the handshake,
    // which takes _dumpLock again on its way out.
    private CancellationTokenSource? _handshakeCancellationTokenSource;

    internal static TimeSpan GetTimerDueTime(DateTimeOffset deadline, DateTimeOffset now)
    {
        TimeSpan remaining = deadline - now;
        return remaining <= TimeSpan.Zero
            ? TimeSpan.Zero
            : remaining > MaxTimerDueTime
                ? MaxTimerDueTime
                : remaining;
    }

    private void OnDeadlineTimerElapsed(CancellationToken cancellationToken)
    {
        TimeSpan dueTime = GetTimerDueTime(_deadlineDumpAt!.Value, _clock.UtcNow);
        if (dueTime > TimeSpan.Zero)
        {
            try
            {
                _deadlineTimer!.Change(dueTime, Timeout.InfiniteTimeSpan);
            }
            catch (ObjectDisposedException)
            {
                // Teardown won the race with this timer callback.
            }

            return;
        }

        TriggerDumpOnce(cancellationToken, triggeredByDeadline: true);
    }

    public HangDumpProcessLifetimeHandler(
        PipeNameDescription pipeNameDescription,
        IMessageBus messageBus,
        IOutputDevice outputDevice,
        ICommandLineOptions commandLineOptions,
        ITask task,
        IEnvironment environment,
        ILoggerFactory loggerFactory,
        IConfiguration configuration,
        IProcessHandler processHandler,
        IClock clock)
    {
        _logger = loggerFactory.CreateLogger<HangDumpProcessLifetimeHandler>();
        _traceEnabled = _logger.IsEnabled(LogLevel.Trace);
        _pipeNameDescription = pipeNameDescription;
        _messageBus = messageBus;
        _outputDisplay = new OutputDeviceWriter(outputDevice, this);
        _commandLineOptions = commandLineOptions;
        _task = task;
        _environment = environment;
        _configuration = configuration;
        _processHandler = processHandler;
        _clock = clock;
    }

    public string Uid => nameof(HangDumpProcessLifetimeHandler);

    public string Version => ExtensionVersion.DefaultSemVer;

    public string DisplayName => ExtensionResources.HangDumpExtensionDisplayName;

    public string Description => ExtensionResources.HangDumpExtensionDescription;

    public Type[] DataTypesProduced => [typeof(FileArtifact)];

    public Task<bool> IsEnabledAsync() => Task.FromResult(HangDumpOptions.IsEnabled(_commandLineOptions));

    public async Task BeforeTestHostProcessStartAsync(CancellationToken cancellationToken)
    {
        _activityTimerValue = _commandLineOptions.TryGetOptionArgumentList(HangDumpCommandLineProvider.HangDumpTimeoutOptionName, out string[]? timeout)
            ? TimeSpanParser.Parse(timeout[0])
            : TimeSpan.FromMinutes(30);

        if (_commandLineOptions.TryGetOptionArgumentList(HangDumpCommandLineProvider.HangDumpTypeOptionName, out string[]? dumpType))
        {
            _dumpType = dumpType[0];
        }
        else if (_commandLineOptions.TryGetOptionArgumentList(HangDumpCommandLineProvider.HangDumpTypeIfSupportedOptionName, out string[]? dumpTypeIfSupported))
        {
            // The "-if-supported" variant accepts the full set of dump types regardless of TFM
            // (see HangDumpCommandLineProvider.ValidateOptionArgumentsAsync). When the user
            // requests a value that the current runtime cannot honor, fall back to the closest
            // supported value (see MapToSupportedDumpType) and emit a single informational
            // message so the CI log makes the substitution visible without breaking the run.
            string requested = dumpTypeIfSupported[0];
            _dumpType = HangDumpCommandLineProvider.MapToSupportedDumpType(requested);
            if (!string.Equals(_dumpType, requested, StringComparison.OrdinalIgnoreCase))
            {
                await _outputDisplay.DisplayAsync(
                    new FormattedTextOutputDeviceData(string.Format(CultureInfo.InvariantCulture, ExtensionResources.HangDumpTypeIfSupportedFallbackInfoMessage, requested, _dumpType)),
                    cancellationToken).ConfigureAwait(false);
            }
        }

        if (_commandLineOptions.TryGetOptionArgumentList(HangDumpCommandLineProvider.HangDumpFileNameOptionName, out string[]? fileName))
        {
            _dumpFileNamePattern = fileName[0];
        }

        await _logger.LogInformationAsync($"Hang dump timeout setup {_activityTimerValue}.").ConfigureAwait(false);

        // In addition to the inactivity timeout above, honor an absolute CI deadline (if provided).
        // We compute the wall-clock instant at which we should start taking the dump so that the dump
        // has a chance to complete before the CI runner hard-kills the process.
        if (DeadlineHelper.TryGetDeadline(_environment, out DateTimeOffset deadline))
        {
            _deadlineDumpAt = DeadlineHelper.SubtractSaturating(deadline, DeadlineHelper.GetDumpMargin(_environment));
            await _logger.LogInformationAsync($"Hang dump deadline setup {_deadlineDumpAt:o}.").ConfigureAwait(false);
        }

        _singleConnectionNamedPipeServer = new(_pipeNameDescription, CallbackAsync, _environment, _logger, _task, cancellationToken);
        _singleConnectionNamedPipeServer.RegisterSerializer(new VoidResponseSerializer(), typeof(VoidResponse));
        _singleConnectionNamedPipeServer.RegisterSerializer(new ConsumerPipeNameRequestSerializer(), typeof(ConsumerPipeNameRequest));
        _singleConnectionNamedPipeServer.RegisterSerializer(new ActivitySignalRequestSerializer(), typeof(ActivitySignalRequest));

        _waitConnectionTask = _task.Run(
            async () =>
            {
                await _logger.LogDebugAsync($"Waiting for connection to {_singleConnectionNamedPipeServer.PipeName.Name}").ConfigureAwait(false);
                await _singleConnectionNamedPipeServer.WaitConnectionAsync(cancellationToken).TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout, cancellationToken).ConfigureAwait(false);
            }, cancellationToken);
    }

    private async Task<IResponse> CallbackAsync(IRequest request)
    {
        if (request is ConsumerPipeNameRequest consumerPipeNameRequest)
        {
            await _logger.LogDebugAsync($"Consumer pipe name received '{consumerPipeNameRequest.PipeName}'").ConfigureAwait(false);

            // exitProcessOnConnectionLoss: false, because this is an auxiliary channel. It carries nothing but
            // the best-effort in-progress-test query used to annotate a dump, and the peer is a test host we
            // are often about to dump and kill -- so a disconnect here is expected rather than fatal. With the
            // default (true) a host that drops while the query is in flight would call IEnvironment.Exit on
            // this controller, killing the very process that still has to take and publish the dump, and the
            // catch in QueryInProgressTestsWithTimeoutAsync would never run. Surfacing it as an exception lets
            // the query fall back to an empty list and the dump continue.
            _namedPipeClient = new NamedPipeClient(consumerPipeNameRequest.PipeName, _environment, exitProcessOnConnectionLoss: false);
            _namedPipeClient.RegisterSerializer(new GetInProgressTestsResponseSerializer(), typeof(GetInProgressTestsResponse));
            _namedPipeClient.RegisterSerializer(new GetInProgressTestsRequestSerializer(), typeof(GetInProgressTestsRequest));
            _namedPipeClient.RegisterSerializer(new VoidResponseSerializer(), typeof(VoidResponse));
            _waitConsumerPipeName.Set();
            return VoidResponse.CachedInstance;
        }
        else if (request is ActivitySignalRequest)
        {
            if (_traceEnabled)
            {
                _logger.LogTrace($"Activity signal received by the test host '{_clock.UtcNow}'");
            }

            _activityTimer?.Change(_activityTimerValue!.Value, TimeSpan.FromMilliseconds(-1));
            return VoidResponse.CachedInstance;
        }
        else
        {
            throw new ArgumentOutOfRangeException(string.Format(CultureInfo.InvariantCulture, ExtensionResources.HangDumpUnsupportedRequestTypeErrorMessage, request));
        }
    }

    public async Task OnTestHostProcessStartedAsync(ITestHostProcessInformation testHostProcessInformation, CancellationToken cancellationToken)
    {
        ApplicationStateGuard.Ensure(_waitConnectionTask is not null);
        ApplicationStateGuard.Ensure(_singleConnectionNamedPipeServer is not null);

        // Read the pipe server once, here, where the guard above proves it is set. The dereference is in a
        // nested try several awaits down, and a field can in principle change under those awaits, so a local
        // is what makes the null-safety contract hold at the point it is actually used.
        NamedPipeServer singleConnectionNamedPipeServer = _singleConnectionNamedPipeServer;

        _testHostProcessInformation = testHostProcessInformation;

        // The pipe handshake below must be interruptible by a dump. Killing the test host does not
        // complete this process's own WaitConnectionAsync -- that pipe is waiting for a client that will
        // now never connect -- so without this token a host that wedged before connecting keeps Started
        // blocked for DefaultHangTimeSpanTimeout (five minutes), far past the default 30s dump margin and
        // the CI hard deadline, and OnTestHostProcessExitedAsync never runs to publish the dump that was
        // taken. Published before the deadline timer is armed so a timer that fires immediately (a
        // deadline already in the past) still finds it.
        var handshakeCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        lock (_dumpLock)
        {
            _handshakeCancellationTokenSource = handshakeCancellation;
        }

        CancellationToken handshakeToken = handshakeCancellation.Token;

        try
        {
            // Arm the absolute CI deadline timer as early as possible, before we block on the pipe
            // handshake below. If the test host wedges during startup (never connects back over the
            // pipe), those waits would otherwise block well past the deadline and the deadline dump/kill
            // would never be armed, which defeats the purpose of the deadline. The dump path only needs
            // the test host PID, which we already have here; the in-progress-test list (which needs the
            // consumer pipe) is best-effort and skipped when the pipe never connected.
            if (_deadlineDumpAt is { } deadlineDumpAt)
            {
                _deadlineTimer = new Timer(
                    _ => OnDeadlineTimerElapsed(cancellationToken),
                    null,
                    Timeout.InfiniteTimeSpan,
                    TimeSpan.FromMilliseconds(-1));
                _deadlineTimer.Change(GetTimerDueTime(deadlineDumpAt, _clock.UtcNow), Timeout.InfiniteTimeSpan);
            }

            // Once a dump has started, the test host is being dumped and killed out from under this
            // handshake, so the pipe waits below will throw (cancellation, timeout, or a torn-down pipe).
            // Let Started return normally in that case so the lifetime handler still receives
            // OnTestHostProcessExitedAsync, which is where the dump files are published; otherwise a
            // deadline dump would be taken but never surfaced as an artifact.
            try
            {
                await _logger.LogDebugAsync($"Wait for test host connection to the server pipe '{singleConnectionNamedPipeServer.PipeName.Name}'").ConfigureAwait(false);
                await _waitConnectionTask.TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout, handshakeToken).ConfigureAwait(false);
                using CancellationTokenSource timeout = new(TimeoutHelper.DefaultHangTimeSpanTimeout);
                using var linkedCancellationToken = CancellationTokenSource.CreateLinkedTokenSource(handshakeToken, timeout.Token);
                _waitConsumerPipeName.Wait(linkedCancellationToken.Token);
                ApplicationStateGuard.Ensure(_namedPipeClient is not null);
                await _namedPipeClient.ConnectAsync(handshakeToken).TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout, handshakeToken).ConfigureAwait(false);
                await _logger.LogDebugAsync($"Connected to the test host server pipe '{_namedPipeClient.PipeName}'").ConfigureAwait(false);

                // The inactivity timer only makes sense once the host has connected and can send activity
                // signals; before that there is nothing to reset it. The deadline timer above is independent.
                _activityTimer = new Timer(
                    _ => TriggerDumpOnce(cancellationToken, triggeredByDeadline: false),
                    null,
                    _activityTimerValue!.Value,
                    TimeSpan.FromMilliseconds(-1));
            }
            catch (Exception ex) when (Volatile.Read(ref _dumpTaken) != 0)
            {
                // A dump is already in progress; the failed handshake is expected. Return normally so
                // OnTestHostProcessExitedAsync runs and publishes the dump that is being taken.
                await RunBestEffortDiagnosticAsync(
                    () => _logger.LogDebugAsync($"Test host handshake failed after the dump started; continuing so the dump can be published. {ex}"),
                    BestEffortDiagnosticsTimeout).ConfigureAwait(false);
            }
        }
        finally
        {
            // Unpublish before disposing, so a later dump reads null instead of a disposed source.
            lock (_dumpLock)
            {
                _handshakeCancellationTokenSource = null;
            }

            handshakeCancellation.Dispose();
        }
    }

    private static string GetDiskInfo()
    {
        var builder = new StringBuilder();
        DriveInfo[] allDrives = DriveInfo.GetDrives();

        foreach (DriveInfo d in allDrives)
        {
            builder.AppendLine(CultureInfo.InvariantCulture, $"Drive {d.Name}");
            if (d.IsReady)
            {
                builder.AppendLine(CultureInfo.InvariantCulture, $"  Available free space: {d.AvailableFreeSpace} bytes");
                builder.AppendLine(CultureInfo.InvariantCulture, $"  Total free space: {d.TotalFreeSpace} bytes");
                builder.AppendLine(CultureInfo.InvariantCulture, $"  Total size: {d.TotalSize} bytes");
            }
        }

        return builder.ToString();
    }

    public async Task OnTestHostProcessExitedAsync(ITestHostProcessInformation testHostProcessInformation, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_activityTimer is not null)
        {
#if NETCOREAPP
            await _activityTimer.DisposeAsync().ConfigureAwait(false);
#else
            _activityTimer.Dispose();
#endif
        }

        if (_deadlineTimer is not null)
        {
#if NETCOREAPP
            await _deadlineTimer.DisposeAsync().ConfigureAwait(false);
#else
            _deadlineTimer.Dispose();
#endif
        }

        Task? activityIndicatorTask;
        lock (_dumpLock)
        {
            // Timer.DisposeAsync waits for the timer callback, but TriggerDumpOnce returns as soon as it
            // publishes the actual dump task. Capture and await that task before enumerating its artifacts.
            _dumpTaken = 1;
            activityIndicatorTask = _activityIndicatorTask;
        }

        if (activityIndicatorTask is not null)
        {
            await activityIndicatorTask.TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout).ConfigureAwait(false);
        }

        if (!testHostProcessInformation.HasExitedGracefully)
        {
            _logger.LogDebug($"Testhost didn't exit gracefully '{testHostProcessInformation.ExitCode}')");
        }

        foreach (string dumpFile in _dumpFiles)
        {
            await _messageBus.PublishAsync(this, new FileArtifact(new FileInfo(dumpFile), ExtensionResources.HangDumpArtifactDisplayName, ExtensionResources.HangDumpArtifactDescription)).ConfigureAwait(false);
        }
    }

    [UnsupportedOSPlatform("browser")]
    [UnsupportedOSPlatform("ios")]
    [UnsupportedOSPlatform("tvos")]
    [UnsupportedOSPlatform("wasi")]
    private void TriggerDumpOnce(CancellationToken cancellationToken, bool triggeredByDeadline)
    {
        // The inactivity timer and the deadline timer can both fire, and disposal can run
        // concurrently. Claim the gate and publish the running dump task under the same lock, so both
        // disposal paths (which take the lock, claim the gate, and capture _activityIndicatorTask)
        // always observe and await the winning dump instead of tearing down the pipes underneath it.
        CancellationTokenSource? handshakeCancellation;
        lock (_dumpLock)
        {
            if (_dumpTaken != 0)
            {
                return;
            }

            _dumpTaken = 1;
            _activityIndicatorTask = TakeDumpOfTreeAsync(cancellationToken, triggeredByDeadline);
            handshakeCancellation = _handshakeCancellationTokenSource;
        }

        // Interrupt the pipe handshake, if one is still in flight. We are about to dump and kill the test
        // host, and a host that wedged before connecting leaves that handshake waiting for a connection
        // that will never arrive; killing it does not complete our own wait, so nothing else would end it
        // before DefaultHangTimeSpanTimeout and the dump would never reach OnTestHostProcessExitedAsync
        // to be published. Cancelled outside _dumpLock on purpose: the waiters' continuations can run
        // inline here, and they continue into a handshake that takes _dumpLock again on its way out.
        try
        {
            handshakeCancellation?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // The handshake finished and disposed the source between the read above and this call, so
            // there is nothing left to interrupt.
        }
    }

    private async Task TakeDumpOfTreeAsync(CancellationToken cancellationToken, bool triggeredByDeadline)
    {
        // This method is started synchronously inside the _dumpLock (see TriggerDumpOnce), which also
        // publishes the returned task into _activityIndicatorTask. Yield immediately so none of the
        // dump work runs while the lock is held: control returns to the caller, the task field is
        // observed, and the lock is released before the (potentially slow) dump proceeds. HangDump runs
        // out-of-process on a full runtime, so yielding to the thread pool here is safe.
        await Task.Yield();

        ApplicationStateGuard.Ensure(_testHostProcessInformation is not null);

        string dumpReason = triggeredByDeadline
            ? $"CI deadline approaching (dump scheduled at {_deadlineDumpAt:o})"
            : $"Hang dump timeout({_activityTimerValue}) expired";

        // Announcing the dump is diagnostics only, and it runs before the try/finally that kills the
        // process tree. Loggers and output devices propagate exceptions, so letting one escape here would
        // fault the dump task and leave the wedged host alive with no dump at all -- the exact situation
        // this handler exists to resolve. Report the failure and take the dump anyway.
        await RunBestEffortDiagnosticAsync(
            () => _logger.LogInformationAsync($"{dumpReason}. Taking hang dump."),
            BestEffortDiagnosticsTimeout).ConfigureAwait(false);
        await RunBestEffortDiagnosticAsync(
            () => _outputDisplay.DisplayAsync(
                new ErrorMessageOutputDeviceData(triggeredByDeadline
                    ? ExtensionResources.HangDumpDeadlineApproaching
                    : string.Format(CultureInfo.InvariantCulture, ExtensionResources.HangDumpTimeoutExpired, _activityTimerValue)),
                cancellationToken),
            BestEffortDiagnosticsTimeout).ConfigureAwait(false);

        using IProcess process = _processHandler.GetProcessById(_testHostProcessInformation.PID);

        // Walking the tree writes diagnostics through the same logger and output device, so deadline-driven
        // enumeration gets a short bound. Fall back to the root test host process: dumping and killing at least
        // that one is what unblocks the run.
        TimeSpan processTreeTimeout = triggeredByDeadline
            ? BestEffortDiagnosticsTimeout
            : TimeoutHelper.DefaultHangTimeSpanTimeout;
        List<ProcessTreeNode> processTree = await GetProcessTreeWithTimeoutAsync(
            token => process.GetProcessTreeAsync(_logger, _outputDisplay, token),
            processTreeTimeout,
            ex => _logger.LogErrorAsync("Could not enumerate the test host process tree. Falling back to the root test host process.", ex),
            process,
            cancellationToken).ConfigureAwait(false);
        processTree = processTree.Where(p => p.Process?.Name is not null and not "conhost" and not "WerFault").ToList();

        IEnumerable<IProcess> bottomUpTree = processTree.OrderByDescending(t => t.Level).Select(t => t.Process).OfType<IProcess>();

        try
        {
            if (processTree.Count > 1)
            {
                string processTreeDisplay = string.Join(
                    Environment.NewLine,
                    processTree
                        .OrderBy(t => t.Level)
                        .Select(p => $"{(p.Level != 0 ? " + " : " > ")}{new string('-', p.Level)} {p.Process!.Id} - {p.Process.Name}"));
                await RunBestEffortDiagnosticAsync(
                    () => _outputDisplay.DisplayAsync(
                        new ErrorMessageOutputDeviceData($"{ExtensionResources.DumpingProcessTree}{Environment.NewLine}{processTreeDisplay}"),
                        cancellationToken),
                    BestEffortDiagnosticsTimeout).ConfigureAwait(false);
            }
            else
            {
                await RunBestEffortDiagnosticAsync(
                    () => _outputDisplay.DisplayAsync(new ErrorMessageOutputDeviceData(string.Format(CultureInfo.InvariantCulture, ExtensionResources.DumpingProcess, process.Id, process.Name)), cancellationToken),
                    BestEffortDiagnosticsTimeout).ConfigureAwait(false);
            }

            await RunBestEffortDiagnosticAsync(
                () => _logger.LogInformationAsync($"{dumpReason}."),
                BestEffortDiagnosticsTimeout).ConfigureAwait(false);

            await QueryOnceAndDumpTreeAsync(
                bottomUpTree,
                GetInProgressTestsAsync,
                async (p, inProgressTests, ct) =>
                {
                    try
                    {
                        await TakeDumpAsync(p, inProgressTests, ct).ConfigureAwait(false);
                    }
                    catch (Exception e)
                    {
                        await RunBestEffortDiagnosticAsync(
                            () => _logger.LogErrorAsync($"Error while taking dump of process {p.Id} - {p.Name}", e),
                            BestEffortDiagnosticsTimeout).ConfigureAwait(false);
                        await RunBestEffortDiagnosticAsync(
                            () => _outputDisplay.DisplayAsync(new ErrorMessageOutputDeviceData(string.Format(CultureInfo.InvariantCulture, ExtensionResources.ErrorWhileDumpingProcess, p.Id, p.Name, e)), ct),
                            BestEffortDiagnosticsTimeout).ConfigureAwait(false);
                    }
                },
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            NotifyCrashDumpServiceIfEnabled();

            // Some of the processes might crashed, which breaks the process tree (on windows it is just an illusion),
            // so try extra hard to kill all the known processes in the tree, since we already spent a bunch of time getting
            // to know which processes are involved.
            foreach (ProcessTreeNode node in processTree)
            {
                IProcess? p = node.Process;
                if (p == null)
                {
                    continue;
                }

                try
                {
                    if (!p.HasExited)
                    {
                        p.Kill();
                        await p.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
                    }
                }
                catch (Exception e)
                {
                    await RunBestEffortDiagnosticAsync(
                        () => _logger.LogErrorAsync($"Problem killing {p.Id} - {p.Name}", e),
                        BestEffortDiagnosticsTimeout).ConfigureAwait(false);
                    await RunBestEffortDiagnosticAsync(
                        () => _outputDisplay.DisplayAsync(new ErrorMessageOutputDeviceData(string.Format(CultureInfo.InvariantCulture, ExtensionResources.ErrorKillingProcess, p.Id, p.Name, e)), cancellationToken),
                        BestEffortDiagnosticsTimeout).ConfigureAwait(false);
                }
            }
        }
    }

    internal static async Task RunBestEffortDiagnosticAsync(Func<Task> diagnosticAsync, TimeSpan timeout)
    {
        try
        {
            await diagnosticAsync().TimeoutAfterAsync(timeout).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Diagnostics must never prevent the dump or the process-tree kill.
        }
    }

    internal static async Task<List<ProcessTreeNode>> GetProcessTreeWithTimeoutAsync(
        Func<CancellationToken, Task<List<ProcessTreeNode>>> getProcessTreeAsync,
        TimeSpan timeout,
        Func<Exception, Task> logFailureAsync,
        IProcess rootProcess,
        CancellationToken cancellationToken)
    {
        using var timeoutCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCancellationTokenSource.CancelAfter(timeout);

        try
        {
            Task<List<ProcessTreeNode>> processTreeTask = getProcessTreeAsync(timeoutCancellationTokenSource.Token);
            await processTreeTask.TimeoutAfterAsync(timeout, cancellationToken).ConfigureAwait(false);
            return await processTreeTask.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await RunBestEffortDiagnosticAsync(
                () => logFailureAsync(ex),
                BestEffortDiagnosticsTimeout).ConfigureAwait(false);
            return [new ProcessTreeNode { Process = rootProcess, Level = 0 }];
        }
    }

    /// <summary>
    /// Asks <paramref name="queryInProgressTestsAsync"/> once and then dumps every process in
    /// <paramref name="bottomUpTree"/>, annotating each dump with that single answer.
    /// </summary>
    /// <remarks>
    /// The in-progress-test list describes the test host, so it is the same for every process in the tree,
    /// while the query is bounded by <see cref="InProgressTestsQueryTimeout"/>. Asking per process would
    /// multiply that bound by the size of the tree, and with a wedged consumer pipe a six-process tree would
    /// spend the entire default 30s dump margin waiting before a single dump is written.
    /// This is a separate method so that guarantee can be exercised with a fake tree and a stalled query:
    /// <see cref="TakeDumpOfTreeAsync"/> itself dumps and then kills every process it walks, so a test
    /// cannot drive it against a real process tree.
    /// </remarks>
    internal static async Task QueryOnceAndDumpTreeAsync(
        IEnumerable<IProcess> bottomUpTree,
        Func<CancellationToken, Task<(string, int)[]>> queryInProgressTestsAsync,
        Func<IProcess, (string, int)[], CancellationToken, Task> dumpProcessAsync,
        CancellationToken cancellationToken)
    {
        (string, int)[] inProgressTests = await queryInProgressTestsAsync(cancellationToken).ConfigureAwait(false);

        // Do not suspend processes with NetClient dumper it stops the diagnostic thread running in
        // them and hang dump request will get stuck forever, because the process is not co-operating.
        // Instead we start one task per dump asynchronously, and hope that the parent process will start dumping
        // before the child process is done dumping. This way if the parent is waiting for the children to exit,
        // we will be dumping it before it observes the child exiting and we get a more accurate results. If we did not
        // do this, then parent that is awaiting child might exit before we get to dumping it.
        List<Task> dumpTasks = [];
        foreach (IProcess p in bottomUpTree)
        {
            dumpTasks.Add(dumpProcessAsync(p, inProgressTests, cancellationToken));
        }

        await Task.WhenAll(dumpTasks).ConfigureAwait(false);
    }

    /// <summary>
    /// Asks the test host which tests are still running, so the dump can be annotated with them.
    /// </summary>
    /// <remarks>
    /// Called once per dump operation, not once per process: the answer describes the test host and the query
    /// is bounded by <see cref="InProgressTestsQueryTimeout"/>, so repeating it for every process in the tree
    /// would multiply that wait by the tree size and eat the dump margin.
    /// The consumer pipe is only usable once the test host connected back over it. A non-null client is not
    /// enough: it is created when the host sends its pipe name but only connected later, so a deadline dump
    /// firing in that window (or a host that wedged during startup) would hit an unconnected pipe. The list is
    /// therefore best-effort -- a connected-but-wedged host never replies and the app token is not cancelled
    /// mid-run, so any failure is logged and swallowed and an empty list is returned, and it can never block
    /// taking the dump and killing the tree.
    /// </remarks>
    private Task<(string, int)[]> GetInProgressTestsAsync(CancellationToken cancellationToken)
    {
        NamedPipeClient? namedPipeClient = _namedPipeClient;
        return namedPipeClient is null
            ? Task.FromResult<(string, int)[]>([])
            : QueryInProgressTestsWithTimeoutAsync(
                async queryCancellationToken =>
                {
                    GetInProgressTestsResponse tests = await namedPipeClient.RequestReplyAsync<GetInProgressTestsRequest, GetInProgressTestsResponse>(new GetInProgressTestsRequest(), queryCancellationToken).ConfigureAwait(false);
                    return tests.Tests;
                },
                InProgressTestsQueryTimeout,
                ex => _logger.LogDebugAsync($"Could not collect the in-progress tests before dumping (the consumer pipe may not be connected, or the host did not reply within {InProgressTestsQueryTimeout}). Continuing with the dump. {ex}"),
                cancellationToken);
    }

    /// <summary>
    /// Runs <paramref name="requestInProgressTestsAsync"/> under a bound of <paramref name="timeout"/>, and
    /// returns an empty list if it does not answer in time or fails -- including when reporting that failure
    /// itself fails.
    /// </summary>
    /// <remarks>
    /// The bound lives here rather than in the caller's delegate so it is the product, not the caller, that
    /// gives up on a connected-but-wedged host: the application token is not cancelled while the run is still
    /// "in progress", which is exactly when the deadline dump fires, so an unbounded request/reply would block
    /// the dump and the kill indefinitely and consume the whole dump margin.
    /// This is a separate method so that bound can be exercised with a reply that never arrives: the real
    /// request/reply goes over a named pipe to another process, which a unit test cannot stand up.
    /// </remarks>
    internal static async Task<(string, int)[]> QueryInProgressTestsWithTimeoutAsync(
        Func<CancellationToken, Task<(string, int)[]>> requestInProgressTestsAsync,
        TimeSpan timeout,
        Func<Exception, Task> logFailureAsync,
        CancellationToken cancellationToken)
    {
        try
        {
            using var queryCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            queryCts.CancelAfter(timeout);
            Task<(string, int)[]> queryTask = requestInProgressTestsAsync(queryCts.Token);
            await queryTask.TimeoutAfterAsync(timeout, cancellationToken).ConfigureAwait(false);
            return await queryTask.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // The empty-list fallback is the whole point of this method, so it must survive a failing
            // diagnostic too. logFailureAsync is a logger call and logger providers can fail; letting that
            // throw would escape the caller, which is explicitly best-effort, and skip the dump entirely.
            await RunBestEffortDiagnosticAsync(() => logFailureAsync(ex), BestEffortDiagnosticsTimeout).ConfigureAwait(false);

            return [];
        }
    }

    private async Task TakeDumpAsync(IProcess process, (string, int)[] inProgressTests, CancellationToken cancellationToken)
    {
        ApplicationStateGuard.Ensure(_testHostProcessInformation is not null);
        ApplicationStateGuard.Ensure(_dumpType is not null);

        string processId = process.Id.ToString(CultureInfo.InvariantCulture);
        Dictionary<string, string> replacements = ArtifactNamingHelper.GetStandardReplacements(process.Name, processId, _clock.UtcNow);

        string pattern = _dumpFileNamePattern ?? $"{process.Name}_%p_hang.dmp";

        // First resolve {placeholder} templates, then handle legacy %p pattern for backward compatibility.
        string finalDumpFileName = ArtifactNamingHelper.ResolveTemplate(pattern, replacements)
            .Replace("%p", processId);
        string resultsDirectory = Path.GetFullPath(_configuration.GetTestResultDirectory());
        finalDumpFileName = Path.GetFullPath(Path.Combine(resultsDirectory, finalDumpFileName));

        // Reject resolved paths that escape the results directory (e.g. rooted paths or ".." segments).
        // Append a trailing separator to prevent sibling-directory bypass (e.g. "/tmp/results" vs "/tmp/results-evil").
        // Use case-insensitive comparison on Windows where paths are case-insensitive.
        StringComparison pathComparison = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        string separatorStr = Path.DirectorySeparatorChar.ToString();
        string resultsDirectoryGuard = resultsDirectory.EndsWith(separatorStr, StringComparison.Ordinal)
            ? resultsDirectory
            : resultsDirectory + separatorStr;
        if (!finalDumpFileName.StartsWith(resultsDirectoryGuard, pathComparison))
        {
            throw new InvalidOperationException($"The resolved dump file path '{finalDumpFileName}' is outside the results directory '{resultsDirectory}'. Ensure --hangdump-filename is a relative path without '..' segments.");
        }

        // Ensure the destination directory exists (templates may include directory separators, e.g. {asm}/{pname}).
        Directory.CreateDirectory(Path.GetDirectoryName(finalDumpFileName)!);

        // The in-progress tests were queried once for the whole dump operation (see GetInProgressTestsAsync);
        // write them next to this dump so the dump can be read together with what was running.
        if (inProgressTests.Length > 0)
        {
            try
            {
                string hangTestsFileName = Path.ChangeExtension(finalDumpFileName, ".log");
                using (FileStream fs = File.OpenWrite(hangTestsFileName))
                using (StreamWriter sw = new(fs))
                {
                    string inProgressTestsDisplay = string.Join(
                        Environment.NewLine,
                        inProgressTests.Select(test => $"[{TimeSpan.FromSeconds(test.Item2)}] {test.Item1}"));
                    await RunBestEffortDiagnosticAsync(
                        () => _outputDisplay.DisplayAsync(
                            new ErrorMessageOutputDeviceData($"{ExtensionResources.RunningTestsWhileDumping}{Environment.NewLine}{inProgressTestsDisplay}"),
                            cancellationToken),
                        BestEffortDiagnosticsTimeout).ConfigureAwait(false);
                    foreach ((string testName, int seconds) in inProgressTests)
                    {
                        await sw.WriteLineAsync($"[{TimeSpan.FromSeconds(seconds)}] {testName}").ConfigureAwait(false);
                    }
                }

                await _messageBus.PublishAsync(this, new FileArtifact(new FileInfo(hangTestsFileName), ExtensionResources.HangTestListArtifactDisplayName, ExtensionResources.HangTestListArtifactDescription)).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // Writing the list is a convenience; it must never block taking the dump and killing the tree.
                await RunBestEffortDiagnosticAsync(
                    () => _logger.LogDebugAsync($"Could not write the in-progress tests next to the dump of process {process.Id}. Continuing with the dump. {ex}"),
                    BestEffortDiagnosticsTimeout).ConfigureAwait(false);
            }
        }

        await RunBestEffortDiagnosticAsync(
            () => _logger.LogInformationAsync($"Creating dump filename {finalDumpFileName}"),
            BestEffortDiagnosticsTimeout).ConfigureAwait(false);

        await RunBestEffortDiagnosticAsync(
            () => _outputDisplay.DisplayAsync(new ErrorMessageOutputDeviceData(string.Format(CultureInfo.InvariantCulture, ExtensionResources.CreatingDumpFile, finalDumpFileName)), cancellationToken),
            BestEffortDiagnosticsTimeout).ConfigureAwait(false);

#if NETCOREAPP
        DiagnosticsClient diagnosticsClient = new(process.Id);
        DumpType? dumpType = _dumpType.ToLowerInvariant().Trim() switch
        {
            "mini" => DumpType.Normal,
            "heap" => DumpType.WithHeap,
            "triage" => DumpType.Triage,
            "full" => DumpType.Full,
            "none" => null,
            _ => throw ApplicationStateGuard.Unreachable(),
        };

        DumpFileNames dumpFileNames = GetDumpFileNames(finalDumpFileName);

        try
        {
            // Skip creating the dump if the option is set to none, and just kill the process.
            if (dumpType.HasValue)
            {
                diagnosticsClient.WriteDump(dumpType.Value, dumpFileNames.WriteDumpFileName, logDumpGeneration: false);
                _dumpFiles.Add(dumpFileNames.ArtifactDumpFileName);
            }
        }
        catch (Exception e)
        {
            await _logger.LogErrorAsync($"Error while writing dump of process {process.Name} {process.Id}", e).ConfigureAwait(false);
            await _outputDisplay.DisplayAsync(new ErrorMessageOutputDeviceData(string.Format(CultureInfo.InvariantCulture, ExtensionResources.ErrorWhileDumpingProcess, process.Id, process.Name, e)), cancellationToken).ConfigureAwait(false);
        }

#else
        MiniDumpWriteDump.MiniDumpTypeOption? miniDumpTypeOption = _dumpType.ToLowerInvariant().Trim() switch
        {
            "mini" => MiniDumpWriteDump.MiniDumpTypeOption.Mini,
            "heap" => MiniDumpWriteDump.MiniDumpTypeOption.Heap,
            "full" => MiniDumpWriteDump.MiniDumpTypeOption.Full,
            "none" => null,
            _ => throw ApplicationStateGuard.Unreachable(),
        };

        try
        {
            // Skip creating the dump if the option is set to none, and just kill the process.
            if (miniDumpTypeOption.HasValue)
            {
                MiniDumpWriteDump.CollectDumpUsingMiniDumpWriteDump(process.Id, finalDumpFileName, miniDumpTypeOption.Value);
                _dumpFiles.Add(finalDumpFileName);
            }
        }
        catch (Exception e)
        {
            await _logger.LogErrorAsync($"Error while writing dump of process {process.Name} {process.Id}", e).ConfigureAwait(false);
            await _outputDisplay.DisplayAsync(new ErrorMessageOutputDeviceData(string.Format(CultureInfo.InvariantCulture, ExtensionResources.ErrorWhileDumpingProcess, process.Id, process.Name, e)), cancellationToken).ConfigureAwait(false);
        }
#endif
    }

    // Wrap the dump path into "" when it has space in it, this is a workaround for this runtime issue: https://github.com/dotnet/diagnostics/issues/5020
    // It only affects windows. Otherwise the dump creation fails with: [createdump] The pid argument is no longer supported
    internal static DumpFileNames GetDumpFileNames(string dumpFileName)
        => new(
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && dumpFileName.Contains(' ')
                ? $"\"{dumpFileName}\""
                : dumpFileName,
            dumpFileName);

    internal readonly record struct DumpFileNames(string WriteDumpFileName, string ArtifactDumpFileName);

    private static void NotifyCrashDumpServiceIfEnabled()
        => AppDomain.CurrentDomain.SetData("ProcessKilledByHangDump", "true");

    public void Dispose()
    {
        // Stop the deadline and inactivity timers so no callback can start a new dump while we tear
        // down the pipes. The happy path disposes them in OnTestHostProcessExitedAsync, but that runs
        // only on a clean exit; Ctrl+C or an exception skips it, so dispose here too (Timer.Dispose is
        // idempotent, so disposing twice is safe).
        _deadlineTimer?.Dispose();
        _activityTimer?.Dispose();

        Task? activityIndicatorTask;
        lock (_dumpLock)
        {
            // Claim the gate so no timer callback can start a new dump once we begin tearing down the
            // pipes, and capture any dump already in flight so we wait for it below.
            _dumpTaken = 1;
            activityIndicatorTask = _activityIndicatorTask;
        }

        if (activityIndicatorTask is not null)
        {
            bool waitResult;
            try
            {
                waitResult = activityIndicatorTask.Wait(TimeoutHelper.DefaultHangTimeSpanTimeout);
            }
            catch (Exception e)
            {
                _outputDisplay.DisplayAsync(new ErrorMessageOutputDeviceData(string.Format(CultureInfo.InvariantCulture, ExtensionResources.HangDumpFailed, e.ToString(), GetDiskInfo())), CancellationToken.None).GetAwaiter().GetResult();
                throw;
            }

            if (!waitResult)
            {
                throw new InvalidOperationException($"_activityIndicatorTask didn't exit in {TimeoutHelper.DefaultHangTimeSpanTimeout} seconds");
            }
        }

        _namedPipeClient?.Dispose();
        _waitConsumerPipeName.Dispose();
        _singleConnectionNamedPipeServer?.Dispose();
    }

#if NETCOREAPP
    public async ValueTask DisposeAsync()
    {
        // Stop the deadline and inactivity timers so no callback can start a new dump while we tear
        // down the pipes. The happy path disposes them in OnTestHostProcessExitedAsync, but that runs
        // only on a clean exit; Ctrl+C or an exception skips it, so dispose here too (Timer.Dispose is
        // idempotent, so disposing twice is safe).
        _deadlineTimer?.Dispose();
        _activityTimer?.Dispose();

        Task? activityIndicatorTask;
        lock (_dumpLock)
        {
            // Claim the gate so no timer callback can start a new dump once we begin tearing down the
            // pipes, and capture any dump already in flight so we await it below.
            _dumpTaken = 1;
            activityIndicatorTask = _activityIndicatorTask;
        }

        if (activityIndicatorTask is not null)
        {
            try
            {
                await activityIndicatorTask.TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                await _outputDisplay.DisplayAsync(new ErrorMessageOutputDeviceData(string.Format(CultureInfo.InvariantCulture, ExtensionResources.HangDumpFailed, e.ToString(), GetDiskInfo())), CancellationToken.None).ConfigureAwait(false);
                throw;
            }
        }

        _namedPipeClient?.Dispose();
        _waitConsumerPipeName.Dispose();
        _singleConnectionNamedPipeServer?.Dispose();
    }
#endif
}

internal sealed class OutputDeviceWriter
{
    private readonly IOutputDevice _outputDevice;
    private readonly IOutputDeviceDataProducer _outputDeviceDataProducer;

    public OutputDeviceWriter(IOutputDevice outputDevice, IOutputDeviceDataProducer outputDeviceDataProducer)
    {
        _outputDevice = outputDevice;
        _outputDeviceDataProducer = outputDeviceDataProducer;
    }

    /// <summary>
    /// Displays the output data asynchronously, using the stored producer.
    /// </summary>
    /// <param name="data">The output data.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task DisplayAsync(IOutputDeviceData data, CancellationToken cancellationToken)
        => await _outputDevice.DisplayAsync(_outputDeviceDataProducer, data, cancellationToken).ConfigureAwait(false);
}
