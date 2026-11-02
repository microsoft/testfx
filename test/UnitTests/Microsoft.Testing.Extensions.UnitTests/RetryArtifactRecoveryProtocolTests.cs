// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.UnitTests.Helpers;
using Microsoft.Testing.Platform.Extensions.ArtifactPostProcessing;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.OutputDevice;

using Moq;

using ArtifactProbe = Microsoft.Testing.Extensions.UnitTests.Helpers.RetryRecoveryProtocolFixture.ArtifactProbe;
using Attempt = Microsoft.Testing.Extensions.UnitTests.Helpers.RetryRecoveryProtocolFixture.Attempt;

namespace Microsoft.Testing.Extensions.UnitTests;

#pragma warning disable TPEXP // Assert the public artifact handoff, not private recovery state.

[TestClass]
public sealed class RetryArtifactRecoveryProtocolTests
{
    private const int MaxLineBytes = 64 * 1024;
    private const int MaxManifestBytes = 16 * 1024 * 1024;
    private const int MaxRecords = 10_000;

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Orchestrate_RecoveryReplacesKindsAndDeduplicatesPathsAcrossRealAttempts()
    {
        var fixture = new RetryRecoveryProtocolFixture(TestContext.CancellationToken);
        const string Kind = "report.é東京";
        const string CaseDistinctKind = "Report.é東京";
        ArtifactProbe typed = fixture.WatchKind(Kind);
        ArtifactProbe caseDistinct = fixture.WatchKind(CaseDistinctKind);
        ArtifactProbe legacy = fixture.WatchExtension(".legacy");
        ArtifactProbe extra = fixture.WatchExtension(".extra");
        ArtifactProbe emptyKind = fixture.WatchKind(string.Empty);
        fixture.ChildBody = async attempt =>
        {
            string[] names = ["live-first.old", "live-second.old", "case.case", "original.legacy", "empty-old.empty",
                "recovered-first.typed", "recovered-final.typed", "recovered.extra", "empty-first.empty", "empty-final.empty"];
            foreach (string name in names)
            {
                attempt.AddArtifact(attempt.PathFor(name), $"attempt {attempt.Number}: {name} Ω 東京");
            }

            await attempt.PublishAsync(attempt.PathFor(names[0]), Kind);
            await attempt.PublishAsync(attempt.PathFor(names[1]), Kind);
            await attempt.PublishAsync(attempt.PathFor(names[2]), CaseDistinctKind);
            await attempt.PublishAsync(attempt.PathFor(names[3]), null);
            await attempt.PublishAsync(attempt.PathFor(names[4]), string.Empty);
            attempt.SetManifest(
                Entry(attempt.PathFor("recovered-first.typed"), Kind) + "\r\n"
                + Entry(attempt.PathFor(Path.Combine(".", "original.legacy")), null) + "\n"
                + Entry(attempt.PathFor("recovered.extra"), null) + "\r\n"
                + Entry(attempt.PathFor("recovered.extra"), null) + "\n"
                + Entry(attempt.PathFor("empty-first.empty"), string.Empty) + "\n"
                + Entry(attempt.PathFor("recovered-final.typed"), Kind) + "\r\n"
                + Entry(attempt.PathFor("empty-final.empty"), string.Empty));
            await attempt.ResultAsync("flaky-東京", "Suite recovers 東京",
                attempt.Number == 1 ? new FailedTestNodeStateProperty("initial failure") : PassedTestNodeStateProperty.CachedInstance);
            if (attempt.Number == 1)
            {
                await attempt.ResultAsync("stable", "Stable test", PassedTestNodeStateProperty.CachedInstance);
            }

            return attempt.Number == 1 ? 2 : 0;
        };
        try
        {
            Assert.AreEqual(0, await fixture.RunAsync());

            Assert.HasCount(2, fixture.Attempts);
            Attempt first = fixture.Attempts[0];
            Attempt second = fixture.Attempts[1];
            Assert.AreNotEqual(first.PipeName, second.PipeName);
            Assert.AreNotEqual(first.ManifestPath, second.ManifestPath);
            Assert.IsEmpty(first.RetryIds);
            Assert.AreSequenceEqual(new[] { "flaky-東京" }, second.RetryIds);
            Assert.IsFalse(first.Options.ContainsKey("filter-uid"));
            Assert.AreSequenceEqual(new[] { "flaky-東京" }, second.Options["filter-uid"]);
            AssertInputs(typed, fixture.Token, (first.PathFor("recovered-final.typed"), Kind, "1"), (second.PathFor("recovered-final.typed"), Kind, "2"));
            AssertInputs(caseDistinct, fixture.Token, (first.PathFor("case.case"), CaseDistinctKind, "1"), (second.PathFor("case.case"), CaseDistinctKind, "2"));
            AssertInputs(legacy, fixture.Token, (first.PathFor("original.legacy"), null, "1"), (second.PathFor("original.legacy"), null, "2"));
            AssertInputs(extra, fixture.Token, (first.PathFor("recovered.extra"), null, "1"), (second.PathFor("recovered.extra"), null, "2"));
            AssertInputs(emptyKind, fixture.Token, (first.PathFor("empty-final.empty"), string.Empty, "1"), (second.PathFor("empty-final.empty"), string.Empty, "2"));
            ArtifactPostProcessingRunSummary summary = typed.Calls[0].Context.RunSummary!;
            Assert.AreEqual(2L, summary.TotalTests);
            Assert.AreEqual(2L, summary.PassedTests);
            Assert.AreEqual(0L, summary.FailedTests);
            Assert.AreEqual(0L, summary.SkippedTests);
            Assert.AreEqual(0, summary.ExitCode);
            Assert.AreEqual(1, summary.TestModuleCount);
            string[] text = [.. fixture.Displays.Select(value => value switch
            {
                FormattedTextOutputDeviceData formatted => formatted.Text,
                TextOutputDeviceData plain => plain.Text,
                _ => string.Empty,
            })];
            Assert.Contains("  total: 2", text);
            Assert.Contains("  retried: 1 test(s), 1 extra run(s)", text);
            Assert.Contains("  succeeded: 2", text);
            Assert.IsEmpty(fixture.Warnings);
            Assert.HasCount(10, fixture.Copies);
            foreach ((string Source, string Destination, bool Overwrite) copy in fixture.Copies)
            {
                Assert.AreEqual(second.Directory, Path.GetDirectoryName(copy.Source));
                Assert.AreEqual(Path.Combine(fixture.ResultsDirectory, Path.GetFileName(copy.Source)), copy.Destination);
                Assert.IsTrue(copy.Overwrite);
            }

            AssertSuccessfulCleanup(fixture);
        }
        finally
        {
            await fixture.CloseAsync();
        }
    }

