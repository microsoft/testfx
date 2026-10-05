namespace UnskipClosedTests.Tool;

internal static partial class ApplyEngine
{
    private sealed record SourceEdit(string Path, int Start, int Length, Candidate Candidate);
    private sealed record VerificationOutcome(bool Success, string Reason);

    public static async Task<ApplyResult> ApplyAsync(
        string requestedRoot,
        ToolConfig config,
        string manifestPath,
        string agentOutputPath,
        string? evidencePath)
    {
        Manifest requestedManifest = ManifestValidator.Read(manifestPath);
        if (!string.Equals(requestedManifest.ConfigDigest, ConfigLoader.Digest(config), StringComparison.Ordinal))
        {
            throw new ContractException("Apply config does not match manifest config_digest.");
        }

        List<string> selectedIds = ReadAgentSelection(agentOutputPath, requestedManifest.ManifestDigest);
        Dictionary<string, Candidate> requestedCandidates = requestedManifest.Candidates
            .ToDictionary(static candidate => candidate.CandidateId, StringComparer.Ordinal);
        foreach (string candidateId in selectedIds)
        {
            if (!requestedCandidates.TryGetValue(candidateId, out Candidate? candidate))
            {
                throw new ContractException($"Agent output selected unknown candidate_id '{candidateId}'.");
            }

            if (!candidate.Decision.Eligible)
            {
                throw new ContractException($"Agent output selected ineligible candidate_id '{candidateId}'.");
            }
        }

        GitRepository repository = GitRepository.Open(requestedRoot, requestedManifest.Repository);
        if (!string.Equals(repository.Commit, requestedManifest.SourceCommit, StringComparison.Ordinal))
        {
            throw new ContractException(
                $"Manifest source_commit {requestedManifest.SourceCommit} is stale; checked out commit is {repository.Commit}.");
        }

        Manifest freshInventory = InventoryEngine.Create(repository.Root, requestedManifest.Repository, config);
        Manifest freshResolved = await IssueResolver.ResolveAsync(freshInventory, evidencePath);
        if (!string.Equals(freshResolved.ManifestDigest, requestedManifest.ManifestDigest, StringComparison.Ordinal))
        {
            throw new ContractException(
                "Re-inventory/re-resolution did not reproduce the trusted manifest; source, owners, spans, references, or evidence are stale.");
        }

        if (selectedIds.Count == 0)
        {
            return EmptyResult(requestedManifest.SourceCommit, requestedManifest.ManifestDigest);
        }

        Dictionary<string, Candidate> candidates = freshResolved.Candidates
            .ToDictionary(static candidate => candidate.CandidateId, StringComparer.Ordinal);
        List<SourceEdit> edits = selectedIds
            .Select(candidateId => CreateEdit(repository, config, candidates[candidateId]))
            .OrderBy(static edit => edit.Path, StringComparer.Ordinal)
            .ThenByDescending(static edit => edit.Start)
            .ToList();
        RejectOverlappingEdits(edits);

        Dictionary<string, byte[]> originalBytes = edits
            .Select(static edit => edit.Path)
            .Distinct(StringComparer.Ordinal)
            .ToDictionary(
                static path => path,
                path => ReadBytes(PathRules.ResolveInsideRoot(repository.Root, path, "candidate path")),
                StringComparer.Ordinal);
        Dictionary<string, byte[]> expectedBytes = originalBytes.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value.ToArray(),
            StringComparer.Ordinal);
        List<string> retained = [];
        List<RevertedCandidateResult> reverted = [];

        string workRoot = Path.Combine(
            repository.MetadataDirectory(),
            "unskip-closed-tests",
            requestedManifest.ManifestDigest);
        try
        {
            ResetDirectory(workRoot);
            foreach (SourceEdit edit in edits)
            {
                string fullPath = PathRules.ResolveInsideRoot(repository.Root, edit.Path, "candidate path");
                byte[] beforeCandidate = expectedBytes[edit.Path].ToArray();
                byte[] editedBytes = ApplyTextEdit(beforeCandidate, edit.Start, edit.Length);
                WriteBytes(fullPath, editedBytes);
                expectedBytes[edit.Path] = editedBytes;

                VerificationOutcome outcome = await VerifyCandidateAsync(
                    repository,
                    config,
                    requestedManifest,
                    edit.Candidate,
                    workRoot);
                string? mutation = RestoreUnexpectedSourceMutations(repository, expectedBytes);
                if (mutation is not null)
                {
                    outcome = new VerificationOutcome(false, $"verification_mutated_source:{mutation}");
                }

                if (outcome.Success)
                {
                    retained.Add(edit.Candidate.CandidateId);
                }
                else
                {
                    expectedBytes[edit.Path] = beforeCandidate;
                    WriteBytes(fullPath, beforeCandidate);
                    reverted.Add(CreateRevertedCandidate(edit.Candidate, outcome.Reason));
                }
            }
        }
        catch
        {
            RestoreFiles(repository, originalBytes);
            throw;
        }
        finally
        {
            TryDeleteDirectory(workRoot);
        }

