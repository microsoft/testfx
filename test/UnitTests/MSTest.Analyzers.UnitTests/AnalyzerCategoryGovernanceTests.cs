// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;

using Microsoft.CodeAnalysis;

namespace MSTest.Analyzers.Test;

/// <summary>
/// Lightweight governance tests that pin the diagnostic <c>Category</c> taxonomy for performance-impacting
/// analyzers so misclassifications are caught at build time without any Roslyn workspace overhead.
/// See https://github.com/microsoft/testfx/issues/9467.
/// </summary>
[TestClass]
public sealed class AnalyzerCategoryGovernanceTests
{
    private const string PerformanceCategory = "Performance";

    [TestMethod]
    public void UseParallelizeAttribute_MainRule_UsesPerformanceCategory()
        => Assert.AreEqual(PerformanceCategory, UseParallelizeAttributeAnalyzer.Rule.Category);

    [TestMethod]
    public void AvoidThreadSleepAndTaskWaitInTests_UsesPerformanceCategory()
    {
        DiagnosticDescriptor descriptor = new AvoidThreadSleepAndTaskWaitInTestsAnalyzer().SupportedDiagnostics
            .Single(d => d.Id == "MSTEST0067");
        Assert.AreEqual(PerformanceCategory, descriptor.Category);
    }
}
