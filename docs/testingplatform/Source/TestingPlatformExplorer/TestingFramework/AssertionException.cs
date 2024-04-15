// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace TestingPlatformExplorer.TestingFramework;

[Serializable]
public class AssertionException : Exception
{
    public AssertionException()
    {
    }

    public AssertionException(string? message)
        : base(message)
    {
    }

    public AssertionException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