        if (retained.Count == 0)
        {
            RestoreFiles(repository, originalBytes);
            return new ApplyResult
            {
                SourceCommit = requestedManifest.SourceCommit,
                ManifestDigest = requestedManifest.ManifestDigest,
                RevertedCandidates = reverted.OrderBy(static item => item.CandidateId, StringComparer.Ordinal).ToList(),
                HasChanges = false,
                PrTitle = "",
                PrBody = "",
            };
        }

        Manifest finalEligibility;
        try
        {
            finalEligibility = await IssueResolver.ResolveAsync(freshInventory, evidencePath);
        }
        catch
        {
            RestoreFiles(repository, originalBytes);
            throw;
        }

        HashSet<string> finallyEligible = finalEligibility.Candidates
            .Where(static candidate => candidate.Decision.Eligible)
            .Select(static candidate => candidate.CandidateId)
            .ToHashSet(StringComparer.Ordinal);
        List<string> expiredCandidates = retained
            .Where(candidateId => !finallyEligible.Contains(candidateId))
            .ToList();
        if (expiredCandidates.Count > 0)
        {
            reverted.AddRange(expiredCandidates.Select(candidateId =>
                CreateRevertedCandidate(candidates[candidateId], "eligibility_changed_after_verification")));
            retained = retained.Except(expiredCandidates, StringComparer.Ordinal).ToList();
            RestoreFiles(repository, originalBytes);
            if (retained.Count > 0)
            {
                HashSet<string> retainedSet = retained.ToHashSet(StringComparer.Ordinal);
                Dictionary<string, byte[]> rebuiltBytes = originalBytes.ToDictionary(
                    static pair => pair.Key,
                    static pair => pair.Value.ToArray(),
                    StringComparer.Ordinal);
                foreach (SourceEdit edit in edits.Where(edit => retainedSet.Contains(edit.Candidate.CandidateId)))
                {
                    rebuiltBytes[edit.Path] = ApplyTextEdit(
                        rebuiltBytes[edit.Path],
                        edit.Start,
                        edit.Length);
                }

                RestoreFiles(repository, rebuiltBytes);
            }
        }

        if (retained.Count == 0)
        {
            return new ApplyResult
            {
                SourceCommit = requestedManifest.SourceCommit,
                ManifestDigest = requestedManifest.ManifestDigest,
                RevertedCandidates = reverted
                    .OrderBy(static item => item.CandidateId, StringComparer.Ordinal)
                    .ToList(),
                HasChanges = false,
            };
        }

        Dictionary<string, Candidate> finalCandidates = finalEligibility.Candidates
            .ToDictionary(static candidate => candidate.CandidateId, StringComparer.Ordinal);
        List<Candidate> retainedCandidates = retained.Select(id => finalCandidates[id])
            .OrderBy(static candidate => candidate.Path, StringComparer.Ordinal)
            .ThenBy(static candidate => candidate.AttributeSpan.Start)
            .ToList();
        List<ChangedFileResult> changedFiles = CreateChangedFiles(
            repository.Root,
            retainedCandidates.Select(static candidate => candidate.Path));
        return new ApplyResult
        {
            SourceCommit = requestedManifest.SourceCommit,
            ManifestDigest = requestedManifest.ManifestDigest,
            RetainedCandidates = retainedCandidates
                .Select(static candidate => new RetainedCandidateResult
                {
                    CandidateId = candidate.CandidateId,
                    Path = candidate.Path,
                    TestFqns = candidate.Owner.TestFqns.Order(StringComparer.Ordinal).ToList(),
                })
                .OrderBy(static candidate => candidate.CandidateId, StringComparer.Ordinal)
                .ToList(),
            RevertedCandidates = reverted.OrderBy(static item => item.CandidateId, StringComparer.Ordinal).ToList(),
            ChangedFiles = changedFiles,
            ChangedPaths = changedFiles.Select(static file => file.Path).ToList(),
            HasChanges = true,
            PrTitle = CreatePrTitle(retained.Count),
            PrBody = CreatePrBody(requestedManifest, retainedCandidates, reverted),
        };
    }
}
