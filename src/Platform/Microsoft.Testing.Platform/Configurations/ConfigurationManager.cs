﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Resources;
using Microsoft.Testing.Platform.Services;

using static Microsoft.Testing.Platform.Configurations.JsonConfigurationSource;

namespace Microsoft.Testing.Platform.Configurations;

internal sealed class ConfigurationManager : IConfigurationManager
{
    private readonly List<Func<IConfigurationSource>> _configurationSources = [];

    public void AddConfigurationSource(Func<IConfigurationSource> source) => _configurationSources.Add(source);

    internal async Task<IConfiguration> BuildAsync(IServiceProvider serviceProvider, FileLoggerProvider? syncFileLoggerProvider)
    {
        List<IConfigurationProvider> configurationProviders = [];
        JsonConfigurationProvider? defaultJsonConfiguration = null;
        foreach (Func<IConfigurationSource> configurationSource in _configurationSources)
        {
            IConfigurationSource serviceInstance = configurationSource();
            if (!await serviceInstance.IsEnabledAsync())
            {
                continue;
            }

            if (serviceInstance is IAsyncInitializableExtension async)
            {
                await async.InitializeAsync();
            }

            IConfigurationProvider configurationProvider = serviceInstance.Build();
            await configurationProvider.LoadAsync();
            if (configurationProvider is JsonConfigurationProvider configuration)
            {
                defaultJsonConfiguration = configuration;
            }

            configurationProviders.Add(configurationProvider);
        }

        if (syncFileLoggerProvider is not null)
        {
            ILogger logger = syncFileLoggerProvider.CreateLogger(nameof(ConfigurationManager));
            if (logger.IsEnabled(LogLevel.Trace))
            {
                if (defaultJsonConfiguration is not null && defaultJsonConfiguration.ConfigurationFile is not null)
                {
                    using Stream configFileStream = serviceProvider.GetFileSystem().NewFileStream(defaultJsonConfiguration.ConfigurationFile, FileMode.Open);
                    StreamReader streamReader = new(configFileStream);
                    await logger.LogTraceAsync($"Configuration file ('{defaultJsonConfiguration.ConfigurationFile}') content:\n{await streamReader.ReadToEndAsync()}");
                }
            }
        }

        return defaultJsonConfiguration is null
            ? throw new InvalidOperationException(PlatformResources.ConfigurationManagerCannotFindDefaultJsonConfigurationErrorMessage)
            : new AggregatedConfiguration(configurationProviders.ToArray());
    }
}
