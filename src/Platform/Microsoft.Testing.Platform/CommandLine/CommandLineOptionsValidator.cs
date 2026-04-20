// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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

        if (await ValidateOptionsArgumentsAsync(commandLineParseResult, providerAndOptionByOptionName).ConfigureAwait(false) is { IsValid: false } result6)
        {
            return result6;
        }

        // Last validation step
        return await ValidateConfigurationAsync(extensionOptionsByProvider.Keys, systemOptionsByProvider.Keys, commandLineOptions).ConfigureAwait(false);
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
        var systemOptionNames = new HashSet<string>();
        foreach (KeyValuePair<ICommandLineOptionsProvider, IReadOnlyCollection<CommandLineOption>> provider in systemOptionsByProvider)
        {
            foreach (CommandLineOption option in provider.Value)
            {
                systemOptionNames.Add(option.Name);
            }
        }

        // Aggregate reserved options by name and track all offending providers
        var reservedOptionToProviderNames = new Dictionary<string, HashSet<string>>();
        foreach (KeyValuePair<ICommandLineOptionsProvider, IReadOnlyCollection<CommandLineOption>> kvp in extensionOptionsByProvider)
        {
            foreach (CommandLineOption option in kvp.Value)
            {
                if (systemOptionNames.Contains(option.Name))
                {
                    if (!reservedOptionToProviderNames.TryGetValue(option.Name, out HashSet<string>? providerNames))
                    {
                        providerNames = new HashSet<string>();
                        reservedOptionToProviderNames[option.Name] = providerNames;
                    }

                    providerNames.Add(kvp.Key.DisplayName);
                }
            }
        }

        StringBuilder? stringBuilder = null;
        foreach (KeyValuePair<string, HashSet<string>> kvp in reservedOptionToProviderNames)
        {
            stringBuilder ??= new StringBuilder();
            stringBuilder.AppendLine(string.Format(CultureInfo.InvariantCulture, PlatformResources.CommandLineOptionIsReserved, kvp.Key, string.Join("', '", kvp.Value)));
        }

        return stringBuilder?.Length > 0
            ? ValidationResult.Invalid(stringBuilder.ToTrimmedString())
            : ValidationResult.Valid();
    }

    private static ValidationResult ValidateOptionsAreNotDuplicated(
        Dictionary<ICommandLineOptionsProvider, IReadOnlyCollection<CommandLineOption>> extensionOptionsByProvider)
    {
        // Use a dictionary to track option names and their distinct providers
        var optionNameToProviders = new Dictionary<string, HashSet<ICommandLineOptionsProvider>>();
        foreach (KeyValuePair<ICommandLineOptionsProvider, IReadOnlyCollection<CommandLineOption>> kvp in extensionOptionsByProvider)
        {
            ICommandLineOptionsProvider provider = kvp.Key;
            foreach (CommandLineOption option in kvp.Value)
            {
                string name = option.Name;
                if (!optionNameToProviders.TryGetValue(name, out HashSet<ICommandLineOptionsProvider>? providers))
                {
                    providers = [];
                    optionNameToProviders[name] = providers;
                }

                providers.Add(provider);
            }
        }

        // Check for duplications
        StringBuilder? stringBuilder = null;
        foreach (KeyValuePair<string, HashSet<ICommandLineOptionsProvider>> kvp in optionNameToProviders)
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
        var validOptionNames = new HashSet<string>();
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
            ValidationResult result = await provider.ValidateOptionArgumentsAsync(option, optionRecord.Arguments).ConfigureAwait(false);
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

    private static string ToTrimmedString(this StringBuilder stringBuilder)
    {
        // Trim trailing CR/LF characters directly from the StringBuilder to avoid extra allocations
        while (stringBuilder.Length > 0 && stringBuilder[stringBuilder.Length - 1] is '\r' or '\n')
        {
            stringBuilder.Length--;
        }

        return stringBuilder.ToString();
    }
}
