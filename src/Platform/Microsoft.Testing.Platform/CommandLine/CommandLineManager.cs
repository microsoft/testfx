// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.CommandLine;

internal sealed class CommandLineManager(IRuntimeFeature runtimeFeature, IEnvironment environment, IProcessHandler processHandler, ITestApplicationModuleInfo testApplicationModuleInfo) : ICommandLineManager
{
    private readonly List<Func<ICommandLineOptionsProvider>> _commandLineProviderFactory = [];
    private readonly IRuntimeFeature _runtimeFeature = runtimeFeature;
    private readonly IEnvironment _environment = environment;
    private readonly IProcessHandler _processHandler = processHandler;
    private readonly ITestApplicationModuleInfo _testApplicationModuleInfo = testApplicationModuleInfo;

    public void AddProvider(Func<ICommandLineOptionsProvider> commandLineProviderFactory)
    {
        ArgumentGuard.IsNotNull(commandLineProviderFactory);
        _commandLineProviderFactory.Add(commandLineProviderFactory);
    }

    internal async Task<CommandLineHandler> BuildAsync(string[] args, IPlatformOutputDevice platformOutputDisplay, CommandLineParseResult parseResult)
    {
        List<ICommandLineOptionsProvider> commandLineOptionsProviders = [];
        foreach (Func<ICommandLineOptionsProvider> commandLineProviderFactory in _commandLineProviderFactory)
        {
            ICommandLineOptionsProvider serviceInstance = commandLineProviderFactory();
            if (!await serviceInstance.IsEnabledAsync())
            {
                continue;
            }

            await serviceInstance.TryInitializeAsync();

            commandLineOptionsProviders.Add(new CommandLineOptionsProviderCache(serviceInstance));
        }

        ICommandLineOptionsProvider[] systemCommandLineOptionsProviders =
        [
            new PlatformCommandLineProvider()
        ];

        return new CommandLineHandler(args, parseResult, commandLineOptionsProviders.ToArray(),
            systemCommandLineOptionsProviders, _testApplicationModuleInfo, _runtimeFeature, platformOutputDisplay, _environment, _processHandler);
    }
}