    [TestMethod]
    public async Task Orchestrate_MalformedMissingAndOutsideEntriesPreserveValidTail()
    {
        var fixture = new RetryRecoveryProtocolFixture(TestContext.CancellationToken);
        const string Kind = "kind.été東京";
        const string InvalidPathBase64 = "!not-base64!";
        const string InvalidKindBase64 = "also-not-base64";
        ArtifactProbe valid = fixture.WatchKind(Kind);
        ArtifactProbe rejected = fixture.WatchKind("rejected");
        fixture.OnStart = attempt =>
        {
            string tail = attempt.PathFor("résultat-東京.report");
            attempt.AddArtifact(tail, "independent surviving Unicode artifact");
            attempt.SetManifest(string.Join(
                "\n",
                "missing separator",
                "\tQQ==",
                InvalidPathBase64 + "\t-",
                Convert.ToBase64String(Encoding.UTF8.GetBytes(tail)) + "\t" + InvalidKindBase64,
                Entry("\0", "rejected"),
                Entry(attempt.PathFor("absent.report"), "rejected"),
                Entry(attempt.Directory, "rejected"),
                Entry(Path.Combine(attempt.Directory + "-sibling", "secret.report"), "rejected"),
                Entry(attempt.PathFor(Path.Combine("nested", "..", "..", "escape.report")), "rejected"),
                Entry(tail, Kind)));
        };
        try
        {
            Assert.AreEqual(0, await fixture.RunAsync());

            Attempt attempt = Assert.ContainsSingle(fixture.Attempts);
            string malformed = $"Ignoring malformed recovered retry artifact manifest entry in '{attempt.ManifestPath}'";
            string escaped = attempt.PathFor(Path.Combine("nested", "..", "..", "escape.report"));
            Assert.AreSequenceEqual(
                new[]
            {
                malformed + ".", malformed + ".",
                malformed + ": " + Base64Error(InvalidPathBase64),
                malformed + ": " + Base64Error(InvalidKindBase64),
                malformed + ": " + InvalidPathError().Message,
                $"Ignoring recovered retry artifact '{attempt.PathFor("absent.report")}' because it does not exist.",
                OutsideAttempt(attempt.Directory, attempt),
                OutsideAttempt(Path.Combine(attempt.Directory + "-sibling", "secret.report"), attempt),
                OutsideAttempt(escaped, attempt),
            }, fixture.Warnings);
            AssertInputs(valid, fixture.Token, (attempt.PathFor("résultat-東京.report"), Kind, "1"));
            Assert.IsEmpty(rejected.Calls);
            foreach (string path in new[] { attempt.Directory, Path.Combine(attempt.Directory + "-sibling", "secret.report"), Path.GetFullPath(escaped) })
            {
                fixture.FileSystem.Verify(value => value.ExistFile(path), Times.Never);
            }

            AssertSuccessfulCleanup(fixture);
        }
        finally
        {
            await fixture.CloseAsync();
        }
    }

    [TestMethod]
    [DataRow(false, DisplayName = "RetryRecovery_OversizedPath")]
    [DataRow(true, DisplayName = "RetryRecovery_OversizedKind")]
    public async Task Orchestrate_DecodedFieldLimitsRejectOversizedEntryAndContinue(bool oversizedKind)
    {
        var fixture = new RetryRecoveryProtocolFixture(TestContext.CancellationToken);
        string boundaryKind = new('k', 1024);
        ArtifactProbe boundary = fixture.WatchKind(boundaryKind);
        ArtifactProbe rejected = fixture.WatchKind(oversizedKind ? boundaryKind + "x" : "oversized-path");
        fixture.OnStart = attempt =>
        {
            string invalidPath = oversizedKind ? attempt.PathFor("oversized.report") : new string('p', (32 * 1024) + 1);
            string kind = oversizedKind ? boundaryKind + "x" : "oversized-path";
            if (oversizedKind)
            {
                attempt.AddArtifact(invalidPath, "must not enter post-processing");
            }

            string validPath = attempt.PathFor("boundary.report");
            attempt.AddArtifact(validPath, "kind length exactly 1024");
            attempt.SetManifest(Entry(invalidPath, kind) + "\r\n" + Entry(validPath, boundaryKind));
        };
        try
        {
            Assert.AreEqual(0, await fixture.RunAsync());

            Attempt attempt = Assert.ContainsSingle(fixture.Attempts);
            AssertInputs(boundary, fixture.Token, (attempt.PathFor("boundary.report"), boundaryKind, "1"));
            Assert.IsEmpty(rejected.Calls);
            Assert.AreSequenceEqual(new[] { $"Ignoring oversized recovered retry artifact manifest entry in '{attempt.ManifestPath}'." }, fixture.Warnings);
            string unprobed = oversizedKind ? attempt.PathFor("oversized.report") : new string('p', (32 * 1024) + 1);
            fixture.FileSystem.Verify(value => value.ExistFile(unprobed), Times.Never);
            AssertSuccessfulCleanup(fixture);
        }
        finally
        {
            await fixture.CloseAsync();
        }
    }

