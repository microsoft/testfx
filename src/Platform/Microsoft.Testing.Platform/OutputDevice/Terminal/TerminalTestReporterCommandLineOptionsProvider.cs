// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.OutputDevice.Terminal;

[Embedded]
internal sealed class TerminalTestReporterCommandLineOptionsProvider : CommandLineOptionsProviderBase
{
    public const string NoProgressOption = "no-progress";
    public const string ProgressOption = "progress";
    public const string NoAnsiOption = "no-ansi";
    public const string AnsiOption = "ansi";
    public const string OutputOption = "output";
    public const string OutputOptionMinimalArgument = "minimal";
    public const string OutputOptionNormalArgument = "normal";
    public const string OutputOptionDetailedArgument = "detailed";
    public const string ShowStdoutOption = "show-stdout";
    public const string ShowStderrOption = "show-stderr";
    public const string ShowOutputAllArgument = "all";
    public const string ShowOutputFailedArgument = "failed";
    public const string ShowOutputNoneArgument = "none";
    public const string ShowSlowestTestsOption = "show-slowest-tests";
    public const string ShowFlakyTestsOption = FlakyTestsReportingOptions.ShowFlakyTestsOptionName;
    public const string ShowTestResultsOption = "show-test-results";
    public const string ShowTestResultsPassedArgument = "passed";
    public const string ShowTestResultsFailedArgument = "failed";
    public const string ShowTestResultsSkippedArgument = "skipped";
    public const string ShowTestResultsAllArgument = "all";
    public const string ShowTestResultsNoneArgument = "none";

    public TerminalTestReporterCommandLineOptionsProvider()
        : base(
            // Stable extension UID. Do not change: it feeds telemetry, --info output, and artifact metadata.
            "TerminalTestReporterCommandLineOptionsProvider",
            PlatformVersion.Version,
            TerminalResources.TerminalTestReporterDisplayName,
            TerminalResources.TerminalTestReporterDescription,
            [
                new(NoProgressOption, TerminalResources.TerminalNoProgressOptionDescription, ArgumentArity.Zero, isHidden: false, isBuiltIn: true),
                new(ProgressOption, TerminalResources.TerminalProgressOptionDescription, ArgumentArity.ExactlyOne, isHidden: false, isBuiltIn: true),
                new(NoAnsiOption, TerminalResources.TerminalNoAnsiOptionDescription, ArgumentArity.Zero, isHidden: false, isBuiltIn: true),
                new(AnsiOption, TerminalResources.TerminalAnsiOptionDescription, ArgumentArity.ExactlyOne, isHidden: false, isBuiltIn: true),
                new(OutputOption, TerminalResources.TerminalOutputOptionDescription, ArgumentArity.ExactlyOne, isHidden: false, isBuiltIn: true),
                new(ShowStdoutOption, TerminalResources.TerminalShowStdoutOptionDescription, ArgumentArity.ExactlyOne, isHidden: false, isBuiltIn: true),
                new(ShowStderrOption, TerminalResources.TerminalShowStderrOptionDescription, ArgumentArity.ExactlyOne, isHidden: false, isBuiltIn: true),
                new(ShowSlowestTestsOption, TerminalResources.TerminalShowSlowestTestsOptionDescription, ArgumentArity.ExactlyOne, isHidden: false, isBuiltIn: true),
                new(ShowFlakyTestsOption, TerminalResources.TerminalShowFlakyTestsOptionDescription, ArgumentArity.ZeroOrOne, isHidden: false, isBuiltIn: true),
                new(ShowTestResultsOption, TerminalResources.TerminalShowTestResultsOptionDescription, ArgumentArity.OneOrMore, isHidden: false, isBuiltIn: true),
            ])
    {
    }

