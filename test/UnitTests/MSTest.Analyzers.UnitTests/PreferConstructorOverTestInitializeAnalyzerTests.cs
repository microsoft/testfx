// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Framework;
using Microsoft.Testing.TestInfrastructure;

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.PreferConstructorOverTestInitializeAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace MSTest.Analyzers.Test;

[TestGroup]
public sealed class PreferConstructorOverTestInitializeAnalyzerTests(ITestExecutionContext testExecutionContext) : TestBase(testExecutionContext)
{
    public async Task WhenTestClassHasCtor_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                public MyTestClass()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenTestClassHasTestInitialize_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestInitialize]
                public void [|MyTestInit|]()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenTestClassHasTestInitializeAsync_NoDiagnostic()
    {
        string code = """
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestInitialize]
                public Task MyTestInit()
                {
                    return Task.CompletedTask;
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenTestClassHasTestInitializeAndCtor_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                public MyTestClass()
                {
                }

                [TestInitialize]
                public void [|MyTestInit|]()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }
}
