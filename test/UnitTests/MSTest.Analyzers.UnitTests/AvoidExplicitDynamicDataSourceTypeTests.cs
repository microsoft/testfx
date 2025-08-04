// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.AvoidExplicitDynamicDataSourceTypeAnalyzer,
    MSTest.Analyzers.AvoidExplicitDynamicDataSourceTypeFixer>;

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class AvoidExplicitDynamicDataSourceTypeTests
{
    [TestMethod]
    public async Task WhenDynamicDataUsesAutoDetectImplicitly_NoDiagnostic()
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


                [DynamicData("Data", typeof(MyTestClass))]
                [TestMethod]
                public void TestMethod2(object[] o) { }

                static IEnumerable<object[]> Data => new[] { new object[] { 1 } };
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenDynamicDataUsesAutoDetectExplicitly_Diagnostic()
    {
        string code = """
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [[|DynamicData("Data", DynamicDataSourceType.AutoDetect)|]]
                [TestMethod]
                public void TestMethod1(object[] o) { }

                [[|DynamicData("Data", typeof(MyTestClass), DynamicDataSourceType.AutoDetect)|]]
                [TestMethod]
                public void TestMethod2(object[] o) { }

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

                [DynamicData("Data", typeof(MyTestClass))]
                [TestMethod]
                public void TestMethod2(object[] o) { }

                static IEnumerable<object[]> Data => new[] { new object[] { 1 } };
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
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
                [[|DynamicData("Data", DynamicDataSourceType.Property)|]]
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

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
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
                [[|DynamicData("GetData", DynamicDataSourceType.Method)|]]
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

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
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
                [[|DynamicData("Data", typeof(MyTestClass), DynamicDataSourceType.Property)|]]
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

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
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
                [[|DynamicData("GetData", typeof(MyTestClass), DynamicDataSourceType.Method)|]]
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

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
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
                [[|DynamicData("Data", DynamicDataSourceType.Property)|]]
                [[|DynamicData("GetData", DynamicDataSourceType.Method)|]]
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

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }
}
