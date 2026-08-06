// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// The PackagedApp extension only targets .NET (net8.0/net9.0), so these tests are compiled only there.
#if !NETFRAMEWORK

using Microsoft.Testing.Extensions;
using Microsoft.Testing.Extensions.PackagedApp;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class PackagedAppActivationArgumentsTests
{
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
            Assert.DoesNotContain(secret, Encoding.Unicode.GetString(File.ReadAllBytes(activation.PayloadPath)));

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

    private static string CreateTemporaryDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "PackagedAppActivationArgumentsTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
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
