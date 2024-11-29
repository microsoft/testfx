// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

using Microsoft.Testing.Extensions;
using Microsoft.Testing.Platform.Builder;

// Create the test application builder
ITestApplicationBuilder testApplicationBuilder = await TestApplication.CreateBuilderAsync(args);

// Register the testing framework
testApplicationBuilder.AddMSTest(() => new[] { Assembly.GetExecutingAssembly() });

// Register Code Coverage extension
testApplicationBuilder.AddCodeCoverageProvider();

// Register TRX report extension
testApplicationBuilder.AddTrxReportProvider();

// Register Telemetry extension
testApplicationBuilder.AddAppInsightsTelemetryProvider();

// Alternatively, instead of registering everything manually, I could rely on the MSBuild hooks and use
// testApplicationBuilder.AddSelfRegisteredExtensions(args);
// This is what is called by the generated entry point

// In addition to be using each extension helper method, we can directly register extensions to each of
// the extensibility area. For now, the following 3 are exposed:
// testApplicationBuilder.CommandLine
// testApplicationBuilder.TestHost
// testApplicationBuilder.TestHostControllers
// but the goal is to also expose all these areas: https://github.com/microsoft/testfx/blob/main/src/Platform/Microsoft.Testing.Platform/Builder/TestApplicationBuilder.cs#L57-L69

// NOTE that registering an extension is not enough and each extension has some activation criteria,
// most of the time the presence of a command line option but it could be anything (including nothing).

// Build the test app
using ITestApplication testApplication = await testApplicationBuilder.BuildAsync();

// Run the test app
return await testApplication.RunAsync();
