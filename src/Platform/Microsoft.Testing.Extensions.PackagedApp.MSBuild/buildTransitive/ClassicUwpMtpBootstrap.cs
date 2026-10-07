// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Reflection;
using System.Threading.Tasks;

internal static class MicrosoftTestingPlatformApplication
{
    public static async Task<int> RunAsync(string activationArguments)
    {
        Assembly adapterAssembly = Assembly.Load(new AssemblyName("MSTest.TestAdapter"));
        Type bootstrapType = adapterAssembly.GetType(
            "Microsoft.Testing.Extensions.PackagedAppExtensions",
            throwOnError: true);
        MethodInfo runTestsMethod = bootstrapType.GetRuntimeMethod(
            "RunTestsAsync",
            new[] { typeof(string) })
            ?? throw new MissingMethodException(bootstrapType.FullName, "RunTestsAsync");
        var task = (Task<int>)runTestsMethod.Invoke(null, new object[] { activationArguments });
        return await task.ConfigureAwait(false);
    }
}
