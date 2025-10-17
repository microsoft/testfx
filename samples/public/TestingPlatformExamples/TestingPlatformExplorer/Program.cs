// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

using Microsoft.Testing.Extensions;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Services;

using TestingPlatformExplorer.InProcess;
using TestingPlatformExplorer.OutOfProcess;
using TestingPlatformExplorer.TestingFramework;

public static class Program
{
    private static async Task<int> Main(string[] args)
    {
        // Create the test application builder
        ITestApplicationBuilder testApplicationBuilder = await TestApplication.CreateBuilderAsync(args);

        // Register the testing framework
        testApplicationBuilder.AddTestingFramework(() => new[] { Assembly.GetExecutingAssembly() });

        // In-process & out-of-process extensions
        // Register the testing framework command line options
        testApplicationBuilder.CommandLine.AddProvider(() => new TestingFrameworkCommandLineOptions());

        // In-process extensions
        //testApplicationBuilder.TestHost.AddTestHostApplicationLifetime(serviceProvider
        //    => new DisplayTestApplicationLifecycleCallbacks(serviceProvider.GetOutputDevice()));
        //testApplicationBuilder.TestHost.AddTestSessionLifetimeHandle(serviceProvider
        //    => new DisplayTestSessionLifeTimeHandler(serviceProvider.GetOutputDevice()));
        //testApplicationBuilder.TestHost.AddDataConsumer(serviceProvider
        //    => new DisplayDataConsumer(serviceProvider.GetOutputDevice()));

        //// Out-of-process extensions
        //testApplicationBuilder.TestHostControllers.AddEnvironmentVariableProvider(_
        //    => new SetEnvironmentVariableForTestHost());
        //testApplicationBuilder.TestHostControllers.AddProcessLifetimeHandler(serviceProvider =>
        //    new MonitorTestHost(serviceProvider.GetOutputDevice()));

        //// In-process composite extension SessionLifeTimeHandler+DataConsumer
        //CompositeExtensionFactory<DisplayCompositeExtensionFactorySample> compositeExtensionFactory = new(serviceProvider => new DisplayCompositeExtensionFactorySample(serviceProvider.GetOutputDevice()));
        //testApplicationBuilder.TestHost.AddTestSessionLifetimeHandle(compositeExtensionFactory);
        //testApplicationBuilder.TestHost.AddDataConsumer(compositeExtensionFactory);

        //// Register public extensions
        //// Trx
        //testApplicationBuilder.AddTrxReportProvider();

        using ITestApplication testApplication = await testApplicationBuilder.BuildAsync();
        return await testApplication.RunAsync();

    }

    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<TestingPlatformExplorer.App>();

#if DEBUG
        //builder.Logging.AddDebug();
        //builder.Services.AddLogging(configure => configure.AddDebug());
#endif

        return builder.Build();
    }

}

