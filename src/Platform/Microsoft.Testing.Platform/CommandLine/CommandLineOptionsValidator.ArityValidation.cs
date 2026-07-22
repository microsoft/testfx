// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Resources;

namespace Microsoft.Testing.Platform.CommandLine;

internal static partial class CommandLineOptionsValidator
{
    private static ValidationResult ValidateOptionsArgumentArity(
        CommandLineParseResult parseResult,
        IReadOnlyList<JsonCommandLineOptionEntry>? jsonCommandLineOptions,
        Dictionary<string, (ICommandLineOptionsProvider Provider, CommandLineOption Option)> providerAndOptionByOptionName)
    {
        StringBuilder? stringBuilder = null;
        foreach (IGrouping<string, CommandLineParseOption> groupedOptions in parseResult.Options.GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
        {
            // getting the arguments count for an option.
            int arity = 0;
            foreach (CommandLineParseOption optionEntry in groupedOptions)
            {
                arity += optionEntry.Arguments.Length;
            }

            string optionName = groupedOptions.Key;
            (ICommandLineOptionsProvider provider, CommandLineOption option) = providerAndOptionByOptionName[optionName];

            AppendArityErrorIfNeeded(stringBuilder: ref stringBuilder, arity, optionName, provider, option, jsonPrefix: false);
        }

        // Apply the same arity rules to entries sourced from testconfig.json. We skip
        // explicit disable entries ("foo": false) because they convey "the option is not set" —
        // there are no arguments to validate. Unknown options were rejected earlier.
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

                AppendArityErrorIfNeeded(stringBuilder: ref stringBuilder, entry.Arguments.Count, entry.OptionName, match.Provider, match.Option, jsonPrefix: true);
            }
        }

        return stringBuilder?.Length > 0
            ? ValidationResult.Invalid(stringBuilder.ToTrimmedString())
            : ValidationResult.Valid();
    }

    private static void AppendArityErrorIfNeeded(
        ref StringBuilder? stringBuilder,
        int arity,
        string optionName,
        ICommandLineOptionsProvider provider,
        CommandLineOption option,
        bool jsonPrefix)
    {
        string? message = null;
        if (arity > option.Arity.Max && option.Arity.Max == 0)
        {
            message = string.Format(CultureInfo.InvariantCulture, PlatformResources.CommandLineOptionExpectsNoArguments, optionName, provider.DisplayName, provider.Uid);
        }
        else if (arity < option.Arity.Min)
        {
            message = string.Format(CultureInfo.InvariantCulture, PlatformResources.CommandLineOptionExpectsAtLeastArguments, optionName, provider.DisplayName, provider.Uid, option.Arity.Min);
        }
        else if (arity > option.Arity.Max)
        {
            message = string.Format(CultureInfo.InvariantCulture, PlatformResources.CommandLineOptionExpectsAtMostArguments, optionName, provider.DisplayName, provider.Uid, option.Arity.Max);
        }

        if (message is null)
        {
            return;
        }

        stringBuilder ??= new();
        stringBuilder.AppendLine(jsonPrefix
            ? string.Format(CultureInfo.InvariantCulture, PlatformResources.JsonCommandLineOptionsValidationErrorPrefix, message)
            : message);
    }
}
