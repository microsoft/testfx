// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.PreferConstantForResourceLockAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class PreferConstantForResourceLockAnalyzerTests
{
    [TestMethod]
    public async Task WhenResourceKeyIsBareLiteralOnMethod_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [ResourceLock([|"my-resource"|])]
                [TestMethod]
                public void MyTestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenResourceKeyIsBareLiteralOnClass_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [ResourceLock([|"my-resource"|])]
            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenResourceKeyIsWellKnownResourcesConstant_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [ResourceLock(WellKnownResources.EnvironmentVariables)]
                [TestMethod]
                public void MyTestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenResourceKeyIsUserConstant_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public static class MyResources
            {
                public const string Database = "database";
            }

            [TestClass]
            public class MyTestClass
            {
                [ResourceLock(MyResources.Database)]
                [TestMethod]
                public void MyTestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenResourceKeyIsLiteralConcatenation_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [ResourceLock([|"my"|] + "-resource")]
                [TestMethod]
                public void MyTestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenLiteralIsUsedWithMode_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [ResourceLock([|"my-resource"|], Mode = ResourceAccessMode.Read)]
                [TestMethod]
                public void MyTestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenMultipleLocksMixLiteralAndConstant_DiagnosticOnlyOnLiteral()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [ResourceLock(WellKnownResources.Console)]
                [ResourceLock([|"my-resource"|])]
                [TestMethod]
                public void MyTestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }
}