    [TestMethod]
    [DataRow(false, DisplayName = "RetryRecovery_LineExactly64KiB")]
    [DataRow(true, DisplayName = "RetryRecovery_LineOver64KiB")]
    public async Task Orchestrate_LineLimitPreservesPrefixAndHonorsExactBoundary(bool overflow)
    {
        var fixture = new RetryRecoveryProtocolFixture(TestContext.CancellationToken);
        ArtifactProbe prefix = fixture.WatchKind("prefix");
        ArtifactProbe boundary = fixture.WatchKind("boundary");
        ArtifactProbe tail = fixture.WatchKind("tail");
        fixture.OnStart = attempt =>
        {
            foreach (string name in new[] { "prefix.report", "boundary.report", "tail.report" })
            {
                attempt.AddArtifact(attempt.PathFor(name), name + " independent payload");
            }

            attempt.SetManifest(Entry(attempt.PathFor("prefix.report"), "prefix") + "\n"
                + PaddedEntry(attempt.PathFor("boundary.report"), "boundary", MaxLineBytes + (overflow ? 1 : 0)) + "\n"
                + Entry(attempt.PathFor("tail.report"), "tail"));
        };
        try
        {
            Assert.AreEqual(0, await fixture.RunAsync());

            Attempt attempt = Assert.ContainsSingle(fixture.Attempts);
            AssertInputs(prefix, fixture.Token, (attempt.PathFor("prefix.report"), "prefix", "1"));
            if (overflow)
            {
                Assert.IsEmpty(boundary.Calls);
                Assert.IsEmpty(tail.Calls);
                Assert.AreSequenceEqual(new[] { SizeWarning(attempt) }, fixture.Warnings);
            }
            else
            {
                AssertInputs(boundary, fixture.Token, (attempt.PathFor("boundary.report"), "boundary", "1"));
                AssertInputs(tail, fixture.Token, (attempt.PathFor("tail.report"), "tail", "1"));
                Assert.IsEmpty(fixture.Warnings);
            }

            AssertSuccessfulCleanup(fixture);
        }
        finally
        {
            await fixture.CloseAsync();
        }
    }

    [TestMethod]
    [DataRow(false, DisplayName = "RetryRecovery_BytesExactly16MiB")]
    [DataRow(true, DisplayName = "RetryRecovery_BytesOver16MiB")]
    public async Task Orchestrate_ByteLimitPreservesLastWholeRecordAndRejectsOverflow(bool overflow)
    {
        var fixture = new RetryRecoveryProtocolFixture(TestContext.CancellationToken);
        ArtifactProbe filler = fixture.WatchExtension(".filler");
        ArtifactProbe boundary = fixture.WatchKind("byte-boundary");
        ArtifactProbe beyond = fixture.WatchKind("beyond");
        fixture.OnStart = attempt =>
        {
            attempt.AddArtifact(attempt.PathFor("one.filler"), "repeated null-kind record");
            attempt.AddArtifact(attempt.PathFor("boundary.report"), "last complete record at the byte limit");
            attempt.AddArtifact(attempt.PathFor("beyond.report"), "must not be recovered past the byte limit");
            string paddedFiller = PaddedEntry(attempt.PathFor("one.filler"), null, MaxLineBytes - 1) + "\n";
            var manifest = new StringBuilder(MaxManifestBytes + 1024);
            for (int i = 0; i < 255; i++)
            {
                manifest.Append(paddedFiller);
            }

            manifest.Append(PaddedEntry(attempt.PathFor("boundary.report"), "byte-boundary", MaxLineBytes - 1)).Append('\n');
            Assert.AreEqual(MaxManifestBytes, manifest.Length, "Every ASCII record includes its counted LF byte.");
            if (overflow)
            {
                manifest.Append(Entry(attempt.PathFor("beyond.report"), "beyond"));
            }

            attempt.SetManifest(manifest.ToString());
        };
        try
        {
            Assert.AreEqual(0, await fixture.RunAsync());

            Attempt attempt = Assert.ContainsSingle(fixture.Attempts);
            AssertInputs(filler, fixture.Token, (attempt.PathFor("one.filler"), null, "1"));
            AssertInputs(boundary, fixture.Token, (attempt.PathFor("boundary.report"), "byte-boundary", "1"));
            Assert.IsEmpty(beyond.Calls);
            Assert.AreSequenceEqual(overflow ? new[] { SizeWarning(attempt) } : [], fixture.Warnings);
            AssertSuccessfulCleanup(fixture);
        }
        finally
        {
            await fixture.CloseAsync();
        }
    }

