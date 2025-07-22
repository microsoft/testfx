// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.PreferTestCleanupOverDisposeAnalyzer,
    MSTest.Analyzers.PreferTestCleanupOverDisposeFixer>;

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class PreferTestCleanupOverDisposeAnalyzerTests
{
    [TestMethod]
    public async Task WhenNonTestClassHasDispose_NoDiagnostic()
    {
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class MyNonTestClass : IDisposable
            {
                public void Dispose()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenTestClassHasDispose_Diagnostic()
    {
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass : IDisposable, IMyInterface
            {
                public void [|Dispose|]()
                {
                }

                [TestMethod]
                public void Test() {}
            }
            public interface IMyInterface { }
            """;
        string fixedCode = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass : IMyInterface
            {
                [TestCleanup]
                public void TestCleanup()
                {
                }

                [TestMethod]
                public void Test() {}
            }
            public interface IMyInterface { }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

#if NET
    [TestMethod]
    public async Task WhenTestClassHasDisposeAsync_Diagnostic()
    {
        string code = """
            using System;
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass : IAsyncDisposable
            {
                public ValueTask [|DisposeAsync|]()
                {
                    return ValueTask.CompletedTask;
                }
            }
            """;
        string fixedCode = """
            using System;
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestCleanup]
                public ValueTask TestCleanup()
                {
                    return ValueTask.CompletedTask;
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenTestClassHasTestCleanup_NoDiagnostic()
    {
        string code = """
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass1
            {
                [TestCleanup]
                public void MyTestCleanup()
                {
                }
            }

            [TestClass]
            public class MyTestClass2
            {
                [TestCleanup]
                public Task MyTestCleanup()
                {
                    return Task.CompletedTask;
                }
            }

            [TestClass]
            public class MyTestClass3
            {
                [TestCleanup]
                public ValueTask MyTestCleanup()
                {
                    return ValueTask.CompletedTask;
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }
#endif
}
