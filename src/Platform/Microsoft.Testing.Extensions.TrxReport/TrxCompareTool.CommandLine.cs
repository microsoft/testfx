// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;

using Microsoft.Testing.Extensions.TestReports.Resources;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Tools;

namespace Microsoft.Testing.Extensions.TrxReport.Abstractions;

internal sealed class TrxCompareToolCommandLine : IToolCommandLineOptionsProvider
{
    public const string BaselineTrxOptionName = "baseline-trx";
    public const string TrxToCompareOptionName = "trx-to-compare";
    private readonly IExtension _extension;

    public TrxCompareToolCommandLine(IExtension extension)
    {
        _extension = extension;
        Uid = _extension.Uid;
        Version = _extension.Version;
        DisplayName = _extension.DisplayName;
        Description = _extension.Description;
    }

    /// <inheritdoc />
    public string Uid { get; }

    /// <inheritdoc />
    public string Version { get; } = AppVersion.DefaultSemVer;

    /// <inheritdoc />
    public string DisplayName { get; }

    /// <inheritdoc />
    public string Description { get; }

    /// <inheritdoc />
    public string ToolName { get; } = TrxCompareTool.ToolName;

    /// <inheritdoc />
    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public IReadOnlyCollection<CommandLineOption> GetCommandLineOptions()
        =>
        [
            new(BaselineTrxOptionName, ExtensionResources.TrxComparerToolBaselineFileOptionDescription, ArgumentArity.ExactlyOne, false),
            new(TrxToCompareOptionName, ExtensionResources.TrxComparerToolOtherFileOptionDescription, ArgumentArity.ExactlyOne, false)
        ];

    public Task<ValidationResult> ValidateOptionArgumentsAsync(CommandLineOption commandOption, string[] arguments)
    {
        if (commandOption.Name == BaselineTrxOptionName && (!arguments[0].EndsWith(".trx", StringComparison.OrdinalIgnoreCase) || !File.Exists(arguments[0])))
        {
            return ValidationResult.InvalidTask(string.Format(CultureInfo.InvariantCulture, ExtensionResources.TrxComparerToolOptionExpectsSingleArgument, BaselineTrxOptionName));
        }

        if (commandOption.Name == TrxToCompareOptionName && (!arguments[0].EndsWith(".trx", StringComparison.OrdinalIgnoreCase) || !File.Exists(arguments[0])))
        {
            return ValidationResult.InvalidTask(string.Format(CultureInfo.InvariantCulture, ExtensionResources.TrxComparerToolOptionExpectsSingleArgument, TrxToCompareOptionName));
        }

        // No problem found
        return ValidationResult.ValidTask;
    }

    public Task<ValidationResult> ValidateCommandLineOptionsAsync(ICommandLineOptions commandLineOptions)
    {
        if ((commandLineOptions.IsOptionSet(BaselineTrxOptionName) && !commandLineOptions.IsOptionSet(TrxToCompareOptionName))
            || (!commandLineOptions.IsOptionSet(BaselineTrxOptionName) && commandLineOptions.IsOptionSet(TrxToCompareOptionName)))
        {
            return Task.FromResult(
                ValidationResult.Invalid(
                    string.Format(CultureInfo.InvariantCulture, ExtensionResources.TrxComparerToolBothFilesMustBeSpecified, BaselineTrxOptionName, TrxToCompareOptionName)));
        }

        // No problem found
        return ValidationResult.ValidTask;
    }
}