    [TestMethod]
    [DataRow(false, DisplayName = "RetryRecovery_RecordsBelowLimit")]
    [DataRow(true, DisplayName = "RetryRecovery_RecordsAtLimit")]
    public async Task Orchestrate_RecordLimitRetainsLastPermittedRecordWithoutReadingNext(bool atLimit)
    {
        var fixture = new RetryRecoveryProtocolFixture(TestContext.CancellationToken);
        ArtifactProbe filler = fixture.WatchExtension(".filler");
        ArtifactProbe last = fixture.WatchKind("last");
        ArtifactProbe beyond = fixture.WatchKind("beyond");
        fixture.OnStart = attempt =>
        {
            attempt.AddArtifact(attempt.PathFor("one.filler"), "duplicate filler is not a new artifact");
            attempt.AddArtifact(attempt.PathFor("last.report"), "last permitted record");
            attempt.AddArtifact(attempt.PathFor("beyond.report"), "record 10001");
            var manifest = new StringBuilder();
            int fillers = atLimit ? MaxRecords - 1 : MaxRecords - 2;
            string fillerLine = Entry(attempt.PathFor("one.filler"), null) + "\n";
            for (int i = 0; i < fillers; i++)
            {
                manifest.Append(fillerLine);
            }

            manifest.Append(Entry(attempt.PathFor("last.report"), "last"));
            if (atLimit)
            {
                manifest.Append('\n').Append(Entry(attempt.PathFor("beyond.report"), "beyond"));
            }

            attempt.SetManifest(manifest.ToString());
        };
        try
        {
            Assert.AreEqual(0, await fixture.RunAsync());

            Attempt attempt = Assert.ContainsSingle(fixture.Attempts);
            AssertInputs(filler, fixture.Token, (attempt.PathFor("one.filler"), null, "1"));
            AssertInputs(last, fixture.Token, (attempt.PathFor("last.report"), "last", "1"));
            Assert.IsEmpty(beyond.Calls);
            string[] expected = atLimit
                ? [$"Stopped reading recovered retry artifact manifest '{attempt.ManifestPath}' after the maximum of 10000 records."]
                : [];
            Assert.AreSequenceEqual(expected, fixture.Warnings);
            fixture.FileSystem.Verify(value => value.ExistFile(attempt.PathFor("beyond.report")), Times.Never);
            AssertSuccessfulCleanup(fixture);
        }
        finally
        {
            await fixture.CloseAsync();
        }
    }

    [TestMethod]
    [DataRow("open-io", DisplayName = "RetryRecovery_OpenIo")]
    [DataRow("open-denied", DisplayName = "RetryRecovery_OpenDenied")]
    [DataRow("read-io", DisplayName = "RetryRecovery_ReadIo")]
    [DataRow("delete-io", DisplayName = "RetryRecovery_DeleteIo")]
    [DataRow("delete-denied", DisplayName = "RetryRecovery_DeleteDenied")]
    public async Task Orchestrate_ManifestIoFailuresWarnAndPreserveAvailableArtifacts(string partition)
    {
        var fixture = new RetryRecoveryProtocolFixture(TestContext.CancellationToken);
        bool openFailure = partition.StartsWith("open-", StringComparison.Ordinal);
        bool deleteFailure = partition.StartsWith("delete-", StringComparison.Ordinal);
        Exception original = partition.EndsWith("denied", StringComparison.Ordinal)
            ? new UnauthorizedAccessException("owned manifest access denied")
            : new IOException("owned manifest I/O failure");
        ArtifactProbe live = fixture.WatchKind("live");
        ArtifactProbe recovered = fixture.WatchKind("recovered");
        ArtifactProbe tail = fixture.WatchKind("tail");
        fixture.OnStart = attempt =>
        {
            foreach (string name in new[] { "live.report", "prefix.report", "tail.report" })
            {
                attempt.AddArtifact(attempt.PathFor(name), name + " immutable input");
            }

            string prefix = Entry(attempt.PathFor("prefix.report"), "recovered") + "\n";
            attempt.SetManifest(prefix + Entry(attempt.PathFor("tail.report"), "tail"));
            if (openFailure)
            {
                fixture.ManifestOpenFailure = original;
            }
            else if (deleteFailure)
            {
                fixture.ManifestDeleteFailure = original;
            }
            else
            {
                fixture.ManifestReadFailure = original;
                fixture.FailReadAfterBytes = Encoding.UTF8.GetByteCount(prefix);
            }
        };
        fixture.ChildBody = async attempt =>
        {
            await attempt.PublishAsync(attempt.PathFor("live.report"), "live");
            return await PassedAttemptAsync(attempt);
        };
        try
        {
            Assert.AreEqual(0, await fixture.RunAsync());

            Attempt attempt = Assert.ContainsSingle(fixture.Attempts);
            AssertInputs(live, fixture.Token, (attempt.PathFor("live.report"), "live", "1"));
            if (openFailure)
            {
                Assert.IsEmpty(recovered.Calls);
                Assert.IsEmpty(tail.Calls);
                Assert.IsNull(attempt.ManifestStream);
            }
            else
            {
                AssertInputs(recovered, fixture.Token, (attempt.PathFor("prefix.report"), "recovered", "1"));
                Assert.IsTrue(attempt.ManifestStream!.Disposed);
                AssertBefore(fixture, "dispose-read:" + attempt.ManifestPath, "delete-attempt:" + attempt.ManifestPath);
                if (deleteFailure)
                {
                    AssertInputs(tail, fixture.Token, (attempt.PathFor("tail.report"), "tail", "1"));
                }
                else
                {
                    Assert.IsEmpty(tail.Calls, "An I/O error must not admit records after the last complete prefix.");
                }
            }

            Assert.AreSequenceEqual(
                new[]
            {
                deleteFailure
                    ? $"Failed to delete recovered retry artifact manifest '{attempt.ManifestPath}': {original}"
                    : $"Failed to read recovered retry artifact manifest '{attempt.ManifestPath}': {original}",
            }, fixture.Warnings);
            Assert.AreEqual(deleteFailure, fixture.Files.ContainsKey(attempt.ManifestPath));
            if (deleteFailure)
            {
                string expected = Entry(attempt.PathFor("prefix.report"), "recovered") + "\n" + Entry(attempt.PathFor("tail.report"), "tail");
                Assert.AreSequenceEqual(Encoding.UTF8.GetBytes(expected), fixture.Files[attempt.ManifestPath]);
            }

            fixture.FileSystem.Verify(value => value.DeleteFile(attempt.ManifestPath), Times.Once);
            AssertBefore(fixture, "dispose-process:1", "delete:" + ArgumentsPath(attempt));
            AssertBefore(fixture, "delete-attempt:" + attempt.ManifestPath, "process-artifacts");
            attempt.Process.Verify(value => value.Kill(), Times.Never);
            attempt.Process.Verify(value => value.Dispose(), Times.Once);
            fixture.AssertInputsPreserved();
        }
        finally
        {
            await fixture.CloseAsync();
        }
    }

