// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// MSTEST0013: <inheritdoc cref="Resources.AssemblyCleanupShouldBeValidTitle"/>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class AssemblyCleanupShouldBeValidAnalyzer : DiagnosticAnalyzer
{
    internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.AssemblyCleanupShouldBeValidRuleId,
        FixtureMethodAnalyzerHelper.CreateResourceString(nameof(Resources.AssemblyCleanupShouldBeValidTitle)),
        FixtureMethodAnalyzerHelper.CreateResourceString(nameof(Resources.AssemblyCleanupShouldBeValidMessageFormat)),
        FixtureMethodAnalyzerHelper.CreateResourceString(nameof(Resources.AssemblyCleanupShouldBeValidDescription)),
        Category.Usage,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        FixtureMethodAnalyzerHelper.RegisterFixtureAnalyzer(
            context,
            WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingAssemblyCleanupAttribute,
            Rule,
            FixtureKind.Assembly,
            FixtureParameterMode.OptionalTestContext);
    }
}
