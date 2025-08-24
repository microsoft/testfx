// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;

using MSTest.Analyzers.Helpers;

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.UseCancellationTokenPropertyAnalyzer,
    MSTest.Analyzers.UseCancellationTokenPropertyFixer>;

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class UseCancellationTokenPropertyAnalyzerTests
{
    private static DiagnosticDescriptor Rule => UseCancellationTokenPropertyAnalyzer.UseCancellationTokenPropertyRule;

    [TestMethod]
    public async Task WhenUsingCancellationTokenSourceToken_ShouldReportDiagnostic()
    {
        const string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Threading;
            using System.Threading.Tasks;

            [TestClass]
            public class MyTestClass
            {
                public TestContext TestContext { get; set; }

                [TestMethod]
                public async Task MyTest()
                {
                    await SomeAsyncOperation({|#0:TestContext.CancellationTokenSource.Token|});
                }

                private static Task SomeAsyncOperation(CancellationToken cancellationToken)
                    => Task.CompletedTask;
            }
            """;

        const string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Threading;
            using System.Threading.Tasks;

            [TestClass]
            public class MyTestClass
            {
                public TestContext TestContext { get; set; }

                [TestMethod]
                public async Task MyTest()
                {
                    await SomeAsyncOperation(TestContext.CancellationToken);
                }

                private static Task SomeAsyncOperation(CancellationToken cancellationToken)
                    => Task.CompletedTask;
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic(Rule).WithLocation(0),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenUsingCancellationTokenSourceTokenWithVariable_ShouldReportDiagnostic()
    {
        const string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Threading;
            using System.Threading.Tasks;

            [TestClass]
            public class MyTestClass
            {
                public TestContext TestContext { get; set; }

                [TestMethod]
                public async Task MyTest()
                {
                    var context = TestContext;
                    await SomeAsyncOperation({|#0:context.CancellationTokenSource.Token|});
                }

                private static Task SomeAsyncOperation(CancellationToken cancellationToken)
                    => Task.CompletedTask;
            }
            """;

        const string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Threading;
            using System.Threading.Tasks;

            [TestClass]
            public class MyTestClass
            {
                public TestContext TestContext { get; set; }

                [TestMethod]
                public async Task MyTest()
                {
                    var context = TestContext;
                    await SomeAsyncOperation(context.CancellationToken);
                }

                private static Task SomeAsyncOperation(CancellationToken cancellationToken)
                    => Task.CompletedTask;
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic(Rule).WithLocation(0),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenUsingCancellationToken_ShouldNotReportDiagnostic()
    {
        const string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Threading;
            using System.Threading.Tasks;

            [TestClass]
            public class MyTestClass
            {
                public TestContext TestContext { get; set; }

                [TestMethod]
                public async Task MyTest()
                {
                    await SomeAsyncOperation(TestContext.CancellationToken);
                }

                private static Task SomeAsyncOperation(CancellationToken cancellationToken)
                    => Task.CompletedTask;
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenUsingDifferentTokenSource_ShouldNotReportDiagnostic()
    {
        const string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Threading;
            using System.Threading.Tasks;

            [TestClass]
            public class MyTestClass
            {
                private readonly CancellationTokenSource _cts = new();
                public TestContext TestContext { get; set; }

                [TestMethod]
                public async Task MyTest()
                {
                    await SomeAsyncOperation(_cts.Token);
                }

                private static Task SomeAsyncOperation(CancellationToken cancellationToken)
                    => Task.CompletedTask;
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }
}