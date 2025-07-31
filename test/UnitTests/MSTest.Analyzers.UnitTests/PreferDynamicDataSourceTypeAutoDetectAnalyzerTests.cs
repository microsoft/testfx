// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.PreferDynamicDataSourceTypeAutoDetectAnalyzer,
    MSTest.Analyzers.PreferDynamicDataSourceTypeAutoDetectFixer>;

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class PreferDynamicDataSourceTypeAutoDetectAnalyzerTests
{
    [TestMethod]
    public async Task WhenDynamicDataUsesAutoDetect_NoDiagnostic()
    {
        string code = """
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [DynamicData("Data")]
                [TestMethod]
                public void TestMethod1(object[] o) { }

                [DynamicData("Data", DynamicDataSourceType.AutoDetect)]
                [TestMethod]
                public void TestMethod2(object[] o) { }

                [DynamicData("Data", typeof(MyTestClass))]
                [TestMethod]
                public void TestMethod3(object[] o) { }

                [DynamicData("Data", typeof(MyTestClass), DynamicDataSourceType.AutoDetect)]
                [TestMethod]
                public void TestMethod4(object[] o) { }

                static IEnumerable<object[]> Data => new[] { new object[] { 1 } };
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenDynamicDataUsesProperty_ReportsDiagnostic()
    {
        string code = """
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [DynamicData("Data", {|#0:DynamicDataSourceType.Property|})]
                [TestMethod]
                public void TestMethod1(object[] o) { }

                static IEnumerable<object[]> Data => new[] { new object[] { 1 } };
            }
            """;

        string fixedCode = """
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [DynamicData("Data")]
                [TestMethod]
                public void TestMethod1(object[] o) { }

                static IEnumerable<object[]> Data => new[] { new object[] { 1 } };
            }
            """;

        var expected = VerifyCS.Diagnostic().WithLocation(0).WithArguments("Property");
        await VerifyCS.VerifyCodeFixAsync(code, expected, fixedCode);
    }

    [TestMethod]
    public async Task WhenDynamicDataUsesMethod_ReportsDiagnostic()
    {
        string code = """
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [DynamicData("GetData", {|#0:DynamicDataSourceType.Method|})]
                [TestMethod]
                public void TestMethod1(object[] o) { }

                static IEnumerable<object[]> GetData() => new[] { new object[] { 1 } };
            }
            """;

        string fixedCode = """
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [DynamicData("GetData")]
                [TestMethod]
                public void TestMethod1(object[] o) { }

                static IEnumerable<object[]> GetData() => new[] { new object[] { 1 } };
            }
            """;

        var expected = VerifyCS.Diagnostic().WithLocation(0).WithArguments("Method");
        await VerifyCS.VerifyCodeFixAsync(code, expected, fixedCode);
    }

    [TestMethod]
    public async Task WhenDynamicDataUsesPropertyWithDeclaringType_ReportsDiagnostic()
    {
        string code = """
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [DynamicData("Data", typeof(MyTestClass), {|#0:DynamicDataSourceType.Property|})]
                [TestMethod]
                public void TestMethod1(object[] o) { }

                static IEnumerable<object[]> Data => new[] { new object[] { 1 } };
            }
            """;

        string fixedCode = """
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [DynamicData("Data", typeof(MyTestClass))]
                [TestMethod]
                public void TestMethod1(object[] o) { }

                static IEnumerable<object[]> Data => new[] { new object[] { 1 } };
            }
            """;

        var expected = VerifyCS.Diagnostic().WithLocation(0).WithArguments("Property");
        await VerifyCS.VerifyCodeFixAsync(code, expected, fixedCode);
    }

    [TestMethod]
    public async Task WhenDynamicDataUsesMethodWithDeclaringType_ReportsDiagnostic()
    {
        string code = """
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [DynamicData("GetData", typeof(MyTestClass), {|#0:DynamicDataSourceType.Method|})]
                [TestMethod]
                public void TestMethod1(object[] o) { }

                static IEnumerable<object[]> GetData() => new[] { new object[] { 1 } };
            }
            """;

        string fixedCode = """
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [DynamicData("GetData", typeof(MyTestClass))]
                [TestMethod]
                public void TestMethod1(object[] o) { }

                static IEnumerable<object[]> GetData() => new[] { new object[] { 1 } };
            }
            """;

        var expected = VerifyCS.Diagnostic().WithLocation(0).WithArguments("Method");
        await VerifyCS.VerifyCodeFixAsync(code, expected, fixedCode);
    }

    [TestMethod]
    public async Task WhenMultipleDynamicDataAttributesWithExplicitTypes_ReportsMultipleDiagnostics()
    {
        string code = """
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [DynamicData("Data", {|#0:DynamicDataSourceType.Property|})]
                [DynamicData("GetData", {|#1:DynamicDataSourceType.Method|})]
                [TestMethod]
                public void TestMethod1(object[] o) { }

                static IEnumerable<object[]> Data => new[] { new object[] { 1 } };
                static IEnumerable<object[]> GetData() => new[] { new object[] { 2 } };
            }
            """;

        string fixedCode = """
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [DynamicData("Data")]
                [DynamicData("GetData")]
                [TestMethod]
                public void TestMethod1(object[] o) { }

                static IEnumerable<object[]> Data => new[] { new object[] { 1 } };
                static IEnumerable<object[]> GetData() => new[] { new object[] { 2 } };
            }
            """;

        var expected1 = VerifyCS.Diagnostic().WithLocation(0).WithArguments("Property");
        var expected2 = VerifyCS.Diagnostic().WithLocation(1).WithArguments("Method");
        await VerifyCS.VerifyCodeFixAsync(code, [expected1, expected2], fixedCode);
    }
}