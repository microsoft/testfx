// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

using Microsoft.Extensions.Hosting;
using Microsoft.Testing.Extensions;

HostApplicationBuilder builder = Host.CreateApplicationBuilder();
builder.AddTestServiceDefaults();

using IHost host = builder.Build();
return await host.RunTestingPlatformAsync(args, tests =>
{
    tests.AddMSTest(() => [Assembly.GetExecutingAssembly()]);
    tests.AddTestingPlatformDiagnostics();
});
