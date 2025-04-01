﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Builder;

namespace Microsoft.Testing.Extensions.AzureDevOps;

/// <summary>
/// This class is used by Microsoft.Testing.Platform.MSBuild to hook into the Testing Platform Builder to add Azure DevOps reporting support.
/// </summary>
public static class TestingPlatformBuilderHook
{
    /// <summary>
    /// Adds Azure DevOps reporting support to the Testing Platform Builder.
    /// </summary>
    /// <param name="testApplicationBuilder">The test application builder.</param>
    /// <param name="_">The command line arguments.</param>
    public static void AddExtensions(ITestApplicationBuilder testApplicationBuilder, string[] _)
        => testApplicationBuilder.AddAzureDevOpsProvider();
}
