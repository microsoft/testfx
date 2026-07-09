// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK
using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests;

public class DesktopTestSourceTests : TestContainer
{
    private readonly TestSourceHandler _testSource;

    public DesktopTestSourceTests() => _testSource = new TestSourceHandler();

    public void ValidSourceExtensionsShouldContainDllExtensions() => _testSource.ValidSourceExtensions.Contains(".dll").Should().BeTrue();

    public void ValidSourceExtensionsShouldContainExeExtensions() => _testSource.ValidSourceExtensions.ToList().Contains(".exe").Should().BeTrue();

    public void ValidSourceExtensionsShouldContainAppxExtensions() => _testSource.ValidSourceExtensions.Contains(".appx").Should().BeTrue();

    public void IsAssemblyReferencedShouldReturnTrueIfAssemblyNameIsNull() => _testSource.IsAssemblyReferenced(null!, "DummySource").Should().BeTrue();

    public void IsAssemblyReferencedShouldReturnTrueIfSourceIsNull() => _testSource.IsAssemblyReferenced(Assembly.GetExecutingAssembly().GetName(), null!).Should().BeTrue();

    public void IsAssemblyReferencedShouldReturnTrueIfAnAssemblyIsReferencedInSource() => _testSource.IsAssemblyReferenced(typeof(Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute).Assembly.GetName(), Assembly.GetExecutingAssembly().Location).Should().BeTrue();

    public void IsAssemblyReferencedShouldReturnFalseIfAnAssemblyIsNotReferencedInSource() => _testSource.IsAssemblyReferenced(new AssemblyName("foobar"), Assembly.GetExecutingAssembly().Location).Should().BeFalse();

    // The source references the strong-named MSTest.TestFramework assembly. Passing an assembly name with the
    // same simple name but no public key token (as an unsigned build would have) must not be considered a match:
    // the public key tokens differ (signed vs. unsigned), so IsAssemblyReferenced returns false. This guards the
    // signed-vs-unsigned branch of the public-key-token comparison.
    public void IsAssemblyReferencedShouldReturnFalseIfPublicKeyTokenIsMissing() => _testSource.IsAssemblyReferenced(new AssemblyName("MSTest.TestFramework"), Assembly.GetExecutingAssembly().Location).Should().BeFalse();

    // Same simple name but a different (non-null) public key token must also not match: the tokens are compared
    // byte-by-byte and differ. This guards the differing-token branch of the public-key-token comparison.
    public void IsAssemblyReferencedShouldReturnFalseIfPublicKeyTokenDiffers()
    {
        var assemblyName = new AssemblyName("MSTest.TestFramework");
        assemblyName.SetPublicKeyToken([0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08]);
        _testSource.IsAssemblyReferenced(assemblyName, Assembly.GetExecutingAssembly().Location).Should().BeFalse();
    }
}
#endif
