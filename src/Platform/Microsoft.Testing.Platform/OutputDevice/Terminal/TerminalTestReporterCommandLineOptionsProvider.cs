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
    public const string OutputOptionNormalArgument = "normal";
    public const string OutputOptionDetailedArgument = "detailed";
    public const string ShowStdoutOption = "show-stdout";
    public const string ShowStderrOption = "show-stderr";
    public const string ShowOutputAllArgument = "all";
    public const string ShowOutputFailedArgument = "failed";
    public const string ShowOutputNoneArgument = "none";

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
            OutputOption => arguments.Length == 1 && (OutputOptionNormalArgument.Equals(arguments[0], StringComparison.OrdinalIgnoreCase) || OutputOptionDetailedArgument.Equals(arguments[0], StringComparison.OrdinalIgnoreCase))
                ? ValidationResult.ValidTask
                : ValidationResult.InvalidTask(TerminalResources.TerminalOutputOptionInvalidArgument),
            ShowStdoutOption or ShowStderrOption => arguments.Length == 1 && IsValidShowOutputArgument(arguments[0])
                ? ValidationResult.ValidTask
                : ValidationResult.InvalidTask(TerminalResources.TerminalShowOutputOptionInvalidArgument),
            _ => throw ApplicationStateGuard.Unreachable(),
        };

    private static bool IsValidShowOutputArgument(string argument)
        => ShowOutputAllArgument.Equals(argument, StringComparison.OrdinalIgnoreCase)
            || ShowOutputFailedArgument.Equals(argument, StringComparison.OrdinalIgnoreCase)
            || ShowOutputNoneArgument.Equals(argument, StringComparison.OrdinalIgnoreCase);
}