    [TestMethod]
    [DataRow(true, DisplayName = "RetryRecovery_ReadCancellation")]
    [DataRow(false, DisplayName = "RetryRecovery_ReadUnexpected")]
    public async Task Orchestrate_UnhandledManifestFailurePropagatesAfterCleanup(bool canceled)
    {
        var fixture = new RetryRecoveryProtocolFixture(TestContext.CancellationToken);
        using var invocation = CancellationTokenSource.CreateLinkedTokenSource(fixture.Token);
        Exception original = canceled
            ? new OperationCanceledException("manifest read canceled", null, invocation.Token)
            : new InvalidOperationException("unexpected manifest reader failure");
        fixture.ManifestReadFailure = original;
        fixture.BeforeReadFailure = canceled ? () => invocation.Cancel() : null;
        ArtifactProbe probe = fixture.WatchKind("report");
        fixture.OnStart = attempt =>
        {
            attempt.AddArtifact(attempt.PathFor("report.bin"), "untouched input after a failing reader");
            attempt.SetManifest(Entry(attempt.PathFor("report.bin"), "report"));
        };
        try
        {
            Exception actual = canceled
                ? await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => fixture.RunAsync(invocation.Token))
                : await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => fixture.RunAsync(invocation.Token));

            Assert.AreSame(original, actual);
            if (canceled)
            {
                Assert.IsTrue(invocation.IsCancellationRequested);
                Assert.AreEqual(invocation.Token, ((OperationCanceledException)actual).CancellationToken);
            }