    public override Task<ValidationResult> ValidateOptionArgumentsAsync(CommandLineOption commandOption, string[] arguments)
        => commandOption.Name switch
        {
            NoProgressOption => ValidationResult.ValidTask,
            ProgressOption => arguments.Length == 1 && CommandLineOptionArgumentValidator.IsValidBooleanAutoArgument(arguments[0])
                ? ValidationResult.ValidTask
                : ValidationResult.InvalidTask(TerminalResources.TerminalProgressOptionInvalidArgument),
            NoAnsiOption => ValidationResult.ValidTask,
            AnsiOption => arguments.Length == 1 && CommandLineOptionArgumentValidator.IsValidBooleanAutoArgument(arguments[0])
                ? ValidationResult.ValidTask
                : ValidationResult.InvalidTask(TerminalResources.TerminalAnsiOptionInvalidArgument),
            OutputOption => arguments.Length == 1 && (OutputOptionMinimalArgument.Equals(arguments[0], StringComparison.OrdinalIgnoreCase)
                || OutputOptionNormalArgument.Equals(arguments[0], StringComparison.OrdinalIgnoreCase)
                || OutputOptionDetailedArgument.Equals(arguments[0], StringComparison.OrdinalIgnoreCase))
                ? ValidationResult.ValidTask
                : ValidationResult.InvalidTask(TerminalResources.TerminalOutputOptionInvalidArgument),
            ShowStdoutOption or ShowStderrOption => arguments.Length == 1 && IsValidShowOutputArgument(arguments[0])
                ? ValidationResult.ValidTask
                : ValidationResult.InvalidTask(TerminalResources.TerminalShowOutputOptionInvalidArgument),
            ShowSlowestTestsOption => arguments.Length == 1
                && int.TryParse(arguments[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int count) && count >= 1
                ? ValidationResult.ValidTask
                : ValidationResult.InvalidTask(TerminalResources.TerminalShowSlowestTestsOptionInvalidArgument),
            // Bare '--show-flaky-tests' means "on", so zero arguments is valid; a single argument must be one of the
            // usual on/off spellings.
            ShowFlakyTestsOption => arguments.Length == 0 || (arguments.Length == 1 && CommandLineOptionArgumentValidator.IsValidBooleanArgument(arguments[0]))
                ? ValidationResult.ValidTask
                : ValidationResult.InvalidTask(TerminalResources.TerminalShowFlakyTestsOptionInvalidArgument),
            ShowTestResultsOption => TryParseShowTestResultsArguments(arguments, out _, out ShowTestResultsValidationError error)
                ? ValidationResult.ValidTask
                : ValidationResult.InvalidTask(error switch
                {
                    ShowTestResultsValidationError.EmptySelection => TerminalResources.TerminalShowTestResultsOptionEmptySelectionInvalidArgument,
                    ShowTestResultsValidationError.AllOrNoneCombinedWithOtherValues => TerminalResources.TerminalShowTestResultsOptionAllOrNoneCombinedInvalidArgument,
                    _ => TerminalResources.TerminalShowTestResultsOptionUnknownValueInvalidArgument,
                }),
            _ => throw ApplicationStateGuard.Unreachable(),
        };

    internal static bool IsProgressEnabled(ICommandLineOptions commandLineOptions)
        => commandLineOptions.TryGetOptionArgumentList(ProgressOption, out string[]? arguments)
            && arguments is { Length: > 0 }
                ? !CommandLineOptionArgumentValidator.IsOffValue(arguments[0])
                : !commandLineOptions.IsOptionSet(NoProgressOption);

    /// <summary>
    /// Resolves <c>--show-flaky-tests</c>. See <see cref="FlakyTestsReportingOptions.IsEnabled"/>.
    /// </summary>
    internal static bool IsFlakyTestsReportingEnabled(ICommandLineOptions commandLineOptions)
        => FlakyTestsReportingOptions.IsEnabled(commandLineOptions);

    private static bool IsValidShowOutputArgument(string argument)
        => ShowOutputAllArgument.Equals(argument, StringComparison.OrdinalIgnoreCase)
            || ShowOutputFailedArgument.Equals(argument, StringComparison.OrdinalIgnoreCase)
            || ShowOutputNoneArgument.Equals(argument, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Resolves <c>--show-test-results</c> or its passive configuration default, returning <see langword="null"/>
    /// when neither is present so the caller (<c>TerminalOutputDevice.InitializeAsync</c>) can fall back to its
    /// <c>--output</c>-based default.
    /// Arguments are already guaranteed valid by <see cref="ValidateOptionArgumentsAsync"/> by the time a real run
    /// reaches this method; an unexpected parse failure therefore indicates that command-line validation was
    /// bypassed and is reported as an invalid application state.
    /// </summary>
    internal static TestResultVisibility? GetShowTestResultsVisibility(ICommandLineOptions commandLineOptions)
        => !commandLineOptions.TryGetOptionArgumentListOrDefault(ShowTestResultsOption, out string[]? arguments)
            ? null
            : TryParseShowTestResultsArguments(arguments ?? [], out TestResultVisibility visibility, out _)
            ? visibility
            : throw new InvalidOperationException("The --show-test-results option was not validated.");

    /// <summary>
    /// Parses the raw <c>--show-test-results</c> arguments (as aggregated across every occurrence of the option
    /// and every space-separated token within each occurrence) into a <see cref="TestResultVisibility"/> flag set.
    /// Each raw token is additionally split on <c>,</c> so <c>passed,skipped</c>, <c>passed skipped</c>, and
    /// <c>--show-test-results passed --show-test-results skipped</c> are all equivalent. Values are matched
    /// case-insensitively and the ordinary values (<c>passed</c>/<c>failed</c>/<c>skipped</c>) are unioned and
    /// de-duplicated. <c>all</c> and <c>none</c> are each rejected when combined with any other value (including
    /// each other), and an entirely empty selection (e.g. a lone comma) is rejected too.
    /// </summary>
    internal static bool TryParseShowTestResultsArguments(string[] arguments, out TestResultVisibility visibility, out ShowTestResultsValidationError error)
    {
        visibility = TestResultVisibility.None;
        error = ShowTestResultsValidationError.None;

        bool sawAll = false;
        bool sawNone = false;
        bool sawOrdinary = false;

        foreach (string rawArgument in arguments)
        {
            foreach (string token in rawArgument.Split(',').Select(static rawToken => rawToken.Trim()))
            {
                if (token.Length == 0)
                {
                    // Tolerate stray empty tokens produced by a leading/trailing/doubled comma; the overall
                    // selection is still validated for emptiness once every token has been examined.
                    continue;
                }

                if (ShowTestResultsAllArgument.Equals(token, StringComparison.OrdinalIgnoreCase))
                {
                    sawAll = true;
                    visibility |= TestResultVisibility.All;
                }
                else if (ShowTestResultsNoneArgument.Equals(token, StringComparison.OrdinalIgnoreCase))
                {
                    sawNone = true;
                }
                else if (ShowTestResultsPassedArgument.Equals(token, StringComparison.OrdinalIgnoreCase))
                {
                    sawOrdinary = true;
                    visibility |= TestResultVisibility.Passed;
                }
                else if (ShowTestResultsFailedArgument.Equals(token, StringComparison.OrdinalIgnoreCase))
                {
                    sawOrdinary = true;
                    visibility |= TestResultVisibility.Failed;
                }
                else if (ShowTestResultsSkippedArgument.Equals(token, StringComparison.OrdinalIgnoreCase))
                {
                    sawOrdinary = true;
                    visibility |= TestResultVisibility.Skipped;
                }
                else
                {
                    visibility = TestResultVisibility.None;
                    error = ShowTestResultsValidationError.UnknownValue;
                    return false;
                }
            }
        }

        if ((sawAll || sawNone) && (sawOrdinary || (sawAll && sawNone)))
        {
            visibility = TestResultVisibility.None;
            error = ShowTestResultsValidationError.AllOrNoneCombinedWithOtherValues;
            return false;
        }

        if (!sawAll && !sawNone && !sawOrdinary)
        {
            error = ShowTestResultsValidationError.EmptySelection;
            return false;
        }

        return true;
    }
}

/// <summary>
/// The reason <see cref="TerminalTestReporterCommandLineOptionsProvider.TryParseShowTestResultsArguments"/>
/// rejected a <c>--show-test-results</c> selection.
/// </summary>
[Embedded]
internal enum ShowTestResultsValidationError
{
    /// <summary>
    /// The selection was valid (or parsing was not attempted).
    /// </summary>
    None,

    /// <summary>
    /// A token did not match any of the recognized values.
    /// </summary>
    UnknownValue,

    /// <summary>
    /// Every token was blank (e.g. a lone comma), leaving no outcome selected.
    /// </summary>
    EmptySelection,

    /// <summary>
    /// <c>all</c> or <c>none</c> was combined with another value, including with each other.
    /// </summary>
    AllOrNoneCombinedWithOtherValues,
}
