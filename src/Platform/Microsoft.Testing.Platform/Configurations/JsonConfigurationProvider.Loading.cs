// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Resources;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.Configurations;

internal sealed partial class JsonConfigurationSource
{
    internal sealed partial class JsonConfigurationProvider(
        ITestApplicationModuleInfo testApplicationModuleInfo,
        IFileSystem fileSystem,
        CommandLineParseResult commandLineParseResult,
        ILogger? logger) : IConfigurationProvider
    {
        private readonly ITestApplicationModuleInfo _testApplicationModuleInfo = testApplicationModuleInfo;
        private readonly IFileSystem _fileSystem = fileSystem;
        private readonly CommandLineParseResult _commandLineParseResult = commandLineParseResult;
        private readonly ILogger? _logger = logger;
        private Dictionary<string, string?>? _propertyToAllChildren;
        private Dictionary<string, string?>? _singleValueData;

        public string? ConfigurationFile { get; private set; }

        private Task LogInformationAsync(string message)
            => _logger?.LogInformationAsync(message) ?? Task.CompletedTask;

        private Task LogDebugAsync(string message)
            => _logger?.LogDebugAsync(message) ?? Task.CompletedTask;

        private Task LogErrorAsync(string message, Exception exception)
            => _logger?.LogErrorAsync(message, exception) ?? Task.CompletedTask;

        public async Task LoadAsync()
        {
            string configFileName;
            if (_commandLineParseResult.TryGetOptionArgumentList(PlatformCommandLineProvider.ConfigFileOptionKey, out string[]? configOptions))
            {
                configFileName = configOptions[0];
                if (!_fileSystem.ExistFile(configFileName))
                {
                    try
                    {
                        // Get the full path for better error messages.
                        // As this is only for the purpose of throwing an exception, ignore any exceptions during the GetFullPath call.
                        configFileName = Path.GetFullPath(configFileName);
                    }
                    catch (Exception ex)
                    {
                        // Best-effort path resolution; surface the failure at Debug so logs explain why the
                        // error message may show a relative path instead of an absolute one.
                        await LogDebugAsync($"Path.GetFullPath('{configFileName}') failed while preparing FileNotFoundException: {ex.GetType().FullName}: {ex.Message}").ConfigureAwait(false);
                    }

                    throw new FileNotFoundException(string.Format(CultureInfo.InvariantCulture, PlatformResources.ConfigurationFileNotFound, configFileName), configFileName);
                }
            }
            else
            {
                configFileName = _testApplicationModuleInfo.TryGetCurrentTestApplicationFullPath() is { } fullPath
                    ? $"{Path.Combine(
                        Path.GetDirectoryName(fullPath)!,
                        Path.GetFileNameWithoutExtension(fullPath))}{PlatformConfigurationConstants.PlatformConfigSuffixFileName}"
                    : $"{_testApplicationModuleInfo.TryGetAssemblyName()}{PlatformConfigurationConstants.PlatformConfigSuffixFileName}";

                if (!_fileSystem.ExistFile(configFileName))
                {
                    await LogDebugAsync($"Default JSON config file '{configFileName}' not found; skipping load.").ConfigureAwait(false);
                    return;
                }
            }

            await LogInformationAsync($"Config file '{configFileName}' loaded.").ConfigureAwait(false);

            ConfigurationFile = configFileName;

            using IFileStream fileStream = _fileSystem.NewFileStream(configFileName, FileMode.Open, FileAccess.Read);
            try
            {
                (_singleValueData, _propertyToAllChildren) = JsonConfigurationFileParser.Parse(fileStream.Stream);
            }
            catch (Exception ex)
            {
                await LogErrorAsync($"Failed to parse configuration file '{configFileName}'", ex).ConfigureAwait(false);
                throw;
            }
        }

        public bool TryGet(string key, out string? value)
        {
            value = null;
            return (_singleValueData != null && _singleValueData.TryGetValue(key, out value)) || (_propertyToAllChildren != null && _propertyToAllChildren.TryGetValue(key, out value));
        }
    }
}
