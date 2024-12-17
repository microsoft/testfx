// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

using Microsoft.Testing.Extensions.Hosting;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.Extensions;

public static class HotReloadExtensions
{
    public static void AddHotReloadProvider(this ITestApplicationBuilder builder)
        => ((TestHostManager)builder.TestHost).AddTestFrameworkInvoker(serviceProvider =>
            new HotReloadTestHostTestFrameworkInvoker(serviceProvider));
}
