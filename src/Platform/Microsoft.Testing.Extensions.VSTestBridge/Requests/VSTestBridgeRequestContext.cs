// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Extensions.VSTestBridge.Requests;

internal readonly struct VSTestBridgeRequestContext
{
    public VSTestBridgeRequestContext(VSTestBridgedTestFrameworkBase adapterExtension)
    {
        IServiceProvider serviceProvider = adapterExtension.ServiceProvider;
        Configuration = serviceProvider.GetConfiguration();
        CommandLineOptions = serviceProvider.GetRequiredService<ICommandLineOptions>();
        LoggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        FileSystem = serviceProvider.GetFileSystem();
        ClientInfo = serviceProvider.GetClientInfo();
        OutputDevice = serviceProvider.GetOutputDevice();
        TestApplicationModuleInfo = serviceProvider.GetTestApplicationModuleInfo();
        NamedFeatureCapability = serviceProvider.GetTestFrameworkCapabilities().GetCapability<INamedFeatureCapability>();
        MessageBus = serviceProvider.GetMessageBus();
    }

    public IConfiguration Configuration { get; }

    public ICommandLineOptions CommandLineOptions { get; }

    public ILoggerFactory LoggerFactory { get; }

    public IFileSystem FileSystem { get; }

    public IClientInfo ClientInfo { get; }

    public IOutputDevice OutputDevice { get; }

    public ITestApplicationModuleInfo TestApplicationModuleInfo { get; }

    public INamedFeatureCapability? NamedFeatureCapability { get; }

    public IMessageBus MessageBus { get; }
}
