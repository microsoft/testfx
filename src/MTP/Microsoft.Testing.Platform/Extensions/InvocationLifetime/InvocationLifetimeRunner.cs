// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Resources;

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
    private readonly Dictionary<string, string?> _originalValues = [with(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal)];
    private int _enteredCount;
    private bool _setupStarted;
    private int? _cleanupExitCode;

    public InvocationLifetimeRunner(
        IReadOnlyList<IInvocationLifetimeHandler> handlers,
        IEnvironment environment,
        IOutputDevice outputDevice,
        ILogger logger,
        bool setOwnerMarker,
        bool restoreEnvironment,
        TimeSpan? setupTimeout = null)
    {
        _handlers = handlers;
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

    public bool IsCleanupCompleted => _cleanupExitCode is not null;

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

    /// <summary>
    /// Runs the cleanup of every handler whose setup was entered, in reverse registration order, then disposes all
    /// the handlers. Calling it again returns the result of the first call.
    /// </summary>
    /// <returns>The exit code of the invocation, which becomes a failure when a cleanup failed after a success.</returns>
    public async Task<int> RunCleanupAsync(int exitCode, bool isCanceled)
    {
        if (_cleanupExitCode is { } completedExitCode)
        {
            return completedExitCode;
        }

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

        int finalExitCode = failed && exitCode == (int)ExitCode.Success
            ? (int)ExitCode.GenericFailure
            : exitCode;
        _cleanupExitCode = finalExitCode;
        return finalExitCode;
    }

    /// <summary>
    /// Disposes the handlers when the host exits before the setup started (for example on an incompatible protocol),
    /// without calling their cleanup. No-op once the setup started, because the cleanup owns the disposal then.
    /// </summary>
    public async Task ReleaseIfNotStartedAsync()
    {
        if (!_setupStarted && _cleanupExitCode is null)
        {
            await RunCleanupAsync((int)ExitCode.GenericFailure, isCanceled: false).ConfigureAwait(false);
        }
    }

    private void SetEnvironmentVariable(string name, string? value)
    {
        if (_restoreEnvironment && !_originalValues.ContainsKey(name))
        {
            _originalValues[name] = _environment.GetEnvironmentVariable(name);
        }

        _environment.SetEnvironmentVariable(name, value);
    }

    private async Task ReportFailureAsync(string messageFormat, string handlerUid, Exception exception)
    {
        string message = string.Format(CultureInfo.CurrentCulture, messageFormat, handlerUid, exception.Message);
        try
        {
            await _outputDevice.DisplayAsync(this, new ErrorMessageOutputDeviceData(message), CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex) when (IsNonFatal(ex))
        {
            // Reporting is best-effort: it must not skip the cleanup of the remaining handlers.
        }

        await TryLogAsync(() => _logger.LogErrorAsync($"{message}{_environment.NewLine}{exception}")).ConfigureAwait(false);
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
