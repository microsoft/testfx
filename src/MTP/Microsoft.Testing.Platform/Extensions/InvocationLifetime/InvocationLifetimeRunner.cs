// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.IPC.Models;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Resources;
using Microsoft.Testing.Platform.ServerMode;

namespace Microsoft.Testing.Platform.Extensions.InvocationLifetime;

/// <summary>
/// Runs the invocation lifetime handlers of the process that owns the invocation. The runner owns the handlers,
/// including their disposal, because the hosts dispose their service provider before the cleanup can run.
/// </summary>
internal sealed class InvocationLifetimeRunner : IOutputDeviceDataProducer
{
    private readonly IReadOnlyList<IInvocationLifetimeHandler> _handlers;
    private readonly IEnvironment _environment;
    private readonly IOutputDevice _outputDevice;
    private readonly ILogger _logger;
    private readonly bool _setOwnerMarker;
    private readonly bool _restoreEnvironment;
    private readonly TimeSpan? _setupTimeout;
    private readonly IReadOnlyList<string>? _uncoordinatedHandlerUids;
    private readonly Dictionary<string, string?> _originalValues = [with(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal)];
    private readonly List<InvocationLifetimeOutput> _outputs = [];
    private int _enteredCount;
    private bool _setupStarted;
    private bool _captureOutputs;
    private string? _firstFailureMessage;
    private IInvocationLifetimeCoordinationChannel? _coordinationChannel;
    private bool _isCoordinatedSetupSucceeded;
    private Task<int>? _cleanupTask;

    public InvocationLifetimeRunner(
        IReadOnlyList<IInvocationLifetimeHandler> handlers,
        IEnvironment environment,
        IOutputDevice outputDevice,
        ILogger logger,
        bool setOwnerMarker,
        bool restoreEnvironment,
        TimeSpan? setupTimeout = null,
        bool isCoordinatedOwnerCandidate = false,
        IReadOnlyList<string>? uncoordinatedHandlerUids = null)
    {
        _handlers = handlers;
        _uncoordinatedHandlerUids = uncoordinatedHandlerUids;
        IsCoordinatedOwnerCandidate = isCoordinatedOwnerCandidate;
        _environment = environment;
        _outputDevice = outputDevice;
        _logger = logger;
        _setOwnerMarker = setOwnerMarker;
        _restoreEnvironment = restoreEnvironment;
        _setupTimeout = setupTimeout;
    }

    public string Uid => nameof(InvocationLifetimeRunner);

    public string Version => PlatformVersion.Version;

    public string DisplayName => "Invocation lifetime";

    public string Description => "Runs the invocation lifetime handlers.";

    public bool IsCleanupCompleted => _cleanupTask is { IsCompleted: true };

    /// <summary>
    /// Gets a value indicating whether dotnet test selected this module to own the coordinated invocation lifetime
    /// (<c>TESTINGPLATFORM_INVOCATIONLIFETIME_ROLE=owner</c>), so this process advertises its handlers during the
    /// handshake.
    /// </summary>
    public bool IsCoordinatedOwnerCandidate { get; }

    public bool IsCoordinated => _coordinationChannel is not null;

    /// <summary>
    /// Gets the value of the handshake property that advertises the handlers to dotnet test: a JSON string array of
    /// the UIDs of the enabled handlers.
    /// </summary>
    public string GetHandlersHandshakeValue()
    {
        StringBuilder builder = new("[");
        for (int i = 0; i < _handlers.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            InvocationLifetimeJson.AppendString(builder, _handlers[i].Uid);
        }

        return builder.Append(']').ToString();
    }

