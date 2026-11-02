// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace TestFx.BazelValidation;

internal static class RuntimeProvenance
{
    [ModuleInitializer]
    internal static void Verify()
    {
#if NET11_0
        const int expectedMajor = 11;
#elif NET10_0
        const int expectedMajor = 10;
#elif NET9_0
        const int expectedMajor = 9;
#elif NET8_0
        const int expectedMajor = 8;
#else
#error The runtime validation asset must target a supported portable framework.
#endif
        if (Environment.Version.Major != expectedMajor)
        {
            throw new InvalidOperationException($"Expected runtime {expectedMajor}, loaded {Environment.Version}.");
        }

        Console.WriteLine($"BAZEL_RUNTIME_VERIFIED: {RuntimeInformation.FrameworkDescription}");
        Console.WriteLine($"BAZEL_CORELIB: {typeof(object).Assembly.Location}");
    }
}
