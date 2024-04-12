// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace TestingPlatformExplorer.TestingFramework;

[Serializable]
public class AssertException : Exception
{
    public AssertException()
    {
    }

    public AssertException(string message)
        : base(message)
    {
    }

    public AssertException(string message, Exception inner)
        : base(message, inner)
    {
    }
}

public static class Assert
{
    public static void AreEqual<T>(T expected, T actual)
    {
        if (expected is null)
        {
            throw new ArgumentNullException(nameof(expected));
        }

        if (actual is null)
        {
            throw new ArgumentNullException(nameof(expected));
        }

        if (!expected.Equals(actual))
        {
            throw new AssertException($"Expected: {expected}, Actual: {actual}");
        }
    }
}
