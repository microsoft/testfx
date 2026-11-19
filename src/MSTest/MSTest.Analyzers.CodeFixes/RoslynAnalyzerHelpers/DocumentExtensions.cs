// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

#if !MICROSOFT_CODEANALYSIS_PUBLIC_API_ANALYZERS

using Microsoft.CodeAnalysis;

namespace Analyzer.Utilities;

internal static class DocumentExtensions
{
    public static async ValueTask<SemanticModel> GetRequiredSemanticModelAsync(this Document document, CancellationToken cancellationToken)
    {
        if (document.TryGetSemanticModel(out var semanticModel))
            return semanticModel;
        semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        return semanticModel ?? throw new InvalidOperationException("SyntaxTree is required to accomplish the task but is not supported by document");
    }

    public static async ValueTask<SyntaxNode> GetRequiredSyntaxRootAsync(this Document document, CancellationToken cancellationToken)
    {
        if (document.TryGetSyntaxRoot(out SyntaxNode? root))
            return root;

        root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        return root ?? throw new InvalidOperationException("SyntaxTree is required to accomplish the task but is not supported by document");
    }
}

#endif
