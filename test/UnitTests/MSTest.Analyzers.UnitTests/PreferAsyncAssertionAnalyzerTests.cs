// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.PreferAsyncAssertionAnalyzer,
    MSTest.Analyzers.PreferAsyncAssertionFixer>;

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class PreferAsyncAssertionAnalyzerTests
{
    [TestMethod]
    public async Task WhenAssertThrowsExactlyBlocksOnTask_CodeFixUsesAsyncAssertionAndUpdatesSignature()
    {
        string code = """
            using System;
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    [|Assert.ThrowsExactly<InvalidOperationException>(() => BarAsync().GetAwaiter().GetResult())|];
                }

                private Task BarAsync() => Task.CompletedTask;
            }
            """;

        string fixedCode = """
            using System;
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public async Task MyTestMethod()
                {
                    await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => BarAsync());
                }

                private Task BarAsync() => Task.CompletedTask;
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertThrowsBlocksOnGenericTask_CodeFixUsesAsyncAssertion()
    {
        string code = """
            using System;
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public async Task MyTestMethod()
                {
                    Exception exception = [|Assert.Throws<InvalidOperationException>(() => BarAsync().GetAwaiter().GetResult())|];
                    Assert.IsNotNull(exception);
                    await Task.CompletedTask;
                }

                private Task<int> BarAsync() => Task.FromResult(42);
            }
            """;

        string fixedCode = """
            using System;
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public async Task MyTestMethod()
                {
                    Exception exception = await Assert.ThrowsAsync<InvalidOperationException>(() => BarAsync());
                    Assert.IsNotNull(exception);
                    await Task.CompletedTask;
                }

                private Task<int> BarAsync() => Task.FromResult(42);
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertionDoesNotBlockOnTask_NoDiagnostic()
    {
        string code = """
            using System;
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public async Task MyTestMethod()
                {
                    await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => BarAsync());
                    Assert.ThrowsExactly<InvalidOperationException>(() => throw new InvalidOperationException());
                }

                private Task BarAsync() => Task.CompletedTask;
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }
}
