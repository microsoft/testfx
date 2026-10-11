// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

internal static class AspNetCoreTestHost
{
    public static Task<IHost> CreateHost()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.AddTestServiceDefaults();
        builder.Services.AddMSTestTestClassInjection();
        builder.Services.AddSingleton<AspNetCoreApplication>();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration["Greeting"] = "Hello from ASP.NET Core";

        WebApplication app = builder.Build();
        app.MapDefaultEndpoints();
        app.MapGet("/greeting", (IConfiguration configuration) => configuration["Greeting"]);
        app.Services.GetRequiredService<AspNetCoreApplication>().Initialize(app);
        return Task.FromResult<IHost>(app);
    }
}

public sealed class AspNetCoreApplication
{
    private WebApplication? _application;

    public void Initialize(WebApplication application) => _application = application;

    public HttpClient CreateClient()
    {
        WebApplication application = _application
            ?? throw new InvalidOperationException("The ASP.NET Core application has not been initialized.");
        IServer server = application.Services.GetRequiredService<IServer>();
        string address = server.Features.Get<IServerAddressesFeature>()?.Addresses.Single()
            ?? throw new InvalidOperationException("The ASP.NET Core server did not publish an address.");
        return new HttpClient { BaseAddress = new Uri(address) };
    }
}
