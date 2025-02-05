// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP
using Microsoft.Testing.Platform.Builder;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// This class is used by Microsoft.Testing.Platform.MSBuild to hook into the Testing Platform Builder to add MSTest support.
/// </summary>
[SuppressMessage("ApiDesign", "RS0030:Do not use banned APIs", Justification = "We can use MTP from this folder")]
public static class TestingPlatformBuilderHook
{
    /// <summary>
    /// Adds MSTest support to the Testing Platform Builder.
    /// </summary>
    /// <param name="testApplicationBuilder">The test application builder on which registering MSTest.</param>
    /// <param name="arguments">The test application cli arguments.</param>
#pragma warning disable IDE0060 // Remove unused parameter
    public static void AddExtensions(ITestApplicationBuilder testApplicationBuilder, string[] arguments)
        => testApplicationBuilder.AddMSTest(() => [Assembly.GetEntryAssembly()!]);
}
#endif
