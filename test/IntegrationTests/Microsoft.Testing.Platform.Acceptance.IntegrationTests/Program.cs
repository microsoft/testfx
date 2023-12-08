// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;

using Microsoft.Testing.Framework.Configurations;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests;

ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);

// Experiment, set the max tests to the processor count let's see if tests will be less flaky due to the less parallel processes.
builder.AddTestFramework(new TestFrameworkConfiguration(Debugger.IsAttached ? 1 : Environment.ProcessorCount), new SourceGeneratedTestNodesBuilder());
using ITestApplication app = await builder.BuildAsync();
int returnValue = await app.RunAsync();
Console.WriteLine($"Process started: {CommandLine.TotalProcessesAttempt}");
return returnValue;
