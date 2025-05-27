// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Resources;

namespace Microsoft.Testing.Platform.CommandLine;

internal static class CommandLineOptionsValidator
{
    public static async Task<ValidationResult> ValidateAsync(
        CommandLineParseResult commandLineParseResult,
        IEnumerable<ICommandLineOptionsProvider> systemCommandLineOptionsProviders,
        IEnumerable<ICommandLineOptionsProvider> extensionCommandLineOptionsProviders,
        ICommandLineOptions commandLineOptions)
    {
        if (commandLineParseResult.HasError)
        {
            StringBuilder stringBuilder = new();
            stringBuilder.AppendLine(PlatformResources.InvalidCommandLineArguments);
            foreach (string error in commandLineParseResult.Errors)
            {
                stringBuilder.AppendLine(CultureInfo.InvariantCulture, $"\t- {error}");
            }

            return ValidationResult.Invalid(stringBuilder.ToTrimmedString());
        }

        var extensionOptionsByProvider = extensionCommandLineOptionsProviders.ToDictionary(p => p, p => p.GetCommandLineOptions());
        if (ValidateExtensionOptionsDoNotContainReservedPrefix(extensionOptionsByProvider) is { IsValid: false } result)
        {
            return result;
        }

        var systemOptionsByProvider = systemCommandLineOptionsProviders.ToDictionary(p => p, p => p.GetCommandLineOptions());
        if (ValidateExtensionOptionsDoNotContainReservedOptions(extensionOptionsByProvider, systemOptionsByProvider) is { IsValid: false } result2)
        {
            return result2;
        }

        if (ValidateOptionsAreNotDuplicated(extensionOptionsByProvider) is { IsValid: false } result3)
        {
            return result3;
        }

        if (ValidateNoUnknownOptions(commandLineParseResult, extensionOptionsByProvider, systemOptionsByProvider) is { IsValid: false } result4)
        {
            return result4;
        }

        var providerAndOptionByOptionName = extensionOptionsByProvider.Union(systemOptionsByProvider)
            .SelectMany(tuple => tuple.Value.Select(option => (provider: tuple.Key, option)))
            .ToDictionary(tuple => tuple.option.Name);

        if (ValidateOptionsArgumentArity(commandLineParseResult, providerAndOptionByOptionName) is { IsValid: false } result5)
        {
            return result5;
        }

        if (await ValidateOptionsArgumentsAsync(commandLineParseResult, providerAndOptionByOptionName) is { IsValid: false } result6)
        {
            return result6;
        }

        // Last validation step
        return await ValidateConfigurationAsync(extensionOptionsByProvider.Keys, systemOptionsByProvider.Keys, commandLineOptions);
    }

    private static ValidationResult ValidateExtensionOptionsDoNotContainReservedPrefix(
        Dictionary<ICommandLineOptionsProvider, IReadOnlyCollection<CommandLineOption>> extensionOptionsByProvider)
    {
        StringBuilder? stringBuilder = null;
        foreach (KeyValuePair<ICommandLineOptionsProvider, IReadOnlyCollection<CommandLineOption>> providerWithOptions in extensionOptionsByProvider)
        {
            foreach (CommandLineOption option in providerWithOptions.Value)
            {
                if (option.IsBuiltIn)
                {
                    continue;
                }

                string trimmedOption = option.Name.Trim(CommandLineParseResult.OptionPrefix);
                if (trimmedOption.StartsWith("internal", StringComparison.OrdinalIgnoreCase)
                    || option.Name.StartsWith("-internal", StringComparison.OrdinalIgnoreCase))
                {
                    stringBuilder ??= new();
                    ICommandLineOptionsProvider commandLineOptionsProvider = providerWithOptions.Key;
                    stringBuilder.AppendLine(string.Format(CultureInfo.InvariantCulture, PlatformResources.CommandLineOptionIsUsingReservedPrefix, trimmedOption, commandLineOptionsProvider.DisplayName, commandLineOptionsProvider.Uid));
                }
            }
        }

        return stringBuilder?.Length > 0
            ? ValidationResult.Invalid(stringBuilder.ToTrimmedString())
            : ValidationResult.Valid();
    }

