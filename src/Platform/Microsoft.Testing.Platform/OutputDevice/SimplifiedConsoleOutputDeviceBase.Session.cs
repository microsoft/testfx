// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.OutputDevice;

/// <summary>
/// Session lifecycle display logic: banner, before/after session output and session start/stop handling.
/// </summary>
internal abstract partial class SimplifiedConsoleOutputDeviceBase
{
    public async Task DisplayBannerAsync(string? bannerMessage, CancellationToken cancellationToken)
    {
        using (await _asyncMonitor.LockAsync(TimeoutHelper.DefaultHangTimeSpanTimeout).ConfigureAwait(false))
        {
            if (_bannerDisplayed)
            {
                return;
            }

            // skip the banner for the children processes
            _environment.SetEnvironmentVariable(OutputDeviceBannerHelper.TESTINGPLATFORM_CONSOLEOUTPUTDEVICE_SKIP_BANNER, "1");

            _bannerDisplayed = true;

            if (bannerMessage is not null)
            {
                ConsoleLog(bannerMessage);
                return;
            }

            ConsoleLog(OutputDeviceBannerHelper.BuildBannerText(_platformInformation, _runtimeFeature, _longArchitecture, _runtimeFramework));
        }
    }

    public Task DisplayBeforeSessionStartAsync(CancellationToken cancellationToken)
    {
        AppendAssemblyLinkTargetFrameworkAndArchitecture(_console, _assemblyName, _targetFramework, _longArchitecture);
        return Task.CompletedTask;
    }

    private static void AppendAssemblyLinkTargetFrameworkAndArchitecture(IConsole console, string assembly, string? targetFramework, string? architecture)
    {
        var builder = new StringBuilder();
        builder.Append(assembly);
        if (targetFramework == null && architecture == null)
        {
            console.WriteLine(builder.ToString());
            return;
        }

        builder.Append(" (");
        if (targetFramework != null)
        {
            builder.Append(targetFramework);
            builder.Append('|');
        }

        if (architecture != null)
        {
            builder.Append(architecture);
        }

        builder.Append(')');

        console.WriteLine(builder.ToString());
    }

    public async Task DisplayAfterSessionEndRunAsync(CancellationToken cancellationToken)
    {
        using (await _asyncMonitor.LockAsync(TimeoutHelper.DefaultHangTimeSpanTimeout).ConfigureAwait(false))
        {
            if (_firstCallTo_OnSessionStartingAsync)
            {
                return;
            }

            int total = _skippedTests + _passedTests + _failedTests;

            // The abort callback sets _wasCancelled, but it does not cover every cancellation path
            // (e.g. cancellations that request the session token without going through the abort
            // callback). Fold in the token state so the verdict and routing also reflect those.
            bool wasCancelled = _wasCancelled || cancellationToken.IsCancellationRequested;

            // minimumExpectedTests is always 0 here because SimplifiedConsoleOutputDeviceBase does not
            // receive ICommandLineOptions. The --minimum-expected-tests policy is still enforced via
            // TestApplicationResult (exit code), but it is not surfaced in this summary.
            string text = TestRunSummaryHelper.FormatSummaryText(total, _failedTests, _passedTests, _skippedTests, wasCancelled, minimumExpectedTests: 0);

            if (TestRunSummaryHelper.IsRunFailed(total, _failedTests, _skippedTests, wasCancelled, minimumExpectedTests: 0))
            {
                ConsoleError(text);
            }
            else
            {
                ConsoleLog(text);
            }
        }
    }

    public async Task OnTestSessionFinishingAsync(ITestSessionContext testSessionContext)
    {
        SlowTestReporterState? reporterState = Interlocked.Exchange(ref _slowTestReporterState, null);

#if NET
        if (reporterState is not null)
        {
            await reporterState.CancellationTokenSource.CancelAsync().ConfigureAwait(false);
        }
#else
#pragma warning disable VSTHRD103 // CancellationTokenSource.CancelAsync is not available on this target framework.
        reporterState?.CancellationTokenSource.Cancel();
#pragma warning restore VSTHRD103
#endif

        if (reporterState is not null)
        {
            await reporterState.Task.ConfigureAwait(false);
            reporterState.CancellationTokenSource.Dispose();
        }

        using (await _asyncMonitor.LockAsync(TimeoutHelper.DefaultHangTimeSpanTimeout).ConfigureAwait(false))
        {
            _progressMessages.Clear();
            _activeTestTracker.Clear();
        }
    }

    public Task OnTestSessionStartingAsync(ITestSessionContext testSessionContext)
    {
        CancellationToken cancellationToken = testSessionContext.CancellationToken;
        cancellationToken.ThrowIfCancellationRequested();

        // We implement IDataConsumerService and IOutputDisplayService.
        // So the engine is calling us before as IDataConsumerService and after as IOutputDisplayService.
        // The engine look for the ITestSessionLifetimeHandler in both case and call it.
        if (_firstCallTo_OnSessionStartingAsync)
        {
            _firstCallTo_OnSessionStartingAsync = false;
            if (_activeTestTracker.IsEnabled)
            {
                var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                _slowTestReporterState = new(cancellationTokenSource, ReportSlowTestsAsync(cancellationTokenSource.Token));
            }
        }

        return Task.CompletedTask;
    }
}
