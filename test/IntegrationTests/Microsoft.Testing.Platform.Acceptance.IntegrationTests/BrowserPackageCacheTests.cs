// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestClass]
public sealed class BrowserPackageCacheTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Create_UsesExternalShortPathAndDisposeRemovesIt()
    {
        string longProjectPath = Path.Combine(
            TestContext.TestRunResultsDirectory ?? Path.GetTempPath(),
            new string('p', 80),
            new string('q', 80));
        Directory.CreateDirectory(longProjectPath);

        string cachePath;
        using (var cache = BrowserPackageCache.Create(longProjectPath))
        {
            cachePath = cache.Path;
            Assert.IsTrue(Directory.Exists(cachePath));
            Assert.IsFalse(
                cachePath.StartsWith(
                    longProjectPath,
                    OperatingSystem.IsWindows()
                        ? StringComparison.OrdinalIgnoreCase
                        : StringComparison.Ordinal));
            Assert.IsLessThan(120, cachePath.Length);
        }

        Assert.IsFalse(Directory.Exists(cachePath));
    }
}
