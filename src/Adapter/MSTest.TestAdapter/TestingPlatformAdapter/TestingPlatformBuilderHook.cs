// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP
using System.Reflection;

using Microsoft.Testing.Platform.Builder;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

public static class TestingPlatformBuilderHook
{
#pragma warning disable IDE0060 // Remove unused parameter
    public static void AddExtensions(ITestApplicationBuilder testApplicationBuilder, string[] arguments)
    {
#if NET8_0_OR_GREATER
        if (!System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported)
        {
            // We don't have a reliable way to get reference to the entry dll when compiled as NativeAOT. So instead we do the same registration
            // in source generator.
            return;
        }
#endif
        testApplicationBuilder.AddMSTest(() => [Assembly.GetEntryAssembly()!]);
    }
}
#endif
