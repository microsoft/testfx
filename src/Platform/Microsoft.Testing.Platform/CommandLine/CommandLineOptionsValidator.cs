// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Resources;
using Microsoft.Testing.Platform.Tools;

namespace Microsoft.Testing.Platform.CommandLine;

internal static partial class CommandLineOptionsValidator
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

        extensionCommandLineOptionsProviders = commandLineParseResult.ToolName is string toolName
            ? extensionCommandLineOptionsProviders
                .OfType<IToolCommandLineOptionsProvider>()
                .Where(provider => provider.ToolName == toolName)
            : extensionCommandLineOptionsProviders.Where(provider => provider is not IToolCommandLineOptionsProvider);

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

    internal static ValidationResult ValidateToolProviders(
        IEnumerable<IToolCommandLineOptionsProvider> toolProviders,
        IEnumerable<ICommandLineOptionsProvider> systemProviders)
    {
        var toolOptionsByProvider =
            toolProviders.ToDictionary(provider => (ICommandLineOptionsProvider)provider, provider => provider.GetCommandLineOptions());
        var systemOptionsByProvider =
            systemProviders.ToDictionary(provider => provider, provider => provider.GetCommandLineOptions());

        ValidationResult reservedPrefixResult = ValidateExtensionOptionsDoNotContainReservedPrefix(toolOptionsByProvider);
        return !reservedPrefixResult.IsValid
            ? reservedPrefixResult
            : ValidateExtensionOptionsDoNotContainReservedOptions(toolOptionsByProvider, systemOptionsByProvider) is { IsValid: false } reservedOptionResult
                ? reservedOptionResult
                : ValidateOptionsAreNotDuplicated(toolOptionsByProvider, systemOptionsByProvider);
    }
}
