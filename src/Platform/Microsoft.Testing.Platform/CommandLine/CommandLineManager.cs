// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.Tools;

namespace Microsoft.Testing.Platform.CommandLine;

internal sealed class CommandLineManager(IRuntimeFeature runtimeFeature, IEnvironment environment, ITestApplicationModuleInfo testApplicationModuleInfo) : ICommandLineManager
{
    private readonly List<Func<ICommandLineOptionsProvider>> _commandLineProviderFactory = [];
    private readonly IRuntimeFeature _runtimeFeature = runtimeFeature;
    private readonly IEnvironment _environment = environment;
    private readonly ITestApplicationModuleInfo _testApplicationModuleInfo = testApplicationModuleInfo;

    public void AddProvider(Func<ICommandLineOptionsProvider> commandLineProviderFactory)
    {
        ArgumentGuard.IsNotNull(commandLineProviderFactory);
        _commandLineProviderFactory.Add(commandLineProviderFactory);
    }

    internal async Task<CommandLineHandler> BuildAsync(IPlatformOutputDevice platformOutputDisplay, CommandLineParseResult parseResult)
    {
        List<ICommandLineOptionsProvider> commandLineOptionsProviders = [];
        foreach (Func<ICommandLineOptionsProvider> commandLineProviderFactory in _commandLineProviderFactory)
        {
            ICommandLineOptionsProvider commandLineOptionsProvider = commandLineProviderFactory();
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
            systemCommandLineOptionsProviders, _testApplicationModuleInfo, _runtimeFeature, platformOutputDisplay, _environment);
    }
}
