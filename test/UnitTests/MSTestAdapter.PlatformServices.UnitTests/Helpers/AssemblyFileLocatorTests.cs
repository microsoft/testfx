// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Helpers;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Helpers;

public sealed class AssemblyFileLocatorTests : TestContainer
{
    public void FileIdentityUsesLocationAndSingleFileFallbacks()
    {
        var assembly = new LocatedAssembly();
        string directory = Path.Combine(Path.GetTempPath(), "coverage-fixtures");
        string location = Path.Combine(directory, "product.dll");
        assembly.FileLocation = location;

        AssemblyFileLocator.TryGetLocation(assembly).Should().Be(location);
        AssemblyFileLocator.GetDirectoryOrAppContextBase(assembly).Should().Be(directory);
        AssemblyFileLocator.GetFileNameOrSimpleName(assembly).Should().Be("product.dll");

        assembly.FileLocation = "relative.dll";
        AssemblyFileLocator.GetDirectoryOrAppContextBase(assembly).Should().Be(AppContext.BaseDirectory);
        AssemblyFileLocator.GetFileNameOrSimpleName(assembly).Should().Be("relative.dll");

        assembly.FileLocation = string.Empty;
        assembly.SimpleName = "Embedded.Product";
        AssemblyFileLocator.TryGetLocation(assembly).Should().BeNull();
        AssemblyFileLocator.GetDirectoryOrAppContextBase(assembly).Should().Be(AppContext.BaseDirectory);
        AssemblyFileLocator.GetFileNameOrSimpleName(assembly).Should().Be("Embedded.Product.dll");

        assembly.SimpleName = null;
        AssemblyFileLocator.GetFileNameOrSimpleName(assembly).Should().BeEmpty();
    }

    private sealed class LocatedAssembly : Assembly
    {
        internal string FileLocation { get; set; } = string.Empty;

        internal string? SimpleName { get; set; }

        public override string Location => FileLocation;

        public override AssemblyName GetName() => SimpleName is null ? new AssemblyName() : new AssemblyName(SimpleName);
    }
}
