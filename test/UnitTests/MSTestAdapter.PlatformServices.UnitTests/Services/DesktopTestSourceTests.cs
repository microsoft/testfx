// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET462
using System.Reflection;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests;

public class DesktopTestSourceTests : TestContainer
{
    private readonly TestSource _testSource;

    public DesktopTestSourceTests()
    {
        _testSource = new TestSource();
    }

    public void ValidSourceExtensionsShouldContainDllExtensions() => Verify(_testSource.ValidSourceExtensions.Contains(".dll"));

    public void ValidSourceExtensionsShouldContainExeExtensions() => Verify(_testSource.ValidSourceExtensions.ToList().Contains(".exe"));

    public void ValidSourceExtensionsShouldContainAppxExtensions() => Verify(_testSource.ValidSourceExtensions.Contains(".appx"));

    public void IsAssemblyReferencedShouldReturnTrueIfAssemblyNameIsNull() => Verify(_testSource.IsAssemblyReferenced(null, "DummySource"));

    public void IsAssemblyReferencedShouldReturnTrueIfSourceIsNull() => Verify(_testSource.IsAssemblyReferenced(Assembly.GetExecutingAssembly().GetName(), null));

    public void IsAssemblyReferencedShouldReturnTrueIfAnAssemblyIsReferencedInSource() => Verify(_testSource.IsAssemblyReferenced(typeof(Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute).Assembly.GetName(), Assembly.GetExecutingAssembly().Location));

    public void IsAssemblyReferencedShouldReturnFalseIfAnAssemblyIsNotReferencedInSource() => Verify(!_testSource.IsAssemblyReferenced(new AssemblyName("foobar"), Assembly.GetExecutingAssembly().Location));
}
#endif
