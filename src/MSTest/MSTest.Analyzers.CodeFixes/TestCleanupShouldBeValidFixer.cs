// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Composition;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// Code fixer for <see cref="TestCleanupShouldBeValidAnalyzer"/>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(TestCleanupShouldBeValidFixer))]
[Shared]
public sealed class TestCleanupShouldBeValidFixer : FixtureMethodSignatureFixerBase
{
    /// <inheritdoc />
    protected override string DiagnosticRuleId
        => DiagnosticIds.TestCleanupShouldBeValidRuleId;

    /// <inheritdoc />
    protected override bool IsParameterLess
        => true;

    /// <inheritdoc />
    protected override bool ShouldBeStatic
        => false;
}
