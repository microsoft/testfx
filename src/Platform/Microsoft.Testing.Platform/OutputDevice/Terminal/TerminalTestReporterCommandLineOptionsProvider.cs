// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Resources;

namespace Microsoft.Testing.Platform.OutputDevice.Terminal;

internal sealed class TerminalTestReporterCommandLineOptionsProvider : CommandLineOptionsProviderBase
{
    public const string NoProgressOption = "no-progress";
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
            nameof(TerminalTestReporterCommandLineOptionsProvider),
            PlatformVersion.Version,
            PlatformResources.TerminalTestReporterDisplayName,
            PlatformResources.TerminalTestReporterDescription,
            [
                new(NoProgressOption, PlatformResources.TerminalNoProgressOptionDescription, ArgumentArity.Zero, isHidden: false, isBuiltIn: true),
                new(NoAnsiOption, PlatformResources.TerminalNoAnsiOptionDescription, ArgumentArity.Zero, isHidden: false, isBuiltIn: true),
                new(AnsiOption, PlatformResources.TerminalAnsiOptionDescription, ArgumentArity.ExactlyOne, isHidden: false, isBuiltIn: true),
                new(OutputOption, PlatformResources.TerminalOutputOptionDescription, ArgumentArity.ExactlyOne, isHidden: false, isBuiltIn: true),
                new(ShowStdoutOption, PlatformResources.TerminalShowStdoutOptionDescription, ArgumentArity.ExactlyOne, isHidden: false, isBuiltIn: true),
                new(ShowStderrOption, PlatformResources.TerminalShowStderrOptionDescription, ArgumentArity.ExactlyOne, isHidden: false, isBuiltIn: true),
            ])
    {
    }

    public override Task<ValidationResult> ValidateOptionArgumentsAsync(CommandLineOption commandOption, string[] arguments)
        => commandOption.Name switch
        {
            NoProgressOption => ValidationResult.ValidTask,
            NoAnsiOption => ValidationResult.ValidTask,
            AnsiOption => arguments.Length == 1 && CommandLineOptionArgumentValidator.IsValidBooleanAutoArgument(arguments[0])
                ? ValidationResult.ValidTask
                : ValidationResult.InvalidTask(PlatformResources.TerminalAnsiOptionInvalidArgument),
            OutputOption => OutputOptionNormalArgument.Equals(arguments[0], StringComparison.OrdinalIgnoreCase) || OutputOptionDetailedArgument.Equals(arguments[0], StringComparison.OrdinalIgnoreCase)
                ? ValidationResult.ValidTask
                : ValidationResult.InvalidTask(PlatformResources.TerminalOutputOptionInvalidArgument),
            ShowStdoutOption or ShowStderrOption => IsValidShowOutputArgument(arguments[0])
                ? ValidationResult.ValidTask
                : ValidationResult.InvalidTask(PlatformResources.TerminalShowOutputOptionInvalidArgument),
            _ => throw ApplicationStateGuard.Unreachable(),
        };

    private static bool IsValidShowOutputArgument(string argument)
        => ShowOutputAllArgument.Equals(argument, StringComparison.OrdinalIgnoreCase)
            || ShowOutputFailedArgument.Equals(argument, StringComparison.OrdinalIgnoreCase)
            || ShowOutputNoneArgument.Equals(argument, StringComparison.OrdinalIgnoreCase);
}
