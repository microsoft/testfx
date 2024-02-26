// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.TestFramework;

namespace Contoso.EnsureTestFramework;

public static class TestApplicationBuilderExtensions
{
    public static void AddEnsureTestFramework(this ITestApplicationBuilder testApplicationBuilder)
    {
        EnsureTestFrameworkExtension extension = new();
        testApplicationBuilder.RegisterTestFramework(GetCapabilities, GetFramework);
    }

    private static ITestFrameworkCapabilities GetCapabilities(IServiceProvider provider)
    {
        return new EnsureTestFrameworkCapabilities();
    }

    private static ITestFramework GetFramework(ITestFrameworkCapabilities capabilities, IServiceProvider provider)
    {
        EnsureTestFrameworkExtension extension = new();
        return new EnsureTestFramework(extension, capabilities, provider);
    }
}
