﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

using Microsoft.Testing.Platform.Builder;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Testing.Platform.Extensions.MSTest;

public static class TestingPlatformBuilderHook
{
#pragma warning disable IDE0060 // Remove unused parameter
    public static void AddExtensions(ITestApplicationBuilder testApplicationBuilder, string[] arguments)
#pragma warning restore IDE0060 // Remove unused parameter
    {
        testApplicationBuilder.AddMSTest(() => new[] { Assembly.GetEntryAssembly()! });
    }
}
