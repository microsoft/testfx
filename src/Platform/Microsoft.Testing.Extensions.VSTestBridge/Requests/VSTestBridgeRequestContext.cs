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
        ServiceProvider = adapterExtension.ServiceProvider;
        Configuration = ServiceProvider.GetConfiguration();
        CommandLineOptions = ServiceProvider.GetRequiredService<ICommandLineOptions>();
        LoggerFactory = ServiceProvider.GetRequiredService<ILoggerFactory>();
        FileSystem = ServiceProvider.GetFileSystem();
        ClientInfo = ServiceProvider.GetClientInfo();
        OutputDevice = ServiceProvider.GetOutputDevice();
        TestApplicationModuleInfo = ServiceProvider.GetTestApplicationModuleInfo();
        NamedFeatureCapability = ServiceProvider.GetTestFrameworkCapabilities().GetCapability<INamedFeatureCapability>();
        MessageBus = ServiceProvider.GetMessageBus();
    }

    public IServiceProvider ServiceProvider { get; }

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
