// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !NETFRAMEWORK
using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Services;
#pragma warning disable SA1649 // SA1649FileNameMustMatchTypeName

public class TestSourceHostTests : TestContainer
{
    private readonly TestSourceHost _testSourceHost;

    public TestSourceHostTests() => _testSourceHost = new TestSourceHost(null!, null, null);

    public void CreateInstanceForTypeCreatesAnInstanceOfAGivenTypeThroughDefaultConstructor()
    {
        var type = _testSourceHost.CreateInstanceForType(typeof(DummyType), null) as DummyType;

        type.Should().NotBeNull();
        type.IsDefaultConstructorCalled.Should().BeTrue();
    }
}

public class DummyType
{
    public DummyType() => IsDefaultConstructorCalled = true;

    public bool IsDefaultConstructorCalled { get; set; }
}

#pragma warning restore SA1649 // SA1649FileNameMustMatchTypeName

#endif
