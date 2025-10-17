// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

using Microsoft.Maui;
using Microsoft.Maui.Hosting;

namespace MauiApp1;

internal class Program : MauiApplication
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    static void Main(string[] args)
    {
        var app = new Program();
        app.Run(args);
    }
}
