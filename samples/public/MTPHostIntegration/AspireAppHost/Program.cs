// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);
builder.AddProject<Projects.AspireApi>("api")
    .WithHttpEndpoint()
    .WithHttpHealthCheck("/health");
builder.Build().Run();
