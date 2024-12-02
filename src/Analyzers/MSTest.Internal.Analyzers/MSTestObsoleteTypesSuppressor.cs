// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Diagnostics;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MSTest.Internal.Analyzers;

/// <summary>
/// MSTESTINT1: Suppress type is obsolete for known MSTest types.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MSTestObsoleteTypesSuppressor : DiagnosticSuppressor
{
    // CS0618: Member is obsolete.
    // https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/compiler-messages/cs0618
    private const string SuppressedDiagnosticId = "CS0618";

    private static readonly ImmutableArray<string> TypesToSuppress = ImmutableArray.Create(
        "Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution.TestAssemblyInfo",
        "Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution.TestClassInfo",
        "Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution.TestMethodInfo",
        "Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.RunConfigurationSettings",
        "Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.TestSource",
        "Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface.IFileOperations",
        "Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface.ISettingsProvider",
        "Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface.IThreadOperations",
        "Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface.ITraceListenerManager",
        "Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface.ITraceListener",
        "Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface.ITestSource",
        "Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface.ITestSourceHost",
        "Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface.IAdapterTraceLogger",
        "Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface.ITestDeployment",
        "Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.MSTestSettings",
        "Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.AssemblyResolver",
        "Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Deployment.TestRunDirectories",
        "Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.ISettingsProvider",
        "Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.MSTestSettingsProvider");

    internal static readonly SuppressionDescriptor Rule =
        new("MSTESTINT1", SuppressedDiagnosticId, "Type is obsolete only so we can change accessibility");

    public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions { get; } = ImmutableArray.Create(Rule);

    public override void ReportSuppressions(SuppressionAnalysisContext context)
    {
        foreach (Diagnostic diagnostic in context.ReportedDiagnostics)
        {
            // The diagnostic is reported on the test method
            if (diagnostic.Location.SourceTree is not { } tree)
            {
                continue;
            }

            SyntaxNode root = tree.GetRoot(context.CancellationToken);
            SyntaxNode node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);

            SemanticModel semanticModel = context.GetSemanticModel(tree);
            TypeInfo typeInfo = semanticModel.GetTypeInfo(node, context.CancellationToken);

            if (typeInfo.Type is not null
                && TypesToSuppress.Contains(typeInfo.Type.ToDisplayString()))
            {
                context.ReportSuppression(Suppression.Create(Rule, diagnostic));
            }
        }
    }
}
