// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Builder;

using NUnit.VisualStudio.TestAdapter.TestingPlatformAdapter;

// Mirrors samples/NUnitPlayground/Program.cs without the preview AffectedTests package.
Environment.SetEnvironmentVariable("DOTNET_CLI_TELEMETRY_OPTOUT", "1");

ITestApplicationBuilder testApplicationBuilder = await TestApplication.CreateBuilderAsync(args);
testApplicationBuilder.AddNUnit(() => [Assembly.GetEntryAssembly()!]);
using ITestApplication testApplication = await testApplicationBuilder.BuildAsync();
return await testApplication.RunAsync();
