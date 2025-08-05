// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.VisualBasic.Testing;

using TestContext = Microsoft.VisualStudio.TestTools.UnitTesting.TestContext;

namespace MSTest.Analyzers.Test;

public static partial class VisualBasicCodeFixVerifier<TAnalyzer, TCodeFix>
    where TAnalyzer : DiagnosticAnalyzer, new()
    where TCodeFix : CodeFixProvider, new()
{
    public class Test : VisualBasicCodeFixTest<TAnalyzer, TCodeFix, DefaultVerifier>
    {
        public Test()
        {
#if NET462
            ReferenceAssemblies = ReferenceAssemblies.NetFramework.Net462.Default;
            TestState.AdditionalReferences.Add(MetadataReference.CreateFromFile(typeof(ValueTask<>).Assembly.Location));
            TestState.AdditionalReferences.Add(MetadataReference.CreateFromFile(typeof(IAsyncDisposable).Assembly.Location));
#else
            ReferenceAssemblies = ReferenceAssemblies.Net.Net60;
#endif
            TestState.AdditionalReferences.Add(MetadataReference.CreateFromFile(typeof(ParallelizeAttribute).Assembly.Location));
            TestState.AdditionalReferences.Add(MetadataReference.CreateFromFile(typeof(TestContext).Assembly.Location));
        }
    }
}
