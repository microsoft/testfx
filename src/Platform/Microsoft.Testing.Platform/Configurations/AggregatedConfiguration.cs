// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.Configurations;

internal sealed class AggregatedConfiguration(
    IConfigurationProvider[] configurationProviders,
    ITestApplicationModuleInfo testApplicationModuleInfo,
    IFileSystem fileSystem,
    IEnvironment environment,
    CommandLineParseResult commandLineParseResult) : IConfiguration
{
    public const string DefaultTestResultFolderName = "TestResults";
    private readonly IConfigurationProvider[] _configurationProviders = configurationProviders;
    private readonly ITestApplicationModuleInfo _testApplicationModuleInfo = testApplicationModuleInfo;
    private readonly IFileSystem _fileSystem = fileSystem;
    private readonly IEnvironment _environment = environment;
    private readonly CommandLineParseResult _commandLineParseResult = commandLineParseResult;
    private string? _resultsDirectory;
    private string? _currentWorkingDirectory;

    public string? this[string key]
    {
        get
        {
            if (key == PlatformConfigurationConstants.PlatformResultDirectory)
            {
                _resultsDirectory = GetResultsDirectoryCore(_commandLineParseResult);
                return _resultsDirectory;
            }

            if (key is PlatformConfigurationConstants.PlatformCurrentWorkingDirectory or PlatformConfigurationConstants.PlatformTestHostWorkingDirectory)
            {
                _currentWorkingDirectory = GetCurrentWorkingDirectoryCore();
                return _currentWorkingDirectory;
            }

            // Fallback to calculating from configuration providers.
            return CalculateFromConfigurationProviders(key);
        }
    }

    private string? CalculateFromConfigurationProviders(string key)
    {
        foreach (IConfigurationProvider source in _configurationProviders)
        {
            if (source.TryGet(key, out string? value))
            {
                return value;
            }
        }

        return null;
    }

    public /* for testing */ void SetCurrentWorkingDirectory(string workingDirectory) =>
        _currentWorkingDirectory = workingDirectory;

    /// <summary>
    /// Resolves the value of a command-line option across all registered providers at the
    /// <em>option</em> granularity (not per-key), so that CLI provider entries fully shadow
    /// JSON entries for the same option even when the storage shapes don't overlap (e.g.,
    /// CLI <c>--list-tests</c> with no args vs. JSON <c>"list-tests": ["json"]</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Providers are walked in registration order; the first one that has any data for
    /// <paramref name="optionName"/> wins and its values are returned exclusively. A bare
    /// boolean <c>"false"</c> at a winning provider is treated as an explicit disable and
    /// short-circuits the search.
    /// </para>
    /// <para>
    /// Provider contract assumptions for the <c>commandLineOptions:*</c> section (Option C,
    /// issue #6349):
    /// <list type="bullet">
    ///   <item><description><c>TryGet</c> returning <c>true</c> with a <c>null</c> value is
    ///   treated as if the key were absent at this provider — a custom provider that wants
    ///   to explicitly shadow lower providers must return either the boolean string
    ///   <c>"False"</c> (disable) or a non-null argument value.</description></item>
    ///   <item><description>Indexed argument entries are required to be contiguous starting
    ///   at <c>:0</c>. A gap stops collection — sparse arrays will silently truncate. The
    ///   JSON provider always emits contiguous indices for arrays.</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <param name="optionName">Option name with or without leading <c>--</c>.</param>
    /// <param name="isSet">Set to <c>true</c> if any provider reports the option as set.</param>
    /// <param name="arguments">Empty for zero-arity / disabled, otherwise the argument list
    /// from the winning provider.</param>
    /// <returns><c>true</c> if any provider had any data for the option (even an explicit
    /// disable); <c>false</c> if no provider mentioned the option.</returns>
    internal bool TryGetCommandLineOptionFromProviders(string optionName, out bool isSet, out string[] arguments)
    {
        string baseKey = PlatformConfigurationConstants.CommandLineOptionsSectionName
            + PlatformConfigurationConstants.KeyDelimiter
            + optionName.Trim(CommandLineParseResult.OptionPrefix);

        foreach (IConfigurationProvider provider in _configurationProviders)
        {
            // Try indexed entries first (multi/single value options).
            List<string>? collected = null;
            int index = 0;
            while (provider.TryGet(baseKey + PlatformConfigurationConstants.KeyDelimiter + index.ToString(CultureInfo.InvariantCulture), out string? indexed)
                && indexed is not null)
            {
                collected ??= [];
                collected.Add(indexed);
                index++;
            }

            if (collected is { Count: > 0 })
            {
                isSet = true;
                arguments = [.. collected];
                return true;
            }

            // Bare key (zero-arity presence marker, or a scalar JSON value).
            if (provider.TryGet(baseKey, out string? bare) && bare is not null)
            {
                if (bool.TryParse(bare, out bool boolValue))
                {
                    isSet = boolValue;
                    arguments = [];
                    return true;
                }

                isSet = true;
                arguments = [bare];
                return true;
            }
        }

        isSet = false;
        arguments = [];
        return false;
    }

    /// <summary>
    /// Returns the immediate (one-level) string entries declared under <paramref name="sectionName"/>
    /// in the loaded testconfig.json file. Returns an empty list if no JSON configuration source is
    /// active or the section is absent.
    /// </summary>
    internal IReadOnlyList<KeyValuePair<string, string?>> GetTestConfigJsonSection(string sectionName)
    {
        JsonConfigurationSource.JsonConfigurationProvider? jsonProvider = _configurationProviders
            .OfType<JsonConfigurationSource.JsonConfigurationProvider>()
            .FirstOrDefault();

        return jsonProvider?.GetSection(sectionName) ?? [];
    }

    public async Task CheckTestResultsDirectoryOverrideAndCreateItAsync(IFileLoggerProvider? fileLoggerProvider)
    {
        _resultsDirectory = _fileSystem.CreateDirectory(this[PlatformConfigurationConstants.PlatformResultDirectory]!);

        // In case of the result directory is overridden by the config file we move logs to it.
        // This can happen in case of VSTest mode where the result directory is set to a different location.
        // This behavior is non documented and we reserve the right to change it in the future.
        if (fileLoggerProvider is not null)
        {
            await fileLoggerProvider.CheckLogFolderAndMoveToTheNewIfNeededAsync(_resultsDirectory).ConfigureAwait(false);
        }
    }

    private string GetResultsDirectoryCore(CommandLineParseResult commandLineParseResult)
    {
        // We already calculated it before, or was set by unit tests via SetCurrentWorkingDirectory.
        if (_resultsDirectory is not null)
        {
            return _resultsDirectory;
        }

        // NOTE: It's important to consider the `--results-directory` option before the configurations.
        // It makes more sense to respect it first, and is also relevant for Retry extension.
        // Consider user is setting ResultsDirectory in configuration file.
        // When tests are retried, we re-run the executable again, but we pass --results-directory containing the folder where the current retry attempt
        // results should be stored. But we will also still pass configuration file (e.g, runsettings via --settings) that the user originally specified.
        // In that case, we will want to respect --results-directory.
        //
        // Option C (issue #6349): when the CLI configuration source is registered (the default
        // host path), consult the unified command-line view first so a value supplied either via
        // CLI or via testconfig.json's "commandLineOptions:results-directory" is honored with the
        // same priority. CLI still wins over JSON because the CLI provider is Order=0.
        //
        // When the CLI source is NOT registered (test-only callers that build AggregatedConfiguration
        // by hand), fall back to the legacy parseResult-first behavior so we don't silently demote
        // CLI precedence behind any other provider that happens to expose the same key.
        bool cliSourceRegistered = false;
        foreach (IConfigurationProvider provider in _configurationProviders)
        {
            if (provider is CommandLineConfigurationProvider)
            {
                cliSourceRegistered = true;
                break;
            }
        }

        if (cliSourceRegistered
            && TryGetCommandLineOptionFromProviders(PlatformCommandLineProvider.ResultDirectoryOptionKey, out bool resultDirIsSet, out string[] resultDirArgs)
            && resultDirIsSet
            && resultDirArgs.Length > 0)
        {
            return resultDirArgs[0];
        }

        // Fallback for legacy callers without the CLI source registered: read directly from the
        // original parse result so explicit CLI wins over any custom provider for this option.
        if (commandLineParseResult.TryGetOptionArgumentList(PlatformCommandLineProvider.ResultDirectoryOptionKey, out string[]? resultDirectoryArg))
        {
            return resultDirectoryArg[0];
        }

        // If not specified by command line, then use the configuration providers.
        // And finally fallback to DefaultTestResultFolderName relative to the current working directory.
        // Note: PlatformCurrentWorkingDirectory already incorporates DOTNET_CLI_TEST_COMMAND_WORKING_DIRECTORY
        // (via GetCurrentWorkingDirectoryCore), so we don't need to check that env var separately here.
        return CalculateFromConfigurationProviders(PlatformConfigurationConstants.PlatformResultDirectory)
            ?? Path.Combine(this[PlatformConfigurationConstants.PlatformCurrentWorkingDirectory]!, DefaultTestResultFolderName);
    }

    private string GetCurrentWorkingDirectoryCore()
    {
        // If the value is already set, use that.
        if (_currentWorkingDirectory is not null)
        {
            return _currentWorkingDirectory;
        }

        // If first time calculating it, prefer the value from configuration,
        string? fromConfig = CalculateFromConfigurationProviders(PlatformConfigurationConstants.PlatformCurrentWorkingDirectory);
        if (fromConfig is not null)
        {
            return fromConfig;
        }

        // then check if dotnet test working directory is set (to keep PlatformCurrentWorkingDirectory and
        // PlatformResultDirectory consistent when running under 'dotnet test'),
        string? dotnetTestCwd = _environment.GetEnvironmentVariable(EnvironmentVariableConstants.DOTNET_CLI_TEST_COMMAND_WORKING_DIRECTORY);
        if (!RoslynString.IsNullOrWhiteSpace(dotnetTestCwd))
        {
            return dotnetTestCwd;
        }

        // then fallback to the actual working directory.
        return _testApplicationModuleInfo.GetCurrentTestApplicationDirectory();
    }
}
