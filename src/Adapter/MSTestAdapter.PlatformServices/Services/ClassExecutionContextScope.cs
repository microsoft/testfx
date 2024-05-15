// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

internal readonly struct ClassExecutionContextScope : IExecutionContextScope
{
    public ClassExecutionContextScope(Type type)
    {
        Type = type;
        IsCleanup = false;
        RemainingCleanupCount = 0;
    }

    public ClassExecutionContextScope(Type type, int remainingCleanupCount)
    {
        Type = type;
        IsCleanup = true;
        RemainingCleanupCount = remainingCleanupCount;
    }

    public Type Type { get; }

    public bool IsCleanup { get; }

    public int RemainingCleanupCount { get; }

    public override readonly int GetHashCode() => Type.GetHashCode();

    public override readonly bool Equals(object? obj) => Type.Equals(obj);
}
