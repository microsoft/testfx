// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace TestingPlatformExplorer.TestingFramework;

public static class Assert
{
    public static void AreEqual<T>(T expected, T actual)
    {
        if (expected is null)
        {
            throw new ArgumentException("'expected' cannot be null", nameof(expected));
        }

        if (actual is null)
        {
            throw new ArgumentException("'actual' cannot be null", nameof(actual));
        }

        if (!expected.Equals(actual))
        {
            throw new AssertionException($"Expected: {expected}, Actual: {actual}");
        }
    }
}
