// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Builder;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;
using System;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.VisualStudio.TestTools.UnitTesting;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace UnitTest;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// Initializes the singleton application object.  This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override
#if MSTEST_RUNNER
        async
#endif
        void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
#if !MSTEST_RUNNER
        Microsoft.VisualStudio.TestPlatform.TestExecutor.UnitTestClient.CreateDefaultUI();
#endif

        _window = new MainWindow();
        _window.Activate();
        UITestMethodAttribute.DispatcherQueue = DispatcherQueue.GetForCurrentThread();

        // Replace back with e.Arguments when https://github.com/microsoft/microsoft-ui-xaml/issues/3368 is fixed
#if MSTEST_RUNNER
        // Ideally we would want to reuse the generated main so we don't have to manually handle all dependencies
        // but this type is generated too late in the build process so we fail before.
        // You can build, inspect the generated type to copy its content if you want.
        // await TestingPlatformEntryPoint.Main(Environment.GetCommandLineArgs().Skip(1).ToArray());
        try
        {

            string[] cliArgs = Environment.GetCommandLineArgs().Skip(1).ToArray();
            ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(cliArgs);
            Microsoft.Testing.Platform.MSBuild.TestingPlatformBuilderHook.AddExtensions(builder, cliArgs);
            Microsoft.Testing.Extensions.Telemetry.TestingPlatformBuilderHook.AddExtensions(builder, cliArgs);
            TestingPlatformBuilderHook.AddExtensions(builder, cliArgs);
            using ITestApplication app = await builder.BuildAsync();
            await app.RunAsync();
        }
        finally
        {
            _window.Close();
        }
#else
        Microsoft.VisualStudio.TestPlatform.TestExecutor.UnitTestClient.Run(Environment.CommandLine);
#endif
    }

    private Window _window;
}
