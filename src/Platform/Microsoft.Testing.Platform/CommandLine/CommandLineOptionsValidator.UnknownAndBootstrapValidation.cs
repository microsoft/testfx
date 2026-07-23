// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Resources;

namespace Microsoft.Testing.Platform.CommandLine;

internal static partial class CommandLineOptionsValidator
{
    private static ValidationResult ValidateNoUnknownOptions(
        CommandLineParseResult parseResult,
        IReadOnlyList<JsonCommandLineOptionEntry>? jsonCommandLineOptions,
        Dictionary<ICommandLineOptionsProvider, IReadOnlyCollection<CommandLineOption>> extensionOptionsByProvider,
        Dictionary<ICommandLineOptionsProvider, IReadOnlyCollection<CommandLineOption>> systemOptionsByProvider)
    {
        // Use OrdinalIgnoreCase so a JSON entry like "Timeout" resolves to the registered "timeout"
        // option (testconfig.json keys are case-insensitive everywhere else in the platform). CLI
        // parsing is already case-sensitive but a case-insensitive lookup is a strict superset and
        // does not change CLI behavior.
        var validOptionNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<ICommandLineOptionsProvider, IReadOnlyCollection<CommandLineOption>> provider in extensionOptionsByProvider)
        {
            foreach (CommandLineOption option in provider.Value)
            {
                validOptionNames.Add(option.Name);
            }
        }

        foreach (KeyValuePair<ICommandLineOptionsProvider, IReadOnlyCollection<CommandLineOption>> provider in systemOptionsByProvider)
        {
            foreach (CommandLineOption option in provider.Value)
            {
                validOptionNames.Add(option.Name);
            }
        }

        StringBuilder? stringBuilder = null;
        foreach (CommandLineParseOption optionRecord in parseResult.Options)
        {
            if (!validOptionNames.Contains(optionRecord.Name))
            {
                stringBuilder ??= new();
                stringBuilder.AppendLine(string.Format(CultureInfo.InvariantCulture, PlatformResources.CommandLineUnknownOption, optionRecord.Name));
            }
        }

        // Also surface unknown entries under the testconfig.json "commandLineOptions" section.
        // We intentionally validate even when the CLI provides a matching option of the same name
        // (which would shadow the JSON value at lookup time): a JSON typo silently overridden by
        // the CLI is still a typo that the user wants to know about.
        if (jsonCommandLineOptions is { Count: > 0 })
        {
            foreach (JsonCommandLineOptionEntry entry in jsonCommandLineOptions)
            {
                if (!validOptionNames.Contains(entry.OptionName))
                {
                    stringBuilder ??= new();
                    string innerError = string.Format(CultureInfo.InvariantCulture, PlatformResources.CommandLineUnknownOption, entry.OptionName);
                    stringBuilder.AppendLine(string.Format(CultureInfo.InvariantCulture, PlatformResources.JsonCommandLineOptionsValidationErrorPrefix, innerError));
                }
            }
        }

        return stringBuilder?.Length > 0
            ? ValidationResult.Invalid(stringBuilder.ToTrimmedString())
            : ValidationResult.Valid();
    }

    private static ValidationResult ValidateNoBootstrapOnlyOptionsInJson(
        IReadOnlyList<JsonCommandLineOptionEntry>? jsonCommandLineOptions)
    {
        if (jsonCommandLineOptions is not { Count: > 0 })
        {
            return ValidationResult.Valid();
        }

        StringBuilder? stringBuilder = null;
        foreach (JsonCommandLineOptionEntry entry in jsonCommandLineOptions)
        {
            if (!BootstrapOnlyOptions.Contains(entry.OptionName))
            {
                continue;
            }

            stringBuilder ??= new();
            string innerError = string.Format(CultureInfo.InvariantCulture, PlatformResources.JsonCommandLineOptionIsBootstrapOnlyErrorMessage, entry.OptionName);
            stringBuilder.AppendLine(string.Format(CultureInfo.InvariantCulture, PlatformResources.JsonCommandLineOptionsValidationErrorPrefix, innerError));
        }

        return stringBuilder?.Length > 0
            ? ValidationResult.Invalid(stringBuilder.ToTrimmedString())
            : ValidationResult.Valid();
    }
}
