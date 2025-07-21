// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.PreferDisposeOverTestCleanupAnalyzer,
    MSTest.Analyzers.PreferDisposeOverTestCleanupFixer>;

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class PreferDisposeOverTestCleanupAnalyzerTests
{
    [TestMethod]
    public async Task WhenTestClassHasDispose_NoDiagnostic()
    {
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass : IDisposable
            {
                public void Dispose()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

#if NET
    [TestMethod]
    public async Task WhenTestClassHasDisposeAsync_NoDiagnostic()
    {
        string code = """
            using System;
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass : IAsyncDisposable
            {
                public ValueTask DisposeAsync()
                {
                    return ValueTask.CompletedTask;
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }
#endif

    [TestMethod]
    public async Task WhenTestClassHasTestCleanup_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            
            [TestClass]
            public class MyTestClass
            {
                [TestCleanup]
                public void [|MyTestCleanup|]()
                {
                    int x = 1;
                }
            }
            """;
        string fixedCode = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            
            [TestClass]
            public class MyTestClass : IDisposable
            {
                public void Dispose()
                {
                    int x = 1;
                }
            }
            """;
        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenTestClassHasTestCleanup_WithAnotherBaseClass_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System;
            
            public class LocalBase{}

            [TestClass]
            public class MyTestClass : LocalBase
            {
                [TestCleanup]
                public void [|MyTestCleanup|]()
                {
                }
            }
            """;
        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System;

            public class LocalBase{}
            
            [TestClass]
            public class MyTestClass : LocalBase, IDisposable
            {
                public void Dispose()
                {
                }
            }
            """;
        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenTestClassHasTestCleanup_AndHasDispose_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System;
            
            [TestClass]
            public class MyTestClass : IDisposable
            {
                [TestCleanup]
                public void [|MyTestCleanup|]()
                {
                    int y = 1;
                }

                public void Dispose()
                {
                    int x = 1;
                }
            }
            """;
        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System;
            
            [TestClass]
            public class MyTestClass : IDisposable
            {
                public void Dispose()
                {
                    int x = 1;
                    int y = 1;
                }
            }
            """;
        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenTestClassHasTestCleanup_AndHasDisposeInAnotherPartial_Diagnostic()
    {
        // This scenario is currently broken. The test is to document the current behavior
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System;

            public partial class MyTestClass : IDisposable
            {
                public void Dispose()
                {
                    int x = 1;
                }
            }

            [TestClass]
            public partial class MyTestClass
            {
                [TestCleanup]
                public void [|MyTestCleanup|]()
                {
                    int y = 1;
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System;
            
            public partial class MyTestClass : IDisposable
            {
                public void Dispose()
                {
                    int x = 1;
                }
            }
            
            [TestClass]
            public partial class MyTestClass : IDisposable
            {
                public void {|CS0111:Dispose|}()
                {
                    int y = 1;
                }
            }
            """;
        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenTestClassHasTestCleanupTask_Diagnostic()
    {
        string code = """
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System;
            
            [TestClass]
            public class MyTestClass
            {
                [TestCleanup]
                public Task [|MyTestCleanup|]()
                {
                    int x=1;
                    return Task.CompletedTask;
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

#if NET
    [TestMethod]
    public async Task WhenTestClassHasTestCleanupValueTask_Diagnostic()
    {
        string code = """
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System;
            
            [TestClass]
            public class MyTestClass
            {
                [TestCleanup]
                public ValueTask [|MyTestCleanup|]()
                {
                    return ValueTask.CompletedTask;
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }
#endif
}
