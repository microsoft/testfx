// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.OutputDevice.Terminal;
using Microsoft.Testing.Platform.Resources;

namespace Microsoft.Testing.Platform.OutputDevice;

internal sealed partial class TerminalOutputDevice
{
    public async Task InitializeAsync()
    {
        if (_fileLoggerInformation is not null)
        {
            _logger = _loggerFactory.CreateLogger(GetType().ToString());
        }

        _isListTests = _commandLineOptions.IsOptionSet(PlatformCommandLineProvider.DiscoverTestsOptionKey);
        _isListTestsJson = PlatformCommandLineProvider.IsListTestsJsonOutput(_commandLineOptions);
        _isAzureDevOpsEnvironment = AzureDevOpsLogIssueFormatter.IsAzureDevOpsEnvironment(_environment);

        if (_isListTestsJson)
        {
            // In JSON discovery mode the standard abort callback would call
            // TerminalTestReporter.StartCancelling which writes "Cancelling test session" to
            // stdout, corrupting the JSON document. Route a single-line cancellation notice
            // to stderr instead so the user still gets feedback on Ctrl+C.
            await _policiesService.RegisterOnAbortCallbackAsync(
                async () =>
                {
                    await WriteToStandardErrorAsync(PlatformResources.CancellingTestSession).ConfigureAwait(false);
                    await WriteToStandardErrorAsync(PlatformResources.PressCtrlCAgainToForceExit).ConfigureAwait(false);
                }).ConfigureAwait(false);
        }
        else
        {
            await _policiesService.RegisterOnAbortCallbackAsync(
                () =>
                {
                    _terminalTestReporter?.StartCancelling();
                    return Task.CompletedTask;
                }).ConfigureAwait(false);
        }

        _isServerMode = _commandLineOptions.IsOptionSet(PlatformCommandLineProvider.ServerOptionKey);
        bool noAnsi = _commandLineOptions.IsOptionSet(TerminalTestReporterCommandLineOptionsProvider.NoAnsiOption);

        // --ansi <auto|on|off>, when present (with any value), takes precedence over the legacy --no-ansi flag.
        // This keeps the help text honest: passing --ansi at all overrides --no-ansi.
        AnsiOverride ansiOverride = AnsiOverride.None;
        if (_commandLineOptions.TryGetOptionArgumentList(TerminalTestReporterCommandLineOptionsProvider.AnsiOption, out string[]? ansiArguments)
            && ansiArguments is { Length: > 0 })
        {
            string ansiValue = ansiArguments[0];
            ansiOverride = CommandLineOptionArgumentValidator.IsOnValue(ansiValue)
                ? AnsiOverride.ForceOn
                : CommandLineOptionArgumentValidator.IsOffValue(ansiValue)
                    ? AnsiOverride.ForceOff
                    : AnsiOverride.Auto;
        }

        // When --ansi auto is explicitly specified, it overrides --no-ansi too.
        bool effectiveNoAnsi = noAnsi && ansiOverride == AnsiOverride.None;

        // Honor the de-facto cross-tool NO_COLOR convention (https://no-color.org): when present with any
        // non-empty value, suppress ANSI color/cursor codes unless the user explicitly opted in via
        // `--ansi on` (which still wins, matching the precedence documented in --help).
        bool noColorEnv = !RoslynString.IsNullOrEmpty(_environment.GetEnvironmentVariable("NO_COLOR"));

        bool inCI = new CIEnvironmentDetector(_environment).IsCIEnvironment();
        bool isLLMEnvironment = new LLMEnvironmentDetector(_environment).IsLLMEnvironment();

        AnsiMode ansiMode = ansiOverride switch
        {
            // User explicitly forced ANSI on (e.g. `--ansi on`). Bypass CI / LLM / NO_COLOR / redirection
            // detection so colors and cursor movement are emitted even when stdout is redirected.
            AnsiOverride.ForceOn => AnsiMode.ForceAnsi,

            // User explicitly disabled ANSI (`--ansi off`).
            // Note that NoAnsi also implies no progress.
            AnsiOverride.ForceOff => AnsiMode.NoAnsi,

            // No --ansi argument was provided, or `--ansi auto` was provided.
            // Fall back to environment-based detection.
            // In LLM environments, prefer simple text output so that the LLM can parse it easily.
            // NO_COLOR (https://no-color.org) is honored as well: any non-empty value suppresses ANSI.
            _ when effectiveNoAnsi || noColorEnv || isLLMEnvironment => AnsiMode.NoAnsi,
            _ when inCI => AnsiMode.SimpleAnsi,
            _ => AnsiMode.AnsiIfPossible,
        };

        // --progress <auto|on|off> is the modern, positive form. When present it wins over the legacy
        // --no-progress flag. --no-progress keeps working as a deprecated alias for --progress off and
        // emits a single deprecation warning per process.
        bool noProgress;
        if (_commandLineOptions.TryGetOptionArgumentList(TerminalTestReporterCommandLineOptionsProvider.ProgressOption, out string[]? progressArguments)
            && progressArguments is { Length: > 0 })
        {
            // 'on' and 'auto' currently behave the same: progress is shown unless the terminal is not capable
            // of in-place updates (NoAnsi/SimpleAnsi) or we are a test-host controller, --list-tests, or server
            // mode. A dedicated heartbeat renderer for the non-cursor modes is tracked by #9125.
            noProgress = CommandLineOptionArgumentValidator.IsOffValue(progressArguments[0]);
        }
        else if (_commandLineOptions.IsOptionSet(TerminalTestReporterCommandLineOptionsProvider.NoProgressOption))
        {
            noProgress = true;

            // The deprecation warning is a nudge for interactive users to move to --progress off. We deliberately
            // do not emit it in CI: it is invisible noise there, and build infrastructure (e.g. the Arcade SDK test
            // runner) passes --no-progress unconditionally, so emitting to stderr would surface as a build error in
            // logging setups that treat any stderr output as a failure.
            if (!inCI && Interlocked.Exchange(ref s_noProgressDeprecationWarningEmitted, 1) == 0)
            {
                await WriteToStandardErrorAsync(PlatformResources.TerminalNoProgressDeprecatedWarning).ConfigureAwait(false);
            }
        }
        else
        {
            noProgress = false;
        }

        // _runtimeFeature.IsHotReloadEnabled is not set to true here, even if the session will be HotReload,
        // we need to postpone that decision until the first test result.
        //
        // This works but is NOT USED, we prefer to have the same experience of not showing passed tests in hotReload mode as in normal mode.
        // Func<bool> showPassed = () => _runtimeFeature.IsHotReloadEnabled;
        Func<bool> showPassed = () => false;
        bool outputOption = _commandLineOptions.TryGetOptionArgumentList(TerminalTestReporterCommandLineOptionsProvider.OutputOption, out string[]? arguments);
        if (outputOption && arguments?.Length > 0 && TerminalTestReporterCommandLineOptionsProvider.OutputOptionDetailedArgument.Equals(arguments[0], StringComparison.OrdinalIgnoreCase))
        {
            showPassed = () => true;
        }

        OutputShowMode showStdout = GetShowOutputMode(_commandLineOptions, TerminalTestReporterCommandLineOptionsProvider.ShowStdoutOption, isLLMEnvironment);
        OutputShowMode showStderr = GetShowOutputMode(_commandLineOptions, TerminalTestReporterCommandLineOptionsProvider.ShowStderrOption, isLLMEnvironment);

        Func<bool?> shouldShowProgress = noProgress
            // User preference is to not show progress.
            ? static () => false
            // User preference is to allow showing progress, figure if we should actually show it based on whether or not we are a testhost controller.
            //
            // TestHost controller is not running any tests and it should not be writing progress.
            //
            // The test host controller info is not setup and populated until after this constructor, because it writes banner and then after it figures out if
            // the runner is a testHost controller, so we would always have it as null if we capture it directly. Instead we need to check it via
            // func.
            : () => _isListTests || _isServerMode
                ? false
                : !_testHostControllerInfo.IsCurrentProcessTestHostController;

        // This is single exe run, don't show all the details of assemblies and their summaries.
        _terminalTestReporter = new TerminalTestReporter(_assemblyName, _targetFramework, _shortArchitecture, _console, _testApplicationCancellationTokenSource, new()
        {
            ShowPassedTests = showPassed,
            MinimumExpectedTests = PlatformCommandLineProvider.GetMinimumExpectedTests(_commandLineOptions),
            AnsiMode = ansiMode,
            ShowActiveTests = true,
            ShowProgress = shouldShowProgress,
            ShowStdout = showStdout,
            ShowStderr = showStderr,
            HeartbeatSilenceThreshold = GetProgressThreshold(_environment, MTP_PROGRESS_SILENCE_SECONDS, defaultSeconds: 30),
            SlowTestThreshold = GetProgressThreshold(_environment, MTP_PROGRESS_SLOW_TEST_SECONDS, defaultSeconds: 60),
        });
    }

