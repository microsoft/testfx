// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

internal static class AspireTestHost
{
    public static Task<IHost> CreateHost()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.AddTestServiceDefaults();
        return Task.FromResult(builder.Build());
    }
}
