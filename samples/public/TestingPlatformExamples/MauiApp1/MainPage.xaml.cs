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
using System.Collections.ObjectModel;

namespace MauiApp1;

public partial class MainPage : ContentPage
{
    int count = 0;
    ITestApplication testApplication;

    ObservableCollection<string> Logs = new ObservableCollection<string>();

    public MainPage()
    {
        InitializeComponent();
        this.Loaded += OnLoaded;
        cvLogs.ItemsSource = Logs;
    }

    private async void OnLoaded(object? sender, EventArgs e)
    {
        Logs.Clear();
        Logs.Add("Loaded the testApplicationBuilder");
        string cacheDir = FileSystem.CacheDirectory;

#if ANDROID

        cacheDir = Android.App.Application.Context.CacheDir!.AbsolutePath;

#endif
        ITestApplicationBuilder testApplicationBuilder = await TestApplication.CreateBuilderAsync(new[] {
            "--results-directory", cacheDir
            });

        //testApplicationBuilder.TestHost.AddTestHostApplicationLifetime(serviceProvider
        //    => new DisplayTestApplicationLifecycleCallbacks(serviceProvider.GetOutputDevice()));
        //testApplicationBuilder.TestHost.AddTestSessionLifetimeHandle(serviceProvider
        //    => new DisplayTestSessionLifeTimeHandler(serviceProvider.GetOutputDevice()));
        testApplicationBuilder.TestHost.AddDataConsumer(serviceProvider
            => new DisplayDataConsumer(serviceProvider.GetOutputDevice(), Logs));

        // Register the xUnit test framework using the proper registration method
        testApplicationBuilder.RegisterTestFramework(
            _ => new XunitFrameworkCapabilities(),
            (capabilities, serviceProvider) => new XunitFramework(capabilities, serviceProvider));

        testApplication = await testApplicationBuilder.BuildAsync();

    }

    private async void OnCounterClicked(object? sender, EventArgs e)
    {
        count++;
        Logs.Add("Start running tests");
        await testApplication.RunAsync();
        Logs.Add("Finished running tests");
    }
}

