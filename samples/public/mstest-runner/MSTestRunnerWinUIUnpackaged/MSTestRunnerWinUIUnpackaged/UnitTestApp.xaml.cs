// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Testing.Platform.Builder;
using Microsoft.UI.Xaml;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;

namespace MSTestRunnerWinUIUnpackaged;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class UnitTestApp : Application
{
    private Window? _window;

    /// <summary>
    /// Initializes the singleton application object. This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public UnitTestApp() => InitializeComponent();

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new UnitTestAppWindow();
        _window.Activate();

        // Tests marked [UITestMethod] run on this window's dispatcher queue. This process has already
        // called Application.Start, so the dispatcher has to be published here. Do NOT use
        // [assembly: WinUITestTarget(...)] in a self-hosted app like this one: it would make
        // UITestMethodAttribute start a second application in the same process.
        UITestMethodAttribute.DispatcherQueue = _window.DispatcherQueue;

        try
        {
            // Ideally we would want to reuse the generated main so we don't have to manually handle all
            // dependencies, but this type is generated too late in the build process so we fail before.
            // You can build, inspect the generated type to copy its content if you want.
            string[] cliArgs = Environment.GetCommandLineArgs()
                .Skip(1)
                .Where(arg => !arg.Contains("EnableMSTestRunner"))
                .ToArray();
            ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(cliArgs);
            builder.AddSelfRegisteredExtensions(cliArgs);
            using ITestApplication app = await builder.BuildAsync();

            // The WinUI-generated entry point is 'void', so the run's exit code has to be published
            // through Environment.ExitCode. Without this the app would always exit 0 and failing tests
            // would never fail the build.
            Environment.ExitCode = await app.RunAsync();
        }
        finally
        {
            // Closing the window lets the message loop drain so the process can exit once the run is
            // done. Without this an unpackaged test app — which is itself the test host — would hang
            // after reporting results.
            _window.Close();
            Exit();
        }
    }
}
