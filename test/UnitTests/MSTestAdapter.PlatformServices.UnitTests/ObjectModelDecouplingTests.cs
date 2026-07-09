// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests;

public class ObjectModelDecouplingTests : TestContainer
{
    // Guards the platform-agnostic contract: the compiled MSTestAdapter.PlatformServices assembly must not
    // reference the VSTest object model (Microsoft.VisualStudio.TestPlatform.ObjectModel) or any other
    // Microsoft.*.TestPlatform.* assembly. All VSTest coupling lives in the MSTest.TestAdapter layer above it.
    public void PlatformServicesAssemblyShouldNotReferenceAnyTestPlatformAssembly()
    {
        Assembly platformServices = typeof(TestSourceHandler).Assembly;

        IEnumerable<string> testPlatformReferences = platformServices
            .GetReferencedAssemblies()
            .Select(reference => reference.Name!)
            .Where(name => name.IndexOf("TestPlatform", StringComparison.OrdinalIgnoreCase) >= 0);

        testPlatformReferences.Should().BeEmpty(
            "PlatformServices must stay platform-agnostic and must not reference the VSTest object model or any Microsoft.*.TestPlatform.* assembly");
    }
}
