// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// MSTEST0012: <inheritdoc cref="Resources.AssemblyInitializeShouldBeValidTitle"/>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class AssemblyInitializeShouldBeValidAnalyzer : DiagnosticAnalyzer
{
    internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.AssemblyInitializeShouldBeValidRuleId,
        FixtureMethodAnalyzerHelper.CreateResourceString(nameof(Resources.AssemblyInitializeShouldBeValidTitle)),
        FixtureMethodAnalyzerHelper.CreateResourceString(nameof(Resources.AssemblyInitializeShouldBeValidMessageFormat)),
        FixtureMethodAnalyzerHelper.CreateResourceString(nameof(Resources.AssemblyInitializeShouldBeValidDescription)),
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
            WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingAssemblyInitializeAttribute,
            Rule,
            FixtureKind.Assembly,
            FixtureParameterMode.MustHaveTestContext);
    }
}
