// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.DynamicDataShouldBeValidAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace MSTest.Analyzers.Test;

[TestGroup]
public sealed class DynamicDataShouldBeValidAnalyzerTests(ITestExecutionContext testExecutionContext) : TestBase(testExecutionContext)
{
    public async Task ValidUsages_NoDiagnostic()
    {
        string code = """
            using System;
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [DynamicData("Data")]
                [TestMethod]
                public void TestMethod1(object[] o)
                {
                }

                [DynamicData("SomeData", typeof(SomeClass))]
                [TestMethod]
                public void TestMethod2(object[] o)
                {
                }

                [DynamicData(dynamicDataSourceName: "Data")]
                [TestMethod]
                public void TestMethod3(object[] o)
                {
                }

                [DynamicData(dynamicDataDeclaringType: typeof(SomeClass), dynamicDataSourceName: "SomeData")]
                [TestMethod]
                public void TestMethod4(object[] o)
                {
                }

                [DynamicData("GetData", DynamicDataSourceType.Method)]
                [TestMethod]
                public void TestMethod11(object[] o)
                {
                }

                [DynamicData("GetSomeData", typeof(SomeClass), DynamicDataSourceType.Method)]
                [TestMethod]
                public void TestMethod12(object[] o)
                {
                }

                [DynamicData(dynamicDataSourceType: DynamicDataSourceType.Method, dynamicDataSourceName: "GetData")]
                [TestMethod]
                public void TestMethod13(object[] o)
                {
                }

                [DynamicData(dynamicDataDeclaringType: typeof(SomeClass), dynamicDataSourceType: DynamicDataSourceType.Method, dynamicDataSourceName: "GetSomeData")]
                [TestMethod]
                public void TestMethod14(object[] o)
                {
                }

                [DynamicData("DataTuple")]
                [TestMethod]
                public void TestMethod101(int i, string s)
                {
                }

                [DynamicData("SomeDataTuple", typeof(SomeClass))]
                [TestMethod]
                public void TestMethod102(int i, string s)
                {
                }

                [DynamicData(dynamicDataSourceName: "DataTuple")]
                [TestMethod]
                public void TestMethod103(int i, string s)
                {
                }

                [DynamicData(dynamicDataDeclaringType: typeof(SomeClass), dynamicDataSourceName: "SomeDataTuple")]
                [TestMethod]
                public void TestMethod104(int i, string s)
                {
                }

                [DynamicData("GetDataTuple", DynamicDataSourceType.Method)]
                [TestMethod]
                public void TestMethod111(int i, string s)
                {
                }

                [DynamicData("GetSomeDataTuple", typeof(SomeClass), DynamicDataSourceType.Method)]
                [TestMethod]
                public void TestMethod112(int i, string s)
                {
                }

                [DynamicData(dynamicDataSourceType: DynamicDataSourceType.Method, dynamicDataSourceName: "GetDataTuple")]
                [TestMethod]
                public void TestMethod113(int i, string s)
                {
                }

                [DynamicData(dynamicDataDeclaringType: typeof(SomeClass), dynamicDataSourceType: DynamicDataSourceType.Method, dynamicDataSourceName: "GetSomeDataTuple")]
                [TestMethod]
                public void TestMethod114(int i, string s)
                {
                }

                [DynamicData("DataValueTuple")]
                [TestMethod]
                public void TestMethod201(int i, string s)
                {
                }

                [DynamicData("SomeDataValueTuple", typeof(SomeClass))]
                [TestMethod]
                public void TestMethod202(int i, string s)
                {
                }

                [DynamicData(dynamicDataSourceName: "DataValueTuple")]
                [TestMethod]
                public void TestMethod203(int i, string s)
                {
                }

                [DynamicData(dynamicDataDeclaringType: typeof(SomeClass), dynamicDataSourceName: "SomeDataValueTuple")]
                [TestMethod]
                public void TestMethod204(int i, string s)
                {
                }

                [DynamicData("GetDataValueTuple", DynamicDataSourceType.Method)]
                [TestMethod]
                public void TestMethod211(int i, string s)
                {
                }

                [DynamicData("GetSomeDataValueTuple", typeof(SomeClass), DynamicDataSourceType.Method)]
                [TestMethod]
                public void TestMethod212(int i, string s)
                {
                }

                [DynamicData(dynamicDataSourceType: DynamicDataSourceType.Method, dynamicDataSourceName: "GetDataValueTuple")]
                [TestMethod]
                public void TestMethod213(int i, string s)
                {
                }

                [DynamicData(dynamicDataDeclaringType: typeof(SomeClass), dynamicDataSourceType: DynamicDataSourceType.Method, dynamicDataSourceName: "GetSomeDataValueTuple")]
                [TestMethod]
                public void TestMethod214(int i, string s)
                {
                }

                public static IEnumerable<object[]> Data => new List<object[]>();
                public static IEnumerable<Tuple<int, string>> DataTuple => new List<Tuple<int, string>>();
                public static IEnumerable<(int, string)> DataValueTuple => new List<(int, string)>();
                public static IEnumerable<object[]> GetData() => new List<object[]>();
                public static IEnumerable<Tuple<int, string>> GetDataTuple() => new List<Tuple<int, string>>();
                public static IEnumerable<(int, string)> GetDataValueTuple() => new List<(int, string)>();
            }

            public class SomeClass
            {
                public static IEnumerable<object[]> SomeData => new List<object[]>();
                public static IEnumerable<Tuple<int, string>> SomeDataTuple => new List<Tuple<int, string>>();
                public static IEnumerable<(int, string)> SomeDataValueTuple => new List<(int, string)>();
                public static IEnumerable<object[]> GetSomeData() => new List<object[]>();
                public static IEnumerable<Tuple<int, string>> GetSomeDataTuple() => new List<Tuple<int, string>>();
                public static IEnumerable<(int, string)> GetSomeDataValueTuple() => new List<(int, string)>();
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenDataSourceMemberDoesNotExist_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [{|#0:DynamicData("MemberNotFound")|}]
                [TestMethod]
                public void TestMethod1(object[] o)
                {
                }

                [{|#1:DynamicData("MemberNotFound", typeof(SomeClass))|}]
                [TestMethod]
                public void TestMethod2(object[] o)
                {
                }

                [{|#2:DynamicData(dynamicDataSourceName: "MemberNotFound")|}]
                [TestMethod]
                public void TestMethod3(object[] o)
                {
                }

                [{|#3:DynamicData(dynamicDataDeclaringType: typeof(SomeClass), dynamicDataSourceName: "MemberNotFound")|}]
                [TestMethod]
                public void TestMethod4(object[] o)
                {
                }

                [{|#4:DynamicData("MemberNotFound", DynamicDataSourceType.Method)|}]
                [TestMethod]
                public void TestMethod11(object[] o)
                {
                }

                [{|#5:DynamicData("MemberNotFound", typeof(SomeClass), DynamicDataSourceType.Method)|}]
                [TestMethod]
                public void TestMethod12(object[] o)
                {
                }

                [{|#6:DynamicData(dynamicDataSourceType: DynamicDataSourceType.Method, dynamicDataSourceName: "MemberNotFound")|}]
                [TestMethod]
                public void TestMethod13(object[] o)
                {
                }

                [{|#7:DynamicData(dynamicDataDeclaringType: typeof(SomeClass), dynamicDataSourceType: DynamicDataSourceType.Method, dynamicDataSourceName: "MemberNotFound")|}]
                [TestMethod]
                public void TestMethod14(object[] o)
                {
                }
            }

            public class SomeClass
            {
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.MemberNotFoundRule).WithLocation(0).WithArguments("MyTestClass", "MemberNotFound"),
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.MemberNotFoundRule).WithLocation(1).WithArguments("SomeClass", "MemberNotFound"),
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.MemberNotFoundRule).WithLocation(2).WithArguments("MyTestClass", "MemberNotFound"),
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.MemberNotFoundRule).WithLocation(3).WithArguments("SomeClass", "MemberNotFound"),
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.MemberNotFoundRule).WithLocation(4).WithArguments("MyTestClass", "MemberNotFound"),
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.MemberNotFoundRule).WithLocation(5).WithArguments("SomeClass", "MemberNotFound"),
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.MemberNotFoundRule).WithLocation(6).WithArguments("MyTestClass", "MemberNotFound"),
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.MemberNotFoundRule).WithLocation(7).WithArguments("SomeClass", "MemberNotFound"));
    }

    /*
    public async Task WhenDataDisplayMemberDoesNotExist_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [DynamicData("SomeProperty")]
                [TestMethod]
                public void TestMethod1(object[] o)
                {
                }

                [DynamicData("SomeProperty", typeof(SomeClass))]
                [TestMethod]
                public void TestMethod2(object[] o)
                {
                }

                [DynamicData(dynamicDataSourceName: "SomeProperty")]
                [TestMethod]
                public void TestMethod3(object[] o)
                {
                }

                [DynamicData(dynamicDataDeclaringType: typeof(SomeClass), dynamicDataSourceName: "SomeProperty")]
                [TestMethod]
                public void TestMethod4(object[] o)
                {
                }
            }

            public class SomeClass
            {
                public static IEnumerable<object[]> SomeProperty { get; set; }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }
    */

    public async Task WhenAppliedToNonTestMethod_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [DynamicData("SomeProperty")]
                public void {|#0:TestMethod1|}(object[] o)
                {
                }

                [DynamicData("SomeProperty", typeof(SomeClass))]
                public void {|#1:TestMethod2|}(object[] o)
                {
                }

                [DynamicData(dynamicDataSourceName: "SomeProperty")]
                public void {|#2:TestMethod3|}(object[] o)
                {
                }

                [DynamicData(dynamicDataDeclaringType: typeof(SomeClass), dynamicDataSourceName: "SomeProperty")]
                public void {|#3:TestMethod4|}(object[] o)
                {
                }
            }

            public class SomeClass
            {
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.NotTestMethodRule).WithLocation(0),
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.NotTestMethodRule).WithLocation(1),
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.NotTestMethodRule).WithLocation(2),
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.NotTestMethodRule).WithLocation(3));
    }

    public async Task WhenDataSourceMemberFoundMultipleTimes_Diagnostic()
    {
        string code = """
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [{|#0:DynamicData("GetData", DynamicDataSourceType.Method)|}]
                [TestMethod]
                public void TestMethod1(object[] o)
                {
                }

                [{|#1:DynamicData("GetSomeData", typeof(SomeClass), DynamicDataSourceType.Method)|}]
                [TestMethod]
                public void TestMethod2(object[] o)
                {
                }

                [{|#2:DynamicData(dynamicDataSourceType: DynamicDataSourceType.Method, dynamicDataSourceName: "GetData")|}]
                [TestMethod]
                public void TestMethod3(object[] o)
                {
                }

                [{|#3:DynamicData(dynamicDataDeclaringType: typeof(SomeClass), dynamicDataSourceType: DynamicDataSourceType.Method, dynamicDataSourceName: "GetSomeData")|}]
                [TestMethod]
                public void TestMethod4(object[] o)
                {
                }

                public static IEnumerable<object[]> GetData() => new List<object[]>();
                public static IEnumerable<object[]> GetData(int i) => new List<object[]>();
            }

            public class SomeClass
            {
                public static IEnumerable<object[]> GetSomeData() => new List<object[]>();
                public static IEnumerable<object[]> GetSomeData(int i) => new List<object[]>();
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.FoundTooManyMembersRule).WithLocation(0).WithArguments("MyTestClass", "GetData"),
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.FoundTooManyMembersRule).WithLocation(1).WithArguments("SomeClass", "GetSomeData"),
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.FoundTooManyMembersRule).WithLocation(2).WithArguments("MyTestClass", "GetData"),
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.FoundTooManyMembersRule).WithLocation(3).WithArguments("SomeClass", "GetSomeData"));
    }

    public async Task WhenMemberKindIsMixedUp_Diagnostic()
    {
        string code = """
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [{|#0:DynamicData("GetData")|}]
                [TestMethod]
                public void TestMethod1(object[] o)
                {
                }

                [{|#1:DynamicData("GetSomeData", typeof(SomeClass))|}]
                [TestMethod]
                public void TestMethod2(object[] o)
                {
                }

                [{|#2:DynamicData(dynamicDataSourceName: "GetData")|}]
                [TestMethod]
                public void TestMethod3(object[] o)
                {
                }

                [{|#3:DynamicData(dynamicDataDeclaringType: typeof(SomeClass), dynamicDataSourceName: "GetSomeData")|}]
                [TestMethod]
                public void TestMethod4(object[] o)
                {
                }

                [{|#4:DynamicData("Data", DynamicDataSourceType.Method)|}]
                [TestMethod]
                public void TestMethod11(object[] o)
                {
                }

                [{|#5:DynamicData("SomeData", typeof(SomeClass), DynamicDataSourceType.Method)|}]
                [TestMethod]
                public void TestMethod12(object[] o)
                {
                }

                [{|#6:DynamicData(dynamicDataSourceType: DynamicDataSourceType.Method, dynamicDataSourceName: "Data")|}]
                [TestMethod]
                public void TestMethod13(object[] o)
                {
                }

                [{|#7:DynamicData(dynamicDataDeclaringType: typeof(SomeClass), dynamicDataSourceType: DynamicDataSourceType.Method, dynamicDataSourceName: "SomeData")|}]
                [TestMethod]
                public void TestMethod14(object[] o)
                {
                }

                public static IEnumerable<object[]> Data => new List<object[]>();
                public static IEnumerable<object[]> GetData() => new List<object[]>();
            }

            public class SomeClass
            {
                public static IEnumerable<object[]> SomeData => new List<object[]>();
                public static IEnumerable<object[]> GetSomeData() => new List<object[]>();
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.MemberMethodRule).WithLocation(0).WithArguments("MyTestClass", "MemberNotFound"),
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.MemberMethodRule).WithLocation(1).WithArguments("SomeClass", "MemberNotFound"),
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.MemberMethodRule).WithLocation(2).WithArguments("MyTestClass", "MemberNotFound"),
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.MemberMethodRule).WithLocation(3).WithArguments("SomeClass", "MemberNotFound"),
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.MemberPropertyRule).WithLocation(4).WithArguments("MyTestClass", "MemberNotFound"),
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.MemberPropertyRule).WithLocation(5).WithArguments("SomeClass", "MemberNotFound"),
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.MemberPropertyRule).WithLocation(6).WithArguments("MyTestClass", "MemberNotFound"),
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.MemberPropertyRule).WithLocation(7).WithArguments("SomeClass", "MemberNotFound"));
    }

    public async Task WhenDataSourceReturnTypeIsInvalid_Diagnostic()
    {
        string code = """
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [{|#0:DynamicData("Data")|}]
                [TestMethod]
                public void TestMethod1(object[] o)
                {
                }

                [{|#1:DynamicData("SomeData", typeof(SomeClass))|}]
                [TestMethod]
                public void TestMethod2(object[] o)
                {
                }

                [{|#2:DynamicData(dynamicDataSourceName: "Data")|}]
                [TestMethod]
                public void TestMethod3(object[] o)
                {
                }

                [{|#3:DynamicData(dynamicDataDeclaringType: typeof(SomeClass), dynamicDataSourceName: "SomeData")|}]
                [TestMethod]
                public void TestMethod4(object[] o)
                {
                }

                [{|#4:DynamicData("GetData", DynamicDataSourceType.Method)|}]
                [TestMethod]
                public void TestMethod11(object[] o)
                {
                }

                [{|#5:DynamicData("GetSomeData", typeof(SomeClass), DynamicDataSourceType.Method)|}]
                [TestMethod]
                public void TestMethod12(object[] o)
                {
                }

                [{|#6:DynamicData(dynamicDataSourceType: DynamicDataSourceType.Method, dynamicDataSourceName: "GetData")|}]
                [TestMethod]
                public void TestMethod13(object[] o)
                {
                }

                [{|#7:DynamicData(dynamicDataDeclaringType: typeof(SomeClass), dynamicDataSourceType: DynamicDataSourceType.Method, dynamicDataSourceName: "GetSomeData")|}]
                [TestMethod]
                public void TestMethod14(object[] o)
                {
                }

                public static IEnumerable<object> Data => new List<object>();
                public static IEnumerable<object> GetData() => new List<object>();
            }

            public class SomeClass
            {
                public static IEnumerable<object> SomeData => new List<object>();
                public static IEnumerable<object> GetSomeData() => new List<object>();
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.MemberMethodRule).WithLocation(0).WithArguments("MyTestClass", "MemberNotFound"),
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.MemberMethodRule).WithLocation(1).WithArguments("SomeClass", "MemberNotFound"),
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.MemberMethodRule).WithLocation(2).WithArguments("MyTestClass", "MemberNotFound"),
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.MemberMethodRule).WithLocation(3).WithArguments("SomeClass", "MemberNotFound"),
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.MemberPropertyRule).WithLocation(4).WithArguments("MyTestClass", "MemberNotFound"),
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.MemberPropertyRule).WithLocation(5).WithArguments("SomeClass", "MemberNotFound"),
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.MemberPropertyRule).WithLocation(6).WithArguments("MyTestClass", "MemberNotFound"),
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.MemberPropertyRule).WithLocation(7).WithArguments("SomeClass", "MemberNotFound"));
    }
}
