// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

using Microsoft.Testing.Platform.Capabilities;
using Microsoft.Testing.Platform.Capabilities.TestFramework;

namespace Microsoft.Testing.Framework;

internal sealed class FactoryTestNodesBuilder : ITestNodesBuilder
{
    private readonly Func<TestNode[]> _testNodesFactory;

    public FactoryTestNodesBuilder(Func<TestNode[]> testNodesFactory)
        => _testNodesFactory = testNodesFactory;

    public bool IsSupportingTrxProperties { get; }

    IReadOnlyCollection<ITestFrameworkCapability> ICapabilities<ITestFrameworkCapability>.Capabilities => [];

    public Task<TestNode[]> BuildAsync(ITestSessionContext _) => Task.FromResult(_testNodesFactory());
}
