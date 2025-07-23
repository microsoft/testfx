// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.NonNullableReferenceNotInitializedSuppressor,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace MSTest.Analyzers.UnitTests;

[TestClass]
public sealed class NonNullableReferenceNotInitializedSuppressorTests
{
    [TestMethod]
    public async Task TestContextPropertyOnTestClass_DiagnosticIsSuppressed()
    {
        string code = @"
#nullable enable

using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class SomeClass
{
    public TestContext {|#0:TestContext|} { get; set; }
}
";

        await VerifySingleSuppressionAsync(code, isSuppressed: true);
    }

    [TestMethod]
    public async Task TestContextPropertyOnNonTestClass_DiagnosticIsNotSuppressed()
    {
        string code = @"
#nullable enable

using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

public class SomeClass
{
    public TestContext {|#0:TestContext|} { get; set; }
}
";

        await VerifySingleSuppressionAsync(code, isSuppressed: false);
    }

    private Task VerifySingleSuppressionAsync(string source, bool isSuppressed)
        => new VerifyCS.Test
        {
            TestCode = source,
            ExpectedDiagnostics =
            {
                DiagnosticResult.CompilerError("CS8618")
                    .WithLocation(0)
                    .WithOptions(DiagnosticOptions.IgnoreAdditionalLocations)
                    .WithArguments("property", "TestContext")
                    .WithIsSuppressed(isSuppressed),
            },
        }.RunAsync();
}
