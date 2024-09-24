// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

using Microsoft.Testing.Platform.Builder;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[assembly: Parallelize(Workers = 0, Scope = ExecutionScope.MethodLevel)]

// To attach to the children
// Microsoft.Testing.TestInfrastructure.DebuggerUtility.AttachCurrentProcessToParentVSProcess();
ITestApplicationBuilder testApplicationBuilder = await TestApplication.CreateBuilderAsync(args);
testApplicationBuilder.AddMSTest(() => [Assembly.GetEntryAssembly()!]);

// Enable Trx
// testApplicationBuilder.AddTrxReportProvider();

// Enable Telemetry
// testApplicationBuilder.AddAppInsightsTelemetryProvider();
using ITestApplication testApplication = await testApplicationBuilder.BuildAsync();
return await testApplication.RunAsync();
