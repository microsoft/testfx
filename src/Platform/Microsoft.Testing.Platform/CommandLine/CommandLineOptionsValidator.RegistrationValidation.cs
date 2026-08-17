// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Resources;

namespace Microsoft.Testing.Platform.CommandLine;

internal static partial class CommandLineOptionsValidator
{
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
        // Most options are unique, so retain only their first provider. Allocate a HashSet only
        // after finding a provider collision, while preserving the registration order used by
        // diagnostics. OrdinalIgnoreCase ensures we catch case-differing duplicates (e.g.
        // "Timeout" vs "timeout") with a friendly error rather than a later ArgumentException.
        var optionNameToProviders = new Dictionary<string, (ICommandLineOptionsProvider FirstProvider, HashSet<ICommandLineOptionsProvider>? DuplicateProviders)>(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<ICommandLineOptionsProvider, IReadOnlyCollection<CommandLineOption>> kvp in extensionOptionsByProvider.Concat(systemOptionsByProvider))
        {
            ICommandLineOptionsProvider provider = kvp.Key;
            foreach (CommandLineOption option in kvp.Value)
            {
                string name = option.Name;
                if (!optionNameToProviders.TryGetValue(name, out (ICommandLineOptionsProvider FirstProvider, HashSet<ICommandLineOptionsProvider>? DuplicateProviders) registration))
                {
                    optionNameToProviders.Add(name, (provider, null));
                    continue;
                }

                if (registration.DuplicateProviders is null)
                {
                    registration.DuplicateProviders = [registration.FirstProvider];
                }

                registration.DuplicateProviders.Add(provider);
                optionNameToProviders[name] = registration;
            }
        }

        // Check for duplications
        StringBuilder? stringBuilder = null;
        foreach (KeyValuePair<string, (ICommandLineOptionsProvider FirstProvider, HashSet<ICommandLineOptionsProvider>? DuplicateProviders)> kvp in optionNameToProviders)
        {
            if (kvp.Value.DuplicateProviders is { Count: > 1 } providers)
            {
                string duplicatedOption = kvp.Key;
                stringBuilder ??= new();
                IEnumerable<string> faultyProvidersDisplayNames = providers.Select(p => p.DisplayName);
                stringBuilder.AppendLine(string.Format(CultureInfo.InvariantCulture, PlatformResources.CommandLineOptionIsDeclaredByMultipleProviders, duplicatedOption, string.Join("', '", faultyProvidersDisplayNames)));
            }
        }

        return stringBuilder?.Length > 0
            ? ValidationResult.Invalid(stringBuilder.ToTrimmedString())
            : ValidationResult.Valid();
    }
}
