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
        IReadOnlyList<JsonCommandLineOptionEntry>? jsonCommandLineOptionDefaults,
        Dictionary<ICommandLineOptionsProvider, IReadOnlyCollection<CommandLineOption>> extensionOptionsByProvider,
        Dictionary<ICommandLineOptionsProvider, IReadOnlyCollection<CommandLineOption>> systemOptionsByProvider,
        HashSet<string>? registeredDefaultOptionNames)
    {
        // Use OrdinalIgnoreCase so a JSON entry like "Timeout" resolves to the registered "timeout"
        // option (testconfig.json keys are case-insensitive everywhere else in the platform). CLI
        // parsing is already case-sensitive but a case-insensitive lookup is a strict superset and
        // does not change CLI behavior.
        var validOptionNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visibleOptionNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        bool includeKnownExtensionOptions = !parseResult.HasTool;
        CollectOptionNames(extensionOptionsByProvider, validOptionNames, visibleOptionNames);
        CollectOptionNames(systemOptionsByProvider, validOptionNames, visibleOptionNames);

        StringBuilder? stringBuilder = null;
        foreach (CommandLineParseOption optionRecord in parseResult.Options)
        {
            if (!validOptionNames.Contains(optionRecord.Name))
            {
                stringBuilder ??= new();
                AppendUnknownOptionError(stringBuilder, optionRecord.Name, optionRecord.Arguments, validOptionNames, visibleOptionNames, includeKnownExtensionOptions);
            }
        }

        ValidateJsonEntries(jsonCommandLineOptions, PlatformConfigurationConstants.CommandLineOptionsSectionName);
        ValidateJsonEntries(jsonCommandLineOptionDefaults, PlatformConfigurationConstants.CommandLineOptionDefaultsSectionName, registeredDefaultOptionNames);

        if (stringBuilder?.Length > 0)
        {
            stringBuilder.AppendLine(PlatformResources.CommandLineUnknownOptionsHint);
        }

        return stringBuilder?.Length > 0
            ? ValidationResult.Invalid(stringBuilder.ToTrimmedString())
            : ValidationResult.Valid();

        void ValidateJsonEntries(IReadOnlyList<JsonCommandLineOptionEntry>? entries, string sectionName, HashSet<string>? additionalValidOptionNames = null)
        {
            if (entries is null)
            {
                return;
            }

            foreach (JsonCommandLineOptionEntry entry in entries)
            {
                if (!validOptionNames.Contains(entry.OptionName)
                    && (additionalValidOptionNames is null || !additionalValidOptionNames.Contains(entry.OptionName)))
                {
                    stringBuilder ??= new();
                    StringBuilder innerErrorBuilder = new();
                    AppendUnknownOptionError(innerErrorBuilder, entry.OptionName, entry.Arguments, validOptionNames, visibleOptionNames, includeKnownExtensionOptions);
                    string innerError = innerErrorBuilder.ToTrimmedString();
                    stringBuilder.AppendLine(FormatJsonValidationError(sectionName, innerError));
                }
            }
        }
    }

    private static void CollectOptionNames(
        Dictionary<ICommandLineOptionsProvider, IReadOnlyCollection<CommandLineOption>> optionsByProvider,
        HashSet<string> validOptionNames,
        HashSet<string> visibleOptionNames)
    {
        foreach (KeyValuePair<ICommandLineOptionsProvider, IReadOnlyCollection<CommandLineOption>> provider in optionsByProvider)
        {
            foreach (CommandLineOption option in provider.Value)
            {
                validOptionNames.Add(option.Name);
                if (!option.IsHidden)
                {
                    visibleOptionNames.Add(option.Name);
                }
            }
        }
    }

    private static void AppendUnknownOptionError(
        StringBuilder stringBuilder,
        string unknownOptionName,
        IReadOnlyList<string> arguments,
        HashSet<string> validOptionNames,
        HashSet<string> visibleOptionNames,
        bool includeKnownExtensionOptions)
    {
        stringBuilder.AppendLine(string.Format(CultureInfo.InvariantCulture, PlatformResources.CommandLineUnknownOption, unknownOptionName));

        if (includeKnownExtensionOptions
            && TryAppendVSTestOptionGuidance(stringBuilder, unknownOptionName, arguments, validOptionNames))
        {
            return;
        }

        if (includeKnownExtensionOptions
            && GetKnownExtensionPackage(unknownOptionName) is { } packageName)
        {
            AppendMissingExtensionSuggestion(stringBuilder, unknownOptionName, packageName);
            return;
        }

        IEnumerable<string> candidateOptionNames = includeKnownExtensionOptions
            ? visibleOptionNames.Concat(KnownExtensionOptionsByPackage.Values.SelectMany(static optionNames => optionNames))
            : visibleOptionNames;
        string? suggestedOptionName = FindSuggestedOption(
            unknownOptionName,
            candidateOptionNames);
        if (suggestedOptionName is null)
        {
            return;
        }

        stringBuilder.AppendLine(string.Format(CultureInfo.InvariantCulture, PlatformResources.CommandLineOptionSuggestion, suggestedOptionName));

        if (includeKnownExtensionOptions
            && GetKnownExtensionPackage(suggestedOptionName) is { } suggestedPackageName
            && !validOptionNames.Contains(suggestedOptionName))
        {
            AppendMissingExtensionSuggestion(stringBuilder, suggestedOptionName, suggestedPackageName);
        }
    }

    private static string? FindSuggestedOption(string unknownOptionName, IEnumerable<string> candidateOptionNames)
    {
        int maximumDistance = unknownOptionName.Length switch
        {
            <= 4 => 1,
            <= 12 => 2,
            _ => 3,
        };

        string? bestCandidate = null;
        int bestDistance = maximumDistance + 1;
        bool hasAmbiguousBestCandidate = false;

        foreach (string candidateOptionName in candidateOptionNames.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (Math.Abs(candidateOptionName.Length - unknownOptionName.Length) > maximumDistance)
            {
                continue;
            }

            int distance = CalculateEditDistance(unknownOptionName, candidateOptionName);
            if (distance < bestDistance)
            {
                bestCandidate = candidateOptionName;
                bestDistance = distance;
                hasAmbiguousBestCandidate = false;
            }
            else if (distance == bestDistance)
            {
                hasAmbiguousBestCandidate = true;
            }
        }

        return bestDistance <= maximumDistance && !hasAmbiguousBestCandidate
            ? bestCandidate
            : null;
    }

    private static int CalculateEditDistance(string source, string target)
    {
        int[,] distances = new int[source.Length + 1, target.Length + 1];
        for (int i = 0; i <= source.Length; i++)
        {
            distances[i, 0] = i;
        }

        for (int j = 0; j <= target.Length; j++)
        {
            distances[0, j] = j;
        }

        for (int i = 1; i <= source.Length; i++)
        {
            for (int j = 1; j <= target.Length; j++)
            {
                int substitutionCost = char.ToUpperInvariant(source[i - 1]) == char.ToUpperInvariant(target[j - 1]) ? 0 : 1;
                distances[i, j] = Math.Min(
                    Math.Min(distances[i - 1, j] + 1, distances[i, j - 1] + 1),
                    distances[i - 1, j - 1] + substitutionCost);

                if (i > 1
                    && j > 1
                    && char.ToUpperInvariant(source[i - 1]) == char.ToUpperInvariant(target[j - 2])
                    && char.ToUpperInvariant(source[i - 2]) == char.ToUpperInvariant(target[j - 1]))
                {
                    distances[i, j] = Math.Min(distances[i, j], distances[i - 2, j - 2] + 1);
                }
            }
        }

        return distances[source.Length, target.Length];
    }

    private static ValidationResult ValidateNoBootstrapOnlyOptionsInJson(
        IReadOnlyList<JsonCommandLineOptionEntry>? jsonCommandLineOptions,
        IReadOnlyList<JsonCommandLineOptionEntry>? jsonCommandLineOptionDefaults)
    {
        if (jsonCommandLineOptions is not { Count: > 0 }
            && jsonCommandLineOptionDefaults is not { Count: > 0 })
        {
            return ValidationResult.Valid();
        }

        StringBuilder? stringBuilder = null;
        ValidateEntries(jsonCommandLineOptions, PlatformConfigurationConstants.CommandLineOptionsSectionName);
        ValidateEntries(jsonCommandLineOptionDefaults, PlatformConfigurationConstants.CommandLineOptionDefaultsSectionName);

        return stringBuilder?.Length > 0
            ? ValidationResult.Invalid(stringBuilder.ToTrimmedString())
            : ValidationResult.Valid();

        void ValidateEntries(IReadOnlyList<JsonCommandLineOptionEntry>? entries, string sectionName)
        {
            if (entries is null)
            {
                return;
            }

            foreach (JsonCommandLineOptionEntry entry in entries)
            {
                if (!BootstrapOnlyOptions.Contains(entry.OptionName))
                {
                    continue;
                }

                stringBuilder ??= new();
                string innerError = string.Format(
                    CultureInfo.InvariantCulture,
                    PlatformResources.JsonCommandLineOptionIsBootstrapOnlyErrorMessage,
                    entry.OptionName,
                    sectionName);
                stringBuilder.AppendLine(FormatJsonValidationError(sectionName, innerError));
            }
        }
    }
}
