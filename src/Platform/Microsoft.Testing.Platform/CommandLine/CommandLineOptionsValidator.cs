// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Resources;
using Microsoft.Testing.Platform.Tools;

namespace Microsoft.Testing.Platform.CommandLine;

internal static class CommandLineOptionsValidator
{
    // Options whose values are read directly from CommandLineParseResult during platform
    // bootstrap, before IConfiguration (and therefore testconfig.json) is built. Setting them
    // under "commandLineOptions" in testconfig.json would silently have no effect — fail fast
    // with a clear error instead. Keep this list in sync with the call sites that pull values
    // out of CommandLineParseResult before configuration is built (see TestApplication.cs and
    // JsonConfigurationProvider.cs).
    private static readonly HashSet<string> BootstrapOnlyOptions = new(
        [
            PlatformCommandLineProvider.ConfigFileOptionKey,
            PlatformCommandLineProvider.DiagnosticOptionKey,
            PlatformCommandLineProvider.DiagnosticOutputDirectoryOptionKey,
            PlatformCommandLineProvider.DiagnosticOutputFilePrefixOptionKey,
            PlatformCommandLineProvider.DiagnosticVerbosityOptionKey,
            PlatformCommandLineProvider.DiagnosticFileLoggerSynchronousWriteOptionKey,
        ],
        StringComparer.OrdinalIgnoreCase);

    public static async Task<ValidationResult> ValidateAsync(
        CommandLineParseResult commandLineParseResult,
        IEnumerable<ICommandLineOptionsProvider> systemCommandLineOptionsProviders,
        IEnumerable<ICommandLineOptionsProvider> extensionCommandLineOptionsProviders,
        ICommandLineOptions commandLineOptions,
        IReadOnlyList<JsonCommandLineOptionEntry>? jsonCommandLineOptions = null)
    {
        if (commandLineParseResult.HasError)
        {
            StringBuilder stringBuilder = new();
            stringBuilder.AppendLine(PlatformResources.InvalidCommandLineArguments);
            foreach (string error in commandLineParseResult.Errors)
            {
                stringBuilder.AppendLine(CultureInfo.InvariantCulture, $"\t- {error}");
            }

            return InvalidWithCommandLine(commandLineParseResult, stringBuilder.ToTrimmedString());
        }

        extensionCommandLineOptionsProviders =
            extensionCommandLineOptionsProviders.Where(provider => provider is not IToolCommandLineOptionsProvider);

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

        if (ValidateOptionsAreNotDuplicated(extensionOptionsByProvider, systemOptionsByProvider) is { IsValid: false } result3)
        {
            return result3;
        }

        if (ValidateNoUnknownOptions(commandLineParseResult, jsonCommandLineOptions, extensionOptionsByProvider, systemOptionsByProvider) is { IsValid: false } result4)
        {
            return AddCommandLine(commandLineParseResult, result4);
        }

        if (ValidateNoBootstrapOnlyOptionsInJson(jsonCommandLineOptions) is { IsValid: false } resultBootstrap)
        {
            return AddCommandLine(commandLineParseResult, resultBootstrap);
        }

        // Option names from the platform side are unique (validated above) and lookups against this
        // dictionary may be triggered from the CLI parse result (case-sensitive) or from JSON
        // (testconfig.json is case-insensitive everywhere else). Use OrdinalIgnoreCase so a JSON
        // entry such as "Timeout": "30s" resolves to the same provider/option metadata as
        // "--timeout 30s" on the command line.
        var providerAndOptionByOptionName = extensionOptionsByProvider.Concat(systemOptionsByProvider)
            .SelectMany(tuple => tuple.Value.Select(option => (provider: tuple.Key, option)))
            .ToDictionary(tuple => tuple.option.Name, StringComparer.OrdinalIgnoreCase);

        if (ValidateOptionsArgumentArity(commandLineParseResult, jsonCommandLineOptions, providerAndOptionByOptionName) is { IsValid: false } result5)
        {
            return AddCommandLine(commandLineParseResult, result5);
        }

        if (await ValidateOptionsArgumentsAsync(commandLineParseResult, jsonCommandLineOptions, providerAndOptionByOptionName).ConfigureAwait(false) is { IsValid: false } result6)
        {
            return AddCommandLine(commandLineParseResult, result6);
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
        // Create a HashSet of all system option names for faster lookup. OrdinalIgnoreCase keeps
        // duplicate detection consistent with the case-insensitive lookups performed later in the
        // pipeline and with how testconfig.json stores keys.
        var systemOptionNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<ICommandLineOptionsProvider, IReadOnlyCollection<CommandLineOption>> provider in systemOptionsByProvider)
        {
            foreach (CommandLineOption option in provider.Value)
            {
                systemOptionNames.Add(option.Name);
            }
        }

        // Aggregate reserved options by name and track all offending providers
        var reservedOptionToProviderNames = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<ICommandLineOptionsProvider, IReadOnlyCollection<CommandLineOption>> kvp in extensionOptionsByProvider)
        {
            foreach (CommandLineOption option in kvp.Value)
            {
                if (systemOptionNames.Contains(option.Name))
                {
                    if (!reservedOptionToProviderNames.TryGetValue(option.Name, out HashSet<string>? providerNames))
                    {
                        providerNames = [];
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
        Dictionary<ICommandLineOptionsProvider, IReadOnlyCollection<CommandLineOption>> extensionOptionsByProvider,
        Dictionary<ICommandLineOptionsProvider, IReadOnlyCollection<CommandLineOption>> systemOptionsByProvider)
    {
        // Use a dictionary to track option names and their distinct providers. OrdinalIgnoreCase
        // ensures we catch case-differing duplicates (e.g. "Timeout" vs "timeout") with a friendly
        // error rather than a later ArgumentException when building the lookup dictionary.
        // We cover both extension and system providers so that any pair of case-differing
        // duplicates — extension/extension, extension/system, or system/system — surfaces as a
        // ValidationResult.Invalid rather than crashing the platform.
        var optionNameToProviders = new Dictionary<string, HashSet<ICommandLineOptionsProvider>>(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<ICommandLineOptionsProvider, IReadOnlyCollection<CommandLineOption>> kvp in extensionOptionsByProvider.Concat(systemOptionsByProvider))
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

    private static async Task<ValidationResult> ValidateOptionsArgumentsAsync(
        CommandLineParseResult parseResult,
        IReadOnlyList<JsonCommandLineOptionEntry>? jsonCommandLineOptions,
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

    private static string ToTrimmedString(this StringBuilder stringBuilder)
    {
        // Trim trailing CR/LF characters directly from the StringBuilder to avoid extra allocations
        while (stringBuilder.Length > 0 && stringBuilder[stringBuilder.Length - 1] is '\r' or '\n')
        {
            stringBuilder.Length--;
        }

        return stringBuilder.ToString();
    }

    private static ValidationResult AddCommandLine(CommandLineParseResult parseResult, ValidationResult result)
        => result.IsValid ? result : InvalidWithCommandLine(parseResult, result.ErrorMessage);

    private static ValidationResult InvalidWithCommandLine(CommandLineParseResult parseResult, string errorMessage)
    {
        if (parseResult.CommandLine.Length == 0)
        {
            return ValidationResult.Invalid(errorMessage);
        }

        var stringBuilder = new StringBuilder(errorMessage);
        stringBuilder.AppendLine();
        stringBuilder.AppendFormat(CultureInfo.InvariantCulture, PlatformResources.CommandLineValidationCommandLine, parseResult.CommandLine);
        return ValidationResult.Invalid(stringBuilder.ToString());
    }
}
