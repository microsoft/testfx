// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Testing.Extensions;

WebApplicationBuilder builder = WebApplication.CreateBuilder();
builder.AddTestServiceDefaults();
builder.WebHost.UseUrls("http://127.0.0.1:0");
builder.Configuration["Greeting"] = "Hello from ASP.NET Core";

await using WebApplication app = builder.Build();
app.MapDefaultEndpoints();
app.MapGet("/greeting", (IConfiguration configuration) => configuration["Greeting"]);
AspNetCoreApplication.Initialize(app);

return await app.RunTestingPlatformAsync(args, tests =>
{
    tests.AddMSTest(() => [Assembly.GetExecutingAssembly()]);
    tests.AddTestingPlatformDiagnostics();
});

internal static class AspNetCoreApplication
{
    private static WebApplication? s_application;

    public static void Initialize(WebApplication application) => s_application = application;

    public static HttpClient CreateClient()
    {
        WebApplication application = s_application
            ?? throw new InvalidOperationException("The ASP.NET Core application has not been initialized.");
        IServer server = application.Services.GetRequiredService<IServer>();
        string address = server.Features.Get<IServerAddressesFeature>()?.Addresses.Single()
            ?? throw new InvalidOperationException("The ASP.NET Core server did not publish an address.");
        return new HttpClient { BaseAddress = new Uri(address) };
    }
}
