using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace UnskipClosedTests.Tool;

internal static partial class ApplyEngine
{
    private static SourceEdit CreateEdit(GitRepository repository, ToolConfig config, Candidate candidate)
    {
        if (PathRules.MatchesAnyGlob(candidate.Path, config.ExcludedGlobs.Concat(config.GeneratedGlobs)))
        {
            throw new ContractException($"Candidate path '{candidate.Path}' is excluded or generated.");
        }

        string fullPath = PathRules.ResolveInsideRoot(repository.Root, candidate.Path, "candidate path");
        PathRules.RejectReparsePoints(repository.Root, fullPath);
        byte[] bytes = ReadBytes(fullPath);
        if (!string.Equals(JsonSupport.Sha256(bytes), candidate.SourceSha256, StringComparison.Ordinal))
        {
            throw new ContractException($"Candidate path '{candidate.Path}' source hash is stale.");
        }

        string text = DecodeUtf8(bytes, out _);
        SyntaxTree tree = CSharpSyntaxTree.ParseText(text, path: candidate.Path);
        CompilationUnitSyntax root = tree.GetCompilationUnitRoot();
        List<AttributeSyntax> matches = root.DescendantNodes().OfType<AttributeSyntax>()
            .Where(attribute => attribute.SpanStart == candidate.AttributeSpan.Start &&
                                attribute.Span.Length == candidate.AttributeSpan.Length)
            .ToList();
        if (matches.Count != 1 ||
            !InventoryEngine.AttributeMatches(matches[0], config.IgnoreAttributeNames) ||
            !string.Equals(
                JsonSupport.Sha256(text.Substring(matches[0].SpanStart, matches[0].Span.Length)),
                candidate.AttributeTextSha256,
                StringComparison.Ordinal))
        {
            throw new ContractException($"Candidate '{candidate.CandidateId}' attribute anchor is stale or fabricated.");
        }

        AttributeSyntax attribute = matches[0];
        AttributeListSyntax list = attribute.Parent as AttributeListSyntax
            ?? throw new ContractException($"Candidate '{candidate.CandidateId}' attribute is not in an attribute list.");
        int index = list.Attributes.IndexOf(attribute);
        TextSpan removal;
        if (list.Attributes.Count == 1)
        {
            removal = list.Span;
        }
        else if (index < list.Attributes.Count - 1)
        {
            removal = TextSpan.FromBounds(attribute.SpanStart, list.Attributes[index + 1].SpanStart);
        }
        else
        {
            removal = TextSpan.FromBounds(list.Attributes[index - 1].Span.End, attribute.Span.End);
        }

        return new SourceEdit(candidate.Path, removal.Start, removal.Length, candidate);
    }

    private static void RejectOverlappingEdits(List<SourceEdit> edits)
    {
        foreach (IGrouping<string, SourceEdit> group in edits.GroupBy(static edit => edit.Path, StringComparer.Ordinal))
        {
            SourceEdit? previous = null;
            foreach (SourceEdit edit in group.OrderBy(static item => item.Start))
            {
                if (previous is not null && edit.Start < previous.Start + previous.Length)
                {
                    throw new ContractException(
                        $"Candidate edits '{previous.Candidate.CandidateId}' and '{edit.Candidate.CandidateId}' overlap.");
                }

                previous = edit;
            }
        }
    }

    private static byte[] ApplyTextEdit(byte[] sourceBytes, int start, int length)
    {
        string text = DecodeUtf8(sourceBytes, out bool bom);
        if (start < 0 || length <= 0 || start + length > text.Length)
        {
            throw new ContractException("Candidate edit span is outside the current source.");
        }

        string edited = text.Remove(start, length);
        byte[] content = new UTF8Encoding(false, true).GetBytes(edited);
        if (!bom)
        {
            return content;
        }

        return [0xEF, 0xBB, 0xBF, .. content];
    }

    private static string DecodeUtf8(byte[] bytes, out bool bom)
    {
        bom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
        ReadOnlySpan<byte> content = bom ? bytes.AsSpan(3) : bytes;
        try
        {
            return new UTF8Encoding(false, true).GetString(content);
        }
        catch (DecoderFallbackException ex)
        {
            throw new ContractException($"Candidate source is not valid UTF-8: {ex.Message}");
        }
    }
}
