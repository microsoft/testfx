// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
public sealed class WindowsApplicationModelCleanupTests
{
    [TestMethod]
    [DataRow(false, false)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    [DataRow(true, true)]
    public async Task ExecuteWithPackageCleanupAsync_PreservesLayoutAndFailuresUntilRemovalIsVerified(bool testFails, bool cleanupFails)
    {
        TrackingTestAsset asset = new();
        try
        {
            string marker = Path.Combine(asset.TargetAssetPath, "diagnostic.txt");
            await File.WriteAllTextAsync(marker, "retained diagnostic");
            InvalidOperationException testFailure = new("test failed");
            InvalidOperationException cleanupFailure = new("removal failed");
            bool cleanupCalled = false;

            Task TestAction() => testFails ? Task.FromException(testFailure) : Task.CompletedTask;
            Task RemoveAndVerifyPackage()
            {
                cleanupCalled = true;
                Assert.IsFalse(asset.WasDisposed);
                Assert.IsTrue(File.Exists(marker));
                return cleanupFails ? Task.FromException(cleanupFailure) : Task.CompletedTask;
            }

            Task Execute() => WindowsApplicationModelTestTools.ExecuteWithPackageCleanupAsync(
                asset, "simulated-package", TestAction, RemoveAndVerifyPackage);
            if (cleanupFails)
            {
                AggregateException exception = await Assert.ThrowsExactlyAsync<AggregateException>(Execute);
                Assert.AreSequenceEqual(
                    testFails ? new Exception[] { testFailure, cleanupFailure } : [cleanupFailure],
                    exception.InnerExceptions);
                Assert.Contains("package layout was retained", exception.Message);
            }
            else if (testFails)
            {
                InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(Execute);
                Assert.AreSame(testFailure, exception);
            }
            else
            {
                await Execute();
            }

            Assert.IsTrue(cleanupCalled);
            Assert.AreEqual(!testFails && !cleanupFails, asset.WasDisposed);
            if (!asset.WasDisposed)
            {
                Assert.AreEqual("retained diagnostic", await File.ReadAllTextAsync(marker));
            }
        }
        finally
        {
            asset.Dispose();
        }
    }

    private sealed class TrackingTestAsset() : TestAsset($"Cleanup-{Guid.NewGuid():N}", string.Empty)
    {
        public bool WasDisposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            WasDisposed |= disposing;
            base.Dispose(disposing);
        }
    }
}
