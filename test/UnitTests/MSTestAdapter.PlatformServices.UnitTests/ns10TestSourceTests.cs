﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.Tests.Services;

public class TestSourceTests : TestContainer
{
    private readonly TestSource _testSource;

    public TestSourceTests() => _testSource = new TestSource();

    public void ValidSourceExtensionsShouldContainDllExtensions()
        => Verify(_testSource.ValidSourceExtensions.ToList().Contains(".dll"));

    public void ValidSourceExtensionsShouldContainExeExtensions()
        => Verify(_testSource.ValidSourceExtensions.ToList().Contains(".exe"));

    public void IsAssemblyReferencedShouldReturnTrueIfSourceOrAssemblyNameIsNull()
    {
        Verify(_testSource.IsAssemblyReferenced(null!, null!));
        Verify(_testSource.IsAssemblyReferenced(null!, string.Empty));
        Verify(_testSource.IsAssemblyReferenced(new AssemblyName(), null!));
    }

    public void IsAssemblyReferencedShouldReturnTrueForAllSourceOrAssemblyNames()
    {
#if !NET462
#pragma warning disable IDE0022 // Use expression body for method
        Verify(_testSource.IsAssemblyReferenced(new AssemblyName("ReferenceAssembly"), "SourceAssembly"));
#pragma warning restore IDE0022 // Use expression body for method
#endif
    }
}

#pragma warning restore SA1649 // SA1649FileNameMustMatchTypeName

