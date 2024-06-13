// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Builder;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using MSTestAdapter.Try.UnitTests;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var rdp = new SourceGeneratedReflectionDataProvider
        {
            Assembly = typeof(Program).Assembly,
            AssemblyAttributes = typeof(Program).Assembly.GetCustomAttributes(inherit: true),
            Types = new[]
            {
                typeof(UnitTest1),
            },
            TypesByName = new()
            {
                [typeof(UnitTest1).FullName] = typeof(UnitTest1),
            },

            TypeConstructors = new()
            {
                [typeof(UnitTest1)] = typeof(UnitTest1).GetConstructors(),
            },
            TypeConstructorsInvoker = new()
            {
                [typeof(UnitTest1)] = new[]
                {
                    new MyConstructorInfo()
                    {
                        Parameters = [],
                        Invoker = parameters => new UnitTest1(),
                    },
                },
            },

            TypeAttributes = new()
            {
                [typeof(UnitTest1)] = new[] { new TestClassAttribute() },
            },
            TypeProperties = new()
            {
                [typeof(UnitTest1)] = [
                        typeof(UnitTest1).GetProperty(nameof(UnitTest1.TestContext))
                    ],
            },
            TypePropertiesByName = new()
            {
                [typeof(UnitTest1)] = new()
                {
                    [nameof(UnitTest1.TestContext)] = typeof(UnitTest1).GetProperty(nameof(UnitTest1.TestContext)),
                },
            },

            TypeMethods = new()
            {
                [typeof(UnitTest1)] = new[] { typeof(UnitTest1).GetMethod(nameof(UnitTest1.TestMethod1)) },
            },
            TypeMethodAttributes = new()
            {
                [typeof(UnitTest1)] = new()
                {
                    [nameof(UnitTest1.TestMethod1)] = new[]
                    {
                        new TestMethodAttribute(),
                    },
                },
            },
        };

        bool useNative = true;
        if (useNative)
        {
            Environment.SetEnvironmentVariable("MSTEST_NATIVE", "1");
            ((NativeFileOperations)PlatformServiceProvider.Instance.FileOperations).ReflectionDataProvider = rdp;
            ((NativeReflectionOperations)PlatformServiceProvider.Instance.ReflectionOperations).ReflectionDataProvider = rdp;
        }

        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
        builder.AddMSTest(() => [typeof(Program).Assembly]);
        using ITestApplication app = await builder.BuildAsync();
        return await app.RunAsync();
    }
}
