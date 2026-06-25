// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.VideoRecorder.Resources;
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
    public const string MaxDurationOptionName = "capture-video-max-duration";
    public const string ChaptersOptionName = "capture-video-chapters";

    public const string ModeAlways = "always";
    public const string ModeOnFailure = "on-failure";

    public const string SourceScreen = "screen";
    public const string SourceWindow = "window";

    public const string GranularityTest = "test";
    public const string GranularitySession = "session";

    public const string ChaptersOn = "on";
    public const string ChaptersOff = "off";

    private static readonly string[] ModeValues = [ModeOnFailure, ModeAlways];
    private static readonly string[] SourceValues = [SourceScreen, SourceWindow];
    private static readonly string[] GranularityValues = [GranularityTest, GranularitySession];
    private static readonly string[] ChaptersValues = [ChaptersOn, ChaptersOff];

    public string Uid => nameof(VideoRecorderCommandLineProvider);

    public string Version => "1.0.0";

    public string DisplayName => "Video recorder";

    public string Description => "Command-line options for the video recorder extension.";

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public IReadOnlyCollection<CommandLineOption> GetCommandLineOptions() =>
    [
        new CommandLineOption(EnableOptionName, VideoRecorderResources.OptionDescriptionCaptureVideo, ArgumentArity.ZeroOrOne, isHidden: false),
        new CommandLineOption(SourceOptionName, VideoRecorderResources.OptionDescriptionSource, ArgumentArity.ExactlyOne, isHidden: false),
        new CommandLineOption(GranularityOptionName, VideoRecorderResources.OptionDescriptionGranularity, ArgumentArity.ExactlyOne, isHidden: false),
        new CommandLineOption(ArgsOptionName, VideoRecorderResources.OptionDescriptionArgs, ArgumentArity.ExactlyOne, isHidden: false),
        new CommandLineOption(MaxDurationOptionName, VideoRecorderResources.OptionDescriptionMaxDuration, ArgumentArity.ExactlyOne, isHidden: false),
        new CommandLineOption(ChaptersOptionName, VideoRecorderResources.OptionDescriptionChapters, ArgumentArity.ExactlyOne, isHidden: false),
    ];

    public Task<ValidationResult> ValidateOptionArgumentsAsync(CommandLineOption commandOption, string[] arguments)
        => arguments.Length == 0
            ? ValidationResult.ValidTask
            : commandOption.Name switch
            {
                EnableOptionName => ValidateAllowedValuesAsync(EnableOptionName, arguments[0], ModeValues),
                SourceOptionName => ValidateAllowedValuesAsync(SourceOptionName, arguments[0], SourceValues),
                GranularityOptionName => ValidateAllowedValuesAsync(GranularityOptionName, arguments[0], GranularityValues),
                ChaptersOptionName => ValidateAllowedValuesAsync(ChaptersOptionName, arguments[0], ChaptersValues),
                MaxDurationOptionName when !int.TryParse(arguments[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int seconds) || seconds <= 0
                    => ValidationResult.InvalidTask(string.Format(CultureInfo.CurrentCulture, VideoRecorderResources.InvalidOptionPositiveInteger, arguments[0], MaxDurationOptionName)),
                _ => ValidationResult.ValidTask,
            };

    public Task<ValidationResult> ValidateCommandLineOptionsAsync(ICommandLineOptions commandLineOptions)
    {
        bool anySubOption = commandLineOptions.IsOptionSet(ArgsOptionName)
            || commandLineOptions.IsOptionSet(SourceOptionName)
            || commandLineOptions.IsOptionSet(GranularityOptionName)
            || commandLineOptions.IsOptionSet(MaxDurationOptionName)
            || commandLineOptions.IsOptionSet(ChaptersOptionName);

        return anySubOption && !commandLineOptions.IsOptionSet(EnableOptionName)
            ? ValidationResult.InvalidTask(string.Format(CultureInfo.CurrentCulture, VideoRecorderResources.SubOptionsRequireEnable, EnableOptionName))
            : ValidationResult.ValidTask;
    }

    private static Task<ValidationResult> ValidateAllowedValuesAsync(string optionName, string value, string[] allowed)
        => allowed.Contains(value, StringComparer.OrdinalIgnoreCase)
            ? ValidationResult.ValidTask
            : ValidationResult.InvalidTask(string.Format(
                CultureInfo.CurrentCulture,
                VideoRecorderResources.InvalidOptionValue,
                value,
                optionName,
                string.Join(", ", allowed.Select(v => $"'{v}'"))));
}
