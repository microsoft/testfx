// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Globalization;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MSTest.Internal.Analyzers;

/// <summary>
/// MSTESTINT1: Suppress type is obsolete for known MSTest types.
/// </summary>
#pragma warning disable RS1004 // Recommend adding language support to diagnostic analyzer - For internal use only. We don't have VB code.
[DiagnosticAnalyzer(LanguageNames.CSharp)]
#pragma warning restore RS1004 // Recommend adding language support to diagnostic analyzer
public sealed class MSTestObsoleteTypesSuppressor : DiagnosticSuppressor
{
    // CS0618: Member is obsolete.
    // https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/compiler-messages/cs0618
    private const string SuppressedDiagnosticId = "CS0618";

    internal static readonly SuppressionDescriptor Rule =
        new("MSTESTINT1", SuppressedDiagnosticId, "Type is obsolete only so we can change accessibility");

    public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions { get; } = ImmutableArray.Create(Rule);

    public override void ReportSuppressions(SuppressionAnalysisContext context)
    {
        foreach (Diagnostic diagnostic in context.ReportedDiagnostics)
        {
            // It's very tedious to list all types that we obsoleted. We know for sure that this message is
            // for types that can be used internally but not externally.
            const string PublicTypeObsoleteMessage = "We will remove or hide this type starting with v4. If you are using this type, reach out to our team on https://github.com/microsoft/testfx.";
            if (diagnostic.GetMessage(CultureInfo.InvariantCulture).Contains(PublicTypeObsoleteMessage))
            {
                context.ReportSuppression(Suppression.Create(Rule, diagnostic));
            }
        }
    }
}
