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

            // Registers all MSBuild-contributed extensions, including the Microsoft.Testing.Extensions.PackagedApp
            // launcher (from the PackageReference in the csproj). Because this app is packaged (MSIX), that
            // launcher registers the layout with the OS and activates it by Application User Model ID through
            // the platform's ITestHostLauncher extension point, instead of a plain Process.Start.
            builder.AddSelfRegisteredExtensions(cliArgs);
            using ITestApplication app = await builder.BuildAsync();

            // The WinUI-generated entry point is 'void', so the run's exit code has to be published through
            // Environment.ExitCode. Without this the app would always exit 0 and failing tests would never
            // fail the build.
            Environment.ExitCode = await app.RunAsync();
        }
        finally
        {
            _window.Close();
            Exit();
        }
#else
        Microsoft.VisualStudio.TestPlatform.TestExecutor.UnitTestClient.Run(Environment.CommandLine);
#endif
    }
}
