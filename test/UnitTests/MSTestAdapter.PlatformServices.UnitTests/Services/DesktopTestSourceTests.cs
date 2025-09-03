// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET462
using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests;

public class DesktopTestSourceTests : TestContainer
{
    private readonly TestSource _testSource;

    public DesktopTestSourceTests() => _testSource = new TestSource();

    public void ValidSourceExtensionsShouldContainDllExtensions() => _testSource.ValidSourceExtensions.Contains(".dll").Should().BeTrue();

    public void ValidSourceExtensionsShouldContainExeExtensions() => _testSource.ValidSourceExtensions.ToList().Contains(".exe").Should().BeTrue();

    public void ValidSourceExtensionsShouldContainAppxExtensions() => _testSource.ValidSourceExtensions.Contains(".appx").Should().BeTrue();

    public void IsAssemblyReferencedShouldReturnTrueIfAssemblyNameIsNull() => _testSource.IsAssemblyReferenced(null!, "DummySource").Should().BeTrue();

    public void IsAssemblyReferencedShouldReturnTrueIfSourceIsNull() => _testSource.IsAssemblyReferenced(Assembly.GetExecutingAssembly().GetName(), null!).Should().BeTrue();

    public void IsAssemblyReferencedShouldReturnTrueIfAnAssemblyIsReferencedInSource() => _testSource.IsAssemblyReferenced(typeof(Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute).Assembly.GetName(), Assembly.GetExecutingAssembly().Location).Should().BeTrue();

    public void IsAssemblyReferencedShouldReturnFalseIfAnAssemblyIsNotReferencedInSource() => _testSource.IsAssemblyReferenced(new AssemblyName("foobar"), Assembly.GetExecutingAssembly().Location).Should().BeFalse();
}
#endif
