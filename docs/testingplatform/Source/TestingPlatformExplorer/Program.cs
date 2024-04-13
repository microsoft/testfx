// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Builder;

using TestingPlatformExplorer.FunctionalTestingFramework;
using TestingPlatformExplorer.UnitTests;

var testApplicationBuilder = await TestApplication.CreateBuilderAsync(args);

// Register the testing framework
testApplicationBuilder.AddFunctionalTestingFramework(UnitTestsRegistration.GetActions());

using var testApplication = await testApplicationBuilder.BuildAsync();
return await testApplication.RunAsync();
