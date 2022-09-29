// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.UWP.UnitTests;

using System.Linq;
using System.Reflection;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using TestFramework.ForTestingMSTest;

/// <summary>
/// The universal test source validator tests.
/// </summary>
public class UniversalTestSourceTests : TestContainer
{
    private readonly TestSource _testSource;

    /// <summary>
    /// The test initialization.
    /// </summary>
    public UniversalTestSourceTests()
    {
        _testSource = new TestSource();
    }

    /// <summary>
    /// The valid source extensions should contain .dll as extension.
    /// </summary>
    public void ValidSourceExtensionsShouldContainDllExtensions()
    {
        Verify(_testSource.ValidSourceExtensions.Contains(".dll"));
    }

    /// <summary>
    /// The valid source extensions should contain .exe as extension.
    /// </summary>
    public void ValidSourceExtensionsShouldContainExeExtensions()
    {
        Verify(_testSource.ValidSourceExtensions.Contains(".exe"));
    }

    /// <summary>
    /// The valid source extensions should contain .appx as extension.
    /// </summary>
    public void ValidSourceExtensionsShouldContainAppxExtensions()
    {
        Verify(_testSource.ValidSourceExtensions.Contains(".appx"));
    }

    /// <summary>
    /// The is assembly referenced should return true if assembly name is null.
    /// </summary>
    public void IsAssemblyReferencedShouldReturnTrueIfAssemblyNameIsNull()
    {
        Verify(_testSource.IsAssemblyReferenced(null, "DummySource"));
    }

    /// <summary>
    /// The is assembly referenced should return true if source is null.
    /// </summary>
    public void IsAssemblyReferencedShouldReturnTrueIfSourceIsNull()
    {
        Verify(_testSource.IsAssemblyReferenced(Assembly.GetExecutingAssembly().GetName(), null));
    }

    /// <summary>
    /// The is assembly referenced should return true if an assembly is referenced in source.
    /// </summary>
    public void IsAssemblyReferencedShouldReturnTrueIfAnAssemblyIsReferencedInSource()
    {
        Verify(_testSource.IsAssemblyReferenced(typeof(TestMethodAttribute).Assembly.GetName(), Assembly.GetExecutingAssembly().Location));
    }

    /// <summary>
    /// The is assembly referenced should return false if an assembly is not referenced in source.
    /// </summary>
    public void IsAssemblyReferencedShouldReturnTrueForAllSourceOrAssemblyNames()
    {
        Verify(_testSource.IsAssemblyReferenced(new AssemblyName("ReferenceAssembly"), "SourceAssembly"));
    }
}
