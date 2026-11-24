// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

namespace MSTest.Analyzers;

public sealed partial class PreferAsyncAssertionFixer
{
    private sealed class AwaitableReturnStatementRewriter : CSharpSyntaxRewriter
    {
        private readonly BlockSyntax _rootBody;

        public AwaitableReturnStatementRewriter(BlockSyntax rootBody)
        {
            _rootBody = rootBody;
        }

        public override SyntaxNode? VisitSimpleLambdaExpression(SimpleLambdaExpressionSyntax node)
            => node;

        public override SyntaxNode? VisitParenthesizedLambdaExpression(ParenthesizedLambdaExpressionSyntax node)
            => node;

        public override SyntaxNode? VisitAnonymousMethodExpression(AnonymousMethodExpressionSyntax node)
            => node;

        public override SyntaxNode? VisitLocalFunctionStatement(LocalFunctionStatementSyntax node)
            => node;

        public override SyntaxNode? VisitBlock(BlockSyntax node)
        {
            List<StatementSyntax>? rewrittenStatements = null;

            for (int i = 0; i < node.Statements.Count; i++)
            {
                StatementSyntax statement = node.Statements[i];
                if (statement is ReturnStatementSyntax { Expression: { } returnExpression } returnStatement)
                {
                    rewrittenStatements ??= AddUnchangedStatements(node.Statements, i);
                    bool isFinalRootReturn = ReferenceEquals(node, _rootBody) && i == node.Statements.Count - 1;
                    rewrittenStatements.AddRange(CreateAwaitAndReturnStatements(returnStatement, returnExpression, includeReturn: !isFinalRootReturn));
                    continue;
                }

                var rewrittenStatement = (StatementSyntax)Visit(statement)!;
                if (rewrittenStatements is not null)
                {
                    rewrittenStatements.Add(rewrittenStatement);
                }
                else if (!ReferenceEquals(statement, rewrittenStatement))
                {
                    rewrittenStatements = AddUnchangedStatements(node.Statements, i);
                    rewrittenStatements.Add(rewrittenStatement);
                }
            }

            return rewrittenStatements is null
                ? node
                : node.WithStatements(SyntaxFactory.List(rewrittenStatements));
        }

        public override SyntaxNode? VisitReturnStatement(ReturnStatementSyntax node)
            => node.Expression is { } expression
                ? SyntaxFactory.Block(CreateAwaitAndReturnStatements(node, expression, includeReturn: true)).WithAdditionalAnnotations(Formatter.Annotation)
                : node;

        private static List<StatementSyntax> AddUnchangedStatements(SyntaxList<StatementSyntax> statements, int endIndex)
        {
            var rewrittenStatements = new List<StatementSyntax>(statements.Count + 1);
            for (int j = 0; j < endIndex; j++)
            {
                rewrittenStatements.Add(statements[j]);
            }

            return rewrittenStatements;
        }

        private static StatementSyntax[] CreateAwaitAndReturnStatements(ReturnStatementSyntax returnStatement, ExpressionSyntax expression, bool includeReturn)
        {
            ExpressionStatementSyntax awaitStatement = SyntaxFactory.ExpressionStatement(
                    SyntaxFactory.AwaitExpression(expression.WithoutLeadingTrivia()))
                .WithLeadingTrivia(returnStatement.GetLeadingTrivia())
                .WithAdditionalAnnotations(Formatter.Annotation);

            ReturnStatementSyntax newReturnStatement = returnStatement
                .WithExpression(null)
                .WithLeadingTrivia(SyntaxFactory.ElasticMarker)
                .WithAdditionalAnnotations(Formatter.Annotation);

            return includeReturn ? [awaitStatement, newReturnStatement] : [awaitStatement];
        }
    }

    private sealed class UnawaitableReturnDetector : CSharpSyntaxWalker
    {
        private int _unawaitableDepth;

        public bool Found { get; private set; }

        public override void Visit(SyntaxNode? node)
        {
            if (Found)
            {
                return;
            }

            base.Visit(node);
        }

        public override void VisitSimpleLambdaExpression(SimpleLambdaExpressionSyntax node)
        {
        }

        public override void VisitParenthesizedLambdaExpression(ParenthesizedLambdaExpressionSyntax node)
        {
        }

        public override void VisitAnonymousMethodExpression(AnonymousMethodExpressionSyntax node)
        {
        }

        public override void VisitLocalFunctionStatement(LocalFunctionStatementSyntax node)
        {
        }

        public override void VisitLockStatement(LockStatementSyntax node)
            => VisitInUnawaitableContext(node.Statement);

        public override void VisitUnsafeStatement(UnsafeStatementSyntax node)
            => VisitInUnawaitableContext(node.Block);

        public override void VisitFixedStatement(FixedStatementSyntax node)
            => VisitInUnawaitableContext(node.Statement);

        public override void VisitReturnStatement(ReturnStatementSyntax node)
        {
            if (_unawaitableDepth > 0 && node.Expression is not null)
            {
                Found = true;
                return;
            }

            base.VisitReturnStatement(node);
        }

        private void VisitInUnawaitableContext(SyntaxNode? node)
        {
            _unawaitableDepth++;
            try
            {
                Visit(node);
            }
            finally
            {
                _unawaitableDepth--;
            }
        }
    }
}
