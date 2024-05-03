// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

using Microsoft.Testing.Platform.Builder;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Playground;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        // Opt-out telemetry
        Environment.SetEnvironmentVariable("DOTNET_CLI_TELEMETRY_OPTOUT", "1");

        ITestApplicationBuilder testApplicationBuilder = await TestApplication.CreateBuilderAsync(args);
        testApplicationBuilder.AddMSTest(() => new[] { Assembly.GetEntryAssembly()! });

        // Enable Trx
        // testApplicationBuilder.AddTrxReportProvider();
        using ITestApplication testApplication = await testApplicationBuilder.BuildAsync();
        return await testApplication.RunAsync();
    }
}
