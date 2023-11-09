// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.OutputDevice;

namespace Microsoft.Testing.Platform.CommandLine;

internal sealed class CommandLineManager : ICommandLineManager
{
    private readonly List<Func<ICommandLineOptionsProvider>> _commandLineProviderFactory = [];

    public void AddProvider(Func<ICommandLineOptionsProvider> commandLineProviderFactory)
    {
        ArgumentGuard.IsNotNull(commandLineProviderFactory);
        _commandLineProviderFactory.Add(commandLineProviderFactory);
    }

    internal async Task<CommandLineHandler> BuildAsync(string[] args, IRuntime runtime, IRuntimeFeature runtimeFeature,
        IPlatformOutputDevice platformOutputDisplay, IEnvironment environment, IProcessHandler process, CommandLineParseResult parseResult)
    {
        List<ICommandLineOptionsProvider> commandLineOptionsProviders = [];
        foreach (Func<ICommandLineOptionsProvider> commandLineProviderFactory in _commandLineProviderFactory)
        {
            ICommandLineOptionsProvider serviceInstance = commandLineProviderFactory();
            if (!await serviceInstance.IsEnabledAsync())
            {
                continue;
            }

            if (serviceInstance is IAsyncInitializableExtension async)
            {
                await async.InitializeAsync();
            }

            commandLineOptionsProviders.Add(serviceInstance);
        }

        ICommandLineOptionsProvider[] systemCommandLineOptionsProviders = new[]
        {
            new PlatformCommandLineProvider(),
        };

        return new CommandLineHandler(args, parseResult, commandLineOptionsProviders.ToArray(),
            systemCommandLineOptionsProviders, runtime, runtimeFeature, platformOutputDisplay, environment, process);
    }
}
