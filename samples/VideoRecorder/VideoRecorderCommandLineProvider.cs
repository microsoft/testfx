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
    public const string ArgsOptionName = "capture-video-args";

    public const string ModeAlways = "always";
    public const string ModeOnFailure = "on-failure";

    public const string SourceScreen = "screen";
    public const string SourceWindow = "window";

    private static readonly string[] ModeValues = [ModeOnFailure, ModeAlways];
    private static readonly string[] SourceValues = [SourceScreen, SourceWindow];

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
                : ValidationResult.ValidTask;

    public Task<ValidationResult> ValidateCommandLineOptionsAsync(ICommandLineOptions commandLineOptions)
        => (commandLineOptions.IsOptionSet(ArgsOptionName) || commandLineOptions.IsOptionSet(SourceOptionName)) && !commandLineOptions.IsOptionSet(EnableOptionName)
            ? ValidationResult.InvalidTask($"--{ArgsOptionName} and --{SourceOptionName} require --{EnableOptionName} to be specified.")
            : ValidationResult.ValidTask;
}
