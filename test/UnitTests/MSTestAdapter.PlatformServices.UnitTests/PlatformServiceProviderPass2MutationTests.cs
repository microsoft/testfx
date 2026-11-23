// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests;

[DoNotParallelize]
public sealed class PlatformServiceProviderPass2MutationTests : TestContainer
{
    private readonly IPlatformServiceProvider _previousProvider = PlatformServiceProvider.Instance;
    private readonly IPlatformServiceProvider _provider;

    public PlatformServiceProviderPass2MutationTests()
    {
        PlatformServiceProvider.Instance = null;
        _provider = PlatformServiceProvider.Instance;
    }

    protected override void Dispose(bool disposing)
    {
        if (!IsDisposed)
        {
            base.Dispose(disposing);
            PlatformServiceProvider.Instance = _previousProvider;
        }
    }

    public void TheTestDataSourceIsCreatedOnFirstUseAndSharedByEveryLaterAccess()
    {
        object first = _provider.TestDataSource;
        object second = _provider.TestDataSource;

        first.Should().BeOfType<TestDataSource>();
        second.Should().BeSameAs(first);
    }
}
