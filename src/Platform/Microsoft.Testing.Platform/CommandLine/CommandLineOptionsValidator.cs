// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;

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
        IEnumerable<string> allExtensionOptions = extensionOptionsByProvider.Values.SelectMany(x => x).Select(x => x.Name).Distinct();
        IEnumerable<string> allSystemOptions = systemOptionsByProvider.Values.SelectMany(x => x).Select(x => x.Name).Distinct();

        IEnumerable<string> invalidReservedOptions = allSystemOptions.Intersect(allExtensionOptions);
        if (invalidReservedOptions.Any())
        {
            var stringBuilder = new StringBuilder();
            foreach (string reservedOption in invalidReservedOptions)
            {
                IEnumerable<string> faultyProviderNames = extensionOptionsByProvider.Where(tuple => tuple.Value.Any(x => x.Name == reservedOption)).Select(tuple => tuple.Key.DisplayName);
                stringBuilder.AppendLine(string.Format(CultureInfo.InvariantCulture, PlatformResources.CommandLineOptionIsReserved, reservedOption, string.Join("', '", faultyProviderNames)));
            }

            return ValidationResult.Invalid(stringBuilder.ToTrimmedString());
        }

        return ValidationResult.Valid();
    }

    private static ValidationResult ValidateOptionsAreNotDuplicated(
        Dictionary<ICommandLineOptionsProvider, IReadOnlyCollection<CommandLineOption>> extensionOptionsByProvider)
    {
        IEnumerable<string> duplications = extensionOptionsByProvider.Values.SelectMany(x => x)
            .Select(x => x.Name)
            .GroupBy(x => x)
            .Where(x => x.Skip(1).Any())
            .Select(x => x.Key);

        StringBuilder? stringBuilder = null;
        foreach (string duplicatedOption in duplications)
        {
            IEnumerable<string> faultyProvidersDisplayNames = extensionOptionsByProvider.Where(tuple => tuple.Value.Any(x => x.Name == duplicatedOption)).Select(tuple => tuple.Key.DisplayName);
            stringBuilder ??= new();
            stringBuilder.AppendLine(string.Format(CultureInfo.InvariantCulture, PlatformResources.CommandLineOptionIsDeclaredByMultipleProviders, duplicatedOption, string.Join("', '", faultyProvidersDisplayNames)));
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
        StringBuilder? stringBuilder = null;
        foreach (OptionRecord optionRecord in parseResult.Options)
        {
            if (!extensionOptionsByProvider.Union(systemOptionsByProvider).Any(tuple => tuple.Value.Any(x => x.Name == optionRecord.Option)))
            {
                stringBuilder ??= new();
                stringBuilder.AppendLine(string.Format(CultureInfo.InvariantCulture, PlatformResources.CommandLineUnknownOption, optionRecord.Option));
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
        StringBuilder stringBuilder = new();
        foreach (IGrouping<string, OptionRecord> groupedOptions in parseResult.Options.GroupBy(x => x.Option))
        {
            // getting the arguments count for an option.
            int arity = 0;
            foreach (OptionRecord optionEntry in groupedOptions)
            {
                arity += optionEntry.Arguments.Length;
            }

            string optionName = groupedOptions.Key;
            (ICommandLineOptionsProvider provider, CommandLineOption option) = providerAndOptionByOptionName[optionName];

            if (arity > option.Arity.Max && option.Arity.Max == 0)
            {
                stringBuilder.AppendLine(string.Format(CultureInfo.InvariantCulture, PlatformResources.CommandLineOptionExpectsNoArguments, optionName, provider.DisplayName, provider.Uid));
            }
            else if (arity < option.Arity.Min)
            {
                stringBuilder.AppendLine(string.Format(CultureInfo.InvariantCulture, PlatformResources.CommandLineOptionExpectsAtLeastArguments, optionName, provider.DisplayName, provider.Uid, option.Arity.Min));
            }
            else if (arity > option.Arity.Max)
            {
                stringBuilder.AppendLine(string.Format(CultureInfo.InvariantCulture, PlatformResources.CommandLineOptionExpectsAtMostArguments, optionName, provider.DisplayName, provider.Uid, option.Arity.Max));
            }
        }

        return stringBuilder.Length > 0
            ? ValidationResult.Invalid(stringBuilder.ToTrimmedString())
            : ValidationResult.Valid();
    }

    private static async Task<ValidationResult> ValidateOptionsArgumentsAsync(
        CommandLineParseResult parseResult,
        Dictionary<string, (ICommandLineOptionsProvider Provider, CommandLineOption Option)> providerAndOptionByOptionName)
    {
        ApplicationStateGuard.Ensure(parseResult is not null);

        StringBuilder? stringBuilder = null;
        foreach (OptionRecord optionRecord in parseResult.Options)
        {
            (ICommandLineOptionsProvider provider, CommandLineOption option) = providerAndOptionByOptionName[optionRecord.Option];
            ValidationResult result = await provider.ValidateOptionArgumentsAsync(option, optionRecord.Arguments);
            if (!result.IsValid)
            {
                stringBuilder ??= new();
                stringBuilder.AppendLine(string.Format(CultureInfo.InvariantCulture, PlatformResources.CommandLineInvalidArgumentsForOption, optionRecord.Option, result.ErrorMessage));
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
#pragma warning disable RS0030 // Do not use banned APIs
        => stringBuilder.ToString().TrimEnd(Environment.NewLine.ToCharArray());
#pragma warning restore RS0030 // Do not use banned APIs
}
