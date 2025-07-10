// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions;

using Microsoft.VisualStudio.TestTools.UnitTesting;

[assembly: Parallelize(Scope = Microsoft.VisualStudio.TestTools.UnitTesting.ExecutionScope.ClassLevel, Workers = 0)]

ITestApplicationBuilder testApplicationBuilder = await TestApplication.CreateBuilderAsync(args);

testApplicationBuilder.AddMSTest(() => [Assembly.GetEntryAssembly()!]);
testApplicationBuilder.AddTrxReportProvider();
testApplicationBuilder.AddAppInsightsTelemetryProvider();
testApplicationBuilder.AddCodeCoverageProvider();
testApplicationBuilder.AddAzureDevOpsProvider();
using ITestApplication testApplication = await testApplicationBuilder.BuildAsync();
return await testApplication.RunAsync();
