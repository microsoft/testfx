// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

#if !MICROSOFT_CODEANALYSIS_PUBLIC_API_ANALYZERS

using Microsoft.CodeAnalysis;

namespace Analyzer.Utilities
{
    internal static class DocumentExtensions
    {
        public static async ValueTask<SyntaxNode> GetRequiredSyntaxRootAsync(this Document document, CancellationToken cancellationToken)
        {
            if (document.TryGetSyntaxRoot(out var root))
                return root;

            root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            return root ?? throw new InvalidOperationException("SyntaxTree is required to accomplish the task but is not supported by document");
        }
    }
}

#endif
