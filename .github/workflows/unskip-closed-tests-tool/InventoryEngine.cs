using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace UnskipClosedTests.Tool;

internal static partial class InventoryEngine
{
    private sealed record ParsedFile(
        string Path,
        string FullPath,
        byte[] Bytes,
        string BlobOid,
        SyntaxTree Tree,
        CompilationUnitSyntax Root);

    private sealed record PendingCandidate(
        ParsedFile File,
        AttributeSyntax Attribute,
        OwnerIdentity Owner,
        string StableOwnerId,
        List<IssueReference> References,
        List<string> StructuralDeferrals);

    public static Manifest Create(string requestedRoot, string? repositoryOverride, ToolConfig config)
    {
        GitRepository repository = GitRepository.Open(requestedRoot, repositoryOverride);
        List<ParsedFile> files = LoadFiles(repository, config);
        Dictionary<string, int> typeDeclarationCounts = CountTypeDeclarations(files);
        List<PendingCandidate> pending = [];

        foreach (ParsedFile file in files)
        {
            foreach (AttributeSyntax attribute in file.Root.DescendantNodes().OfType<AttributeSyntax>())
            {
                if (!AttributeMatches(attribute, config.IgnoreAttributeNames))
                {
                    continue;
                }

                List<IssueReference> references = ExtractReferences(attribute, repository.Repository);
                if (references.Count == 0)
                {
                    continue;
                }

                if (attribute.FirstAncestorOrSelf<MethodDeclarationSyntax>() is MethodDeclarationSyntax method &&
                    method.AttributeLists.Any(list => list.Span.Contains(attribute.Span)))
                {
                    OwnerIdentity owner = CreateMethodOwner(method, config);
                    List<string> deferrals = [];
                    if (owner.TestFqns.Count == 0)
                    {
                        deferrals.Add("no_enumerated_tests");
                    }
                    if (HasGeneratedMarker(method))
                    {
                        deferrals.Add("generated_declaration");
                    }

                    string stableOwnerId = StableOwnerId(repository.Repository, file.Path, owner, method);
                    pending.Add(new PendingCandidate(file, attribute, owner, stableOwnerId, references, deferrals));
                    continue;
                }

                if (attribute.FirstAncestorOrSelf<ClassDeclarationSyntax>() is ClassDeclarationSyntax type &&
                    type.AttributeLists.Any(list => list.Span.Contains(attribute.Span)))
                {
                    (OwnerIdentity owner, List<string> deferrals) =
                        CreateClassOwner(type, config, typeDeclarationCounts);
                    if (HasGeneratedMarker(type))
                    {
                        deferrals.Add("generated_declaration");
                    }

                    string stableOwnerId = StableOwnerId(repository.Repository, file.Path, owner, type);
                    pending.Add(new PendingCandidate(file, attribute, owner, stableOwnerId, references, deferrals));
                }
            }
        }

        List<Candidate> candidates = [];
        foreach (IGrouping<string, PendingCandidate> ownerGroup in pending
                     .OrderBy(static item => item.File.Path, StringComparer.Ordinal)
                     .ThenBy(static item => item.Attribute.SpanStart)
                     .GroupBy(static item => item.StableOwnerId, StringComparer.Ordinal))
        {
            int ordinal = 0;
            foreach (PendingCandidate item in ownerGroup)
            {
                ordinal++;
                FileLinePositionSpan lineSpan = item.Attribute.GetLocation().GetLineSpan();
                SourceSpan sourceSpan = new()
                {
                    Start = item.Attribute.SpanStart,
                    Length = item.Attribute.Span.Length,
                    StartLine = lineSpan.StartLinePosition.Line + 1,
                    StartColumn = lineSpan.StartLinePosition.Character + 1,
                    EndLine = lineSpan.EndLinePosition.Line + 1,
                    EndColumn = lineSpan.EndLinePosition.Character + 1,
                };
                string attributeText = item.File.Root.SyntaxTree.GetText().ToString(item.Attribute.Span);
                string candidateId = JsonSupport.Sha256(
                    $"candidate-v1\0{repository.Repository}\0{item.File.Path}\0{item.StableOwnerId}\0" +
                    $"{item.File.BlobOid}\0{sourceSpan.Start}:{sourceSpan.Length}\0{ordinal}");

                candidates.Add(new Candidate
                {
                    CandidateId = candidateId,
                    StableOwnerId = item.StableOwnerId,
                    Path = item.File.Path,
                    BlobOid = item.File.BlobOid,
                    SourceSha256 = JsonSupport.Sha256(item.File.Bytes),
                    AttributeSpan = sourceSpan,
                    AttributeTextSha256 = JsonSupport.Sha256(attributeText),
                    Owner = item.Owner,
                    CanonicalIssueReferences = item.References,
                    Decision = new CandidateDecision
                    {
                        Eligible = false,
                        Deferrals = item.StructuralDeferrals.Order(StringComparer.Ordinal).ToList(),
                    },
                });
            }
        }

        candidates = candidates
            .OrderBy(static candidate => candidate.Path, StringComparer.Ordinal)
            .ThenBy(static candidate => candidate.AttributeSpan.Start)
            .ToList();
        Manifest manifest = new()
        {
            Repository = repository.Repository,
            SourceCommit = repository.Commit,
            GitObjectFormat = repository.ObjectFormat,
            ConfigDigest = ConfigLoader.Digest(config),
            CandidateCount = candidates.Count,
            Candidates = candidates,
        };
        manifest.ManifestDigest = JsonSupport.ManifestDigest(manifest);
        return manifest;
    }
}
