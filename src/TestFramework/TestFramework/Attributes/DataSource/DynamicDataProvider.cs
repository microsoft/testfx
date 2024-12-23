// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

internal static class DynamicDataProvider
{
    [field: MaybeNull]
    public static IDynamicDataOperations Instance
    {
        get => field
            ?? throw new InvalidOperationException($"Dynamic data provider is not set, it should be set by MSTest adapter. " +
                $"If you are seeing this error, you are using Test Framework without Test Adapter, and your adapter should set {nameof(DynamicDataProvider)}.{nameof(Instance)}. In MSTestAdapter, this happens when PlatformServiceProvider.Instance is called.");
        internal set;
    }
}
