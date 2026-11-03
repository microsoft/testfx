// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestClass]
public sealed class AcceptanceTestBaseTests
{
    [TestMethod]
    public void PackageVersionsAreResolvedOnlyWhenUsed()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"acceptance-package-versions-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        try
        {
            File.WriteAllBytes(Path.Combine(directory, "Required.Package.1.2.3.nupkg"), []);
            Lazy<string> requiredVersion = AcceptanceTestBase.CreatePackageVersion(directory, "Required.Package.");
            Lazy<string> absentVersion = AcceptanceTestBase.CreatePackageVersion(directory, "Absent.Package.");

            Assert.AreEqual("1.2.3", requiredVersion.Value);
            Assert.IsFalse(absentVersion.IsValueCreated);

            InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(() => _ = absentVersion.Value);
            Assert.Contains("but found 0", exception.Message);
            Assert.Contains("Absent.Package.", exception.Message);

            File.WriteAllBytes(Path.Combine(directory, "Required.Package.2.0.0.nupkg"), []);
            Lazy<string> duplicateVersion = AcceptanceTestBase.CreatePackageVersion(directory, "Required.Package.");
            exception = Assert.ThrowsExactly<InvalidOperationException>(() => _ = duplicateVersion.Value);
            Assert.Contains("but found 2", exception.Message);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
