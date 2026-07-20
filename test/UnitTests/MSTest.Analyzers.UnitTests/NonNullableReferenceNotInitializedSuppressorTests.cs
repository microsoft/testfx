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

    [TestMethod]
    public async Task TestContextPropertyOnTestClassConstructor_DiagnosticIsSuppressed()
    {
        string code = @"
#nullable enable

using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class SomeClass
{
    public {|#0:SomeClass|}()
    {
    }

    public TestContext TestContext { get; set; }
}
";

        await VerifySingleSuppressionAsync(code, isSuppressed: true);
    }

    [TestMethod]
    public async Task TestContextPropertyOnClassWithDerivedTestClassAttribute_DiagnosticIsSuppressed()
    {
        string code = @"
#nullable enable

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[AttributeUsage(AttributeTargets.Class)]
public class DerivedTestClassAttribute : TestClassAttribute { }

[DerivedTestClass]
public class SomeClass
{
    public TestContext {|#0:TestContext|} { get; set; }
}
";

        await VerifySingleSuppressionAsync(code, isSuppressed: true);
    }

    [TestMethod]
    public async Task TestContextPropertyWithWrongTypeName_DiagnosticIsNotSuppressed()
    {
        string code = @"
#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;

public class MyCustomContext { }

[TestClass]
public class SomeClass
{
    public MyCustomContext {|#0:TestContext|} { get; set; }
}
";

        await VerifySingleSuppressionAsync(code, isSuppressed: false);
    }

    [TestMethod]
    public async Task TestContextPropertyWithWrongPropertyName_DiagnosticIsNotSuppressed()
    {
        string code = @"
#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class SomeClass
{
    public TestContext {|#0:MyContext|} { get; set; }
}
";

        var test = new VerifyCS.Test
        {
            TestCode = code,
        };

        test.ExpectedDiagnostics.Add(DiagnosticResult.CompilerError("CS8618")
            .WithLocation(0)
            .WithOptions(DiagnosticOptions.IgnoreAdditionalLocations)
            .WithArguments("property", "MyContext")
            .WithIsSuppressed(false));

        await test.RunAsync();
    }

    [TestMethod]
    public async Task TestContextFieldOnTestClass_DiagnosticIsNotSuppressed()
    {
        string code = @"
#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class SomeClass
{
    public TestContext {|#0:_testContext|};
}
";

        var test = new VerifyCS.Test
        {
            TestCode = code,
        };

        test.ExpectedDiagnostics.Add(DiagnosticResult.CompilerError("CS8618")
            .WithLocation(0)
            .WithOptions(DiagnosticOptions.IgnoreAdditionalLocations)
            .WithArguments("field", "_testContext")
            .WithIsSuppressed(false));

        await test.RunAsync();
    }

    [TestMethod]
    public async Task TestContextGetterOnlyPropertyOnTestClass_DiagnosticIsNotSuppressed()
    {
        // MSTest cannot assign a getter-only property, so CS8618 must remain visible.
        string code = @"
#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class SomeClass
{
    public TestContext {|#0:TestContext|} { get; }
}
";

        await VerifySingleSuppressionAsync(code, isSuppressed: false);
    }

    [TestMethod]
    public async Task PrivateTestContextPropertyOnTestClass_DiagnosticIsNotSuppressed()
    {
        string code = @"
#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class SomeClass
{
    private TestContext {|#0:TestContext|} { get; set; }
}
";

        await VerifySingleSuppressionAsync(code, isSuppressed: false);
    }

    private Task VerifySingleSuppressionAsync(string source, bool isSuppressed)
        => VerifyDiagnosticsAsync(source, [(0, isSuppressed)]);

    private async Task VerifyDiagnosticsAsync(string source, List<(int Location, bool IsSuppressed)> diagnostics)
    {
        var test = new VerifyCS.Test
        {
            TestCode = source,
        };

        foreach ((int location, bool isSuppressed) in diagnostics)
        {
            test.ExpectedDiagnostics.Add(DiagnosticResult.CompilerError("CS8618")
                .WithLocation(location)
                .WithOptions(DiagnosticOptions.IgnoreAdditionalLocations)
                .WithArguments("property", "TestContext")
                .WithIsSuppressed(isSuppressed));
        }

        await test.RunAsync();
    }
}
