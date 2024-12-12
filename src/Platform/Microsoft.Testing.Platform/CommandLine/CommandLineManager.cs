// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.Tools;

namespace Microsoft.Testing.Platform.CommandLine;

internal sealed class CommandLineManager(IRuntimeFeature runtimeFeature, ITestApplicationModuleInfo testApplicationModuleInfo) : ICommandLineManager
{
    private readonly List<Func<IServiceProvider, ICommandLineOptionsProvider>> _commandLineProviderFactory = [];
    private readonly IRuntimeFeature _runtimeFeature = runtimeFeature;
    private readonly ITestApplicationModuleInfo _testApplicationModuleInfo = testApplicationModuleInfo;

    public void AddProvider(Func<ICommandLineOptionsProvider> commandLineProviderFactory)
    {
        Guard.NotNull(commandLineProviderFactory);
        _commandLineProviderFactory.Add(_ => commandLineProviderFactory());
    }

    public void AddProvider(Func<IServiceProvider, ICommandLineOptionsProvider> commandLineProviderFactory)
    {
        Guard.NotNull(commandLineProviderFactory);
        _commandLineProviderFactory.Add(commandLineProviderFactory);
    }

    internal async Task<CommandLineHandler> BuildAsync(CommandLineParseResult parseResult, IServiceProvider serviceProvider)
    {
        List<ICommandLineOptionsProvider> commandLineOptionsProviders = [];
        foreach (Func<IServiceProvider, ICommandLineOptionsProvider> commandLineProviderFactory in _commandLineProviderFactory)
        {
            ICommandLineOptionsProvider commandLineOptionsProvider = commandLineProviderFactory(serviceProvider);
            if (!await commandLineOptionsProvider.IsEnabledAsync())
            {
                continue;
            }

            await commandLineOptionsProvider.TryInitializeAsync();

            commandLineOptionsProviders.Add(
                commandLineOptionsProvider is IToolCommandLineOptionsProvider toolCommandLineOptionsProvider
                    ? new ToolCommandLineOptionsProviderCache(toolCommandLineOptionsProvider)
                    : new CommandLineOptionsProviderCache(commandLineOptionsProvider));
        }

        ICommandLineOptionsProvider[] systemCommandLineOptionsProviders =
        [
            new PlatformCommandLineProvider()
        ];

        return new CommandLineHandler(parseResult, commandLineOptionsProviders,
            systemCommandLineOptionsProviders, _testApplicationModuleInfo, _runtimeFeature);
    }
}
