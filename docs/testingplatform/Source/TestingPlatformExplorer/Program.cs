// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

using Microsoft.Testing.Platform.Builder;

using TestingPlatformExplorer.TestingFramework;

var testApplicationBuilder = await TestApplication.CreateBuilderAsync(args);

// Register the testing framework
testApplicationBuilder.AddTestingFramework(new[] { Assembly.GetExecutingAssembly() });

using var testApplication = await testApplicationBuilder.BuildAsync();
return await testApplication.RunAsync();
