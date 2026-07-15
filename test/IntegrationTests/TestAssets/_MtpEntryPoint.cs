// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// Shared Microsoft.Testing.Platform entry point for test assets that are built as executables in v5.
// Building the assets as MTP executables lets vstest.console (18.10+) host them through the translation
// layer (see MSTest.VstestConsoleWrapper.IntegrationTests). Assets opt in by linking this file and
// setting <OutputType>Exe</OutputType>.
using System.Reflection;

using Microsoft.Testing.Platform.Builder;
using Microsoft.VisualStudio.TestTools.UnitTesting;

ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
builder.AddMSTest(() => new[] { Assembly.GetEntryAssembly()! });
using ITestApplication app = await builder.BuildAsync();
return await app.RunAsync();
