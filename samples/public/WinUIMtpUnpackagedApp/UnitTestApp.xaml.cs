// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

using Microsoft.UI.Xaml;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;

namespace WinUIMtpUnpackagedApp;

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
            // The WinUI-generated entry point is void, so publish the generated MTP runner's exit code.
            Environment.ExitCode = await MicrosoftTestingPlatformApplication.RunAsync(Environment.GetCommandLineArgs()[1..]);
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
