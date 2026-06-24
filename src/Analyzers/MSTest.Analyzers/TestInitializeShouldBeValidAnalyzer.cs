// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// MSTEST0008: <inheritdoc cref="Resources.TestInitializeShouldBeValidTitle"/>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class TestInitializeShouldBeValidAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc cref="Resources.TestInitializeShouldBeValidTitle" />
    public static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.TestInitializeShouldBeValidRuleId,
        FixtureMethodDiagnosticAnalyzer.CreateResourceString(nameof(Resources.TestInitializeShouldBeValidTitle)),
        FixtureMethodDiagnosticAnalyzer.CreateResourceString(nameof(Resources.TestInitializeShouldBeValidMessageFormat)),
        FixtureMethodDiagnosticAnalyzer.CreateResourceString(nameof(Resources.TestInitializeShouldBeValidDescription)),
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
        FixtureMethodDiagnosticAnalyzer.RegisterFixtureMethodSymbolAction(
            context,
            WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestInitializeAttribute,
            static (symbolContext, symbols) => FixtureMethodAnalyzerHelper.AnalyzeInstanceFixtureMethod(symbolContext, symbols, Rule));
    }
}
