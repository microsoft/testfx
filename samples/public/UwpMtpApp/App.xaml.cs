// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics.CodeAnalysis;

using Microsoft.Testing.Extensions;

using Windows.ApplicationModel.Activation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace UwpMtpApp;

public sealed partial class App : Application
{
    public App() => InitializeComponent();

    [SuppressMessage("Reliability", "CA2007:Consider calling ConfigureAwait on the awaited task", Justification = "The continuation must resume on the UWP dispatcher.")]
    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        var rootFrame = Window.Current.Content as Frame;
        if (rootFrame is null)
        {
            rootFrame = new Frame();
            rootFrame.NavigationFailed += OnNavigationFailed;
            Window.Current.Content = rootFrame;
        }

        if (rootFrame.Content is null)
        {
            rootFrame.Navigate(typeof(MainPage));
        }

        Window.Current.Activate();
        try
        {
            string[] testArguments = PackagedAppExtensions.GetTestApplicationArguments(args.Arguments);
            Environment.ExitCode = await MicrosoftTestingPlatformApplication.RunAsync(testArguments);
        }
        finally
        {
            Exit();
        }
    }

    private static void OnNavigationFailed(object sender, NavigationFailedEventArgs args)
        => throw new InvalidOperationException($"Failed to load page '{args.SourcePageType.FullName}'.");
}
