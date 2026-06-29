// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

using Microsoft.Testing.Extensions;
using Microsoft.Testing.Platform.Builder;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CtrfPlayground.Mtp;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        ITestApplicationBuilder testApplicationBuilder = await TestApplication.CreateBuilderAsync(args);
        testApplicationBuilder.AddMSTest(() => [Assembly.GetEntryAssembly()!]);
        testApplicationBuilder.AddCtrfReportProvider();
        using ITestApplication app = await testApplicationBuilder.BuildAsync();
        return await app.RunAsync();
    }
}