    private static ValidationResult ValidateExtensionOptionsDoNotContainReservedOptions(
        Dictionary<ICommandLineOptionsProvider, IReadOnlyCollection<CommandLineOption>> extensionOptionsByProvider,
        Dictionary<ICommandLineOptionsProvider, IReadOnlyCollection<CommandLineOption>> systemOptionsByProvider)
    {
        // Create a HashSet of all system option names for faster lookup
        HashSet<string> systemOptionNames = new();
        foreach (var provider in systemOptionsByProvider)
        {
            foreach (var option in provider.Value)
            {
                systemOptionNames.Add(option.Name);
            }
        }

        StringBuilder? stringBuilder = null;
        foreach (var provider in extensionOptionsByProvider)
        {
            foreach (var option in provider.Value)
            {
                if (systemOptionNames.Contains(option.Name))
                {
                    stringBuilder ??= new StringBuilder();
                    stringBuilder.AppendLine(string.Format(CultureInfo.InvariantCulture,
                        PlatformResources.CommandLineOptionIsReserved,
                        option.Name,
                        provider.Key.DisplayName));
                }
            }
        }

        return stringBuilder?.Length > 0
            ? ValidationResult.Invalid(stringBuilder.ToTrimmedString())
            : ValidationResult.Valid();
    }

    private static ValidationResult ValidateOptionsAreNotDuplicated(
        Dictionary<ICommandLineOptionsProvider, IReadOnlyCollection<CommandLineOption>> extensionOptionsByProvider)
    {
        // Use a dictionary to track option names and their providers
        Dictionary<string, List<ICommandLineOptionsProvider>> optionNameToProviders = new();
        foreach (var kvp in extensionOptionsByProvider)
        {
            var provider = kvp.Key;
            foreach (var option in kvp.Value)
            {
                string name = option.Name;
                if (!optionNameToProviders.TryGetValue(name, out var providers))
                {
                    providers = new List<ICommandLineOptionsProvider>();
                    optionNameToProviders[name] = providers;
                }

                providers.Add(provider);
            }
        }

        // Check for duplications
        StringBuilder? stringBuilder = null;
        foreach (var kvp in optionNameToProviders)
        {
            if (kvp.Value.Count > 1)
            {
                string duplicatedOption = kvp.Key;
                stringBuilder ??= new();
                IEnumerable<string> faultyProvidersDisplayNames = kvp.Value.Select(p => p.DisplayName);
                stringBuilder.AppendLine(string.Format(CultureInfo.InvariantCulture, PlatformResources.CommandLineOptionIsDeclaredByMultipleProviders, duplicatedOption, string.Join("', '", faultyProvidersDisplayNames)));
            }
        }

        return stringBuilder?.Length > 0
            ? ValidationResult.Invalid(stringBuilder.ToTrimmedString())
            : ValidationResult.Valid();
    }

