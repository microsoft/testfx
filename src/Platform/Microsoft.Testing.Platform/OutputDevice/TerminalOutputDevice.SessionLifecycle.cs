// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Resources;

namespace Microsoft.Testing.Platform.OutputDevice;

internal sealed partial class TerminalOutputDevice
{
    private async Task LogDebugAsync(string message)
    {
        if (_logger is not null)
        {
            await _logger.LogDebugAsync(message).ConfigureAwait(false);
        }
    }

    // Sole point that bypasses IConsole to reach stderr (IConsole has no stderr abstraction today).
    // Used whenever stdout must stay clean or for process-level notices that should not pollute
    // stdout: the --list-tests json paths (so stdout stays reserved for the JSON document), the
    // Ctrl+C cancellation notice, and the --no-progress deprecation warning. If IConsole ever grows
    // a stderr abstraction, replace this helper.
    private static async Task WriteToStandardErrorAsync(string message)
        => await Console.Error.WriteLineAsync(message).ConfigureAwait(false);

    public async Task DisplayBannerAsync(string? bannerMessage, CancellationToken cancellationToken)
    {
        RoslynDebug.Assert(_terminalTestReporter is not null);

        if (_isListTestsJson)
        {
            // Machine-readable mode: keep stdout clean, suppress banner & file logger notice.
            // The env var propagates to any child test host the controller spawns so they also
            // stay quiet — important because the JSON document must be the sole stdout content.
            _bannerDisplayed = true;
            _environment.SetEnvironmentVariable(OutputDeviceBannerHelper.TESTINGPLATFORM_CONSOLEOUTPUTDEVICE_SKIP_BANNER, "1");
            return;
        }

        using (await _asyncMonitor.LockAsync(TimeoutHelper.DefaultHangTimeSpanTimeout).ConfigureAwait(false))
        {
            if (!_bannerDisplayed && !_isServerMode)
            {
                // skip the banner for the children processes
                _environment.SetEnvironmentVariable(OutputDeviceBannerHelper.TESTINGPLATFORM_CONSOLEOUTPUTDEVICE_SKIP_BANNER, "1");

                _bannerDisplayed = true;

                if (bannerMessage is not null)
                {
                    _terminalTestReporter.WriteMessage(bannerMessage);
                }
                else
                {
                    _terminalTestReporter.WriteMessage(OutputDeviceBannerHelper.BuildBannerText(_platformInformation, _runtimeFeature, _longArchitecture, _runtimeFramework));
                }
            }

            if (_fileLoggerInformation is not null)
            {
                if (_fileLoggerInformation.SynchronousWrite)
                {
                    _terminalTestReporter.WriteWarningMessage(string.Format(CultureInfo.CurrentCulture, PlatformResources.DiagnosticFileLevelWithFlush, _fileLoggerInformation.LogLevel, _fileLoggerInformation.LogFile.FullName), padding: null);
                }
                else
                {
                    _terminalTestReporter.WriteWarningMessage(string.Format(CultureInfo.CurrentCulture, PlatformResources.DiagnosticFileLevelWithAsyncFlush, _fileLoggerInformation.LogLevel, _fileLoggerInformation.LogFile.FullName), padding: null);
                }
            }
        }
    }

    public async Task DisplayBeforeHotReloadSessionStartAsync(CancellationToken cancellationToken)
        => await DisplayBeforeSessionStartAsync(cancellationToken).ConfigureAwait(false);

    public async Task DisplayBeforeSessionStartAsync(CancellationToken cancellationToken)
    {
        if (_isServerMode || _isListTestsJson)
        {
            return;
        }

        RoslynDebug.Assert(_terminalTestReporter is not null);

        // Start test execution here, rather than in ShowBanner, because then we know
        // if we are a testHost controller or not, and if we should show progress bar.
        _terminalTestReporter.TestExecutionStarted(_clock.UtcNow, workerCount: 1, isDiscovery: _isListTests);
        _terminalTestReporter.AssemblyRunStarted();
        if (_logger is not null && _logger.IsEnabled(LogLevel.Trace))
        {
            await _logger.LogTraceAsync("DisplayBeforeSessionStartAsync").ConfigureAwait(false);
        }
    }

    public async Task DisplayAfterHotReloadSessionEndAsync(CancellationToken cancellationToken)
    {
        if (_isListTestsJson)
        {
            // JSON discovery is a point-in-time snapshot. Re-emitting after every hot-reload
            // cycle would produce multiple growing JSON documents on stdout, which would break
            // any consumer that pipes the output (the accumulated _discoveredTestsForJson buffer
            // would also re-include earlier tests every cycle).
            return;
        }

        await DisplayAfterSessionEndRunInternalAsync().ConfigureAwait(false);
    }

    public async Task DisplayAfterSessionEndRunAsync(CancellationToken cancellationToken)
    {
        // Under --server (e.g. `dotnet test` with `--server dotnettestcli`) the terminal device stays
        // silent: discovered tests are streamed to the SDK through the dotnet-test pipe (see
        // DotnetTestDataConsumer), and the SDK owns rendering — including building the --list-tests json
        // document by combining the discovered tests from every test app into a single output.
        if (_isServerMode)
        {
            return;
        }

        // Do NOT check and store the value in the constructor
        // it won't be populated yet, so you will always see false.
        if (_runtimeFeature.IsHotReloadEnabled)
        {
            return;
        }

        await DisplayAfterSessionEndRunInternalAsync().ConfigureAwait(false);
    }

    private async Task DisplayAfterSessionEndRunInternalAsync()
    {
        RoslynDebug.Assert(_terminalTestReporter is not null);

        if (_isListTestsJson)
        {
            using (await _asyncMonitor.LockAsync(TimeoutHelper.DefaultHangTimeSpanTimeout).ConfigureAwait(false))
            {
                if (_processRole == TestProcessRole.TestHost)
                {
                    _console.WriteLine(DiscoveredTestsJsonSerializer.Serialize(_discoveredTestsForJson));
                }
            }

            return;
        }

        using (await _asyncMonitor.LockAsync(TimeoutHelper.DefaultHangTimeSpanTimeout).ConfigureAwait(false))
        {
            if (_processRole == TestProcessRole.TestHost)
            {
                _terminalTestReporter.AssemblyRunCompleted();
                _terminalTestReporter.TestExecutionCompleted(_clock.UtcNow);
            }
            else
            {
                _terminalTestReporter.PrintOutOfProcessArtifacts();
            }
        }
    }
}
