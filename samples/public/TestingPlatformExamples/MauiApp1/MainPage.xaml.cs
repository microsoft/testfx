// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

using Microsoft.Testing.Extensions;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Requests;
using Microsoft.Testing.Platform.Capabilities;

using Xunit;
using System.Collections.Generic;
using XUnitTestFx = Xunit.v3;

namespace MauiApp1;

public partial class MainPage : ContentPage
{
    int count = 0;

    public MainPage()
    {
        InitializeComponent();
        this.Loaded += OnLoaded;
    }

    private async void OnLoaded(object? sender, EventArgs e)
    {
        // Create the test application builder

    }

    private async void OnCounterClicked(object? sender, EventArgs e)
    {
        count++;

        if (count == 1)
            CounterBtn.Text = $"Clicked {count} time";
        else
            CounterBtn.Text = $"Clicked {count} times";

        SemanticScreenReader.Announce(CounterBtn.Text);

        string cacheDir = FileSystem.CacheDirectory;

#if ANDROID

        cacheDir = Android.App.Application.Context.CacheDir!.AbsolutePath;

#endif
        ITestApplicationBuilder testApplicationBuilder = await TestApplication.CreateBuilderAsync(new[] {
            "--results-directory", cacheDir
            });

        // Register the xUnit test framework using the proper registration method
        testApplicationBuilder.RegisterTestFramework(
            _ => new XunitFrameworkCapabilities(),
            (capabilities, serviceProvider) => new XunitFramework(capabilities, serviceProvider));

        //testApplicationBuilder.TestHostControllers.AddProcessLifetimeHandler(serviceProvider =>
        //    new MonitorTestHost(serviceProvider.GetOutputDevice()));

        // Build and run the test application
        using ITestApplication testApplication = await testApplicationBuilder.BuildAsync();
        await testApplication.RunAsync();
    }
}

