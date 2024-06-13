// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Builder;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var rdp = new SourceGeneratedReflectionDataProvider
        {
            Assembly = typeof(Program).Assembly,
            Types = new[]
            {
                typeof(Program),
            },
        };

        ((NativeFileOperations)PlatformServiceProvider.Instance.FileOperations).ReflectionDataProvider = rdp;
        ((NativeReflectionOperations)PlatformServiceProvider.Instance.ReflectionOperations).ReflectionDataProvider = rdp;

        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
        builder.AddMSTest(() => [typeof(Program).Assembly]);
        using ITestApplication app = await builder.BuildAsync();
        return await app.RunAsync();
    }
}
