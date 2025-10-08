// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    Microsoft.CodeAnalysis.Testing.EmptyDiagnosticAnalyzer,
    MSTest.Analyzers.UseExecuteAsyncOverrideFixer>;

namespace MSTest.Analyzers.UnitTests;

[TestClass]
public sealed class UseExecuteAsyncOverrideFixerTests
{
    [TestMethod]
    public async Task FixExecuteOverrideWithBlockBody_ShouldTransformToExecuteAsync()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class C : TestMethodAttribute
            {
                public override TestResult[] {|CS0115:Execute|}(ITestMethod testMethod)
                {
                    return localFunction();

                    TestResult[] localFunction()
                    {
                        return new TestResult[] { };
                    }
                }
            }
            """;

        string fixedCode = """
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class C : TestMethodAttribute
            {
                public override Task<TestResult[]> ExecuteAsync(ITestMethod testMethod)
                {
                    return Task.FromResult(localFunction());

                    TestResult[] localFunction()
                    {
                        return new TestResult[] { };
                    }
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task FixExecuteOverrideWithExpressionBody_ShouldTransformToExecuteAsync()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class C : TestMethodAttribute
            {
                public override TestResult[] {|CS0115:Execute|}(ITestMethod testMethod)
                    => new TestResult[] { };
            }
            """;

        string fixedCode = """
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class C : TestMethodAttribute
            {
                public override Task<TestResult[]> ExecuteAsync(ITestMethod testMethod)
                    => Task.FromResult(new TestResult[] { });
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task FixExecuteOverrideWithMultipleReturns_ShouldTransformAllReturns()
    {
        string code = """
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class C : TestMethodAttribute
            {
                public override TestResult[] {|CS0115:Execute|}(ITestMethod testMethod)
                {
                    if (testMethod == null)
                    {
                        return new TestResult[] { };
                    }
                    
                    return new TestResult[] { };
                }
            }
            """;

        string fixedCode = """
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class C : TestMethodAttribute
            {
                public override Task<TestResult[]> ExecuteAsync(ITestMethod testMethod)
                {
                    if (testMethod == null)
                    {
                        return Task.FromResult(new TestResult[] { });
                    }
                    
                    return Task.FromResult(new TestResult[] { });
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task NonExecuteMethod_ShouldNotOfferCodeFix()
    {
        string code = """
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class C : TestMethodAttribute
            {
                public override TestResult[] {|CS0115:SomeOtherMethod|}(ITestMethod testMethod)
                {
                    return new TestResult[] { };
                }
            }
            """;

        // Should not offer a code fix since it's not an Execute method
        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task ExecuteMethodWithWrongSignature_ShouldNotOfferCodeFix()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class C : TestMethodAttribute
            {
                public override int {|CS0115:Execute|}(ITestMethod testMethod)
                {
                    return 0;
                }
            }
            """;

        // Should not offer a code fix since return type is not TestResult[]
        await VerifyCS.VerifyCodeFixAsync(code, code);
    }
}