    // Reads an integer number of seconds from the given environment variable, falling back to
    // <paramref name="defaultSeconds"/> when unset or invalid. A value of 0 disables the related
    // heartbeat rule (returns TimeSpan.Zero). Negative or non-integer values are ignored.
    private static TimeSpan GetProgressThreshold(IEnvironment environment, string variableName, int defaultSeconds)
    {
        string? raw = environment.GetEnvironmentVariable(variableName);
        return !RoslynString.IsNullOrWhiteSpace(raw)
            && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int seconds)
            && seconds >= 0
            ? TimeSpan.FromSeconds(seconds)
            : TimeSpan.FromSeconds(defaultSeconds);
    }

    // When the option is absent, default to OutputShowMode.Failed when running under a known
    // LLM/AI environment (less token noise for agents) and to OutputShowMode.All otherwise.
    // An explicit --show-stdout/--show-stderr value always wins over the LLM-aware default.
    // TODO(#8772): Update the --show-stdout/--show-stderr help text to reflect the LLM-aware default.
    private static OutputShowMode GetShowOutputMode(ICommandLineOptions commandLineOptions, string optionName, bool isLLMEnvironment)
        => commandLineOptions.TryGetOptionArgumentList(optionName, out string[]? arguments) && arguments is { Length: > 0 }
            ? arguments[0] switch
            {
                string s when TerminalTestReporterCommandLineOptionsProvider.ShowOutputFailedArgument.Equals(s, StringComparison.OrdinalIgnoreCase) => OutputShowMode.Failed,
                string s when TerminalTestReporterCommandLineOptionsProvider.ShowOutputNoneArgument.Equals(s, StringComparison.OrdinalIgnoreCase) => OutputShowMode.None,
                _ => OutputShowMode.All,
            }
            : isLLMEnvironment ? OutputShowMode.Failed : OutputShowMode.All;

    private enum AnsiOverride
    {
        /// <summary>
        /// The <c>--ansi</c> option was not provided.
        /// </summary>
        None,

        /// <summary>
        /// The <c>--ansi auto</c> option was provided. The user explicitly opts in to auto-detection
        /// (and out of the legacy <c>--no-ansi</c> flag if it was also passed).
        /// </summary>
        Auto,

        /// <summary>
        /// The <c>--ansi on</c> option was provided, forcing ANSI output regardless of environment.
        /// </summary>
        ForceOn,

        /// <summary>
        /// The <c>--ansi off</c> option was provided, disabling ANSI output.
        /// </summary>
        ForceOff,
    }

    private static string GetShortArchitecture(string runtimeIdentifier)
    {
        int firstIndexOfDash = runtimeIdentifier.IndexOf(Dash);
        return firstIndexOfDash < 0 ? runtimeIdentifier : runtimeIdentifier.Substring(firstIndexOfDash + 1);
    }
}
