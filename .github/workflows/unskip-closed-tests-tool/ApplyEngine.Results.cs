using System.Text;

namespace UnskipClosedTests.Tool;

internal static partial class ApplyEngine
{
    internal static List<ChangedFileResult> CreateChangedFiles(
        string repositoryRoot,
        IEnumerable<string> paths) =>
        paths
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .Select(path =>
            {
                string normalized = PathRules.ValidateRelativePath(path, "changed path");
                string fullPath = PathRules.ResolveInsideRoot(repositoryRoot, normalized, "changed path");
                return new ChangedFileResult
                {
                    Path = normalized,
                    ContentSha256 = JsonSupport.Sha256(ReadBytes(fullPath)),
                };
            })
            .ToList();

    internal static string CreatePrTitle(int retainedCount) =>
        retainedCount == 1
            ? "[unskip-closed-tests] Unskip test for completed GitHub work item"
            : $"[unskip-closed-tests] Unskip {retainedCount} tests for completed GitHub work items";

    private static RevertedCandidateResult CreateRevertedCandidate(Candidate candidate, string reason) => new()
    {
        CandidateId = candidate.CandidateId,
        Path = candidate.Path,
        TestFqns = candidate.Owner.TestFqns.Order(StringComparer.Ordinal).ToList(),
        Reason = reason,
    };

    private static ApplyResult EmptyResult(string sourceCommit, string manifestDigest) => new()
    {
        SourceCommit = sourceCommit,
        ManifestDigest = manifestDigest,
        HasChanges = false,
    };

    private static string CreatePrBody(
        Manifest manifest,
        IReadOnlyList<Candidate> retained,
        IReadOnlyList<RevertedCandidateResult> reverted)
    {
        StringBuilder body = new();
        body.Append("<!-- unskip-closed-tests:v1;source=");
        body.Append(manifest.SourceCommit);
        body.Append(";manifest=");
        body.Append(manifest.ManifestDigest);
        body.AppendLine(" -->");
        body.AppendLine();
        body.AppendLine("## Verified unskips");
        body.AppendLine();
        foreach (Candidate candidate in retained)
        {
            body.Append("- `");
            body.Append(candidate.Path);
            body.Append("` — ");
            body.Append(string.Join(", ", candidate.Owner.TestFqns.Select(static fqn => $"`{fqn}`")));
            body.Append(" (");
            body.Append(string.Join(", ", candidate.CanonicalIssueReferences.Select(static reference =>
                $"[{reference.Canonical}]({reference.Url})")));
            body.AppendLine(")");
        }

        body.AppendLine();
        body.AppendLine("Each retained edit was verified independently by the configured trusted command and exact TRX FQN mapping.");
        if (reverted.Count > 0)
        {
            body.AppendLine();
            body.AppendLine($"The helper reverted {reverted.Count} candidate(s) that did not satisfy verification.");
        }

        return body.ToString().TrimEnd();
    }
}
