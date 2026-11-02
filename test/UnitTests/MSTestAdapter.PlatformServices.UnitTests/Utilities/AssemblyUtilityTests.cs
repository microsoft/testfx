// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Utilities;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Utilities;

public sealed class AssemblyUtilityTests : TestContainer
{
    public void IsAssemblyExtensionRequiresLeadingDotAndIgnoresCase()
    {
        AssemblyUtility utility = new();
        foreach (string extension in new[] { ".dll", ".DLL", ".exe", ".ExE" })
        {
            utility.IsAssemblyExtension(extension).Should().BeTrue(extension);
        }

        foreach (string extension in new[] { string.Empty, "dll", "exe", ".dll.config", ".resources", ".dll " })
        {
            utility.IsAssemblyExtension(extension).Should().BeFalse(extension);
        }
    }

#if NETFRAMEWORK
    public void GetFullPathToDependentAssembliesRetainsBadImageWarningAcrossIsolatedDomain()
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string image = Path.Combine(root, "not-an-assembly.dll");
            File.WriteAllText(image, "not a PE image");
            IReadOnlyList<string> dependencies = new AssemblyUtility().GetFullPathToDependentAssemblies(image, null, out IList<string> warnings);

            dependencies.Should().BeEmpty();
            warnings.Should().ContainSingle();
            warnings[0].Should().Contain(image).And.Contain(nameof(BadImageFormatException));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    public void IsAssemblyDistinguishesManagedImagesInvalidImagesAndMissingFiles()
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string image = Path.Combine(root, "not-an-assembly.dll");
            File.WriteAllText(image, "not a PE image");
            AssemblyUtility.IsAssembly(typeof(AssemblyUtility).Assembly.Location).Should().BeTrue();
            AssemblyUtility.IsAssembly(image).Should().BeFalse();
            Action missing = () => AssemblyUtility.IsAssembly(Path.Combine(root, "missing.dll"));
            missing.Should().Throw<FileNotFoundException>();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    public void GetSatelliteAssembliesReturnsBothManagedExtensionsAndRejectsInvalidCultureAndImage()
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string assembly = Path.Combine(root, "sample.dll");
            File.Copy(typeof(AssemblyUtility).Assembly.Location, assembly);
            string english = Path.Combine(root, "en");
            string french = Path.Combine(root, "fr");
            string invalidCulture = Path.Combine(root, "not-a-culture");
            Directory.CreateDirectory(english);
            Directory.CreateDirectory(french);
            Directory.CreateDirectory(invalidCulture);
            string englishDll = Path.Combine(english, "sample.resources.dll");
            string englishExe = Path.Combine(english, "sample.resources.exe");
            File.Copy(assembly, englishDll);
            File.Copy(assembly, englishExe);
            File.Copy(assembly, Path.Combine(invalidCulture, "sample.resources.dll"));
            File.WriteAllText(Path.Combine(french, "sample.resources.dll"), "not a PE image");

            new AssemblyUtility().GetSatelliteAssemblies(assembly).Should().BeEquivalentTo([englishDll, englishExe]);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    public void GetSatelliteAssembliesReturnsEmptyWhenNoCultureDirectoriesExist()
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string assembly = Path.Combine(root, "sample.exe");
            File.Copy(typeof(AssemblyUtility).Assembly.Location, assembly);
            new AssemblyUtility().GetSatelliteAssemblies(assembly).Should().BeEmpty();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    public void GetResolutionPathsContainsCanonicalExecutingAssemblyAndDistinctApplicationBase()
    {
        string executingDirectory = Path.GetDirectoryName(new Uri(typeof(AssemblyUtility).Assembly.CodeBase).LocalPath)!;
        IList<string> paths = AssemblyUtility.GetResolutionPaths();
        paths.Should().Contain(executingDirectory);
        paths.Should().OnlyHaveUniqueItems();
        paths.Should().HaveCount(string.Equals(executingDirectory, AppDomain.CurrentDomain.BaseDirectory, StringComparison.OrdinalIgnoreCase) ? 1 : 2);
        if (paths.Count == 2)
        {
            paths[1].Should().Be(AppDomain.CurrentDomain.BaseDirectory);
        }
    }
#endif
}
