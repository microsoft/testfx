// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.Diagnostics;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Extensions;

public static class HangDumpExtensions
{
    public static void AddHangDumpProvider(this ITestApplicationBuilder builder)
    {
        CurrentTestApplicationModuleInfo testApplicationModuleInfo = new(new SystemEnvironment(), new SystemProcessHandler());
        string mutexSuffix = Guid.NewGuid().ToString("N");
        PipeNameDescription pipeNameDescription = NamedPipeServer.GetPipeName(Guid.NewGuid().ToString("N"));
        HangDumpConfiguration hangDumpConfiguration = new(testApplicationModuleInfo, pipeNameDescription, mutexSuffix);

        builder.TestHostControllers.AddProcessLifetimeHandler(serviceProvider
            => new HangDumpProcessLifetimeHandler(
                pipeNameDescription,
                serviceProvider.GetMessageBus(),
                serviceProvider.GetOutputDevice(),
                serviceProvider.GetCommandLineOptions(),
                serviceProvider.GetTask(),
                serviceProvider.GetEnvironment(),
                serviceProvider.GetLoggerFactory(),
                serviceProvider.GetTestApplicationModuleInfo(),
                serviceProvider.GetConfiguration(),
                serviceProvider.GetProcessHandler(),
                serviceProvider,
                serviceProvider.GetClock()));

        builder.TestHostControllers.AddEnvironmentVariableProvider(serviceProvider
            => new HangDumpEnvironmentVariableProvider(serviceProvider.GetCommandLineOptions(), hangDumpConfiguration));

        builder.CommandLine.AddProvider(()
            => new HangDumpCommandLineProvider(hangDumpConfiguration));

        var hangDumpActivityIndicatorComposite
            = new CompositeExtensionFactory<HangDumpActivityIndicator>(serviceProvider => new HangDumpActivityIndicator(
                serviceProvider.GetCommandLineOptions(),
                serviceProvider.GetEnvironment(),
                serviceProvider.GetTask(),
                serviceProvider.GetTestApplicationModuleInfo(),
                serviceProvider.GetLoggerFactory(),
                serviceProvider.GetClock()));

        builder.TestHost.AddDataConsumer(hangDumpActivityIndicatorComposite);
        builder.TestHost.AddTestSessionLifetimeHandle(hangDumpActivityIndicatorComposite);
    }
}
