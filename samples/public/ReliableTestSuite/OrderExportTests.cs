// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ReliableTestSuite;

/// <summary>
/// STEP 1 - ELIMINATE BEFORE YOU LOCK.
///
/// These tests all write files, yet they need no [ResourceLock] and no [DoNotParallelize].
/// The reason: each test writes into its OWN unique directory, created in [TestInitialize] and
/// removed in [TestCleanup]. There is no shared resource to contend on, so the scheduler is free
/// to run every method concurrently. The cheapest concurrency bug is the one that cannot exist
/// because nothing is shared.
///
/// (On MSTest 4.4+, TestContext.TestTempDirectory gives you a per-test folder the platform
/// manages for you, removing even this bookkeeping.)
/// </summary>
[TestClass]
public sealed class OrderExportTests
{
    private string _tempDir = null!;

    [TestInitialize]
    public void CreateUniqueDirectory()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ReliableSuite", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    [TestCleanup]
    public void RemoveUniqueDirectory()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [TestMethod]
    public void ExportsSingleOrder()
    {
        string path = Path.Combine(_tempDir, "orders.csv");

        OrderExporter.ExportCsv([new Order(1, "Contoso", 42.00m)], path);

        string[] lines = File.ReadAllLines(path);
        Assert.HasCount(2, lines);
        Assert.AreEqual("Id,Customer,Total", lines[0]);
        // Assert the WHOLE row, not just a substring: a culture-dependent decimal (42,00) would
        // change this shape and must fail the test rather than slip through.
        Assert.AreEqual("1,Contoso,42.00", lines[1]);
    }

    [TestMethod]
    public void ExportsMultipleOrders()
    {
        string path = Path.Combine(_tempDir, "orders.csv");

        OrderExporter.ExportCsv(
            [new Order(1, "Contoso", 10m), new Order(2, "Fabrikam", 20m)],
            path);

        string[] lines = File.ReadAllLines(path);
        Assert.HasCount(3, lines);
    }

    [TestMethod]
    public void ExportsEmptySequence()
    {
        string path = Path.Combine(_tempDir, "orders.csv");

        OrderExporter.ExportCsv([], path);

        string[] lines = File.ReadAllLines(path);
        Assert.HasCount(1, lines);
    }
}
