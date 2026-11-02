// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// The PackagedApp extension only targets .NET (net8.0/net9.0), so these tests are compiled only there.
#if !NETFRAMEWORK

using Microsoft.Testing.Extensions;
using Microsoft.Testing.Extensions.PackagedApp;
using Microsoft.Testing.Extensions.PackagedApp.Resources;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class PackagedAppActivationArgumentsTests
{
    [TestMethod]
    public void Create_WithNullArguments_Throws()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            _ = PackagedAppActivationArguments.Create([], directory);

            Assert.ThrowsExactly<ArgumentNullException>(
                () => PackagedAppActivationArguments.Create(null!, directory));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void Create_WithNullLocalStateDirectory_Throws()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            _ = PackagedAppActivationArguments.Create([], directory);

            Assert.ThrowsExactly<ArgumentNullException>(
                () => PackagedAppActivationArguments.Create([], null!));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void Create_WithEmptyLocalStateDirectory_Throws()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            _ = PackagedAppActivationArguments.Create([], directory);

            Assert.ThrowsExactly<ArgumentException>(
                () => PackagedAppActivationArguments.Create([], string.Empty));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void Create_WithNullArgument_Throws()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            _ = PackagedAppActivationArguments.Create(["valid"], directory);

            Assert.ThrowsExactly<ArgumentNullException>(
                () => PackagedAppActivationArguments.Create([null!], directory));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void CreateThenRead_InlinePayload_RoundTripsEveryArgumentShape()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            string[] expected =
            [
                "--filter",
                string.Empty,
                " ",
                "two words",
                "\"quoted\"",
                @"trailing\",
                @"before\\\""quote",
                "equals=value:semicolon;colon:",
                "こんにちは 🌍",
                "--filter",
                "second",
            ];

            PackagedAppActivationData activation = PackagedAppActivationArguments.Create(expected, directory);
            string[] actual = PackagedAppActivationArguments.Read(activation.Arguments, directory);

            Assert.IsNull(activation.PayloadPath);
            Assert.IsLessThanOrEqualTo(2048, activation.Arguments.Length);
            AssertArgumentsAreEqual(expected, actual);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void Create_AtInlineBoundary_UsesInlineThenEncryptedPayload()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            PackagedAppActivationData inline = PackagedAppActivationArguments.Create([new string('a', 758)], directory);
            PackagedAppActivationData encrypted = PackagedAppActivationArguments.Create([new string('a', 759)], directory);

            Assert.IsNull(inline.PayloadPath);
            Assert.HasCount(2046, inline.Arguments);
            Assert.IsNotNull(encrypted.PayloadPath);
            Assert.StartsWith("mtp:v1:file:", encrypted.Arguments);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void Create_LargePayload_CreatesMissingLocalStateDirectory()
    {
        string root = CreateTemporaryDirectory();
        string localStateDirectory = Path.Combine(root, "missing", "local-state");
        try
        {
            PackagedAppActivationData activation =
                PackagedAppActivationArguments.Create([new string('x', 3000)], localStateDirectory);

            Assert.IsTrue(Directory.Exists(localStateDirectory));
            Assert.IsNotNull(activation.PayloadPath);
            Assert.IsTrue(File.Exists(activation.PayloadPath));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [DataRow(0)]
    [DataRow(1)]
    [TestMethod]
    public void CreateThenRead_EmptyArguments_RoundTrips(int argumentCount)
    {
        string[] expected = argumentCount == 0 ? [] : [string.Empty];

        PackagedAppActivationData activation =
            PackagedAppActivationArguments.Create(expected, Path.GetTempPath());
        string[] actual = PackagedAppActivationArguments.Read(activation.Arguments, localStateDirectory: null);

        AssertArgumentsAreEqual(expected, actual);
    }

    [TestMethod]
    public void CreateThenRead_LargePayload_UsesEncryptedOneShotFile()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            string secret = "secret-runsettings-" + new string('x', 3000);
            string[] expected = ["--settings", secret, "--filter", "Name=Sensitive"];

            PackagedAppActivationData activation = PackagedAppActivationArguments.Create(expected, directory);

            Assert.IsNotNull(activation.PayloadPath);
            Assert.IsTrue(File.Exists(activation.PayloadPath));
            byte[] encryptedPayload = File.ReadAllBytes(activation.PayloadPath);
            byte[] plaintextSecret = Encoding.Unicode.GetBytes(secret);
            Assert.AreEqual(-1, encryptedPayload.AsSpan().IndexOf(plaintextSecret), "The LocalState payload must not contain the plaintext secret.");

            string[] actual = PackagedAppActivationArguments.Read(activation.Arguments, directory);

            AssertArgumentsAreEqual(expected, actual);
            Assert.IsFalse(File.Exists(activation.PayloadPath), "The activated host must consume the encrypted payload exactly once.");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void Read_LargePayloadWithWrongKey_RejectsAndDeletesPayload()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            PackagedAppActivationData activation = PackagedAppActivationArguments.Create([new string('x', 3000)], directory);
            Assert.IsNotNull(activation.PayloadPath);

            int keySeparator = activation.Arguments.LastIndexOf(':');
            string wrongKey = Convert.ToBase64String(new byte[32]);
            string tamperedArguments = activation.Arguments[..(keySeparator + 1)] + wrongKey;

            Assert.ThrowsExactly<FormatException>(() => PackagedAppActivationArguments.Read(tamperedArguments, directory));
            Assert.IsFalse(File.Exists(activation.PayloadPath), "A rejected payload must not leave user data behind.");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void Read_RejectsMalformedOrForeignActivationArguments()
    {
        Assert.ThrowsExactly<FormatException>(() => PackagedAppActivationArguments.Read("not-an-mtp-activation", localStateDirectory: null));
        Assert.ThrowsExactly<FormatException>(() => PackagedAppActivationArguments.Read("mtp:v1:inline:not-base64!", localStateDirectory: null));
        Assert.ThrowsExactly<FormatException>(() => PackagedAppActivationArguments.Read("mtp:v1:file:not-a-token:not-a-key", Path.GetTempPath()));
    }

    [TestMethod]
    public void Read_RejectsMalformedInlineBinaryPayloads()
    {
        string[] malformedPayloads =
        [
            CreateInlinePayload([0, 0, 0]),
            CreateInlinePayload([255, 255, 255, 255]),
            CreateInlinePayload([1, 0, 0, 0]),
            CreateInlinePayload([1, 0, 0, 0, 255, 255, 255, 255]),
            CreateInlinePayload([0, 0, 0, 0, 1]),
        ];

        foreach (string payload in malformedPayloads)
        {
            Assert.ThrowsExactly<FormatException>(() => PackagedAppActivationArguments.Read(payload, localStateDirectory: null));
        }
    }

    [TestMethod]
    public void Read_ArgumentCountExceedingAvailableHeaders_ReportsInvalidCount()
        => AssertInvalidInlinePayload(
            [2, 0, 0, 0, 0, 0, 0, 0],
            ExtensionResources.ActivationArgumentsInvalidArgumentCount);

    [TestMethod]
    public void Read_TruncatedArgumentHeader_ReportsTruncatedPayload()
        => AssertInvalidInlinePayload(
            [2, 0, 0, 0, 1, 0, 0, 0, (byte)'a', 0, 0, 0, 0],
            ExtensionResources.ActivationArgumentsPayloadTruncated);

    [TestMethod]
    public void Read_ArgumentLengthExceedingRemainingPayload_ReportsInvalidLength()
        => AssertInvalidInlinePayload(
            [1, 0, 0, 0, 2, 0, 0, 0, (byte)'a', 0],
            ExtensionResources.ActivationArgumentsInvalidArgumentLength);

    [TestMethod]
    public void GetAssociatedData_BindsFilePrefixAndToken()
    {
        const string Token = "0123456789abcdef0123456789abcdef";
        byte[] associatedData = (byte[])typeof(PackagedAppActivationArguments)
            .GetMethod(
                "GetAssociatedData",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .Invoke(null, [Token])!;

        Assert.AreSequenceEqual(Encoding.ASCII.GetBytes($"mtp:v1:file:{Token}"), associatedData);
    }

    [TestMethod]
    public void Read_TruncatedEncryptedPayload_RejectsAndDeletesPayload()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            PackagedAppActivationData activation = PackagedAppActivationArguments.Create([new string('x', 3000)], directory);
            Assert.IsNotNull(activation.PayloadPath);
            File.WriteAllBytes(activation.PayloadPath, new byte[10]);

            Assert.ThrowsExactly<FormatException>(() => PackagedAppActivationArguments.Read(activation.Arguments, directory));
            Assert.IsFalse(File.Exists(activation.PayloadPath), "A truncated payload must be consumed exactly once.");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void Read_EncryptedPayload_CanOnlyBeConsumedOnce()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            string[] expected = [new string('x', 3000)];
            PackagedAppActivationData activation = PackagedAppActivationArguments.Create(expected, directory);

            string[] actual = PackagedAppActivationArguments.Read(activation.Arguments, directory);

            AssertArgumentsAreEqual(expected, actual);
            Assert.ThrowsExactly<FormatException>(() => PackagedAppActivationArguments.Read(activation.Arguments, directory));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void TryDeletePayload_NullIsNoOpAndExistingFileIsDeleted()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            string payloadPath = Path.Combine(directory, "payload.bin");
            File.WriteAllText(payloadPath, "payload");

            PackagedAppActivationArguments.TryDeletePayload(payloadPath: null);
            PackagedAppActivationArguments.TryDeletePayload(payloadPath);

            Assert.IsFalse(File.Exists(payloadPath));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void TryDeleteStalePayloads_RemovesOnlyExpiredActivationPayloads()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            DateTime utcNow = new(2026, 8, 7, 1, 0, 0, DateTimeKind.Utc);
            string stalePayload = Path.Combine(directory, "mtp-activation-stale.payload");
            string freshPayload = Path.Combine(directory, "mtp-activation-fresh.payload");
            string unrelatedFile = Path.Combine(directory, "unrelated.payload");
            File.WriteAllText(stalePayload, "stale");
            File.WriteAllText(freshPayload, "fresh");
            File.WriteAllText(unrelatedFile, "unrelated");
            File.SetLastWriteTimeUtc(stalePayload, utcNow - TimeSpan.FromDays(2));
            File.SetLastWriteTimeUtc(freshPayload, utcNow);
            File.SetLastWriteTimeUtc(unrelatedFile, utcNow - TimeSpan.FromDays(2));

            PackagedAppActivationArguments.TryDeleteStalePayloads(directory, utcNow);

            Assert.IsFalse(File.Exists(stalePayload));
            Assert.IsTrue(File.Exists(freshPayload));
            Assert.IsTrue(File.Exists(unrelatedFile));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void Create_InlinePayload_AlsoScavengesStaleSpillPayloads()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            string stalePayload = Path.Combine(directory, "mtp-activation-orphan.payload");
            File.WriteAllText(stalePayload, "stale");
            File.SetLastWriteTimeUtc(stalePayload, DateTime.UnixEpoch);

            PackagedAppActivationData activation = PackagedAppActivationArguments.Create(["--help"], directory);

            Assert.IsNull(activation.PayloadPath);
            Assert.IsFalse(File.Exists(stalePayload));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void GetTestApplicationArguments_ProvidesTheReusableOnLaunchedBootstrap()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            string[] expected = ["--help", "value with spaces", "\"quoted\"", string.Empty];
            PackagedAppActivationData activation = PackagedAppActivationArguments.Create(expected, directory);

            string[] actual = PackagedAppExtensions.GetTestApplicationArguments(activation.Arguments);

            AssertArgumentsAreEqual(expected, actual);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void ReadActivationArgumentsAndApplyConnectBack_PackageShapedHost_RestoresEnvironmentAndConsumesHandshake()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            File.WriteAllText(
                Path.Combine(directory, AppxManifestInfo.AppxManifestFileName),
                """
                <?xml version="1.0" encoding="utf-8"?>
                <Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10">
                  <Identity Name="Contoso.MyTestApp" Publisher="CN=Contoso" Version="1.0.0.0" />
                  <Applications>
                    <Application Id="App" Executable="MyTestApp.exe" EntryPoint="Contoso.MyTestApp.App" />
                  </Applications>
                </Package>
                """);

            const string ControllerPid = "4321";
            string handshakePath = Path.Combine(directory, PackagedAppConnectBackHandshake.GetHandshakeFileName(ControllerPid));
            PackagedAppConnectBackHandshake.Write(
                handshakePath,
                new Dictionary<string, string?>
                {
                    ["TESTINGPLATFORM_DOTNETTEST_EXECUTIONID"] = "controller-execution-id",
                    ["TESTINGPLATFORM_TESTHOSTCONTROLLER_PIPENAME_4321"] = "MONITORTOHOST_deadbeef",
                });

            string[] expectedArguments = ["--internal-testhostcontroller-pid", ControllerPid, "--help"];
            PackagedAppActivationData activation = PackagedAppActivationArguments.Create(expectedArguments, directory);
            var restoredEnvironment = new Dictionary<string, string?>(StringComparer.Ordinal);

            string[] actualArguments = PackagedAppConnectBackReader.ReadActivationArgumentsAndApplyConnectBack(
                activation.Arguments,
                directory,
                directory,
                () => directory,
                (name, value) => restoredEnvironment[name] = value);

            AssertArgumentsAreEqual(expectedArguments, actualArguments);
            Assert.AreEqual("controller-execution-id", restoredEnvironment["TESTINGPLATFORM_DOTNETTEST_EXECUTIONID"]);
            Assert.AreEqual("MONITORTOHOST_deadbeef", restoredEnvironment["TESTINGPLATFORM_TESTHOSTCONTROLLER_PIPENAME_4321"]);
            Assert.IsFalse(File.Exists(handshakePath), "The bootstrap must consume the connect-back handshake before returning.");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void ReadActivationArgumentsAndApplyConnectBack_RetryHandshake_RestoresEnvironmentAndConsumesHandshake()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            File.WriteAllText(
                Path.Combine(directory, AppxManifestInfo.AppxManifestFileName),
                """
                <?xml version="1.0" encoding="utf-8"?>
                <Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10">
                  <Identity Name="Contoso.MyTestApp" Publisher="CN=Contoso" Version="1.0.0.0" />
                  <Applications><Application Id="App" Executable="MyTestApp.exe" /></Applications>
                </Package>
                """);

            string[] expectedArguments = ["--internal-retry-pipename", @"LOCAL\retry-pipe", "--help"];
            string handshakeId = PackagedAppConnectBackHandshake.TryGetHandshakeId(expectedArguments)!;
            string handshakePath = Path.Combine(directory, PackagedAppConnectBackHandshake.GetHandshakeFileName(handshakeId));
            PackagedAppConnectBackHandshake.Write(
                handshakePath,
                new Dictionary<string, string?>
                {
                    ["TESTINGPLATFORM_DOTNETTEST_ATTEMPTNUMBER"] = "2",
                    ["TESTINGPLATFORM_DOTNETTEST_EXECUTIONID"] = "retry-execution-id",
                });
            PackagedAppActivationData activation = PackagedAppActivationArguments.Create(expectedArguments, directory);
            var restoredEnvironment = new Dictionary<string, string?>(StringComparer.Ordinal);

            string[] actualArguments = PackagedAppConnectBackReader.ReadActivationArgumentsAndApplyConnectBack(
                activation.Arguments,
                directory,
                directory,
                () => directory,
                (name, value) => restoredEnvironment[name] = value);

            AssertArgumentsAreEqual(expectedArguments, actualArguments);
            Assert.AreEqual("2", restoredEnvironment["TESTINGPLATFORM_DOTNETTEST_ATTEMPTNUMBER"]);
            Assert.AreEqual("retry-execution-id", restoredEnvironment["TESTINGPLATFORM_DOTNETTEST_EXECUTIONID"]);
            Assert.IsFalse(File.Exists(handshakePath));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "PackagedAppActivationArgumentsTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static string CreateInlinePayload(byte[] payload)
        => "mtp:v1:inline:" + Convert.ToBase64String(payload);

    private static void AssertInvalidInlinePayload(byte[] payload, string expectedMessage)
    {
        string[] warmup = PackagedAppActivationArguments.Read(
            CreateInlinePayload([1, 0, 0, 0, 1, 0, 0, 0, (byte)'a', 0]),
            localStateDirectory: null);
        Assert.AreSequenceEqual(["a"], warmup);

        FormatException exception = Assert.ThrowsExactly<FormatException>(
            () => PackagedAppActivationArguments.Read(CreateInlinePayload(payload), localStateDirectory: null));

        Assert.AreEqual(expectedMessage, exception.Message);
    }

    private static void AssertArgumentsAreEqual(string[] expected, string[] actual)
    {
        Assert.HasCount(expected.Length, actual);
        for (int i = 0; i < expected.Length; i++)
        {
            Assert.AreEqual(expected[i], actual[i], $"Argument {i} differs.");
        }
    }
}

#endif
