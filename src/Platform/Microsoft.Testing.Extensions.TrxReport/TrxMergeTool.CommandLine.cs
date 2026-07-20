// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.TrxReport.Resources;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Tools;

namespace Microsoft.Testing.Extensions.TrxReport.Abstractions;

internal sealed class TrxMergeToolCommandLine(IExtension extension) : IToolCommandLineOptionsProvider
{
    public const string InputOptionName = "input";
    public const string OutputOptionName = "output-trx";

    private readonly IExtension _extension = extension;

    public string Uid => _extension.Uid;

    public string Version => _extension.Version;

    public string DisplayName => _extension.DisplayName;

    public string Description => _extension.Description;

    public string ToolName => TrxMergeTool.ToolName;

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public IReadOnlyCollection<CommandLineOption> GetCommandLineOptions()
        =>
        [
            new(InputOptionName, ExtensionResources.TrxMergeToolInputOptionDescription, ArgumentArity.OneOrMore, false),
            new(OutputOptionName, ExtensionResources.TrxMergeToolOutputOptionDescription, ArgumentArity.ExactlyOne, false),
        ];

    public Task<ValidationResult> ValidateOptionArgumentsAsync(CommandLineOption commandOption, string[] arguments)
        => commandOption.Name == InputOptionName
            && (arguments.Length < 2 || arguments.Any(path => !path.EndsWith(".trx", StringComparison.OrdinalIgnoreCase) || !File.Exists(path)))
                ? ValidationResult.InvalidTask(ExtensionResources.TrxMergeToolInvalidInputs)
                : commandOption.Name == OutputOptionName
            && (arguments.Length != 1 || !arguments[0].EndsWith(".trx", StringComparison.OrdinalIgnoreCase))
                ? ValidationResult.InvalidTask(ExtensionResources.TrxMergeToolInvalidOutput)
                : ValidationResult.ValidTask;

    public Task<ValidationResult> ValidateCommandLineOptionsAsync(ICommandLineOptions commandLineOptions)
        => ValidationResult.ValidTask;
}
