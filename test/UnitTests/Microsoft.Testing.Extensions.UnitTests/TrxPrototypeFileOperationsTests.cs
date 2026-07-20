// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.TrxReport.Abstractions.Streaming;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class TrxPrototypeFileOperationsTests
{
    private static readonly Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    [TestMethod]
    public void CreateTemporarySiblingPath_IsInDestinationDirectory()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            string destinationPath = Path.Combine(directory, "results.trx");
            TrxPrototypeFileOperations operations = new();

            string first = operations.CreateTemporarySiblingPath(destinationPath);
            string second = operations.CreateTemporarySiblingPath(destinationPath);

            Assert.AreEqual(directory, Path.GetDirectoryName(first));
            Assert.AreEqual(directory, Path.GetDirectoryName(second));
            Assert.AreNotEqual(destinationPath, first);
            Assert.AreNotEqual(first, second);
            Assert.IsTrue(Path.GetFileName(first).StartsWith("results.trx.", StringComparison.Ordinal));
            Assert.IsTrue(first.EndsWith(".tmp", StringComparison.Ordinal));
            Assert.IsFalse(File.Exists(first));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void ReplaceTemporarySibling_TargetAbsentOrPresent_PublishesCompleteTemp()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            TrxPrototypeFileOperations operations = new();
            if (!operations.SupportsAtomicReplace)
            {
                Assert.IsFalse(operations.SupportsAtomicReplace);
                return;
            }

            string destinationPath = Path.Combine(directory, "results.trx");
            string firstTemporaryPath = operations.CreateTemporarySiblingPath(destinationPath);
            WriteFile(operations, firstTemporaryPath, "first complete document");

            operations.ReplaceTemporarySibling(firstTemporaryPath, destinationPath);

            Assert.AreEqual("first complete document", File.ReadAllText(destinationPath));
            Assert.IsFalse(File.Exists(firstTemporaryPath));

            string secondTemporaryPath = operations.CreateTemporarySiblingPath(destinationPath);
            WriteFile(operations, secondTemporaryPath, "second complete document");

            operations.ReplaceTemporarySibling(secondTemporaryPath, destinationPath);

            Assert.AreEqual("second complete document", File.ReadAllText(destinationPath));
            Assert.IsFalse(File.Exists(secondTemporaryPath));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void ReplaceTemporarySibling_WhenAtomicReplaceUnsupported_FailsBeforeTargetMutation()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            TrxPrototypeFileOperations operations = new();
            if (operations.SupportsAtomicReplace)
            {
                Assert.IsTrue(operations.SupportsAtomicReplace);
                return;
            }

            string destinationPath = Path.Combine(directory, "results.trx");
            string temporaryPath = operations.CreateTemporarySiblingPath(destinationPath);
            File.WriteAllText(destinationPath, "prior document");
            File.WriteAllText(temporaryPath, "new document");

            _ = Assert.ThrowsExactly<PlatformNotSupportedException>(
                () => operations.ReplaceTemporarySibling(temporaryPath, destinationPath));

            Assert.AreEqual("prior document", File.ReadAllText(destinationPath));
            Assert.AreEqual("new document", File.ReadAllText(temporaryPath));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void Open_RequestedFileShareIsApplied()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            string path = Path.Combine(directory, "results.trx");
            File.WriteAllText(path, "abcdef");
            TrxPrototypeFileOperations operations = new();

            using (ITrxPrototypeFile file = operations.Open(
                       path,
                       FileMode.Open,
                       FileAccess.ReadWrite,
                       FileShare.None))
            {
                Assert.AreEqual(6, file.Length);
                byte[] readBuffer = new byte[2];
                Assert.AreEqual(2, file.Read(readBuffer, 0, readBuffer.Length));
                Assert.AreEqual("ab", Utf8.GetString(readBuffer));

                file.Seek(2, SeekOrigin.Begin);
                file.Write(Utf8.GetBytes("XY"), 0, 2);
                file.SetLength(4);
                file.Flush();

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    _ = Assert.ThrowsExactly<IOException>(
                        () =>
                        {
                            using FileStream ignored = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                        });
                }
            }

            Assert.AreEqual("abXY", File.ReadAllText(path));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"trx-prototype-file-operations-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static void WriteFile(TrxPrototypeFileOperations operations, string path, string content)
    {
        byte[] bytes = Utf8.GetBytes(content);
        using ITrxPrototypeFile file = operations.Open(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.Read | FileShare.Delete);
        file.Write(bytes, 0, bytes.Length);
        file.Flush();
    }
}
