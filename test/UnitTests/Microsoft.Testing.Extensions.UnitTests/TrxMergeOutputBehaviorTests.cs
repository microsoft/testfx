// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

extern alias ctrf;

using Microsoft.Testing.Extensions.UnitTests.Helpers;

using MergeOutput = ctrf::Microsoft.Testing.Extensions.MergeOutputFileHelper;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class TrxMergeOutputBehaviorTests
{
    [TestMethod]
    [DataRow("io")]
    [DataRow("cancellation")]
    [DataRow("ordinary")]
    public async Task TemporarySibling_WriteFailurePreservesCommittedReportAndCleansPartialFile(string failure)
    {
        using var files = new TrxToolWorkspace();
        string output = files.Write("merged.trx", "previous complete report");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Exception original = failure switch
        {
            "io" => new IOException("serialization failed"),
            "cancellation" => new OperationCanceledException("serialization canceled", cancellation.Token),
            _ => new InvalidOperationException("unexpected writer failure"),
        };
        string? temporary = null;

        Exception actual = await Assert.ThrowsAsync<Exception>(() => MergeOutput.WriteViaTemporarySiblingAsync(output, path =>
        {
            temporary = path;
            AssertTemporarySibling(output, path);
            File.WriteAllText(path, "partial document");
            Assert.AreEqual("previous complete report", File.ReadAllText(output), "The committed entry remains readable until writing completes.");
            return Task.FromException(original);
        }));

        Assert.AreSame(original, actual);
        Assert.IsNotNull(temporary);
        Assert.IsFalse(File.Exists(temporary));
        Assert.AreEqual("previous complete report", File.ReadAllText(output));
        Assert.AreEqual(output, Assert.ContainsSingle(Directory.GetFiles(files.Root)));
    }

    [TestMethod]
    [DataRow(false, DisplayName = "TrxMergeOutput_NewDestination")]
    [DataRow(true, DisplayName = "TrxMergeOutput_ExistingDestination")]
    public async Task TemporarySibling_SuccessReplacesOnlyTheDestination(bool existing)
    {
        using var files = new TrxToolWorkspace();
        string input = files.Write("input.trx", "read-only source bytes");
        string output = existing ? files.Write("merged.trx", "stale output with a longer suffix") : files.FilePath("merged.trx");
        string? temporary = null;

        await MergeOutput.WriteViaTemporarySiblingAsync(output, path =>
        {
            temporary = path;
            AssertTemporarySibling(output, path);
            File.WriteAllText(path, "complete report");
            Assert.AreEqual(existing, File.Exists(output));
            if (existing)
            {
                Assert.AreEqual("stale output with a longer suffix", File.ReadAllText(output));
            }

            return Task.CompletedTask;
        });

        Assert.IsNotNull(temporary);
        Assert.IsFalse(File.Exists(temporary));
        Assert.AreEqual("complete report", File.ReadAllText(output));
        Assert.AreEqual("read-only source bytes", File.ReadAllText(input));
        Assert.HasCount(2, Directory.GetFiles(files.Root));
        Assert.IsEmpty(Directory.GetFiles(files.Root, "*.tmp"));
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task TemporarySibling_WindowsCleanupFailureDoesNotMaskPrimaryException()
    {
        using var files = new TrxToolWorkspace();
        string output = files.Write("merged.trx", "previous committed document");
        var original = new InvalidOperationException("primary serialization error");
        FileStream? locked = null;
        string? temporary = null;
        try
        {
            InvalidOperationException actual = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                () => MergeOutput.WriteViaTemporarySiblingAsync(output, path =>
                {
                    temporary = path;
                    locked = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
                    locked.WriteByte(42);
                    return Task.FromException(original);
                }));

            Assert.AreSame(original, actual);
            Assert.IsNotNull(temporary);
            Assert.IsTrue(File.Exists(temporary), "An exclusive Windows handle forces the best-effort deletion failure, rather than a silently unexercised catch.");
            Assert.AreEqual("previous committed document", File.ReadAllText(output));
        }
        finally
        {
            locked?.Dispose();
            if (temporary is not null && File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

#if NETCOREAPP
    [TestMethod]
    [OSCondition(OperatingSystems.Linux)]
    public async Task TemporarySibling_LinuxOutputLinkIsReplacedWithoutFollowingItsTarget()
    {
        using var files = new TrxToolWorkspace();
        string input = files.Write("input.trx", "must remain read-only");
        string output = files.FilePath("merged.trx");
        File.CreateSymbolicLink(output, input);

        await MergeOutput.WriteViaTemporarySiblingAsync(output, path =>
        {
            File.WriteAllText(path, "new merged report");
            return Task.CompletedTask;
        });

        Assert.AreEqual("must remain read-only", File.ReadAllText(input));
        Assert.AreEqual("new merged report", File.ReadAllText(output));
        Assert.IsNull(new FileInfo(output).LinkTarget);
        Assert.IsEmpty(Directory.GetFiles(files.Root, "*.tmp"));
    }
#endif

    [TestMethod]
    public void AliasCheck_MissingOutputAncestorsDoNotMaterializeDirectories()
    {
        using var files = new TrxToolWorkspace();
        string input = files.Write("input.trx", "read-only source");
        string output = files.FilePath(Path.Combine("missing", "nested", "merged.trx"));

        MergeOutput.EnsureOutputDoesNotAliasInput([input], output);

        Assert.IsFalse(Directory.Exists(files.FilePath("missing")));
        Assert.AreEqual("read-only source", File.ReadAllText(input));
        Assert.AreEqual(input, Assert.ContainsSingle(Directory.GetFiles(files.Root)));
    }

    private static void AssertTemporarySibling(string output, string temporary)
    {
        Assert.AreEqual(Path.GetDirectoryName(output), Path.GetDirectoryName(temporary),
            "A temporary sibling, not a global temporary file, is required for same-filesystem replacement.");
        string prefix = Path.GetFileName(output) + ".";
        string leaf = Path.GetFileName(temporary);
        Assert.StartsWith(prefix, leaf);
        Assert.EndsWith(".tmp", leaf);
        Assert.IsTrue(Guid.TryParseExact(leaf.Substring(prefix.Length, leaf.Length - prefix.Length - ".tmp".Length), "N", out _));
    }
}