    /// <summary>
    /// Coordinates this runner with the other modules of the dotnet test invocation, after dotnet test accepted this
    /// process as the owner during the handshake. Must be called before the setup runs.
    /// </summary>
    public void EnableCoordination(IInvocationLifetimeCoordinationChannel coordinationChannel)
    {
        if (_setupStarted)
        {
            throw new InvalidOperationException("The invocation lifetime coordination must be enabled before the setup runs.");
        }

        _coordinationChannel = coordinationChannel;
    }

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    /// <summary>
    /// Runs the setup of every handler, in registration order.
    /// </summary>
    /// <returns><see langword="null"/> when the tests can run, otherwise the exit code of the invocation.</returns>
    public async Task<int?> RunSetupAsync(CancellationToken cancellationToken)
    {
        if (_setupStarted)
        {
            throw new InvalidOperationException("The invocation lifetime setup can only run once.");
        }

        _setupStarted = true;
        if (_uncoordinatedHandlerUids is not null)
        {
            foreach (string handlerUid in _uncoordinatedHandlerUids)
            {
                await TryDisplayAsync(new WarningMessageOutputDeviceData(
                    string.Format(CultureInfo.CurrentCulture, PlatformResources.InvocationLifetimeHandlerNotCoordinatedWarningMessage, handlerUid))).ConfigureAwait(false);
            }
        }

        int? result = await RunSetupCoreAsync(cancellationToken).ConfigureAwait(false);
        if (_coordinationChannel is not { } coordinationChannel)
        {
            return result;
        }

        try
        {
            // Reported even on failure or cancellation: dotnet test waits for it before launching the other modules.
            await coordinationChannel.SendInvocationSetupCompletedAsync(result is null, result is null ? null : _firstFailureMessage, _outputs).ConfigureAwait(false);
            _isCoordinatedSetupSucceeded = result is null;
        }
        catch (Exception ex) when (IsNonFatal(ex))
        {
            // dotnet test never learns that setup completed, so it cannot release the other modules: fail this module.
            await ReportFailureAsync(PlatformResources.InvocationLifetimeCoordinationFailedErrorMessage, ex).ConfigureAwait(false);
            result ??= (int)ExitCode.GenericFailure;
        }

        return result;
    }

    private async Task<int?> RunSetupCoreAsync(CancellationToken cancellationToken)
    {
        // A controller or orchestrator can time out before it launches the leaf test host. Scope that timeout to
        // setup so cancellation is observed by the handler without canceling the application token used by finalization.
        using CancellationTokenSource? setupCancellationTokenSource = _setupTimeout is not null
            ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
            : null;
        if (_setupTimeout is { } setupTimeout && setupCancellationTokenSource is not null)
        {
            setupCancellationTokenSource.CancelAfter(setupTimeout);
        }

        CancellationToken setupCancellationToken = setupCancellationTokenSource?.Token ?? cancellationToken;
        InvocationStartingContext context = new(SetEnvironmentVariable);
        _captureOutputs = _coordinationChannel is not null;
        try
        {
            int? result = await RunHandlersSetupAsync(context, setupCancellationToken).ConfigureAwait(false);
            if (result is not null)
            {
                return result;
            }
        }
        finally
        {
            _captureOutputs = false;
        }

        if (setupCancellationToken.IsCancellationRequested)
        {
            return (int)ExitCode.TestSessionAborted;
        }

        if (_setOwnerMarker)
        {
            SetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_INVOCATIONLIFETIME_OWNER, _environment.ProcessId.ToString(CultureInfo.InvariantCulture));
        }

