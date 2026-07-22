// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Resources;

namespace Microsoft.Testing.Platform.CommandLine;

internal static partial class CommandLineOptionsValidator
{
    private static async Task<ValidationResult> ValidateOptionsArgumentsAsync(
        CommandLineParseResult parseResult,
        IReadOnlyList<JsonCommandLineOptionEntry>? jsonCommandLineOptions,
        Dictionary<string, (ICommandLineOptionsProvider Provider, CommandLineOption Option)> providerAndOptionByOptionName)
    {
        if (parseResult is null)
        {
            throw new ArgumentNullException(nameof(parseResult));
        }

        StringBuilder? stringBuilder = null;
        foreach (IGrouping<string, CommandLineParseOption> optionRecords in parseResult.Options.GroupBy(
            record => record.Name,
            StringComparer.OrdinalIgnoreCase))
        {
            (ICommandLineOptionsProvider provider, CommandLineOption option) = providerAndOptionByOptionName[optionRecords.Key];
            string[] arguments = [.. optionRecords.SelectMany(record => record.Arguments)];
            ValidationResult result = await provider.ValidateOptionArgumentsAsync(option, arguments).ConfigureAwait(false);
            if (!result.IsValid)
            {
                stringBuilder ??= new();
                stringBuilder.AppendLine(string.Format(CultureInfo.InvariantCulture, PlatformResources.CommandLineInvalidArgumentsForOption, optionRecords.Key, result.ErrorMessage));
            }
        }

        // Apply the per-option argument validators to JSON-sourced entries as well. Skip disabled
        // entries (nothing to validate) and entries that the prior arity pass already flagged
        // (calling a provider's validator with too-few/too-many arguments may produce confusing
        // secondary errors or, worse, index out of bounds inside the validator itself).
        if (jsonCommandLineOptions is { Count: > 0 })
        {
            foreach (JsonCommandLineOptionEntry entry in jsonCommandLineOptions)
            {
                if (entry.IsDisabled)
                {
                    continue;
                }

                if (!providerAndOptionByOptionName.TryGetValue(entry.OptionName, out (ICommandLineOptionsProvider Provider, CommandLineOption Option) match))
                {
                    continue;
                }

                if (entry.Arguments.Count < match.Option.Arity.Min || entry.Arguments.Count > match.Option.Arity.Max)
                {
                    continue;
                }

                string[] argumentsArray = entry.Arguments as string[] ?? entry.Arguments.ToArray();
                ValidationResult result = await match.Provider.ValidateOptionArgumentsAsync(match.Option, argumentsArray).ConfigureAwait(false);
                if (!result.IsValid)
                {
                    stringBuilder ??= new();
                    string innerError = string.Format(CultureInfo.InvariantCulture, PlatformResources.CommandLineInvalidArgumentsForOption, entry.OptionName, result.ErrorMessage);
                    stringBuilder.AppendLine(string.Format(CultureInfo.InvariantCulture, PlatformResources.JsonCommandLineOptionsValidationErrorPrefix, innerError));
                }
            }
        }

        return stringBuilder?.Length > 0
            ? ValidationResult.Invalid(stringBuilder.ToTrimmedString())
            : ValidationResult.Valid();
    }

    private static async Task<ValidationResult> ValidateConfigurationAsync(
        Dictionary<ICommandLineOptionsProvider, IReadOnlyCollection<CommandLineOption>>.KeyCollection extensionsProviders,
        Dictionary<ICommandLineOptionsProvider, IReadOnlyCollection<CommandLineOption>>.KeyCollection systemProviders,
        ICommandLineOptions commandLineOptions)
    {
        StringBuilder? stringBuilder = await ValidateConfigurationAsync(systemProviders, commandLineOptions, null).ConfigureAwait(false);
        stringBuilder = await ValidateConfigurationAsync(extensionsProviders, commandLineOptions, stringBuilder).ConfigureAwait(false);

        return stringBuilder?.Length > 0
            ? ValidationResult.Invalid(stringBuilder.ToTrimmedString())
            : ValidationResult.Valid();
    }

    private static async Task<StringBuilder?> ValidateConfigurationAsync(
        Dictionary<ICommandLineOptionsProvider, IReadOnlyCollection<CommandLineOption>>.KeyCollection providers,
        ICommandLineOptions commandLineOptions,
        StringBuilder? stringBuilder)
    {
        foreach (ICommandLineOptionsProvider commandLineOptionsProvider in providers)
        {
            ValidationResult result = await commandLineOptionsProvider.ValidateCommandLineOptionsAsync(commandLineOptions).ConfigureAwait(false);
            if (!result.IsValid)
            {
                stringBuilder ??= new();
                stringBuilder.AppendLine(string.Format(CultureInfo.InvariantCulture, PlatformResources.CommandLineInvalidConfiguration, commandLineOptionsProvider.DisplayName, commandLineOptionsProvider.Uid, result.ErrorMessage));
                stringBuilder.AppendLine();
            }
        }

        return stringBuilder;
    }
}
