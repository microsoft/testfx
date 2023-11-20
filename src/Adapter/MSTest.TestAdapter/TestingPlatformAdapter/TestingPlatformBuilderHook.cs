﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Builder;

namespace Microsoft.Testing.Platform.Extensions.MSTest;

public static class TestingPlatformBuilderHook
{
    public static void AddExtensions(ITestApplicationBuilder testApplicationBuilder, string[] _)
    {
        testApplicationBuilder.AddCrashDumpGenerator(ignoreIfNotSupported: true);
        testApplicationBuilder.AddTrxReportGenerator();
    }
}
