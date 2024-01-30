// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MSTest.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class UseOwnerAttributeOnTestMethodAnalyzer : UseAttributeOnTestMethodBaseAnalyzer
{
    private static readonly LocalizableResourceString Title = new(nameof(Resources.UseOwnerAttributeOnTestMethodAnalyzerTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.UseOwnerAttributeOnTestMethodAnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));

    internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.OwnerAttributeOnTestMethodRuleId,
        Title,
        MessageFormat,
        description: null,
        Category.Usage,
        DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    public UseOwnerAttributeOnTestMethodAnalyzer()
        : base(Rule, WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingOwnerAttribute)
    {
    }

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
        = ImmutableArray.Create(Rule);
}
