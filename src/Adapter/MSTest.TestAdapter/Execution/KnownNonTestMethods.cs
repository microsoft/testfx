// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

internal static class KnownNonTestMethods
{
    private static readonly string[] MethodNames = [
        nameof(Equals),
        nameof(GetHashCode),
        nameof(GetType),
        nameof(ReferenceEquals),
        nameof(ToString)
        ];

    public static bool Contains(string methodName)
    {
        if (methodName == "Equals")
        {
            Console.WriteLine($"Known non test methods called with: {methodName}");
            Console.WriteLine(Environment.StackTrace);
        }
        foreach (string method in MethodNames)
        {
            if (string.Equals(method, methodName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
