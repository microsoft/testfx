// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

extern alias ghactions;

using System.Text.Json;

using ghactions::Microsoft.Testing.Extensions.GitHubActionsReport;
using ghactions::Microsoft.Testing.Extensions.GitHubActionsReport.Resources;

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Helpers;

using Moq;

using GitHubCiRunSummaryAttachment = ghactions::Microsoft.Testing.Extensions.CiRunSummaryAttachment;
using GitHubCiRunSummaryModule = ghactions::Microsoft.Testing.Extensions.CiRunSummaryModule;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class GitHubActionsAttachmentCollectorTests : IDisposable
{
    // Independent expectations: changing a production bound must not silently change the tests.
    private const long FileLimit = 25L * 1024 * 1024;
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "github-attachments-" + Guid.NewGuid().ToString("N"));
    private readonly List<string> _warnings = [];

    public TestContext TestContext { get; set; } = null!;

    private string StagingDirectory => Path.Combine(_directory, "staged");

    [TestMethod]
    public async Task CaptureAsync_StagesExactBytesAndWritesManifest()
    {
        byte[] expected = [0, 1, 13, 10, 128, 255];
        string source = WriteSource("input/error log λ.bin", expected);
        GitHubActionsAttachmentCollector collector = CreateCollector();

        GitHubCiRunSummaryAttachment[]? captured = await collector.CaptureAsync(
            CreateNode("row-1", "Fails (row 1)", source), TestContext.CancellationToken);
        Assert.IsNotNull(captured, "A declared attachment must produce staged metadata.");
        GitHubCiRunSummaryAttachment attachment = Assert.ContainsSingle(captured);

        Assert.AreEqual("staged", attachment.Status);
        Assert.AreEqual("error_log__.bin", attachment.FileName);
        Assert.AreEqual(6L, attachment.SizeBytes);
        string destination = GetStagedPath(attachment);
        Assert.AreSequenceEqual(expected, File.ReadAllBytes(destination));
        Assert.AreSequenceEqual(expected, File.ReadAllBytes(source));
        Assert.IsFalse(File.Exists(Path.Combine(StagingDirectory, "attachments.json")));

        await collector.WriteManifestAsync(CreateModule(), TestContext.CancellationToken);

        using JsonDocument manifest = ReadSingleManifest();
        JsonElement root = manifest.RootElement;
        Assert.AreEqual(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.AreEqual("Attachment.Tests", root.GetProperty("assemblyName").GetString());
        Assert.AreEqual("net8.0", root.GetProperty("targetFramework").GetString());
        Assert.AreEqual("x64", root.GetProperty("architecture").GetString());
        Assert.AreEqual("session-1", root.GetProperty("sessionUid").GetString());
        Assert.AreEqual(2, root.GetProperty("attemptNumber").GetInt32());
        Assert.IsFalse(root.GetProperty("truncated").GetBoolean());
        JsonElement test = Assert.ContainsSingle(root.GetProperty("tests").EnumerateArray());
        Assert.AreEqual("row-1", test.GetProperty("uid").GetString());
        Assert.AreEqual("Example.AttachmentTests.Fails", test.GetProperty("fullyQualifiedName").GetString());
        Assert.AreEqual("Fails (row 1)", test.GetProperty("displayName").GetString());
        AssertManifestAttachment(attachment, Assert.ContainsSingle(test.GetProperty("attachments").EnumerateArray()));
        Assert.HasCount(2, Directory.GetFiles(StagingDirectory, "*", SearchOption.AllDirectories));
        Assert.IsEmpty(_warnings);
    }

    [TestMethod]
    public async Task CaptureAsync_DuplicateBasenamesAcrossTestsAndModules_DoNotOverwrite()
    {
        string first = WriteSource("first/report.log", [1, 2]);
        string second = WriteSource("second/report.log", [3, 4, 5]);
        string third = WriteSource("third/report.log", [6]);
        string existing = Path.Combine(StagingDirectory, "existing.txt");
        Directory.CreateDirectory(StagingDirectory);
        File.WriteAllText(existing, "keep existing output");
        GitHubActionsAttachmentCollector moduleOne = CreateCollector();
        GitHubActionsAttachmentCollector moduleTwo = CreateCollector();

        Task<GitHubCiRunSummaryAttachment[]?> firstCapture =
            moduleOne.CaptureAsync(CreateNode("../same-uid", "Same test", first, second), TestContext.CancellationToken);
        Task<GitHubCiRunSummaryAttachment[]?> otherModuleCapture =
            moduleTwo.CaptureAsync(CreateNode("../same-uid", "Same test", second, third), TestContext.CancellationToken);
        await Task.WhenAll(firstCapture, otherModuleCapture);
        GitHubCiRunSummaryAttachment[] anotherTest = (await moduleOne.CaptureAsync(
            CreateNode("another-uid", "Same test", third), TestContext.CancellationToken))!;
        GitHubCiRunSummaryAttachment[] firstTest = (await firstCapture)!;
        GitHubCiRunSummaryAttachment[] otherModule = (await otherModuleCapture)!;

        GitHubCiRunSummaryAttachment[] all = [.. firstTest, .. anotherTest, .. otherModule];
        Assert.HasCount(5, all.Select(attachment => attachment.RelativePath).Distinct(StringComparer.Ordinal));
        // PropertyBag does not promise declaration order. Pin the full byte multiset instead.
        Assert.AreSequenceEqual(
            new[] { "AQI=", "AwQF" },
            firstTest.Select(attachment => Convert.ToBase64String(File.ReadAllBytes(GetStagedPath(attachment)))).OrderBy(value => value, StringComparer.Ordinal));
        Assert.AreSequenceEqual(new byte[] { 6 }, File.ReadAllBytes(GetStagedPath(anotherTest[0])));
        Assert.AreSequenceEqual(
            new[] { "AwQF", "Bg==" },
            otherModule.Select(attachment => Convert.ToBase64String(File.ReadAllBytes(GetStagedPath(attachment)))).OrderBy(value => value, StringComparer.Ordinal));
        Assert.AreEqual(firstTest[0].RelativePath!.Split('/')[0], anotherTest[0].RelativePath!.Split('/')[0]);
        Assert.AreNotEqual(firstTest[0].RelativePath!.Split('/')[0], otherModule[0].RelativePath!.Split('/')[0]);

        await Task.WhenAll(
            moduleOne.WriteManifestAsync(CreateModule(), TestContext.CancellationToken),
            moduleTwo.WriteManifestAsync(CreateModule(), TestContext.CancellationToken));

        string[] manifests = Directory.GetFiles(StagingDirectory, "attachments.json", SearchOption.AllDirectories);
        Assert.HasCount(2, manifests);
        int[] testCounts = manifests.Select(path =>
        {
            using var manifest = JsonDocument.Parse(File.ReadAllText(path));
            return manifest.RootElement.GetProperty("tests").GetArrayLength();
        }).OrderBy(count => count).ToArray();
        Assert.AreSequenceEqual(new[] { 1, 2 }, testCounts);
        Assert.AreEqual("keep existing output", File.ReadAllText(existing));
        Assert.IsEmpty(_warnings);
    }

    [TestMethod]
    public async Task CaptureAsync_LongFileName_UsesSafeBoundedUploadName()
    {
        string source = WriteSource(new string('a', 100) + " [row].log", [7]);
        GitHubActionsAttachmentCollector collector = CreateCollector();

        GitHubCiRunSummaryAttachment attachment = Assert.ContainsSingle((await collector.CaptureAsync(
            CreateNode("long-name", "Long name", source), TestContext.CancellationToken))!);

        Assert.AreEqual(80, attachment.FileName.Length);
        Assert.EndsWith("__row_.log", attachment.FileName);
        Assert.MatchesRegex("^[a-zA-Z0-9._-]+$", attachment.FileName);
        Assert.AreSequenceEqual(new byte[] { 7 }, File.ReadAllBytes(GetStagedPath(attachment)));
        Assert.IsEmpty(_warnings);
    }

    [TestMethod]
    public async Task CaptureAsync_NoFileArtifacts_DoesNotCreateOutput()
    {
        GitHubActionsAttachmentCollector collector = CreateCollector();

        Assert.IsNull(await collector.CaptureAsync(CreateNode("empty", "No files"), TestContext.CancellationToken));
        await collector.WriteManifestAsync(CreateModule(), TestContext.CancellationToken);

        Assert.IsFalse(Directory.Exists(StagingDirectory));
        Assert.IsEmpty(_warnings);
    }

    [TestMethod]
    public async Task CaptureAsync_NonRegularSource_IsRejectedBeforeOpening()
    {
        string source = Path.Combine(_directory, "non-regular.log");
        Mock<IFileSystem> fileSystem = CreateVirtualFileSystem(source, 0, 0, physicalOutput: true);
        fileSystem.Setup(value => value.IsRegularFile(source)).Returns(false);
        GitHubActionsAttachmentCollector collector = CreateCollector(fileSystem.Object);

        GitHubCiRunSummaryAttachment attachment = Assert.ContainsSingle((await collector.CaptureAsync(
            CreateNode("special", "Non-regular source", source), TestContext.CancellationToken))!);

        Assert.AreEqual("non-regular", attachment.Status);
        Assert.IsNull(attachment.RelativePath);
        Assert.AreEqual(0L, attachment.SizeBytes);
        fileSystem.Verify(value => value.NewFileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read), Times.Never);
        Assert.Contains("non-regular", Assert.ContainsSingle(_warnings));
        Assert.IsFalse(Directory.Exists(StagingDirectory));
        await collector.WriteManifestAsync(CreateModule(), TestContext.CancellationToken);
        using JsonDocument manifest = ReadSingleManifest();
        JsonElement row = Assert.ContainsSingle(manifest.RootElement.GetProperty("tests").EnumerateArray());
        AssertManifestAttachment(attachment, Assert.ContainsSingle(row.GetProperty("attachments").EnumerateArray()));
    }

    [TestMethod]
    [DataRow(0L, true, DisplayName = "Empty file")]
    [DataRow(26214399L, true, DisplayName = "One byte below file limit")]
    [DataRow(26214400L, true, DisplayName = "Exact file limit")]
    [DataRow(26214401L, false, DisplayName = "One byte above file limit")]
    public async Task CaptureAsync_FileSizeBoundary_StagesOnlyWithinLimit(long length, bool stages)
    {
        string source = WriteSource("boundary.bin", []);
        using (var file = new FileStream(source, FileMode.Open, FileAccess.Write))
        {
            file.SetLength(length);
            if (length > 0)
            {
                file.Position = length - 1;
                file.WriteByte(213);
            }
        }

        GitHubActionsAttachmentCollector collector = CreateCollector();
        GitHubCiRunSummaryAttachment attachment = Assert.ContainsSingle((await collector.CaptureAsync(
            CreateNode("boundary", "Boundary", source), TestContext.CancellationToken))!);

        Assert.AreEqual(stages ? "staged" : "size-limit", attachment.Status);
        Assert.AreEqual(stages ? length : 0L, attachment.SizeBytes);
        if (stages)
        {
            using var staged = new FileStream(GetStagedPath(attachment), FileMode.Open, FileAccess.Read);
            Assert.AreEqual(length, staged.Length);
            if (length > 0)
            {
                staged.Position = length - 1;
                Assert.AreEqual(213, staged.ReadByte());
            }

            Assert.IsEmpty(_warnings);
        }
        else
        {
            Assert.IsNull(attachment.RelativePath);
            Assert.IsFalse(Directory.Exists(StagingDirectory));
            Assert.Contains("size-limit", Assert.ContainsSingle(_warnings));
        }
    }

    [TestMethod]
    public async Task CaptureAsync_ModuleByteBudget_AllowsExactLimitButRejectsNextByte()
    {
        string source = Path.Combine(_directory, "budget.bin");
        Mock<IFileSystem> fileSystem = CreateVirtualFileSystem(source, FileLimit + 1, FileLimit + 1);
        GitHubActionsAttachmentCollector collector = CreateCollector(fileSystem.Object);
        GitHubCiRunSummaryAttachment oversized = Assert.ContainsSingle((await collector.CaptureAsync(
            CreateNode("oversized", "Oversized before budget", source), TestContext.CancellationToken))!);
        Assert.AreEqual("size-limit", oversized.Status);
        Assert.IsNull(oversized.RelativePath);
        fileSystem.Setup(value => value.NewFileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read))
            .Throws(new IOException("unreadable before budget"));
        GitHubCiRunSummaryAttachment unreadable = Assert.ContainsSingle((await collector.CaptureAsync(
            CreateNode("unreadable", "Unreadable before budget", source), TestContext.CancellationToken))!);
        Assert.AreEqual("unavailable", unreadable.Status);
        Assert.IsNull(unreadable.RelativePath);
        ConfigureVirtualSource(fileSystem, source, FileLimit, FileLimit);
        var staged = new List<GitHubCiRunSummaryAttachment>();
        for (int i = 0; i < 9; i++)
        {
            staged.Add(Assert.ContainsSingle((await collector.CaptureAsync(
                CreateNode($"large-{i}", $"Large {i}", source), TestContext.CancellationToken))!));
        }

        ConfigureVirtualSource(fileSystem, source, FileLimit - 1, FileLimit - 1);
        staged.Add(Assert.ContainsSingle((await collector.CaptureAsync(
            CreateNode("almost-full", "Almost full", source), TestContext.CancellationToken))!));
        ConfigureVirtualSource(fileSystem, source, 1, 1);
        staged.Add(Assert.ContainsSingle((await collector.CaptureAsync(
            CreateNode("exact", "Exact budget", source), TestContext.CancellationToken))!));
        GitHubCiRunSummaryAttachment overBudget = Assert.ContainsSingle((await collector.CaptureAsync(
            CreateNode("over", "Over budget", source), TestContext.CancellationToken))!);

        Assert.HasCount(11, staged);
        Assert.IsTrue(staged.All(attachment => attachment.Status == "staged"));
        Assert.AreEqual(250L * 1024 * 1024, staged.Sum(attachment => attachment.SizeBytes));
        Assert.AreEqual("size-limit", overBudget.Status);
        Assert.IsNull(overBudget.RelativePath);
        Assert.AreEqual(0L, overBudget.SizeBytes);
        Assert.HasCount(3, _warnings);
        Assert.Contains("size-limit", _warnings[0]);
        Assert.Contains("unavailable", _warnings[1]);
        Assert.Contains("size-limit", _warnings[2]);

        // A byte-budget rejection must not poison later empty files.
        ConfigureVirtualSource(fileSystem, source, 0, 0);
        GitHubCiRunSummaryAttachment empty = Assert.ContainsSingle((await collector.CaptureAsync(
            CreateNode("empty", "Empty after budget", source), TestContext.CancellationToken))!);
        Assert.AreEqual("staged", empty.Status);
        Assert.AreEqual(0L, empty.SizeBytes);
        Assert.IsNotNull(empty.RelativePath);
    }

    [TestMethod]
    public async Task CaptureAsync_PerTestCountLimit_StagesTenAndRecordsSkippedFile()
    {
        string source = WriteSource("count.log", [42]);
        GitHubActionsAttachmentCollector collector = CreateCollector();

        GitHubCiRunSummaryAttachment[] attachments = (await collector.CaptureAsync(
            CreateNode("many-files", "Many files", [.. Enumerable.Repeat(source, 12)]), TestContext.CancellationToken))!;
        await collector.WriteManifestAsync(CreateModule(), TestContext.CancellationToken);

        Assert.HasCount(11, attachments);
        Assert.HasCount(10, attachments.Where(attachment => attachment.Status == "staged"));
        foreach (GitHubCiRunSummaryAttachment attachment in attachments.Take(10))
        {
            Assert.AreSequenceEqual(new byte[] { 42 }, File.ReadAllBytes(GetStagedPath(attachment)));
        }

        Assert.AreEqual("count-limit", attachments[10].Status);
        Assert.IsNull(attachments[10].RelativePath);
        Assert.AreEqual(0L, attachments[10].SizeBytes);
        Assert.Contains("count-limit", Assert.ContainsSingle(_warnings));
        using JsonDocument manifest = ReadSingleManifest();
        JsonElement test = Assert.ContainsSingle(manifest.RootElement.GetProperty("tests").EnumerateArray());
        Assert.AreEqual(11, test.GetProperty("attachments").GetArrayLength());
        AssertManifestAttachment(attachments[10], test.GetProperty("attachments")[10]);
    }

    [TestMethod]
    public async Task CaptureAsync_ModuleCountLimit_StopsAtOneThousandAndMarksManifestTruncated()
    {
        string source = Path.Combine(_directory, "empty.log");
        Mock<IFileSystem> fileSystem = CreateVirtualFileSystem(source, 0, 0, physicalOutput: true);
        GitHubActionsAttachmentCollector collector = CreateCollector(fileSystem.Object);
        for (int i = 0; i < 99; i++)
        {
            GitHubCiRunSummaryAttachment[] batch = (await collector.CaptureAsync(
                CreateNode($"batch-{i}", $"Batch {i}", [.. Enumerable.Repeat(source, 10)]), TestContext.CancellationToken))!;
            Assert.HasCount(10, batch);
        }

        Assert.HasCount(9, (await collector.CaptureAsync(
            CreateNode("before-limit", "Before limit", [.. Enumerable.Repeat(source, 9)]), TestContext.CancellationToken))!);
        GitHubCiRunSummaryAttachment[] crossing = (await collector.CaptureAsync(
            CreateNode("crossing", "Crossing limit", source, source), TestContext.CancellationToken))!;

        Assert.ContainsSingle(crossing.Where(attachment => attachment.Status == "staged"));
        Assert.HasCount(1000, Directory.GetFiles(StagingDirectory, "*.log", SearchOption.AllDirectories));
        Assert.AreEqual(GitHubActionsResources.AttachmentCountLimitWarning, Assert.ContainsSingle(_warnings));
        await collector.WriteManifestAsync(CreateModule(), TestContext.CancellationToken);
        using JsonDocument manifest = ReadSingleManifest();
        Assert.IsTrue(manifest.RootElement.GetProperty("truncated").GetBoolean(), "Crossing the module limit inside the final test must not require another test to mark truncation.");

        GitHubCiRunSummaryAttachment[]? afterLimit = await collector.CaptureAsync(
            CreateNode("after-limit", "After limit", source), TestContext.CancellationToken);
        Assert.IsTrue(afterLimit is null || afterLimit.All(attachment => attachment.RelativePath is null));
        Assert.HasCount(1000, Directory.GetFiles(StagingDirectory, "*.log", SearchOption.AllDirectories));
        Assert.ContainsSingle(_warnings);
    }

    [TestMethod]
    [DataRow(false, DisplayName = "Unavailable entries consume metadata budget")]
    [DataRow(true, DisplayName = "Per-test overflow entries consume metadata budget")]
    public async Task CaptureAsync_ModuleEntryBudget_IncludesUnavailableAndOverflowDiagnostics(bool perTestOverflow)
    {
        string source = Path.Combine(_directory, "entry.log");
        Mock<IFileSystem> fileSystem = CreateVirtualFileSystem(source, 0, 0, physicalOutput: true);
        if (!perTestOverflow)
        {
            fileSystem.Setup(value => value.NewFileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read))
                .Throws(new FileNotFoundException("unavailable attachment"));
        }

        GitHubActionsAttachmentCollector collector = CreateCollector(fileSystem.Object);
        if (perTestOverflow)
        {
            // 90 * (10 staged + 1 count-limit) = 990 entries; only ten more entries fit.
            for (int i = 0; i < 91; i++)
            {
                await collector.CaptureAsync(CreateNode($"batch-{i}", $"Batch {i}", [.. Enumerable.Repeat(source, 12)]), TestContext.CancellationToken);
            }
        }
        else
        {
            for (int i = 0; i < 1000; i++)
            {
                await collector.CaptureAsync(CreateNode($"missing-{i}", $"Missing {i}", source), TestContext.CancellationToken);
            }

            Assert.IsFalse(Directory.Exists(StagingDirectory));
        }

        GitHubCiRunSummaryAttachment[]? afterLimit = await collector.CaptureAsync(
            CreateNode("after-limit", "After limit", source), TestContext.CancellationToken);
        Assert.IsTrue(afterLimit is null || afterLimit.All(attachment => attachment.RelativePath is null));
        await collector.WriteManifestAsync(CreateModule(), TestContext.CancellationToken);

        using JsonDocument manifest = ReadSingleManifest();
        JsonElement[] entries = manifest.RootElement.GetProperty("tests").EnumerateArray()
            .SelectMany(test => test.GetProperty("attachments").EnumerateArray()).ToArray();
        Assert.HasCount(1000, entries);
        Assert.IsTrue(manifest.RootElement.GetProperty("truncated").GetBoolean());
        Assert.HasCount(perTestOverflow ? 910 : 0, entries.Where(attachment => attachment.GetProperty("status").GetString() == "staged"));
        Assert.HasCount(perTestOverflow ? 90 : 1000, entries.Where(attachment => attachment.GetProperty("relativePath").ValueKind == JsonValueKind.Null));
        Assert.HasCount(perTestOverflow ? 910 : 0, Directory.GetFiles(StagingDirectory, "*.log", SearchOption.AllDirectories));
        Assert.AreEqual(GitHubActionsResources.AttachmentCountLimitWarning, _warnings[_warnings.Count - 1]);
        Assert.HasCount(perTestOverflow ? 91 : 1001, _warnings);
    }

    [TestMethod]
    [DataRow("missing", DisplayName = "Missing source")]
    [DataRow("denied", DisplayName = "Unreadable source")]
    [DataRow("unsupported", DisplayName = "Unsupported source")]
    [DataRow("argument", DisplayName = "Invalid source path")]
    [DataRow("security", DisplayName = "Source security restriction")]
    public async Task CaptureAsync_UnavailableFiles_WarnWithoutInventingPaths(string failure)
    {
        string source = Path.Combine(_directory, "unavailable.log");
        Mock<IFileSystem> fileSystem = CreateVirtualFileSystem(source, 0, 0, physicalOutput: true);
        Exception exception = failure switch
        {
            "denied" => new UnauthorizedAccessException("controlled unreadable file"),
            "unsupported" => new NotSupportedException("controlled unsupported file"),
            "argument" => new ArgumentException("controlled invalid source path"),
            "security" => new System.Security.SecurityException("controlled source security restriction"),
            _ => new FileNotFoundException("controlled missing file"),
        };
        fileSystem.Setup(value => value.NewFileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read)).Throws(exception);
        GitHubActionsAttachmentCollector collector = CreateCollector(fileSystem.Object);

        GitHubCiRunSummaryAttachment attachment = Assert.ContainsSingle((await collector.CaptureAsync(
            CreateNode("unavailable", "Unavailable", source), TestContext.CancellationToken))!);

        Assert.AreEqual("unavailable", attachment.Status);
        Assert.AreEqual("unavailable.log", attachment.FileName);
        Assert.IsNull(attachment.RelativePath);
        Assert.AreEqual(0L, attachment.SizeBytes);
        Assert.Contains("unavailable.log", Assert.ContainsSingle(_warnings));
        Assert.Contains("unavailable", _warnings[0]);
        Assert.IsFalse(Directory.Exists(StagingDirectory));
        await collector.WriteManifestAsync(CreateModule(), TestContext.CancellationToken);
        using JsonDocument manifest = ReadSingleManifest();
        JsonElement test = Assert.ContainsSingle(manifest.RootElement.GetProperty("tests").EnumerateArray());
        AssertManifestAttachment(attachment, Assert.ContainsSingle(test.GetProperty("attachments").EnumerateArray()));
        Assert.ContainsSingle(Directory.GetFiles(StagingDirectory, "*", SearchOption.AllDirectories));
    }

    [TestMethod]
    public async Task CaptureAsync_GrowingFile_DeletesIncompleteDestination()
    {
        string source = Path.Combine(_directory, "growing.log");
        Mock<IFileSystem> fileSystem = CreateVirtualFileSystem(source, 1, FileLimit + 1, physicalOutput: true);
        var growing = new Mock<MemoryStream>(new byte[checked((int)FileLimit + 1)]) { CallBase = true };
        growing.SetupGet(value => value.Length).Returns(1);
        fileSystem.Setup(value => value.NewFileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read))
            .Returns(WrapStream(growing.Object));
        GitHubActionsAttachmentCollector collector = CreateCollector(fileSystem.Object);

        GitHubCiRunSummaryAttachment attachment = Assert.ContainsSingle((await collector.CaptureAsync(
            CreateNode("growing", "Growing file", source), TestContext.CancellationToken))!);

        growing.Verify(value => value.ReadAsync(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        Assert.AreEqual("size-limit", attachment.Status, string.Join("; ", _warnings));
        Assert.IsNull(attachment.RelativePath);
        Assert.AreEqual(0L, attachment.SizeBytes);
        Assert.IsEmpty(Directory.GetFiles(StagingDirectory, "*", SearchOption.AllDirectories));
        fileSystem.Verify(value => value.DeleteFile(It.IsAny<string>()), Times.Once);
        Assert.Contains("size-limit", Assert.ContainsSingle(_warnings));
    }

    [TestMethod]
    [DataRow(false, DisplayName = "Destination disposal fails")]
    [DataRow(true, DisplayName = "Cancellation after destination disposal")]
    public async Task CaptureAsync_DestinationDisposalCannotCommitStagedBytes(bool cancelAfterDisposal)
    {
        byte[] expected = [71, 0, 255];
        string source = WriteSource("dispose.log", expected);
        Mock<IFileSystem> fileSystem = CreateVirtualFileSystem(source, expected.Length, expected.Length, physicalOutput: true);
        var real = new SystemFileSystem();
        using var cancellation = new CancellationTokenSource();
        fileSystem.Setup(value => value.NewFileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read))
            .Returns(() => real.NewFileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read));
        fileSystem.Setup(value => value.NewFileStream(
            It.Is<string>(path => Path.GetFileName(path).EndsWith("-dispose.log", StringComparison.Ordinal)),
            FileMode.CreateNew, FileAccess.Write, FileShare.None))
            .Returns((string path, FileMode mode, FileAccess access, FileShare share) =>
            {
                IFileStream target = real.NewFileStream(path, mode, access, share);
                var wrapper = new Mock<IFileStream>();
                wrapper.SetupGet(value => value.Stream).Returns(target.Stream);
                wrapper.Setup(value => value.Dispose()).Callback(() =>
                {
                    target.Dispose();
                    if (cancelAfterDisposal)
                    {
                        cancellation.Cancel();
                    }
                    else
                    {
                        throw new IOException("controlled destination disposal failure");
                    }
                });
                return wrapper.Object;
            });
        GitHubActionsAttachmentCollector collector = CreateCollector(fileSystem.Object);
        TestNode node = CreateNode("destination-disposal", "Destination disposal", source);
        GitHubCiRunSummaryAttachment[]? captured = null;

        if (cancelAfterDisposal)
        {
            OperationCanceledException error = await Assert.ThrowsExactlyAsync<OperationCanceledException>(
                () => collector.CaptureAsync(node, cancellation.Token));
            Assert.AreEqual(cancellation.Token, error.CancellationToken);
        }
        else
        {
            captured = await collector.CaptureAsync(node, cancellation.Token);
        }

        fileSystem.Verify(value => value.DeleteFile(It.IsAny<string>()), Times.Once);
        Assert.IsEmpty(Directory.GetFiles(StagingDirectory, "*", SearchOption.AllDirectories));
        Assert.AreSequenceEqual(expected, File.ReadAllBytes(source));
        Assert.HasCount(cancelAfterDisposal ? 0 : 1, _warnings);
        await collector.WriteManifestAsync(CreateModule(), TestContext.CancellationToken);

        using JsonDocument manifest = ReadSingleManifest();
        JsonElement row = Assert.ContainsSingle(manifest.RootElement.GetProperty("tests").EnumerateArray());
        Assert.AreEqual("destination-disposal", row.GetProperty("uid").GetString());
        JsonElement attachment = Assert.ContainsSingle(row.GetProperty("attachments").EnumerateArray());
        Assert.AreEqual(0L, attachment.GetProperty("sizeBytes").GetInt64());
        Assert.AreEqual("unavailable", attachment.GetProperty("status").GetString());
        Assert.AreEqual(JsonValueKind.Null, attachment.GetProperty("relativePath").ValueKind);
        Assert.ContainsSingle(Directory.GetFiles(StagingDirectory, "*", SearchOption.AllDirectories));
        if (!cancelAfterDisposal)
        {
            Assert.IsNotNull(captured);
            GitHubCiRunSummaryAttachment result = Assert.ContainsSingle(captured);
            Assert.AreEqual(0L, result.SizeBytes);
            Assert.AreEqual("unavailable", result.Status);
            Assert.IsNull(result.RelativePath);
            Assert.Contains("dispose.log", _warnings[0]);
            Assert.Contains("unavailable", _warnings[0]);
        }
    }

    [TestMethod]
    public async Task CaptureAsync_WarningCallbackFails_ManifestStillIndexesEarlierStagedFile()
    {
        string first = WriteSource("first.log", []);
        string second = WriteSource("second.log", []);
        TestNode node = CreateNode("warning-failure", "Warning failure", first, second);
        FileArtifactProperty[] order = node.Properties.OfType<FileArtifactProperty>().ToArray();
        File.WriteAllBytes(order[0].FileInfo.FullName, [71, 0, 255]);
        File.WriteAllBytes(order[1].FileInfo.FullName, [72]);
        Mock<IFileSystem> fileSystem = CreateVirtualFileSystem(first, 0, 0, physicalOutput: true);
        var real = new SystemFileSystem();
        fileSystem.Setup(value => value.NewFileStream(It.IsAny<string>(), FileMode.Open, FileAccess.Read, FileShare.Read))
            .Returns((string path, FileMode mode, FileAccess access, FileShare share) => real.NewFileStream(path, mode, access, share));
        fileSystem.Setup(value => value.NewFileStream(order[1].FileInfo.FullName, FileMode.Open, FileAccess.Read, FileShare.Read))
            .Throws(new UnauthorizedAccessException("second file unavailable"));
        var collector = new GitHubActionsAttachmentCollector(StagingDirectory, fileSystem.Object, warning =>
        {
            _warnings.Add(warning);
            throw new InvalidOperationException("warning callback failed");
        });

        InvalidOperationException error = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => collector.CaptureAsync(node, TestContext.CancellationToken));

        Assert.AreEqual("warning callback failed", error.Message);
        string stagedPath = Assert.ContainsSingle(Directory.GetFiles(StagingDirectory, "*.log", SearchOption.AllDirectories));
        Assert.AreSequenceEqual(new byte[] { 71, 0, 255 }, File.ReadAllBytes(stagedPath));
        await collector.WriteManifestAsync(CreateModule(), TestContext.CancellationToken);

        using JsonDocument manifest = ReadSingleManifest();
        JsonElement row = Assert.ContainsSingle(manifest.RootElement.GetProperty("tests").EnumerateArray());
        Assert.AreEqual("warning-failure", row.GetProperty("uid").GetString());
        JsonElement[] attachments = row.GetProperty("attachments").EnumerateArray().ToArray();
        Assert.HasCount(2, attachments);
        JsonElement staged = Assert.ContainsSingle(attachments.Where(attachment => attachment.GetProperty("status").GetString() == "staged"));
        Assert.AreEqual(3L, staged.GetProperty("sizeBytes").GetInt64());
        Assert.AreEqual(stagedPath, Path.Combine(StagingDirectory, staged.GetProperty("relativePath").GetString()!.Replace('/', Path.DirectorySeparatorChar)));
        JsonElement unavailable = Assert.ContainsSingle(attachments.Where(attachment => attachment.GetProperty("status").GetString() == "unavailable"));
        Assert.AreEqual(JsonValueKind.Null, unavailable.GetProperty("relativePath").ValueKind);
        Assert.AreEqual(0L, unavailable.GetProperty("sizeBytes").GetInt64());
        Assert.ContainsSingle(_warnings);
        Assert.HasCount(2, Directory.GetFiles(StagingDirectory, "*", SearchOption.AllDirectories));
    }

    [TestMethod]
    public async Task CaptureAsync_CanceledBeforeCopy_CreatesNoOutput()
    {
        string source = WriteSource("cancel.log", [1, 2, 3]);
        GitHubActionsAttachmentCollector collector = CreateCollector();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        OperationCanceledException error = await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => collector.CaptureAsync(CreateNode("canceled", "Canceled", source), cancellation.Token));

        Assert.AreEqual(cancellation.Token, error.CancellationToken);
        Assert.IsFalse(Directory.Exists(StagingDirectory));
        Assert.AreSequenceEqual(new byte[] { 1, 2, 3 }, File.ReadAllBytes(source));
        Assert.IsEmpty(_warnings);
    }

    [TestMethod]
    public async Task CaptureAsync_CanceledDuringCopy_PropagatesTokenAndDeletesPartialFile()
    {
        string source = Path.Combine(_directory, "cancel-during.log");
        Mock<IFileSystem> fileSystem = CreateVirtualFileSystem(source, 4, 4, physicalOutput: true);
        using var cancellation = new CancellationTokenSource();
        var sourceStream = new Mock<Stream>();
        sourceStream.SetupGet(value => value.Length).Returns(4);
        int reads = 0;
        sourceStream.Setup(value => value.ReadAsync(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns((byte[] buffer, int offset, int _, CancellationToken token) =>
            {
                if (++reads == 1)
                {
                    buffer[offset] = 17;
                    buffer[offset + 1] = 18;
                    return Task.FromResult(2);
                }

                cancellation.Cancel();
                token.ThrowIfCancellationRequested();
                return Task.FromResult(0);
            });
        fileSystem.Setup(value => value.NewFileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read))
            .Returns(WrapStream(sourceStream.Object));
        GitHubActionsAttachmentCollector collector = CreateCollector(fileSystem.Object);

        OperationCanceledException error = await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => collector.CaptureAsync(CreateNode("cancel-during", "Canceled during copy", source), cancellation.Token));

        Assert.AreEqual(cancellation.Token, error.CancellationToken);
        Assert.AreEqual(2, reads);
        fileSystem.Verify(value => value.DeleteFile(It.IsAny<string>()), Times.Once);
        Assert.IsEmpty(Directory.GetFiles(StagingDirectory, "*", SearchOption.AllDirectories));
        Assert.IsEmpty(_warnings);
    }

    [TestMethod]
    public async Task WriteManifestAsync_Canceled_PreservesCompleteStagedFile()
    {
        string source = WriteSource("complete.log", [11, 12]);
        GitHubActionsAttachmentCollector collector = CreateCollector();
        GitHubCiRunSummaryAttachment attachment = Assert.ContainsSingle((await collector.CaptureAsync(
            CreateNode("complete", "Complete", source), TestContext.CancellationToken))!);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        OperationCanceledException error = await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => collector.WriteManifestAsync(CreateModule(), cancellation.Token));

        Assert.AreEqual(cancellation.Token, error.CancellationToken);
        Assert.AreSequenceEqual(new byte[] { 11, 12 }, File.ReadAllBytes(GetStagedPath(attachment)));
        Assert.IsEmpty(Directory.GetFiles(StagingDirectory, "attachments.json", SearchOption.AllDirectories));
        Assert.IsEmpty(_warnings);
    }

    [TestMethod]
    [DataRow(false, DisplayName = "Manifest stream unavailable")]
    [DataRow(true, DisplayName = "Manifest directory unavailable")]
    public async Task WriteManifestAsync_UnwritableManifest_WarnsWithoutThrowing(bool directoryUnavailable)
    {
        string source = Path.Combine(_directory, "complete.log");
        Mock<IFileSystem> fileSystem = CreateVirtualFileSystem(source, 2, 2, physicalOutput: true);
        GitHubActionsAttachmentCollector collector = CreateCollector(fileSystem.Object);
        GitHubCiRunSummaryAttachment attachment = Assert.ContainsSingle((await collector.CaptureAsync(
            CreateNode("complete", "Complete", source), TestContext.CancellationToken))!);
        if (directoryUnavailable)
        {
            fileSystem.Setup(value => value.CreateDirectory(It.IsAny<string>())).Throws(new IOException("manifest unavailable"));
        }
        else
        {
            fileSystem.Setup(value => value.NewFileStream(
                It.Is<string>(path => Path.GetFileName(path).StartsWith("attachments.json", StringComparison.Ordinal)),
                FileMode.CreateNew, FileAccess.Write, FileShare.None)).Throws(new UnauthorizedAccessException("manifest unavailable"));
        }

        await collector.WriteManifestAsync(CreateModule(), TestContext.CancellationToken);

        Assert.Contains("manifest unavailable", Assert.ContainsSingle(_warnings));
        Assert.Contains("manifest", _warnings[0]);
        Assert.AreEqual(2L, new FileInfo(GetStagedPath(attachment)).Length);
        Assert.IsEmpty(Directory.GetFiles(StagingDirectory, "attachments.json", SearchOption.AllDirectories));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private GitHubActionsAttachmentCollector CreateCollector(IFileSystem? fileSystem = null)
        => new(StagingDirectory, fileSystem ?? new SystemFileSystem(), warning =>
        {
            _warnings.Add(warning);
            return Task.CompletedTask;
        });

    private string WriteSource(string relativePath, byte[] bytes)
    {
        string path = Path.Combine(_directory, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private string GetStagedPath(GitHubCiRunSummaryAttachment attachment)
    {
        Assert.IsNotNull(attachment.RelativePath);
        Assert.IsFalse(Path.IsPathRooted(attachment.RelativePath));
        Assert.DoesNotContain('\\', attachment.RelativePath);
        string[] segments = attachment.RelativePath.Split('/');
        Assert.HasCount(3, segments);
        Assert.DoesNotContain("..", segments);
        Assert.DoesNotContain(".", segments);
        Assert.MatchesRegex("^[a-zA-Z0-9._/-]+$", attachment.RelativePath);
        return Path.Combine(StagingDirectory, attachment.RelativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    private JsonDocument ReadSingleManifest()
        => JsonDocument.Parse(File.ReadAllText(Assert.ContainsSingle(
            Directory.GetFiles(StagingDirectory, "attachments.json", SearchOption.AllDirectories))));

    private static void AssertManifestAttachment(GitHubCiRunSummaryAttachment attachment, JsonElement json)
    {
        Assert.AreEqual(attachment.FileName, json.GetProperty("fileName").GetString());
        Assert.AreEqual(attachment.RelativePath, json.GetProperty("relativePath").GetString());
        Assert.AreEqual(attachment.SizeBytes, json.GetProperty("sizeBytes").GetInt64());
        Assert.AreEqual(attachment.Status, json.GetProperty("status").GetString());
    }

    private static GitHubCiRunSummaryModule CreateModule() => new()
    {
        AssemblyName = "Attachment.Tests",
        TargetFramework = "net8.0",
        Architecture = "x64",
        SessionUid = "session-1",
        AttemptNumber = 2,
    };

    private static TestNode CreateNode(string uid, string displayName, params string[] files)
    {
        var properties = new PropertyBag(
            new FailedTestNodeStateProperty("failure"),
            new TestMethodIdentifierProperty("Attachment.Tests", "Example", "AttachmentTests", "Fails", 0, [], "System.Void"));
        for (int i = 0; i < files.Length; i++)
        {
            properties.Add(new FileArtifactProperty(new FileInfo(files[i]), $"Attachment {i}"));
        }

        return new TestNode { Uid = uid, DisplayName = displayName, Properties = properties };
    }

    private static Mock<IFileSystem> CreateVirtualFileSystem(string source, long length, long available, bool physicalOutput = false)
    {
        var real = new SystemFileSystem();
        var fileSystem = new Mock<IFileSystem>();
        fileSystem.Setup(value => value.IsRegularFile(It.IsAny<string>())).Returns(true);
        fileSystem.Setup(value => value.CreateDirectory(It.IsAny<string>()))
            .Returns((string path) => physicalOutput ? real.CreateDirectory(path) : path);
        fileSystem.Setup(value => value.ExistFile(It.IsAny<string>()))
            .Returns((string path) => physicalOutput && real.ExistFile(path));
        fileSystem.Setup(value => value.MoveFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
            .Callback((string sourcePath, string destinationPath, bool overwrite) =>
            {
                if (physicalOutput)
                {
                    real.MoveFile(sourcePath, destinationPath, overwrite);
                }
            });
        fileSystem.Setup(value => value.NewFileStream(It.IsAny<string>(), FileMode.CreateNew, FileAccess.Write, FileShare.None))
            .Returns((string path, FileMode mode, FileAccess access, FileShare share) => physicalOutput
                ? real.NewFileStream(path, mode, access, share)
                : WrapStream(Stream.Null));
        fileSystem.Setup(value => value.DeleteFile(It.IsAny<string>())).Callback((string path) =>
        {
            if (physicalOutput)
            {
                real.DeleteFile(path);
            }
        });
        ConfigureVirtualSource(fileSystem, source, length, available);
        return fileSystem;
    }

    private static void ConfigureVirtualSource(Mock<IFileSystem> fileSystem, string source, long length, long available)
        => fileSystem.Setup(value => value.NewFileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read))
            .Returns(() =>
            {
                long remaining = available;
                var stream = new Mock<Stream>();
                stream.SetupGet(value => value.Length).Returns(length);
                stream.Setup(value => value.ReadAsync(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                    .Returns((byte[] _, int _, int count, CancellationToken token) =>
                    {
                        token.ThrowIfCancellationRequested();
                        int read = (int)Math.Min(count, remaining);
                        remaining -= read;
                        return Task.FromResult(read);
                    });
                return WrapStream(stream.Object);
            });

    private static IFileStream WrapStream(Stream stream)
    {
        var file = new Mock<IFileStream>();
        file.SetupGet(value => value.Stream).Returns(stream);
        file.Setup(value => value.Dispose()).Callback(stream.Dispose);
        return file.Object;
    }
}