            Attempt attempt = Assert.ContainsSingle(fixture.Attempts);
            Assert.IsTrue(attempt.ManifestStream!.Disposed);
            Assert.IsFalse(fixture.Files.ContainsKey(attempt.ManifestPath));
            Assert.IsEmpty(fixture.Warnings);
            Assert.IsEmpty(probe.Calls);
            Assert.IsEmpty(fixture.Displays);
            Assert.IsEmpty(fixture.Copies);
            AssertBefore(fixture, "delete:" + ArgumentsPath(attempt), "open:" + attempt.ManifestPath);
            AssertBefore(fixture, "dispose-read:" + attempt.ManifestPath, "delete:" + attempt.ManifestPath);
            fixture.FileSystem.Verify(value => value.DeleteFile(attempt.ManifestPath), Times.Once);
            attempt.Process.Verify(value => value.Kill(), Times.Never);
            attempt.Process.Verify(value => value.Dispose(), Times.Once);
        }
        finally
        {
            await fixture.CloseAsync();
        }
    }

    [TestMethod]
    public async Task Orchestrate_ControllerRootsRetainMappedPrimaryAndDiagnosticArtifactsOnly()
    {
        var fixture = new RetryRecoveryProtocolFixture(TestContext.CancellationToken);
        ArtifactProbe primary = fixture.WatchKind("live-primary");
        ArtifactProbe diagnostic = fixture.WatchKind("live-diagnostic");
        ArtifactProbe recovered = fixture.WatchKind("recovered");
        ArtifactProbe rejectedChild = fixture.WatchKind("rejected-child");
        ArtifactProbe rejectedSibling = fixture.WatchKind("rejected-sibling");
        ArtifactProbe rejectedTraversal = fixture.WatchKind("rejected-traversal");
        string source = Path.Combine(fixture.Root, "child-results");
        string diagnosticSource = Path.Combine(fixture.Root, "child-diagnostics");
        string diagnosticDestination = Path.Combine(fixture.Root, "controller-diagnostics");
        fixture.OnStart = attempt =>
        {
            string allowed = attempt.PathFor("allowed");
            fixture.EnvironmentValues[RetryRecoveryProtocolFixture.RootsConfiguredVariable] = "1";
            fixture.EnvironmentValues[RetryRecoveryProtocolFixture.ArtifactDestinationVariable] = Path.Combine(allowed, ".") + Path.DirectorySeparatorChar;
            fixture.EnvironmentValues[RetryRecoveryProtocolFixture.DiagnosticDestinationVariable] = diagnosticDestination + Path.DirectorySeparatorChar;
            attempt.EnvironmentValues[RetryRecoveryProtocolFixture.ArtifactSourceVariable] = source;
            attempt.EnvironmentValues[RetryRecoveryProtocolFixture.ArtifactDestinationVariable] = allowed;
            attempt.EnvironmentValues[RetryRecoveryProtocolFixture.DiagnosticSourceVariable] = diagnosticSource;
            attempt.EnvironmentValues[RetryRecoveryProtocolFixture.DiagnosticDestinationVariable] = diagnosticDestination;
            attempt.AddArtifact(Path.Combine(source, "live.primary"), "primary source bytes");
            attempt.AddArtifact(Path.Combine(allowed, "live.primary"), "primary mapped bytes");
            attempt.AddArtifact(Path.Combine(diagnosticSource, "live.diag"), "diagnostic source bytes");
            attempt.AddArtifact(Path.Combine(diagnosticDestination, "live.diag"), "diagnostic mapped bytes");
            attempt.AddArtifact(Path.Combine(source + "-sibling", "rejected.report"), "unmapped source bytes");
            attempt.AddArtifact(Path.Combine(allowed, "recovered.report"), "allowed recovered bytes");
            attempt.AddArtifact(Path.Combine(allowed + "-sibling", "rejected.report"), "controller prefix-sibling bytes");
            attempt.AddArtifact(attempt.PathFor("outside.report"), "controller traversal-escape bytes");
            attempt.SetManifest(Entry(Path.Combine(allowed, "recovered.report"), "recovered") + "\r\n"
                + Entry(Path.Combine(allowed + "-sibling", "rejected.report"), "rejected-sibling") + "\n"
                + Entry(Path.Combine(allowed, "..", "outside.report"), "rejected-traversal"));
        };
        fixture.ChildBody = async attempt =>
        {
            await attempt.PublishAsync(Path.Combine(source, "live.primary"), "live-primary");
            await attempt.PublishAsync(Path.Combine(diagnosticSource, "live.diag"), "live-diagnostic");
            await attempt.PublishAsync(Path.Combine(source + "-sibling", "rejected.report"), "rejected-child");
            return await PassedAttemptAsync(attempt);
        };
        try
        {
            Assert.AreEqual(0, await fixture.RunAsync());

            Attempt attempt = Assert.ContainsSingle(fixture.Attempts);
            string allowed = attempt.PathFor("allowed");
            string snapshot = Path.Combine(attempt.RetryRoot, "Artifacts", "1", "0001-live.diag");
            AssertInputs(primary, fixture.Token, (Path.Combine(allowed, "live.primary"), "live-primary", "1"));
            AssertInputs(diagnostic, fixture.Token, (snapshot, "live-diagnostic", "1"));
            AssertInputs(recovered, fixture.Token, (Path.Combine(allowed, "recovered.report"), "recovered", "1"));
            Assert.IsEmpty(rejectedChild.Calls);
            Assert.IsEmpty(rejectedSibling.Calls);
            Assert.IsEmpty(rejectedTraversal.Calls);
            Assert.AreSequenceEqual(
                new[]
            {
                OutsideRoots(Path.Combine(source + "-sibling", "rejected.report")),
                OutsideRoots(attempt.PathFor("outside.report")),
                OutsideRoots(Path.Combine(allowed + "-sibling", "rejected.report")),
            }, fixture.Warnings);
            Assert.AreSequenceEqual(
                new[]
            {
                (Path.Combine(diagnosticDestination, "live.diag"), snapshot, true),
                (Path.Combine(allowed + "-sibling", "rejected.report"), Path.Combine(fixture.ResultsDirectory, "allowed-sibling", "rejected.report"), true),
                (Path.Combine(allowed, "live.primary"), Path.Combine(fixture.ResultsDirectory, "allowed", "live.primary"), true),
                (Path.Combine(allowed, "recovered.report"), Path.Combine(fixture.ResultsDirectory, "allowed", "recovered.report"), true),
                (attempt.PathFor("outside.report"), Path.Combine(fixture.ResultsDirectory, "outside.report"), true),
                (snapshot, Path.Combine(diagnosticDestination, "live.diag"), true),
            }, fixture.Copies);
            Assert.AreSequenceEqual(Encoding.UTF8.GetBytes("diagnostic mapped bytes"), fixture.Files[snapshot]);
            fixture.AssertInputsPreserved();
            AssertSuccessfulCleanup(fixture);
        }
        finally
        {
            await fixture.CloseAsync();
        }
    }

    [TestMethod]
    [DataRow(null, 0, false, DisplayName = "RetryRecovery_RootFlagAbsent")]
    [DataRow("true", 0, false, DisplayName = "RetryRecovery_RootFlagOther")]
    [DataRow("1", 0, true, DisplayName = "RetryRecovery_RootsNull")]
    [DataRow("1", 1, true, DisplayName = "RetryRecovery_RootsEmpty")]
    [DataRow("1", 2, true, DisplayName = "RetryRecovery_RootItself")]
    public async Task Orchestrate_ControllerRootFlagControlsFailClosedEmptyRoots(string? flag, int roots, bool rejected)
    {
        var fixture = new RetryRecoveryProtocolFixture(TestContext.CancellationToken);
        string external = Path.Combine(fixture.Root, "external", "evidence.report");
        ArtifactProbe probe = fixture.WatchKind("external");
        fixture.EnvironmentValues[RetryRecoveryProtocolFixture.RootsConfiguredVariable] = flag;
        if (roots > 0)
        {
            fixture.EnvironmentValues[RetryRecoveryProtocolFixture.ArtifactDestinationVariable] = roots == 1 ? string.Empty : external;
            fixture.EnvironmentValues[RetryRecoveryProtocolFixture.DiagnosticDestinationVariable] = string.Empty;
        }

        fixture.OnStart = attempt =>
        {
            attempt.AddArtifact(external, "external immutable input");
            attempt.SetManifest(string.Empty);
        };
        fixture.ChildBody = async attempt =>
        {
            await attempt.PublishAsync(external, "external");
            return await PassedAttemptAsync(attempt);
        };
        try
        {
            Assert.AreEqual(0, await fixture.RunAsync());

            Attempt attempt = Assert.ContainsSingle(fixture.Attempts);
            if (rejected)
            {
                Assert.IsEmpty(probe.Calls);
                Assert.IsEmpty(fixture.Copies);
                Assert.AreSequenceEqual(new[] { OutsideRoots(external) }, fixture.Warnings);
            }
            else
            {
                string snapshot = Path.Combine(attempt.RetryRoot, "Artifacts", "1", "0000-evidence.report");
                AssertInputs(probe, fixture.Token, (snapshot, "external", "1"));
                Assert.AreSequenceEqual(new[] { (external, snapshot, true), (snapshot, external, true) }, fixture.Copies);
                Assert.IsEmpty(fixture.Warnings);
            }

            Times reads = flag == "1" ? Times.Once() : Times.Never();
            fixture.Environment.Verify(value => value.GetEnvironmentVariable(RetryRecoveryProtocolFixture.ArtifactDestinationVariable), reads);
            fixture.Environment.Verify(value => value.GetEnvironmentVariable(RetryRecoveryProtocolFixture.DiagnosticDestinationVariable), reads);
            AssertSuccessfulCleanup(fixture, hasProcessor: !rejected);
        }
        finally
        {
            await fixture.CloseAsync();
        }
    }

    [TestMethod]
    public async Task Orchestrate_InvalidControllerRootFailsAfterManifestCleanup()
    {
        var fixture = new RetryRecoveryProtocolFixture(TestContext.CancellationToken);
        ArtifactProbe probe = fixture.WatchKind("report");
        fixture.EnvironmentValues[RetryRecoveryProtocolFixture.RootsConfiguredVariable] = "1";
        fixture.EnvironmentValues[RetryRecoveryProtocolFixture.ArtifactDestinationVariable] = "\0";
        fixture.OnStart = attempt =>
        {
            attempt.AddArtifact(attempt.PathFor("report.bin"), "retained original despite invalid root configuration");
            attempt.SetManifest(Entry(attempt.PathFor("report.bin"), "report"));
        };
        try
        {
            ArgumentException error = await Assert.ThrowsExactlyAsync<ArgumentException>(() => fixture.RunAsync());

            Assert.AreEqual(InvalidPathError().Message, error.Message);
            Attempt attempt = Assert.ContainsSingle(fixture.Attempts);
            Assert.IsTrue(attempt.ManifestStream!.Disposed);
            Assert.IsFalse(fixture.Files.ContainsKey(attempt.ManifestPath));
            Assert.IsEmpty(probe.Calls);
            Assert.IsEmpty(fixture.Warnings);
            Assert.IsEmpty(fixture.Copies);
            AssertBefore(fixture, "dispose-read:" + attempt.ManifestPath, "delete:" + attempt.ManifestPath);
            attempt.Process.Verify(value => value.Kill(), Times.Never);
            attempt.Process.Verify(value => value.Dispose(), Times.Once);
        }
        finally
        {
            await fixture.CloseAsync();
        }
    }

    [TestMethod]
    public async Task Orchestrate_CancellationBeforeHandshakeKillsAndDrainsFakeChild()
    {
        var fixture = new RetryRecoveryProtocolFixture(TestContext.CancellationToken) { ExpectChildCancellation = true };
        using var invocation = CancellationTokenSource.CreateLinkedTokenSource(fixture.Token);
        fixture.OnStart = attempt =>
        {
            attempt.SetManifest("owned manifest from a canceled launch");
            invocation.Cancel();
        };
        ArtifactProbe probe = fixture.WatchKind("report");
        try
        {
            OperationCanceledException error = await Assert.ThrowsAsync<OperationCanceledException>(() => fixture.RunAsync(invocation.Token));

            Assert.IsTrue(invocation.IsCancellationRequested);
            Assert.IsTrue(error.CancellationToken.IsCancellationRequested, "The runner exposes its canceled linked connection token.");
            Attempt attempt = Assert.ContainsSingle(fixture.Attempts);
            Assert.AreEqual(attempt.Token, attempt.ChildCancellation!.CancellationToken);
            Assert.IsEmpty(attempt.RetryIds);
            Assert.IsNull(attempt.ManifestStream);
            Assert.IsFalse(fixture.Files.ContainsKey(attempt.ManifestPath));
            Assert.IsEmpty(probe.Calls);
            Assert.IsEmpty(fixture.Warnings);
            Assert.IsEmpty(fixture.Copies);
            attempt.Process.Verify(value => value.Kill(), Times.Once);
            attempt.Process.Verify(value => value.WaitForExitAsync(It.IsAny<CancellationToken>()), Times.Once);
            attempt.Process.Verify(value => value.Dispose(), Times.Once);
            AssertBefore(fixture, "kill-process:1", "wait-process:1");
            AssertBefore(fixture, "waited-process:1", "dispose-process:1");
            AssertBefore(fixture, "dispose-process:1", "delete:" + attempt.ManifestPath);
            AssertBefore(fixture, "delete:" + attempt.ManifestPath, "delete:" + ArgumentsPath(attempt));
            fixture.FileSystem.Verify(
                value => value.NewFileStream(
                attempt.ManifestPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite), Times.Never);
            fixture.FileSystem.Verify(value => value.DeleteFile(attempt.ManifestPath), Times.Once);
            fixture.AssertInputsPreserved();
        }
        finally
        {
            await fixture.CloseAsync();
        }
    }

    [TestMethod]
    public async Task Orchestrate_LaunchFailureDeletesOwnedManifestAndGeneratedArguments()
    {
        var original = new InvalidOperationException("mock process handler refused the launch");
        var fixture = new RetryRecoveryProtocolFixture(TestContext.CancellationToken)
        {
            LaunchFailure = original,
            OnStart = attempt => attempt.SetManifest("owned manifest from a failed launch"),
        };
        ArtifactProbe probe = fixture.WatchKind("report");
        try
        {
            InvalidOperationException actual = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => fixture.RunAsync());

            Assert.AreSame(original, actual);
            Attempt attempt = Assert.ContainsSingle(fixture.Attempts);
            Assert.IsNull(attempt.ManifestStream);
            Assert.IsFalse(fixture.Files.ContainsKey(attempt.ManifestPath));
            Assert.IsFalse(fixture.Files.ContainsKey(ArgumentsPath(attempt)));
            Assert.IsEmpty(probe.Calls);
            Assert.IsEmpty(fixture.Warnings);
            Assert.IsEmpty(fixture.Displays);
            Assert.IsEmpty(fixture.Copies);
            AssertBefore(fixture, "delete:" + attempt.ManifestPath, "delete:" + ArgumentsPath(attempt));
            fixture.FileSystem.Verify(value => value.DeleteFile(attempt.ManifestPath), Times.Once);
            fixture.FileSystem.Verify(
                value => value.NewFileStream(
                attempt.ManifestPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite), Times.Never);
            attempt.Process.VerifyNoOtherCalls();
            fixture.AssertInputsPreserved();
        }
        finally
        {
            await fixture.CloseAsync();
        }
    }

    // Encode fixture bytes independently; using the production manifest writer would hide wire-contract errors.
    private static string Entry(string path, string? kind)
        => Convert.ToBase64String(Encoding.UTF8.GetBytes(path)) + "\t"
            + (kind is null ? "-" : Convert.ToBase64String(Encoding.UTF8.GetBytes(kind)));

    private static string PaddedEntry(string path, string? kind, int bytes)
    {
        string encodedPath = Convert.ToBase64String(Encoding.UTF8.GetBytes(path));
        string encodedKind = kind is null ? "-" : Convert.ToBase64String(Encoding.UTF8.GetBytes(kind));
        return encodedPath + new string(' ', bytes - encodedPath.Length - encodedKind.Length - 1) + "\t" + encodedKind;
    }

    private static string Base64Error(string value)
        => Assert.ThrowsExactly<FormatException>(() => Convert.FromBase64String(value)).Message;

    private static ArgumentException InvalidPathError()
        => Assert.ThrowsExactly<ArgumentException>(() => Path.GetFullPath("\0"));

    private static string OutsideAttempt(string path, Attempt attempt)
        => $"Ignoring recovered retry artifact '{path}' because it is outside the retry attempt directory '{attempt.Directory}'.";

    private static string OutsideRoots(string path)
        => $"Ignoring retry artifact '{path}' because it is outside the configured AppContainer artifact roots.";

    private static string SizeWarning(Attempt attempt)
        => $"Stopped reading recovered retry artifact manifest '{attempt.ManifestPath}' because it exceeded a configured size limit.";

    private static string ArgumentsPath(Attempt attempt)
        => Path.Combine(attempt.RetryRoot, $"retry-arguments-{attempt.Number}.rsp");

    private static async Task<int> PassedAttemptAsync(Attempt attempt)
    {
        await attempt.ResultAsync("stable-1", "Stable one", PassedTestNodeStateProperty.CachedInstance);
        await attempt.ResultAsync("stable-2", "Stable two", PassedTestNodeStateProperty.CachedInstance);
        return 0;
    }

    private static void AssertInputs(
        ArtifactProbe probe, CancellationToken token, params (string Path, string? Kind, string Attempt)[] expected)
    {
        (InputArtifact[] Inputs, string Directory, ArtifactPostProcessingContext Context, CancellationToken Token) call = Assert.ContainsSingle(probe.Calls);
        Assert.AreSequenceEqual(expected.Select(value => value.Path), call.Inputs.Select(value => value.Path));
        Assert.AreSequenceEqual(expected.Select(value => value.Kind), call.Inputs.Select(value => value.Kind));
        Assert.AreSequenceEqual(expected.Select(value => value.Attempt), call.Inputs.Select(value => value.ExecutionId));
        Assert.IsTrue(call.Inputs.All(value => value.ProducingTestModule is null && value.TargetFramework is null && value.Architecture is null));
        Assert.AreEqual(ArtifactPostProcessingMode.RetryAttempts, call.Context.Mode);
        Assert.AreEqual(ArtifactPostProcessingTruncationReason.None, call.Context.TruncationReason);
        Assert.AreEqual(token, call.Token);
    }

    private static void AssertSuccessfulCleanup(RetryRecoveryProtocolFixture fixture, bool hasProcessor = true)
    {
        foreach (Attempt attempt in fixture.Attempts)
        {
            Assert.IsTrue(attempt.ManifestStream!.Disposed);
            Assert.IsFalse(fixture.Files.ContainsKey(attempt.ManifestPath));
            Assert.IsFalse(fixture.Files.ContainsKey(ArgumentsPath(attempt)));
            fixture.FileSystem.Verify(
                value => value.NewFileStream(
                attempt.ManifestPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite), Times.Once);
            fixture.FileSystem.Verify(value => value.DeleteFile(attempt.ManifestPath), Times.Once);
            fixture.FileSystem.Verify(value => value.DeleteFile(ArgumentsPath(attempt)), Times.Once);
            attempt.Process.Verify(value => value.Kill(), Times.Never);
            attempt.Process.Verify(value => value.WaitForExitAsync(CancellationToken.None), Times.Once);
            attempt.Process.Verify(value => value.Dispose(), Times.Once);
            AssertBefore(fixture, "dispose-child:" + attempt.Number, "waited-process:" + attempt.Number);
            AssertBefore(fixture, "waited-process:" + attempt.Number, "dispose-process:" + attempt.Number);
            AssertBefore(fixture, "dispose-process:" + attempt.Number, "delete:" + ArgumentsPath(attempt));
            AssertBefore(fixture, "delete:" + ArgumentsPath(attempt), "open:" + attempt.ManifestPath);
            AssertBefore(fixture, "dispose-read:" + attempt.ManifestPath, "delete:" + attempt.ManifestPath);
            if (hasProcessor)
            {
                AssertBefore(fixture, "delete:" + attempt.ManifestPath, "process-artifacts");
            }
        }

        fixture.FileSystem.Verify(value => value.DeleteFile(fixture.OriginalResponsePath), Times.Never);
        fixture.ProcessHandler.Verify(value => value.Start(It.IsAny<ProcessStartInfo>()), Times.Exactly(fixture.Attempts.Count));
        fixture.AssertInputsPreserved();
    }

    private static void AssertBefore(RetryRecoveryProtocolFixture fixture, string earlier, string later)
    {
        string[] events = fixture.Events.ToArray();
        Assert.Contains(earlier, events);
        Assert.Contains(later, events);
        Assert.IsLessThan(Array.IndexOf(events, later), Array.IndexOf(events, earlier), $"{earlier} must precede {later}.");
    }
}
