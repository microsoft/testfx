// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

using Microsoft.Testing.Platform.Builder;
using Microsoft.UI.Xaml;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace MSTestRunnerWinUI;
/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class UnitTestApp : Application
{
    private Window? _window;

    /// <summary>
    /// Initializes the singleton application object.  This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public UnitTestApp()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    [SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "<Pending>")]
    protected override
#if MSTEST_RUNNER
        async
#endif
        void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
#if !MSTEST_RUNNER
        Microsoft.VisualStudio.TestPlatform.TestExecutor.UnitTestClient.CreateDefaultUI();
#endif

        _window = new UnitTestAppWindow();
        _window.Activate();

        UITestMethodAttribute.DispatcherQueue = _window.DispatcherQueue;

#if MSTEST_RUNNER
        try
        {
            // Ideally we would want to reuse the generated main so we don't have to manually handle all dependencies
            // but this type is generated too late in the build process so we fail before.
            // You can build, inspect the generated type to copy its content if you want.
            //await MSTestRunnerWinUI.MicrosoftTestingPlatformEntryPoint.Main(Environment.GetCommandLineArgs().Skip(1).ToArray());
            string[] cliArgs = Environment.GetCommandLineArgs()
                .Skip(1)
                .Where(arg => !arg.Contains("EnableMSTestRunner"))
                .ToArray();
            ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(cliArgs);
            builder.AddSelfRegisteredExtensions(cliArgs);
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
}
