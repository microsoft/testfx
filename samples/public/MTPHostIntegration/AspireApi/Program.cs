// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Configuration["Greeting"] = "Hello from Aspire";

WebApplication app = builder.Build();
app.MapDefaultEndpoints();
app.MapGet("/greeting", (IConfiguration configuration) => configuration["Greeting"]);
app.Run();
