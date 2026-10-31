// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK
using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Utilities;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Utilities;

public sealed class VSInstallationContinuationTests : TestContainer
{
    public void PortableModeTracksOnlyTheSiblingManifest()
    {
        string root = Path.Combine(Path.GetTempPath(), "testfx-ad03-portable-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string executable = Path.Combine(root, "runner.exe");
            string manifest = Path.Combine(root, "Portable.VsTest.Manifest");
            string otherDirectory = Directory.CreateDirectory(Path.Combine(root, "other")).FullName;

            VSInstallationUtilities.IsProcessRunningInPortableMode(executable).Should().BeFalse();
            File.WriteAllText(manifest, "portable marker");
            VSInstallationUtilities.IsProcessRunningInPortableMode(executable).Should().BeTrue("the marker is sufficient even before the executable is present");
            VSInstallationUtilities.IsProcessRunningInPortableMode(Path.Combine(otherDirectory, "runner.exe")).Should().BeFalse();
            File.WriteAllText(executable, "an executable is not itself a portable marker");
            File.Delete(manifest);
            VSInstallationUtilities.IsProcessRunningInPortableMode(executable).Should().BeFalse();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    public void NullAndBareExecutableNamesAreNotPortable()
    {
        VSInstallationUtilities.IsProcessRunningInPortableMode(null).Should().BeFalse();
        VSInstallationUtilities.IsProcessRunningInPortableMode("runner.exe").Should().BeFalse();
    }
}
#endif
