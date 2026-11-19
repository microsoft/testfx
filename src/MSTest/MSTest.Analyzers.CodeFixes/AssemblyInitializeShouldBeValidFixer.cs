// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Composition;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// Code fixer for <see cref="AssemblyInitializeShouldBeValidAnalyzer"/>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AssemblyInitializeShouldBeValidFixer))]
[Shared]
public sealed class AssemblyInitializeShouldBeValidFixer : FixtureMethodSignatureFixerBase
{
    /// <inheritdoc />
    protected override string DiagnosticRuleId
        => DiagnosticIds.AssemblyInitializeShouldBeValidRuleId;

    /// <inheritdoc />
    protected override bool IsParameterLess
        => false;

    /// <inheritdoc />
    protected override bool ShouldBeStatic
        => true;
}
