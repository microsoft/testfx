// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;

namespace Microsoft.Testing.Extensions.VideoRecorder;

/// <summary>
/// Provides the command-line options that enable and control the video recorder.
/// </summary>
internal sealed class VideoRecorderCommandLineProvider : ICommandLineOptionsProvider
{
    public const string EnableOptionName = "capture-video";
    public const string SourceOptionName = "capture-video-source";
    public const string GranularityOptionName = "capture-video-granularity";
    public const string ArgsOptionName = "capture-video-args";

    public const string ModeAlways = "always";
    public const string ModeOnFailure = "on-failure";

    public const string SourceScreen = "screen";
    public const string SourceWindow = "window";

    public const string GranularityTest = "test";
    public const string GranularitySession = "session";
    public const string GranularityManual = "manual";

    private static readonly string[] ModeValues = [ModeOnFailure, ModeAlways];
    private static readonly string[] SourceValues = [SourceScreen, SourceWindow];
    private static readonly string[] GranularityValues = [GranularityTest, GranularitySession, GranularityManual];

    public string Uid => nameof(VideoRecorderCommandLineProvider);

    public string Version => "1.0.0";

    public string DisplayName => "Video recorder";

    public string Description => "Command-line options for the video recorder extension.";

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public IReadOnlyCollection<CommandLineOption> GetCommandLineOptions() =>
    [
        new CommandLineOption(
            EnableOptionName,
            "Record the screen during the test run. Optionally specify when to keep the video: 'on-failure' (default, keep only when a test fails) or 'always'.",
            ArgumentArity.ZeroOrOne,
            isHidden: false),
        new CommandLineOption(
            SourceOptionName,
            "What to capture: 'screen' (default, the full screen) or 'window' (only the current process window; Windows only, falls back to full screen elsewhere). Requires --capture-video.",
            ArgumentArity.ExactlyOne,
            isHidden: false),
        new CommandLineOption(
            GranularityOptionName,
            "How recordings are split: 'test' (default, one video per test), 'session' (one video for the whole run), or 'manual' (tests call VideoRecorder.Current.Start/StopAsync themselves). Requires --capture-video.",
            ArgumentArity.ExactlyOne,
            isHidden: false),
        new CommandLineOption(
            ArgsOptionName,
            "Extra arguments passed to the underlying recorder (currently ffmpeg), as output/encoding options. Requires --capture-video. Because the value usually starts with '-', use the '=' delimiter so it is not parsed as a separate option, e.g. --capture-video-args=\"-vf scale=1280:-1\".",
            ArgumentArity.ExactlyOne,
            isHidden: false),
    ];

    public Task<ValidationResult> ValidateOptionArgumentsAsync(CommandLineOption commandOption, string[] arguments)
        => commandOption.Name == EnableOptionName && arguments.Length > 0 && !ModeValues.Contains(arguments[0], StringComparer.OrdinalIgnoreCase)
            ? ValidationResult.InvalidTask($"Invalid value '{arguments[0]}' for --{EnableOptionName}. Valid values are '{ModeOnFailure}' and '{ModeAlways}'.")
            : commandOption.Name == SourceOptionName && arguments.Length > 0 && !SourceValues.Contains(arguments[0], StringComparer.OrdinalIgnoreCase)
                ? ValidationResult.InvalidTask($"Invalid value '{arguments[0]}' for --{SourceOptionName}. Valid values are '{SourceScreen}' and '{SourceWindow}'.")
                : commandOption.Name == GranularityOptionName && arguments.Length > 0 && !GranularityValues.Contains(arguments[0], StringComparer.OrdinalIgnoreCase)
                    ? ValidationResult.InvalidTask($"Invalid value '{arguments[0]}' for --{GranularityOptionName}. Valid values are '{GranularityTest}', '{GranularitySession}' and '{GranularityManual}'.")
                    : ValidationResult.ValidTask;

    public Task<ValidationResult> ValidateCommandLineOptionsAsync(ICommandLineOptions commandLineOptions)
        => (commandLineOptions.IsOptionSet(ArgsOptionName) || commandLineOptions.IsOptionSet(SourceOptionName) || commandLineOptions.IsOptionSet(GranularityOptionName)) && !commandLineOptions.IsOptionSet(EnableOptionName)
            ? ValidationResult.InvalidTask($"--{ArgsOptionName}, --{SourceOptionName} and --{GranularityOptionName} require --{EnableOptionName} to be specified.")
            : ValidationResult.ValidTask;
}
