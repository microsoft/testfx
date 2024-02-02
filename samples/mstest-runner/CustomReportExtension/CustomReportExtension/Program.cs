// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Extensions;

ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);

// Register MSTest as the test framework to use.
builder.AddMSTest(() => new[] { typeof(Program).Assembly });

// This is a convenience helper that simplifies the process of creating a composite extension (a type that implements multiple extension points).
// Thanks to that, you can implement multiple extension points in a single class and you don't have to handle communication/synchronization between them.
CompositeExtensionFactory<TestResultConsoleLogger> testUpdateConsoleReporter =
    new(serviceProvider => new TestResultConsoleLogger());

// Register the extension as a data consumer and test session lifetime handler.
builder.TestHost.AddDataConsumer(testUpdateConsoleReporter);
builder.TestHost.AddTestSessionLifetimeHandle(testUpdateConsoleReporter);

ITestApplication app = await builder.BuildAsync();

return await app.RunAsync();
