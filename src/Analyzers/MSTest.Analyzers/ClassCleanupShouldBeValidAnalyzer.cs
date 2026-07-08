// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// MSTEST0011: <inheritdoc cref="Resources.ClassCleanupShouldBeValidTitle"/>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class ClassCleanupShouldBeValidAnalyzer : DiagnosticAnalyzer
{
    internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.ClassCleanupShouldBeValidRuleId,
        FixtureMethodAnalyzerHelper.CreateResourceString(nameof(Resources.ClassCleanupShouldBeValidTitle)),
        FixtureMethodAnalyzerHelper.CreateResourceString(nameof(Resources.ClassCleanupShouldBeValidMessageFormat)),
        FixtureMethodAnalyzerHelper.CreateResourceString(nameof(Resources.ClassCleanupShouldBeValidDescription)),
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
            WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingClassCleanupAttribute,
            Rule,
            FixtureKind.Class,
            FixtureParameterMode.OptionalTestContext);
    }
}
