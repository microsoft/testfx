// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

internal readonly struct InstanceExecutionContextScope : IExecutionContextScope
{
    public InstanceExecutionContextScope(object instance, Type type, bool isCleanup)
    {
        Instance = instance;
        Type = type;
        IsCleanup = isCleanup;
    }

    public object Instance { get; }

    public Type Type { get; }

    public bool IsCleanup { get; }

    public override readonly int GetHashCode() => Instance.GetHashCode();

    public override readonly bool Equals(object? obj) => Instance.Equals(obj);
}