        return null;
    }

    private async Task<int?> RunHandlersSetupAsync(InvocationStartingContext context, CancellationToken setupCancellationToken)
    {
        foreach (IInvocationLifetimeHandler handler in _handlers)
        {
            // A handler that observes the cancellation and returns normally must not let the next ones, or the
            // tests, start.
            if (setupCancellationToken.IsCancellationRequested)
            {
                return (int)ExitCode.TestSessionAborted;
            }

            // Counted before the call: a handler whose setup threw may have acquired part of its resources.
            _enteredCount++;
            try
            {
                await handler.OnInvocationStartingAsync(context, setupCancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (setupCancellationToken.IsCancellationRequested)
            {
                await TryLogAsync(() => _logger.LogDebugAsync($"Invocation lifetime handler '{handler.Uid}' setup was canceled.")).ConfigureAwait(false);
                return (int)ExitCode.TestSessionAborted;
            }
            catch (Exception ex) when (IsNonFatal(ex))
            {
                await ReportFailureAsync(PlatformResources.InvocationLifetimeSetupFailedErrorMessage, handler.Uid, ex).ConfigureAwait(false);
                return (int)ExitCode.GenericFailure;
            }
        }

        return null;
    }

    /// <summary>
    /// Runs the cleanup of every handler whose setup was entered, in reverse registration order, then disposes all
    /// the handlers. Subsequent host finalization paths observe the same result or exception.
    /// </summary>
    /// <returns>The exit code of the invocation, which becomes a failure when a cleanup failed after a success.</returns>
    public Task<int> RunCleanupAsync(int exitCode, bool isCanceled)
        => _cleanupTask ??= RunCleanupCoreAsync(exitCode, isCanceled);

    private async Task<int> RunCleanupCoreAsync(int exitCode, bool isCanceled)
    {
        if (!_isCoordinatedSetupSucceeded || _coordinationChannel is not { } coordinationChannel)
        {
            return await RunHandlersCleanupAsync(exitCode, isCanceled).ConfigureAwait(false);
        }

        // Coordinated owner: report this module's result, then run the cleanup once every module of the invocation
        // completed, with the aggregate verdict of the invocation. This module still exits with its own result.
        int moduleExitCode = exitCode;
        InvocationCleanupRequest? cleanupRequest = null;
        bool coordinationFailed = false;
        try
        {
            await coordinationChannel.SendModuleTestsCompletedAsync(moduleExitCode).ConfigureAwait(false);
            cleanupRequest = await coordinationChannel.WaitForInvocationCleanupRequestAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex) when (IsNonFatal(ex))
        {
            coordinationFailed = true;
            await ReportFailureAsync(PlatformResources.InvocationLifetimeCoordinationFailedErrorMessage, ex).ConfigureAwait(false);
        }

        if (cleanupRequest is null)
        {
            if (!coordinationFailed)
            {
                await ReportFailureAsync(PlatformResources.InvocationLifetimeCleanupNotRequestedErrorMessage, exception: null).ConfigureAwait(false);
            }

            // dotnet test went away: still release the resources. The invocation verdict is unknown, so the handlers
            // get the failure this module reports rather than a success the other modules never confirmed.
            int lostCoordinationExitCode = moduleExitCode == (int)ExitCode.Success ? (int)ExitCode.GenericFailure : moduleExitCode;
            int localExitCode = await RunHandlersCleanupAsync(lostCoordinationExitCode, isCanceled).ConfigureAwait(false);
            return localExitCode == (int)ExitCode.Success ? (int)ExitCode.GenericFailure : localExitCode;
        }

        _firstFailureMessage = null;
        await RunHandlersCleanupAsync(cleanupRequest.AggregateExitCode, cleanupRequest.IsCanceled).ConfigureAwait(false);
        bool cleanupFailed = _firstFailureMessage is not null;
        try
        {
            await coordinationChannel.SendInvocationCleanupCompletedAsync(!cleanupFailed, cleanupFailed ? _firstFailureMessage : null).ConfigureAwait(false);
        }
        catch (Exception ex) when (IsNonFatal(ex))
        {
            cleanupFailed = true;
            await ReportFailureAsync(PlatformResources.InvocationLifetimeCoordinationFailedErrorMessage, ex).ConfigureAwait(false);
        }

        return cleanupFailed && moduleExitCode == (int)ExitCode.Success
            ? (int)ExitCode.GenericFailure
            : moduleExitCode;
    }

    private async Task<int> RunHandlersCleanupAsync(int exitCode, bool isCanceled)
    {
        bool failed = false;

        // Every host turns a canceled run into TestSessionAborted, and a timeout surfaces in a parent process only as
        // that exit code: give the handlers the verdict the invocation actually exits with.
        if (isCanceled && exitCode == (int)ExitCode.GenericFailure)
        {
            exitCode = (int)ExitCode.TestSessionAborted;
        }

        isCanceled |= exitCode == (int)ExitCode.TestSessionAborted;
        InvocationCompletedContext context = new(exitCode, isCanceled);
        for (int i = _enteredCount - 1; i >= 0; i--)
        {
            IInvocationLifetimeHandler handler = _handlers[i];
            try
            {
                // Not canceled with the invocation: cleanup is exactly what must complete on cancellation.
                await handler.OnInvocationCompletedAsync(context, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex) when (IsNonFatal(ex))
            {
                failed = true;
                await ReportFailureAsync(PlatformResources.InvocationLifetimeCleanupFailedErrorMessage, handler.Uid, ex).ConfigureAwait(false);
            }
        }

        foreach (IInvocationLifetimeHandler handler in _handlers)
        {
            try
            {
                await DisposeHelper.DisposeAsync(handler).ConfigureAwait(false);
            }
            catch (Exception ex) when (IsNonFatal(ex))
            {
                failed = true;
                await ReportFailureAsync(PlatformResources.InvocationLifetimeCleanupFailedErrorMessage, handler.Uid, ex).ConfigureAwait(false);
            }
        }

        if (_setOwnerMarker && !_restoreEnvironment)
        {
            _environment.SetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_INVOCATIONLIFETIME_OWNER, null);
        }

        if (_restoreEnvironment)
        {
            foreach (KeyValuePair<string, string?> originalValue in _originalValues)
            {
                _environment.SetEnvironmentVariable(originalValue.Key, originalValue.Value);
            }

            _originalValues.Clear();
        }

        return failed && exitCode == (int)ExitCode.Success
            ? (int)ExitCode.GenericFailure
            : exitCode;
    }

    /// <summary>
    /// Disposes the handlers when the host exits before the setup started (for example on an incompatible protocol),
    /// without calling their cleanup. No-op once the setup started, because the cleanup owns the disposal then.
    /// </summary>
    public async Task ReleaseIfNotStartedAsync()
    {
        if (!_setupStarted)
        {
            await RunCleanupAsync((int)ExitCode.GenericFailure, isCanceled: false).ConfigureAwait(false);
        }
    }

    private void SetEnvironmentVariable(string name, string? value)
    {
        if (_captureOutputs)
        {
            // The last value wins, as it does for the environment of this process.
            _outputs.RemoveAll(output => string.Equals(output.Name, name, RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal));
            _outputs.Add(new InvocationLifetimeOutput(name, value is not null, value));
        }

        if (_restoreEnvironment && !_originalValues.ContainsKey(name))
        {
            _originalValues[name] = _environment.GetEnvironmentVariable(name);
        }

        _environment.SetEnvironmentVariable(name, value);
    }

    private Task ReportFailureAsync(string messageFormat, string handlerUid, Exception exception)
        => ReportFailureCoreAsync(string.Format(CultureInfo.CurrentCulture, messageFormat, handlerUid, exception.Message), exception);

    private Task ReportFailureAsync(string messageFormat, Exception? exception)
        => ReportFailureCoreAsync(
            exception is null ? messageFormat : string.Format(CultureInfo.CurrentCulture, messageFormat, exception.Message),
            exception);

    private async Task ReportFailureCoreAsync(string message, Exception? exception)
    {
        _firstFailureMessage ??= message;
        await TryDisplayAsync(new ErrorMessageOutputDeviceData(message)).ConfigureAwait(false);
        await TryLogAsync(() => _logger.LogErrorAsync(exception is null ? message : $"{message}{_environment.NewLine}{exception}")).ConfigureAwait(false);
    }

    private async Task TryDisplayAsync(IOutputDeviceData data)
    {
        try
        {
            await _outputDevice.DisplayAsync(this, data, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex) when (IsNonFatal(ex))
        {
            // Reporting is best-effort: it must not skip the cleanup of the remaining handlers.
        }
    }

    private static async Task TryLogAsync(Func<Task> logAsync)
    {
        try
        {
            await logAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (IsNonFatal(ex))
        {
            // Logging is best-effort: it must not skip the cleanup of the remaining handlers.
        }
    }

    private static bool IsNonFatal(Exception exception)
        => exception is AggregateException aggregateException
            ? aggregateException.Flatten().InnerExceptions.All(IsNonFatal)
            : exception is not OutOfMemoryException
                and not StackOverflowException
                and not AccessViolationException
                and not AppDomainUnloadedException
                and not BadImageFormatException;
}
