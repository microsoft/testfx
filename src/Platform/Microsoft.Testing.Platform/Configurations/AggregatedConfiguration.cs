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
    CommandLineParseResult commandLineParseResult) : IConfiguration
{
    public const string DefaultTestResultFolderName = "TestResults";
    private readonly IConfigurationProvider[] _configurationProviders = configurationProviders;
    private readonly ITestApplicationModuleInfo _testApplicationModuleInfo = testApplicationModuleInfo;
    private readonly IFileSystem _fileSystem = fileSystem;
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
        _currentWorkingDirectory = Guard.NotNull(workingDirectory);

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
        if (commandLineParseResult.TryGetOptionArgumentList(PlatformCommandLineProvider.ResultDirectoryOptionKey, out string[]? resultDirectoryArg))
        {
            return resultDirectoryArg[0];
        }

        // If not specified by command line, then use the configuration providers.
        // And finally fallback to DefaultTestResultFolderName relative to the current working directory.
        return CalculateFromConfigurationProviders(PlatformConfigurationConstants.PlatformResultDirectory)
            ?? Path.Combine(this[PlatformConfigurationConstants.PlatformCurrentWorkingDirectory]!, DefaultTestResultFolderName);
    }

    private string GetCurrentWorkingDirectoryCore()
         // If the value is already set, use that.
         => _currentWorkingDirectory
            // If first time calculating it, prefer the value from configuration,
            ?? CalculateFromConfigurationProviders(PlatformConfigurationConstants.PlatformCurrentWorkingDirectory)
            // then fallback to the actual working directory.
            ?? _testApplicationModuleInfo.GetCurrentTestApplicationDirectory();
}
