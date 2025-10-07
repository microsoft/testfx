// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.Tests.Services;

public class TestSourceTests : TestContainer
{
    private readonly TestSourceHandler _testSource;

    public TestSourceTests() => _testSource = new TestSourceHandler();

    public void ValidSourceExtensionsShouldContainDllExtensions()
        => _testSource.ValidSourceExtensions.ToList().Should().Contain(".dll");

    public void ValidSourceExtensionsShouldContainExeExtensions()
        => _testSource.ValidSourceExtensions.ToList().Should().Contain(".exe");

    public void IsAssemblyReferencedShouldReturnTrueIfSourceOrAssemblyNameIsNull()
    {
        _testSource.IsAssemblyReferenced(null!, null!).Should().BeTrue();
        _testSource.IsAssemblyReferenced(null!, string.Empty).Should().BeTrue();
        _testSource.IsAssemblyReferenced(new AssemblyName(), null!).Should().BeTrue();
    }

    public void IsAssemblyReferencedShouldReturnTrueForAllSourceOrAssemblyNames()
    {
#if !NETFRAMEWORK
#pragma warning disable IDE0022 // Use expression body for method
        _testSource.IsAssemblyReferenced(new AssemblyName("ReferenceAssembly"), "SourceAssembly").Should().BeTrue();
#pragma warning restore IDE0022 // Use expression body for method
#endif
    }
}

#pragma warning restore SA1649 // SA1649FileNameMustMatchTypeName

