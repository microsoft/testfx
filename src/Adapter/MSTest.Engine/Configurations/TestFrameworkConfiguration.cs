// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

namespace Microsoft.Testing.Framework.Configurations;

public sealed class TestFrameworkConfiguration(int maxParallelTests = int.MaxValue)
{
    public int MaxParallelTests { get; } = maxParallelTests;
}
