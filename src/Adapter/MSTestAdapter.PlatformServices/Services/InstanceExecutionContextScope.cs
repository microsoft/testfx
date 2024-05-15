// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

internal readonly struct InstanceExecutionContextScope : IExecutionContextScope
{
    public InstanceExecutionContextScope(object instance, Type type)
    {
        Instance = instance;
        Type = type;
        IsCleanup = false;
        RemainingCleanupCount = 0;
    }

    public InstanceExecutionContextScope(object instance, Type type, int remainingCleanupCount)
    {
        Instance = instance;
        Type = type;
        IsCleanup = true;
        RemainingCleanupCount = remainingCleanupCount;
    }

    public object Instance { get; }

    public Type Type { get; }

    public bool IsCleanup { get; }

    public int RemainingCleanupCount { get; }

    public override readonly int GetHashCode() => Instance.GetHashCode();

    public override readonly bool Equals(object? obj) => Instance.Equals(obj);
}
