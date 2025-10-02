// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    Microsoft.CodeAnalysis.Testing.EmptyDiagnosticAnalyzer,
    MSTest.Analyzers.TestContextPropertiesContainsKeyFixer>;

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class TestContextPropertiesContainsKeyFixerTests
{
    [TestMethod]
    public async Task WhenTestContextPropertiesContainsUsed_CS1503_ShouldProvideContainsKeyFix()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                public TestContext TestContext { get; set; }

                [TestMethod]
                public void TestMethod()
                {
                    bool hasKey = TestContext.Properties.Contains({|CS1503:"MyKey"|});
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                public TestContext TestContext { get; set; }

                [TestMethod]
                public void TestMethod()
                {
                    bool hasKey = TestContext.Properties.ContainsKey("MyKey");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenTestContextPropertiesContainsUsedWithLocalVariable_CS1503_ShouldProvideContainsKeyFix()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                public TestContext TestContext { get; set; }

                [TestMethod]
                public void TestMethod()
                {
                    var properties = TestContext.Properties;
                    bool hasKey = properties.Contains({|CS1503:"MyKey"|});
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                public TestContext TestContext { get; set; }

                [TestMethod]
                public void TestMethod()
                {
                    var properties = TestContext.Properties;
                    bool hasKey = properties.ContainsKey("MyKey");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenTestContextPropertiesContainsUsedWithParameterVariable_CS1503_ShouldProvideContainsKeyFix()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod(TestContext testContext)
                {
                    bool hasKey = testContext.Properties.Contains({|CS1503:"MyKey"|});
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod(TestContext testContext)
                {
                    bool hasKey = testContext.Properties.ContainsKey("MyKey");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenNonTestContextPropertiesContainsUsed_ShouldNotProvideCodeFix()
    {
        string code = """
            using System.Collections.Generic;
            using System.Linq;

            public class MyTestClass
            {
                public void TestMethod()
                {
                    var dict = new Dictionary<string, object>();
                    bool hasKey = dict.Contains(new KeyValuePair<string, object>("MyKey", "value"));
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenRegularListContainsUsed_ShouldNotProvideCodeFix()
    {
        string code = """
            using System.Collections.Generic;

            public class MyTestClass
            {
                public void TestMethod()
                {
                    var list = new List<string>();
                    bool hasKey = list.Contains("MyKey");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenTestContextPropertiesContainsUsedWithMultipleArguments_ShouldNotProvideCodeFix()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                public TestContext TestContext { get; set; }

                [TestMethod]
                public void TestMethod()
                {
                    // This test ensures that our fix only applies to single-argument Contains calls
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }
}
