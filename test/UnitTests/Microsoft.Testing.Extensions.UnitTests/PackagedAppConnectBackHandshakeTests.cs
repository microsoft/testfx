// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// The PackagedApp extension only targets .NET (net8.0/net9.0), so these tests are compiled only there.
#if !NETFRAMEWORK

using Microsoft.Testing.Extensions.PackagedApp;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class PackagedAppConnectBackHandshakeTests
{
    [TestMethod]
    public void WriteThenReadAndDelete_RoundTripsEntriesAndDeletesFile()
    {
        string filePath = Path.Combine(Path.GetTempPath(), "PackagedAppHandshakeTests", Guid.NewGuid().ToString("N"), "test.handshake");
        try
        {
            var entries = new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                // A typical connect-back value, a value with characters that must survive encoding, an
                // empty string, and a null must all round-trip exactly.
                ["TESTINGPLATFORM_TESTHOSTCONTROLLER_PIPENAME_1234"] = "MONITORTOHOST_deadbeef",
                ["WITH_SPECIAL_CHARS"] = "a=b;c\\d\r\ne\tf\u00e9",
                ["EMPTY_VALUE"] = string.Empty,
                ["NULL_VALUE"] = null,
            };

            PackagedAppConnectBackHandshake.Write(filePath, entries);

            IReadOnlyDictionary<string, string?>? read = PackagedAppConnectBackHandshake.ReadAndDelete(filePath);

            Assert.IsNotNull(read);
            Assert.HasCount(entries.Count, read);
            Assert.AreEqual("MONITORTOHOST_deadbeef", read["TESTINGPLATFORM_TESTHOSTCONTROLLER_PIPENAME_1234"]);
            Assert.AreEqual("a=b;c\\d\r\ne\tf\u00e9", read["WITH_SPECIAL_CHARS"]);
            Assert.AreEqual(string.Empty, read["EMPTY_VALUE"]);
            Assert.IsNull(read["NULL_VALUE"]);

            // The activated host consumes the hand-off exactly once.
            Assert.IsFalse(File.Exists(filePath));
        }
        finally
        {
            string? directory = Path.GetDirectoryName(filePath);
            if (directory is not null && Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [TestMethod]
    public void ReadAndDelete_ReturnsNull_WhenFileDoesNotExist()
    {
        string filePath = Path.Combine(Path.GetTempPath(), "PackagedAppHandshakeTests", Guid.NewGuid().ToString("N"), "missing.handshake");

        Assert.IsNull(PackagedAppConnectBackHandshake.ReadAndDelete(filePath));
    }

    [TestMethod]
    public void TryDelete_RemovesExistingFileAndIgnoresMissingFile()
    {
        string filePath = Path.Combine(Path.GetTempPath(), "PackagedAppHandshakeTests", Guid.NewGuid().ToString("N"), "cleanup.handshake");
        PackagedAppConnectBackHandshake.Write(filePath, new Dictionary<string, string?>(StringComparer.Ordinal) { ["K"] = "V" });
        try
        {
            Assert.IsTrue(File.Exists(filePath));

            PackagedAppConnectBackHandshake.TryDelete(filePath);
            Assert.IsFalse(File.Exists(filePath));

            // Deleting an already-missing file must be a no-op, not throw.
            PackagedAppConnectBackHandshake.TryDelete(filePath);
        }
        finally
        {
            string? directory = Path.GetDirectoryName(filePath);
            if (directory is not null && Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [TestMethod]
    public void GetHandshakeFilePath_IsUnderPackageLocalStateAndIncludesControllerPid()
    {
        string path = PackagedAppConnectBackHandshake.GetHandshakeFilePath("Contoso.MyTestApp_8wekyb3d8bbwe", "4321");

        // Both sides must derive the same, package-scoped path from the package family name and PID.
        Assert.Contains(Path.Combine("Packages", "Contoso.MyTestApp_8wekyb3d8bbwe", "LocalState"), path);
        Assert.Contains("4321", path);
        Assert.EndsWith(".handshake", path);
    }

    [TestMethod]
    public void TryGetTestHostControllerPid_ReturnsValue_WhenOptionPresent()
    {
        string[] arguments = ["--other", "value", "--internal-testhostcontroller-pid", "9876", "--more"];

        Assert.AreEqual("9876", PackagedAppConnectBackHandshake.TryGetTestHostControllerPid(arguments));
    }

    [TestMethod]
    public void TryGetTestHostControllerPid_ReturnsNull_WhenOptionAbsent()
    {
        string[] arguments = ["--diagnostic", "--results-directory", "out"];

        Assert.IsNull(PackagedAppConnectBackHandshake.TryGetTestHostControllerPid(arguments));
    }

    [TestMethod]
    public void TryGetTestHostControllerPid_ReturnsNull_WhenOptionIsLastWithoutValue()
    {
        // A trailing option with no following value must not throw or return a bogus PID.
        string[] arguments = ["--internal-testhostcontroller-pid"];

        Assert.IsNull(PackagedAppConnectBackHandshake.TryGetTestHostControllerPid(arguments));
    }
}

#endif
