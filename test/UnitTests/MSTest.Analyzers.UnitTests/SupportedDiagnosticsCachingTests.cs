// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class SupportedDiagnosticsCachingTests
{
    [TestMethod]
    public void CollectionAssertToAssertAnalyzer_CachesSupportedDiagnostics()
        => AssertSupportedDiagnosticsAreCached(new CollectionAssertToAssertAnalyzer());

    [TestMethod]
    public void StringAssertToAssertAnalyzer_CachesSupportedDiagnostics()
        => AssertSupportedDiagnosticsAreCached(new StringAssertToAssertAnalyzer());

    private static void AssertSupportedDiagnosticsAreCached(DiagnosticAnalyzer analyzer)
    {
        ImmutableArray<DiagnosticDescriptor> supportedDiagnostics = analyzer.SupportedDiagnostics;

        Assert.AreEqual(supportedDiagnostics, analyzer.SupportedDiagnostics);
    }
}
