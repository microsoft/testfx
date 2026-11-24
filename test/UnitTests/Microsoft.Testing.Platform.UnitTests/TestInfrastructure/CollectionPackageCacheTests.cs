// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETCOREAPP
using Microsoft.Testing.Platform.Acceptance.IntegrationTests;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class CollectionPackageCacheTests
{
    [TestMethod]
    public void NormalAcceptanceKeepsFreshPackageCache()
    {
        string fresh = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Assert.AreEqual(Path.Combine(fresh, ".packages"), CollectionPackageCache.Select(null, fresh));
    }

    [TestMethod]
    public void CollectionSharesOnlyExplicitMarkedRunCache()
    {
        string cache = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(cache);
        try
        {
            File.WriteAllText(Path.Combine(cache, CollectionPackageCache.MarkerFileName), CollectionPackageCache.MarkerContent);
            Assert.AreEqual(cache, CollectionPackageCache.Select(cache, "unused"));
            Assert.AreEqual(cache, CollectionPackageCache.Select(cache, "different-child"));
            Assert.DirectoryExists(cache);
        }
        finally
        {
            Directory.Delete(cache, recursive: true);
        }
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(" ")]
    [DataRow("relative-cache")]
    public void InvalidCollectionCachePathIsRejected(string cache)
        => Assert.ThrowsExactly<InvalidOperationException>(() => CollectionPackageCache.Select(cache, "unused"));

    [TestMethod]
    public void UnmarkedOrIncompatibleCacheIsRejected()
    {
        string cache = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(cache);
        try
        {
            Assert.ThrowsExactly<InvalidOperationException>(() => CollectionPackageCache.Select(cache, "unused"));
            File.WriteAllText(Path.Combine(cache, CollectionPackageCache.MarkerFileName), "different-protocol");
            Assert.ThrowsExactly<InvalidOperationException>(() => CollectionPackageCache.Select(cache, "unused"));
        }
        finally
        {
            Directory.Delete(cache, recursive: true);
        }
    }
}
#endif
