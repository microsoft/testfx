// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestClass]
public sealed class BrowserPackageRestoreIdentityTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void AssertMatches_RejectsCachedPackageWithDifferentContent()
    {
        string root = Path.Combine(
            TestContext.TestRunResultsDirectory ?? Path.GetTempPath(),
            $"browser-package-identity-{Guid.NewGuid():N}");
        string feed = Path.Combine(root, "feed");
        Directory.CreateDirectory(feed);
        File.WriteAllText(
            Path.Combine(feed, "Example.Package.1.0.0.nupkg"),
            "source-matched package");
        string assets = Path.Combine(root, "project.assets.json");
        File.WriteAllText(
            assets,
            """
            {
              "libraries": {
                "Example.Package/1.0.0": {
                  "sha512": "sha512-cached-content"
                }
              }
            }
            """);

        Assert.ThrowsExactly<AssertFailedException>(
            () => BrowserPackageRestoreIdentity.AssertMatches(
                assets,
                feed,
                ["Example.Package"]));
    }
}