    private static ValidationResult ValidateNoUnknownOptions(
        CommandLineParseResult parseResult,
        Dictionary<ICommandLineOptionsProvider, IReadOnlyCollection<CommandLineOption>> extensionOptionsByProvider,
        Dictionary<ICommandLineOptionsProvider, IReadOnlyCollection<CommandLineOption>> systemOptionsByProvider)
    {
        // Create a HashSet of all valid option names for faster lookup
        HashSet<string> validOptionNames = new();
        foreach (var provider in extensionOptionsByProvider)
        {
            foreach (var option in provider.Value)
            {
                validOptionNames.Add(option.Name);
            }
        }
        
        foreach (var provider in systemOptionsByProvider)
        {
            foreach (var option in provider.Value)
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

        return stringBuilder?.Length > 0
            ? ValidationResult.Invalid(stringBuilder.ToTrimmedString())
            : ValidationResult.Valid();
    }

    private static ValidationResult ValidateOptionsArgumentArity(
        CommandLineParseResult parseResult,
        Dictionary<string, (ICommandLineOptionsProvider Provider, CommandLineOption Option)> providerAndOptionByOptionName)
    {
        StringBuilder? stringBuilder = null;
        foreach (IGrouping<string, CommandLineParseOption> groupedOptions in parseResult.Options.GroupBy(x => x.Name))
        {
            // getting the arguments count for an option.
            int arity = 0;
            foreach (CommandLineParseOption optionEntry in groupedOptions)
            {
                arity += optionEntry.Arguments.Length;
            }

            string optionName = groupedOptions.Key;
            (ICommandLineOptionsProvider provider, CommandLineOption option) = providerAndOptionByOptionName[optionName];

            if (arity > option.Arity.Max && option.Arity.Max == 0)
            {
                stringBuilder ??= new();
                stringBuilder.AppendLine(string.Format(CultureInfo.InvariantCulture, PlatformResources.CommandLineOptionExpectsNoArguments, optionName, provider.DisplayName, provider.Uid));
            }
            else if (arity < option.Arity.Min)
            {
                stringBuilder ??= new();
                stringBuilder.AppendLine(string.Format(CultureInfo.InvariantCulture, PlatformResources.CommandLineOptionExpectsAtLeastArguments, optionName, provider.DisplayName, provider.Uid, option.Arity.Min));
            }
            else if (arity > option.Arity.Max)
            {
                stringBuilder ??= new();
                stringBuilder.AppendLine(string.Format(CultureInfo.InvariantCulture, PlatformResources.CommandLineOptionExpectsAtMostArguments, optionName, provider.DisplayName, provider.Uid, option.Arity.Max));
            }
        }

        return stringBuilder?.Length > 0
            ? ValidationResult.Invalid(stringBuilder.ToTrimmedString())
            : ValidationResult.Valid();
    }

    private static async Task<ValidationResult> ValidateOptionsArgumentsAsync(
        CommandLineParseResult parseResult,
        Dictionary<string, (ICommandLineOptionsProvider Provider, CommandLineOption Option)> providerAndOptionByOptionName)
    {
        ApplicationStateGuard.Ensure(parseResult is not null);

        StringBuilder? stringBuilder = null;
        foreach (CommandLineParseOption optionRecord in parseResult.Options)
        {
            (ICommandLineOptionsProvider provider, CommandLineOption option) = providerAndOptionByOptionName[optionRecord.Name];
            ValidationResult result = await provider.ValidateOptionArgumentsAsync(option, optionRecord.Arguments);
            if (!result.IsValid)
            {
                stringBuilder ??= new();
                stringBuilder.AppendLine(string.Format(CultureInfo.InvariantCulture, PlatformResources.CommandLineInvalidArgumentsForOption, optionRecord.Name, result.ErrorMessage));
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
        StringBuilder? stringBuilder = await ValidateConfigurationAsync(systemProviders, commandLineOptions, null);
        stringBuilder = await ValidateConfigurationAsync(extensionsProviders, commandLineOptions, stringBuilder);

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
            ValidationResult result = await commandLineOptionsProvider.ValidateCommandLineOptionsAsync(commandLineOptions);
            if (!result.IsValid)
            {
                stringBuilder ??= new();
                stringBuilder.AppendLine(string.Format(CultureInfo.InvariantCulture, PlatformResources.CommandLineInvalidConfiguration, commandLineOptionsProvider.DisplayName, commandLineOptionsProvider.Uid, result.ErrorMessage));
                stringBuilder.AppendLine();
            }
        }

        return stringBuilder;
    }

    private static string ToTrimmedString(this StringBuilder stringBuilder)
    {
        // Use a more efficient approach to trim without creating unnecessary intermediate strings
        string result = stringBuilder.ToString();
        int end = result.Length;
        
        // Find the last non-whitespace char
        while (end > 0)
        {
            char c = result[end - 1];
            if (c != '\r' && c != '\n')
            {
                break;
            }
            end--;
        }
        
        return end == result.Length ? result : result.Substring(0, end);
    }
}
