// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Resources;
using Microsoft.Testing.Platform.Services;

using static Microsoft.Testing.Platform.Configurations.JsonConfigurationSource;

namespace Microsoft.Testing.Platform.Configurations;

internal sealed class ConfigurationManager(IFileSystem fileSystem, ITestApplicationModuleInfo testApplicationModuleInfo) : IConfigurationManager
{
    private readonly List<Func<IConfigurationSource>> _configurationSources = [];
    private readonly IFileSystem _fileSystem = fileSystem;
    private readonly ITestApplicationModuleInfo _testApplicationModuleInfo = testApplicationModuleInfo;

    public void AddConfigurationSource(Func<IConfigurationSource> source) => _configurationSources.Add(source);

    internal async Task<IConfiguration> BuildAsync(IFileLoggerProvider? syncFileLoggerProvider, CommandLineParseResult commandLineParseResult)
    {
        List<(IConfigurationProvider ConfigurationProvider, int Order)> configurationProviders = [];

        JsonConfigurationProvider? defaultJsonConfiguration = null;
        foreach (Func<IConfigurationSource> configurationSource in _configurationSources)
        {
            IConfigurationSource serviceInstance = configurationSource();
            if (!await serviceInstance.IsEnabledAsync().ConfigureAwait(false))
            {
                continue;
            }

            await serviceInstance.TryInitializeAsync().ConfigureAwait(false);

            IConfigurationProvider configurationProvider = await serviceInstance.BuildAsync(commandLineParseResult).ConfigureAwait(false);
            await configurationProvider.LoadAsync().ConfigureAwait(false);
            if (configurationProvider is JsonConfigurationProvider configuration)
            {
                defaultJsonConfiguration = configuration;
            }

            configurationProviders.Add((configurationProvider, serviceInstance.Order));
        }

        if (defaultJsonConfiguration is null)
        {
            throw new InvalidOperationException(PlatformResources.ConfigurationManagerCannotFindDefaultJsonConfigurationErrorMessage);
        }

        configurationProviders.Sort(static (a, b) => a.Order.CompareTo(b.Order));
        var configurationProvidersArray = new IConfigurationProvider[configurationProviders.Count];
        for (int i = 0; i < configurationProvidersArray.Length; i++)
        {
            configurationProvidersArray[i] = configurationProviders[i].ConfigurationProvider;
        }

        if (syncFileLoggerProvider is not null && defaultJsonConfiguration.ConfigurationFile != null)
        {
            ILogger logger = syncFileLoggerProvider.CreateLogger(nameof(ConfigurationManager));
            if (logger.IsEnabled(LogLevel.Trace))
            {
                using IFileStream configFileStream = _fileSystem.NewFileStream(defaultJsonConfiguration.ConfigurationFile, FileMode.Open, FileAccess.Read);
                StreamReader streamReader = new(configFileStream.Stream);
                await logger.LogTraceAsync($"Configuration file ('{defaultJsonConfiguration.ConfigurationFile}') content:\n{await streamReader.ReadToEndAsync().ConfigureAwait(false)}").ConfigureAwait(false);
            }
        }

        return new AggregatedConfiguration(configurationProvidersArray, _testApplicationModuleInfo, _fileSystem, commandLineParseResult);
    }
}
