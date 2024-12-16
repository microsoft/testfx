﻿// Copyright (c) Microsoft Corporation. All rights reserved.
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
                public void TestMethod1Auto(object[] o)
                {
                }

                [DynamicData("Data", DynamicDataSourceType.Property)]
                [TestMethod]
                public void TestMethod1Property(object[] o)
                {
                }

                [DynamicData("SomeData", typeof(SomeClass))]
                [TestMethod]
                public void TestMethod2Auto(object[] o)
                {
                }

                [DynamicData("SomeData", typeof(SomeClass), DynamicDataSourceType.Property)]
                [TestMethod]
                public void TestMethod2Property(object[] o)
                {
                }

                [DynamicData(dynamicDataSourceName: "Data")]
                [TestMethod]
                public void TestMethod3Auto(object[] o)
                {
                }

                [DynamicData(dynamicDataSourceName: "Data", DynamicDataSourceType.Property)]
                [TestMethod]
                public void TestMethod3Property(object[] o)
                {
                }

                [DynamicData(dynamicDataDeclaringType: typeof(SomeClass), dynamicDataSourceName: "SomeData")]
                [TestMethod]
                public void TestMethod4Auto(object[] o)
                {
                }

                [DynamicData(dynamicDataDeclaringType: typeof(SomeClass), dynamicDataSourceName: "SomeData", dynamicDataSourceType: DynamicDataSourceType.Property)]
                [TestMethod]
                public void TestMethod4Property(object[] o)
                {
                }

                [DynamicData("GetData", DynamicDataSourceType.Method)]
                [TestMethod]
                public void TestMethod11Method(object[] o)
                {
                }

                [DynamicData("GetData")]
                [TestMethod]
                public void TestMethod11Auto(object[] o)
                {
                }

                [DynamicData("GetSomeData", typeof(SomeClass), DynamicDataSourceType.Method)]
                [TestMethod]
                public void TestMethod12Method(object[] o)
                {
                }

                [DynamicData("GetSomeData", typeof(SomeClass))]
                [TestMethod]
                public void TestMethod12Auto(object[] o)
                {
                }

                [DynamicData(dynamicDataSourceType: DynamicDataSourceType.Method, dynamicDataSourceName: "GetData")]
                [TestMethod]
                public void TestMethod13Method(object[] o)
                {
                }

                [DynamicData(dynamicDataSourceType: DynamicDataSourceType.AutoDetect, dynamicDataSourceName: "GetData")]
                [TestMethod]
                public void TestMethod13Auto(object[] o)
                {
                }

                [DynamicData(dynamicDataDeclaringType: typeof(SomeClass), dynamicDataSourceType: DynamicDataSourceType.Method, dynamicDataSourceName: "GetSomeData")]
                [TestMethod]
                public void TestMethod14Method(object[] o)
                {
                }

                [DynamicData(dynamicDataDeclaringType: typeof(SomeClass), dynamicDataSourceType: DynamicDataSourceType.AutoDetect, dynamicDataSourceName: "GetSomeData")]
                [TestMethod]
                public void TestMethod14Auto(object[] o)
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

                [DynamicData("DataJaggedArray")]
                [TestMethod]
                public void TestMethod301(MyTestClass[] testClasses)
                {
                }

                [DynamicData("SomeDataJaggedArray", typeof(SomeClass))]
                [TestMethod]
                public void TestMethod302(MyTestClass[] testClasses)
                {
                }

                [DynamicData(dynamicDataSourceName: "DataJaggedArray")]
                [TestMethod]
                public void TestMethod303(MyTestClass[] testClasses)
                {
                }

                [DynamicData(dynamicDataDeclaringType: typeof(SomeClass), dynamicDataSourceName: "SomeDataJaggedArray")]
                [TestMethod]
                public void TestMethod304(MyTestClass[] testClasses)
                {
                }

                [DynamicData("GetDataJaggedArray", DynamicDataSourceType.Method)]
                [TestMethod]
                public void TestMethod311(MyTestClass[] testClasses)
                {
                }

                [DynamicData("GetSomeDataJaggedArray", typeof(SomeClass), DynamicDataSourceType.Method)]
                [TestMethod]
                public void TestMethod312(MyTestClass[] testClasses)
                {
                }

                [DynamicData(dynamicDataSourceType: DynamicDataSourceType.Method, dynamicDataSourceName: "GetDataJaggedArray")]
                [TestMethod]
                public void TestMethod313(MyTestClass[] testClasses)
                {
                }

                [DynamicData(dynamicDataDeclaringType: typeof(SomeClass), dynamicDataSourceType: DynamicDataSourceType.Method, dynamicDataSourceName: "GetSomeDataJaggedArray")]
                [TestMethod]
                public void TestMethod314(MyTestClass[] testClasses)
                {
                }

                [DynamicData("DataNonObjectTypeArray")]
                [TestMethod]
                public void TestMethod401(MyTestClass[] testClasses)
                {
                }

                [DynamicData("SomeDataNonObjectTypeArray", typeof(SomeClass))]
                [TestMethod]
                public void TestMethod402(MyTestClass[] testClasses)
                {
                }

                [DynamicData(dynamicDataSourceName: "DataNonObjectTypeArray")]
                [TestMethod]
                public void TestMethod403(MyTestClass[] testClasses)
                {
                }

                [DynamicData(dynamicDataDeclaringType: typeof(SomeClass), dynamicDataSourceName: "SomeDataNonObjectTypeArray")]
                [TestMethod]
                public void TestMethod404(MyTestClass[] testClasses)
                {
                }

                [DynamicData("GetDataNonObjectTypeArray", DynamicDataSourceType.Method)]
                [TestMethod]
                public void TestMethod411(MyTestClass[] testClasses)
                {
                }

                [DynamicData("GetSomeDataNonObjectTypeArray", typeof(SomeClass), DynamicDataSourceType.Method)]
                [TestMethod]
                public void TestMethod412(MyTestClass[] testClasses)
                {
                }

                [DynamicData(dynamicDataSourceType: DynamicDataSourceType.Method, dynamicDataSourceName: "GetDataNonObjectTypeArray")]
                [TestMethod]
                public void TestMethod413(MyTestClass[] testClasses)
                {
                }

                [DynamicData(dynamicDataDeclaringType: typeof(SomeClass), dynamicDataSourceType: DynamicDataSourceType.Method, dynamicDataSourceName: "GetSomeDataNonObjectTypeArray")]
                [TestMethod]
                public void TestMethod414(MyTestClass[] testClasses)
                {
                }

                public static IEnumerable<object[]> Data => new List<object[]>();
                public static IEnumerable<Tuple<int, string>> DataTuple => new List<Tuple<int, string>>();
                public static IEnumerable<(int, string)> DataValueTuple => new List<(int, string)>();
                public static MyTestClass[][] DataJaggedArray => System.Array.Empty<MyTestClass[]>();
                public static IEnumerable<MyTestClass[]> DataNonObjectTypeArray => new List<MyTestClass[]>();
                public static IEnumerable<object[]> GetData() => new List<object[]>();
                public static IEnumerable<Tuple<int, string>> GetDataTuple() => new List<Tuple<int, string>>();
                public static IEnumerable<(int, string)> GetDataValueTuple() => new List<(int, string)>();
                public static MyTestClass[][] GetDataJaggedArray() => System.Array.Empty<MyTestClass[]>();
                public static IEnumerable<MyTestClass[]> GetDataNonObjectTypeArray() => new List<MyTestClass[]>();
            }

            public class SomeClass
            {
                public static IEnumerable<object[]> SomeData => new List<object[]>();
                public static IEnumerable<Tuple<int, string>> SomeDataTuple => new List<Tuple<int, string>>();
                public static IEnumerable<(int, string)> SomeDataValueTuple => new List<(int, string)>();
                public static MyTestClass[][] SomeDataJaggedArray => System.Array.Empty<MyTestClass[]>();
                public static IEnumerable<MyTestClass[]> SomeDataNonObjectTypeArray => new List<MyTestClass[]>();
                public static IEnumerable<object[]> GetSomeData() => new List<object[]>();
                public static IEnumerable<Tuple<int, string>> GetSomeDataTuple() => new List<Tuple<int, string>>();
                public static IEnumerable<(int, string)> GetSomeDataValueTuple() => new List<(int, string)>();
                public static MyTestClass[][] GetSomeDataJaggedArray() => System.Array.Empty<MyTestClass[]>();
                public static IEnumerable<MyTestClass[]> GetSomeDataNonObjectTypeArray() => new List<MyTestClass[]>();
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

                [{|#4:DynamicData("GetData", DynamicDataSourceType.AutoDetect)|}]
                [TestMethod]
                public void TestMethod5(object[] o)
                {
                }

                [{|#5:DynamicData("GetData")|}]
                [TestMethod]
                public void TestMethod6(object[] o)
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
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.FoundTooManyMembersRule).WithLocation(3).WithArguments("SomeClass", "GetSomeData"),
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.FoundTooManyMembersRule).WithLocation(4).WithArguments("MyTestClass", "GetData"),
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.FoundTooManyMembersRule).WithLocation(5).WithArguments("MyTestClass", "GetData"));
    }

    public async Task WhenMemberKindIsMixedUp_Diagnostic()
    {
        string code = """
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [{|#0:DynamicData("GetData", DynamicDataSourceType.Property)|}]
                [TestMethod]
                public void TestMethod1(object[] o)
                {
                }

                [{|#1:DynamicData("GetSomeData", typeof(SomeClass), DynamicDataSourceType.Property)|}]
                [TestMethod]
                public void TestMethod2(object[] o)
                {
                }

                [{|#2:DynamicData(dynamicDataSourceName: "GetData", DynamicDataSourceType.Property)|}]
                [TestMethod]
                public void TestMethod3(object[] o)
                {
                }

                [{|#3:DynamicData(dynamicDataDeclaringType: typeof(SomeClass), dynamicDataSourceName: "GetSomeData", dynamicDataSourceType: DynamicDataSourceType.Property)|}]
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
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.SourceTypeMethodRule).WithLocation(0).WithArguments("MyTestClass", "GetData"),
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.SourceTypeMethodRule).WithLocation(1).WithArguments("SomeClass", "GetSomeData"),
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.SourceTypeMethodRule).WithLocation(2).WithArguments("MyTestClass", "GetData"),
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.SourceTypeMethodRule).WithLocation(3).WithArguments("SomeClass", "GetSomeData"),
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.SourceTypePropertyRule).WithLocation(4).WithArguments("MyTestClass", "Data"),
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.SourceTypePropertyRule).WithLocation(5).WithArguments("SomeClass", "SomeData"),
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.SourceTypePropertyRule).WithLocation(6).WithArguments("MyTestClass", "Data"),
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.SourceTypePropertyRule).WithLocation(7).WithArguments("SomeClass", "SomeData"));
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
                public void TestMethod5(object[] o)
                {
                }

                [{|#5:DynamicData("GetSomeData", typeof(SomeClass), DynamicDataSourceType.Method)|}]
                [TestMethod]
                public void TestMethod6(object[] o)
                {
                }

                [{|#6:DynamicData(dynamicDataSourceType: DynamicDataSourceType.Method, dynamicDataSourceName: "GetData")|}]
                [TestMethod]
                public void TestMethod7(object[] o)
                {
                }

                [{|#7:DynamicData(dynamicDataDeclaringType: typeof(SomeClass), dynamicDataSourceType: DynamicDataSourceType.Method, dynamicDataSourceName: "GetSomeData")|}]
                [TestMethod]
                public void TestMethod8(object[] o)
                {
                }

                [{|#8:DynamicData("DataArray")|}]
                [TestMethod]
                public void TestMethod9(MyTestClass[] o)
                {
                }

                [{|#9:DynamicData("SomeDataArray", typeof(SomeClass))|}]
                [TestMethod]
                public void TestMethod10(MyTestClass[] o)
                {
                }

                [{|#10:DynamicData(dynamicDataSourceName: "DataArray")|}]
                [TestMethod]
                public void TestMethod11(MyTestClass[] o)
                {
                }

                [{|#11:DynamicData(dynamicDataDeclaringType: typeof(SomeClass), dynamicDataSourceName: "SomeDataArray")|}]
                [TestMethod]
                public void TestMethod12(MyTestClass[] o)
                {
                }

                [{|#12:DynamicData("GetDataArray", DynamicDataSourceType.Method)|}]
                [TestMethod]
                public void TestMethod13(MyTestClass[] o)
                {
                }

                [{|#13:DynamicData("GetSomeDataArray", typeof(SomeClass), DynamicDataSourceType.Method)|}]
                [TestMethod]
                public void TestMethod14(MyTestClass[] o)
                {
                }

                [{|#14:DynamicData(dynamicDataSourceType: DynamicDataSourceType.Method, dynamicDataSourceName: "GetDataArray")|}]
                [TestMethod]
                public void TestMethod15(MyTestClass[] o)
                {
                }

                [{|#15:DynamicData(dynamicDataDeclaringType: typeof(SomeClass), dynamicDataSourceType: DynamicDataSourceType.Method, dynamicDataSourceName: "GetSomeDataArray")|}]
                [TestMethod]
                public void TestMethod16(MyTestClass[] o)
                {
                }

                public static IEnumerable<object> Data => new List<object>();
                public static MyTestClass[] DataArray => System.Array.Empty<MyTestClass>();
                public static IEnumerable<object> GetData() => new List<object>();
                public static MyTestClass[] GetDataArray() => System.Array.Empty<MyTestClass>();
            }

            public class SomeClass
            {
                public static IEnumerable<object> SomeData => new List<object>();
                public static MyTestClass[] SomeDataArray => System.Array.Empty<MyTestClass>();
                public static IEnumerable<object> GetSomeData() => new List<object>();
                public static MyTestClass[] GetSomeDataArray() => System.Array.Empty<MyTestClass>();
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.MemberTypeRule).WithLocation(0).WithArguments("MyTestClass", "Data"),
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.MemberTypeRule).WithLocation(1).WithArguments("SomeClass", "SomeData"),
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.MemberTypeRule).WithLocation(2).WithArguments("MyTestClass", "Data"),
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.MemberTypeRule).WithLocation(3).WithArguments("SomeClass", "SomeData"),
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.MemberTypeRule).WithLocation(4).WithArguments("MyTestClass", "GetData"),
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.MemberTypeRule).WithLocation(5).WithArguments("SomeClass", "GetSomeData"),
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.MemberTypeRule).WithLocation(6).WithArguments("MyTestClass", "GetData"),
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.MemberTypeRule).WithLocation(7).WithArguments("SomeClass", "GetSomeData"),
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.MemberTypeRule).WithLocation(8).WithArguments("MyTestClass", "DataArray"),
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.MemberTypeRule).WithLocation(9).WithArguments("SomeClass", "SomeDataArray"),
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.MemberTypeRule).WithLocation(10).WithArguments("MyTestClass", "DataArray"),
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.MemberTypeRule).WithLocation(11).WithArguments("SomeClass", "SomeDataArray"),
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.MemberTypeRule).WithLocation(12).WithArguments("MyTestClass", "GetDataArray"),
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.MemberTypeRule).WithLocation(13).WithArguments("SomeClass", "GetSomeDataArray"),
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.MemberTypeRule).WithLocation(14).WithArguments("MyTestClass", "GetDataArray"),
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.MemberTypeRule).WithLocation(15).WithArguments("SomeClass", "GetSomeDataArray"));
    }

    public async Task MemberIsNotStatic_Diagnostic()
    {
        string code = """
            using System;
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

                [{|#1:DynamicData("GetData", DynamicDataSourceType.Method)|}]
                [TestMethod]
                public void TestMethod2(object[] o)
                {
                }

                public IEnumerable<object[]> Data => new List<object[]>();
                public IEnumerable<object[]> GetData() => new List<object[]>();
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.DataMemberSignatureRule).WithLocation(0).WithArguments("MyTestClass", "Data"),
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.DataMemberSignatureRule).WithLocation(1).WithArguments("MyTestClass", "GetData"));
    }

    public async Task MemberIsNotPublic_NoDiagnostic()
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

                [DynamicData("GetData", DynamicDataSourceType.Method)]
                [TestMethod]
                public void TestMethod2(object[] o)
                {
                }

                private static IEnumerable<object[]> Data => new List<object[]>();
                private static IEnumerable<object[]> GetData() => new List<object[]>();
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task MethodHasParameters_Diagnostic()
    {
        string code = """
            using System;
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [{|#0:DynamicData("GetData1", DynamicDataSourceType.Method)|}]
                [TestMethod]
                public void TestMethod1(object[] o)
                {
                }

                [{|#1:DynamicData("GetData2", DynamicDataSourceType.Method)|}]
                [TestMethod]
                public void TestMethod2(object[] o)
                {
                }

                public static IEnumerable<object[]> GetData1(int i) => new List<object[]>();
                public static IEnumerable<object[]> GetData2(string s) => new List<object[]>();
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.DataMemberSignatureRule).WithLocation(0).WithArguments("MyTestClass", "GetData1"),
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.DataMemberSignatureRule).WithLocation(1).WithArguments("MyTestClass", "GetData2"));
    }

    public async Task MethodIsGeneric_Diagnostic()
    {
        string code = """
            using System;
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

                public static IEnumerable<object[]> GetData<T>() => new List<object[]>();
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.DataMemberSignatureRule).WithLocation(0).WithArguments("MyTestClass", "GetData"));
    }

    public async Task WhenDisplayMemberIsValid_NoDiagnostic()
    {
        string code = """
            using System.Collections.Generic;
            using System.Reflection;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [DynamicData("Property", DynamicDataDisplayName = "GetDisplayName")]
                [TestMethod]
                public void TestMethod1(object[] o)
                {
                }

                [DynamicData("SomeProperty", typeof(SomeClass), DynamicDataDisplayName = "GetDisplayName")]
                [TestMethod]
                public void TestMethod2(object[] o)
                {
                }

                [DynamicData("Property", DynamicDataDisplayName = "GetSomeDisplayName", DynamicDataDisplayNameDeclaringType = typeof(SomeClass))]
                [TestMethod]
                public void TestMethod3(object[] o)
                {
                }

                [DynamicData("SomeProperty", typeof(SomeClass), DynamicDataDisplayName = "GetSomeDisplayName", DynamicDataDisplayNameDeclaringType = typeof(SomeClass))]
                [TestMethod]
                public void TestMethod4(object[] o)
                {
                }

                [DynamicData(dynamicDataSourceName: "Property", DynamicDataDisplayName = "GetDisplayName")]
                [TestMethod]
                public void TestMethod5(object[] o)
                {
                }

                [DynamicData(dynamicDataDeclaringType: typeof(SomeClass), dynamicDataSourceName: "SomeProperty", DynamicDataDisplayName = "GetDisplayName")]
                [TestMethod]
                public void TestMethod6(object[] o)
                {
                }

                [DynamicData(dynamicDataSourceName: "Property", DynamicDataDisplayName = "GetSomeDisplayName", DynamicDataDisplayNameDeclaringType = typeof(SomeClass))]
                [TestMethod]
                public void TestMethod7(object[] o)
                {
                }

                [DynamicData(dynamicDataDeclaringType: typeof(SomeClass), dynamicDataSourceName: "SomeProperty", DynamicDataDisplayName = "GetSomeDisplayName", DynamicDataDisplayNameDeclaringType = typeof(SomeClass))]
                [TestMethod]
                public void TestMethod8(object[] o)
                {
                }

                public static IEnumerable<object[]> Property => new List<object[]>();

                public static string GetDisplayName(MethodInfo methodInfo, object[] data) => null;
            }

            public class SomeClass
            {
                public static IEnumerable<object[]> SomeProperty => new List<object[]>();

                public static string GetSomeDisplayName(MethodInfo methodInfo, object[] data) => null;
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenDisplayMemberIsNotFound_Diagnostic()
    {
        string code = """
            using System.Collections.Generic;
            using System.Reflection;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [{|#0:DynamicData("Property", DynamicDataDisplayName = "MemberNotFound")|}]
                [TestMethod]
                public void TestMethod1(object[] o)
                {
                }

                [{|#1:DynamicData("SomeProperty", typeof(SomeClass), DynamicDataDisplayName = "MemberNotFound")|}]
                [TestMethod]
                public void TestMethod2(object[] o)
                {
                }

                [{|#2:DynamicData("Property", DynamicDataDisplayName = "MemberNotFound", DynamicDataDisplayNameDeclaringType = typeof(SomeClass))|}]
                [TestMethod]
                public void TestMethod3(object[] o)
                {
                }

                [{|#3:DynamicData("SomeProperty", typeof(SomeClass), DynamicDataDisplayName = "MemberNotFound", DynamicDataDisplayNameDeclaringType = typeof(SomeClass))|}]
                [TestMethod]
                public void TestMethod4(object[] o)
                {
                }

                public static IEnumerable<object[]> Property => new List<object[]>();
            }

            public class SomeClass
            {
                public static IEnumerable<object[]> SomeProperty => new List<object[]>();
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.MemberNotFoundRule).WithLocation(0).WithArguments("MyTestClass", "MemberNotFound"),
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.MemberNotFoundRule).WithLocation(1).WithArguments("MyTestClass", "MemberNotFound"),
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.MemberNotFoundRule).WithLocation(2).WithArguments("SomeClass", "MemberNotFound"),
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.MemberNotFoundRule).WithLocation(3).WithArguments("SomeClass", "MemberNotFound"));
    }

    public async Task WhenDisplayMemberIsNotPublic_Diagnostic()
    {
        string code = """
            using System.Collections.Generic;
            using System.Reflection;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [{|#0:DynamicData("Property", DynamicDataDisplayName = "GetDisplayName")|}]
                [TestMethod]
                public void TestMethod1(object[] o)
                {
                }

                [{|#1:DynamicData("SomeProperty", typeof(SomeClass), DynamicDataDisplayName = "GetDisplayName")|}]
                [TestMethod]
                public void TestMethod2(object[] o)
                {
                }

                [{|#2:DynamicData("Property", DynamicDataDisplayName = "GetSomeDisplayName", DynamicDataDisplayNameDeclaringType = typeof(SomeClass))|}]
                [TestMethod]
                public void TestMethod3(object[] o)
                {
                }

                [{|#3:DynamicData("SomeProperty", typeof(SomeClass), DynamicDataDisplayName = "GetSomeDisplayName", DynamicDataDisplayNameDeclaringType = typeof(SomeClass))|}]
                [TestMethod]
                public void TestMethod4(object[] o)
                {
                }

                public static IEnumerable<object[]> Property => new List<object[]>();

                private static string GetDisplayName(MethodInfo methodInfo, object[] data) => null;
            }

            public class SomeClass
            {
                public static IEnumerable<object[]> SomeProperty => new List<object[]>();

                private static string GetSomeDisplayName(MethodInfo methodInfo, object[] data) => null;
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.DisplayMethodSignatureRule).WithLocation(0).WithArguments("MyTestClass", "GetDisplayName"),
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.DisplayMethodSignatureRule).WithLocation(1).WithArguments("MyTestClass", "GetDisplayName"),
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.DisplayMethodSignatureRule).WithLocation(2).WithArguments("SomeClass", "GetSomeDisplayName"),
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.DisplayMethodSignatureRule).WithLocation(3).WithArguments("SomeClass", "GetSomeDisplayName"));
    }

    public async Task WhenDisplayMemberIsNotStatic_Diagnostic()
    {
        string code = """
            using System.Collections.Generic;
            using System.Reflection;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [{|#0:DynamicData("Property", DynamicDataDisplayName = "GetDisplayName")|}]
                [TestMethod]
                public void TestMethod1(object[] o)
                {
                }

                [{|#1:DynamicData("SomeProperty", typeof(SomeClass), DynamicDataDisplayName = "GetDisplayName")|}]
                [TestMethod]
                public void TestMethod2(object[] o)
                {
                }

                [{|#2:DynamicData("Property", DynamicDataDisplayName = "GetSomeDisplayName", DynamicDataDisplayNameDeclaringType = typeof(SomeClass))|}]
                [TestMethod]
                public void TestMethod3(object[] o)
                {
                }

                [{|#3:DynamicData("SomeProperty", typeof(SomeClass), DynamicDataDisplayName = "GetSomeDisplayName", DynamicDataDisplayNameDeclaringType = typeof(SomeClass))|}]
                [TestMethod]
                public void TestMethod4(object[] o)
                {
                }

                public static IEnumerable<object[]> Property => new List<object[]>();

                public string GetDisplayName(MethodInfo methodInfo, object[] data) => null;
            }

            public class SomeClass
            {
                public static IEnumerable<object[]> SomeProperty => new List<object[]>();

                public string GetSomeDisplayName(MethodInfo methodInfo, object[] data) => null;
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.DisplayMethodSignatureRule).WithLocation(0).WithArguments("MyTestClass", "GetDisplayName"),
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.DisplayMethodSignatureRule).WithLocation(1).WithArguments("MyTestClass", "GetDisplayName"),
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.DisplayMethodSignatureRule).WithLocation(2).WithArguments("SomeClass", "GetSomeDisplayName"),
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.DisplayMethodSignatureRule).WithLocation(3).WithArguments("SomeClass", "GetSomeDisplayName"));
    }

    public async Task WhenDisplayMemberDoesNotReturnString_Diagnostic()
    {
        string code = """
            using System.Collections.Generic;
            using System.Reflection;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [{|#0:DynamicData("Property", DynamicDataDisplayName = "GetDisplayName")|}]
                [TestMethod]
                public void TestMethod1(object[] o)
                {
                }

                [{|#1:DynamicData("SomeProperty", typeof(SomeClass), DynamicDataDisplayName = "GetDisplayName")|}]
                [TestMethod]
                public void TestMethod2(object[] o)
                {
                }

                [{|#2:DynamicData("Property", DynamicDataDisplayName = "GetSomeDisplayName", DynamicDataDisplayNameDeclaringType = typeof(SomeClass))|}]
                [TestMethod]
                public void TestMethod3(object[] o)
                {
                }

                [{|#3:DynamicData("SomeProperty", typeof(SomeClass), DynamicDataDisplayName = "GetSomeDisplayName", DynamicDataDisplayNameDeclaringType = typeof(SomeClass))|}]
                [TestMethod]
                public void TestMethod4(object[] o)
                {
                }

                public static IEnumerable<object[]> Property => new List<object[]>();

                public static int GetDisplayName(MethodInfo methodInfo, object[] data) => default;
            }

            public class SomeClass
            {
                public static IEnumerable<object[]> SomeProperty => new List<object[]>();

                public static int GetSomeDisplayName(MethodInfo methodInfo, object[] data) => default;
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.DisplayMethodSignatureRule).WithLocation(0).WithArguments("MyTestClass", "GetDisplayName"),
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.DisplayMethodSignatureRule).WithLocation(1).WithArguments("MyTestClass", "GetDisplayName"),
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.DisplayMethodSignatureRule).WithLocation(2).WithArguments("SomeClass", "GetSomeDisplayName"),
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.DisplayMethodSignatureRule).WithLocation(3).WithArguments("SomeClass", "GetSomeDisplayName"));
    }

    public async Task WhenDisplayMemberInvalidParameters_Diagnostic()
    {
        string code = """
            using System.Collections.Generic;
            using System.Reflection;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [{|#0:DynamicData("Property", DynamicDataDisplayName = "GetDisplayName")|}]
                [TestMethod]
                public void TestMethod1(object[] o)
                {
                }

                [{|#1:DynamicData("SomeProperty", typeof(SomeClass), DynamicDataDisplayName = "GetDisplayName")|}]
                [TestMethod]
                public void TestMethod2(object[] o)
                {
                }

                [{|#2:DynamicData("Property", DynamicDataDisplayName = "GetSomeDisplayName", DynamicDataDisplayNameDeclaringType = typeof(SomeClass))|}]
                [TestMethod]
                public void TestMethod3(object[] o)
                {
                }

                [{|#3:DynamicData("SomeProperty", typeof(SomeClass), DynamicDataDisplayName = "GetSomeDisplayName", DynamicDataDisplayNameDeclaringType = typeof(SomeClass))|}]
                [TestMethod]
                public void TestMethod4(object[] o)
                {
                }

                [{|#4:DynamicData("Property", DynamicDataDisplayName = "GetDisplayName2")|}]
                [TestMethod]
                public void TestMethod11(object[] o)
                {
                }

                [{|#5:DynamicData("SomeProperty", typeof(SomeClass), DynamicDataDisplayName = "GetDisplayName2")|}]
                [TestMethod]
                public void TestMethod12(object[] o)
                {
                }

                [{|#6:DynamicData("Property", DynamicDataDisplayName = "GetSomeDisplayName2", DynamicDataDisplayNameDeclaringType = typeof(SomeClass))|}]
                [TestMethod]
                public void TestMethod13(object[] o)
                {
                }

                [{|#7:DynamicData("SomeProperty", typeof(SomeClass), DynamicDataDisplayName = "GetSomeDisplayName2", DynamicDataDisplayNameDeclaringType = typeof(SomeClass))|}]
                [TestMethod]
                public void TestMethod14(object[] o)
                {
                }

                [{|#8:DynamicData("Property", DynamicDataDisplayName = "GetDisplayName3")|}]
                [TestMethod]
                public void TestMethod21(object[] o)
                {
                }

                [{|#9:DynamicData("SomeProperty", typeof(SomeClass), DynamicDataDisplayName = "GetDisplayName3")|}]
                [TestMethod]
                public void TestMethod22(object[] o)
                {
                }

                [{|#10:DynamicData("Property", DynamicDataDisplayName = "GetSomeDisplayName3", DynamicDataDisplayNameDeclaringType = typeof(SomeClass))|}]
                [TestMethod]
                public void TestMethod23(object[] o)
                {
                }

                [{|#11:DynamicData("SomeProperty", typeof(SomeClass), DynamicDataDisplayName = "GetSomeDisplayName3", DynamicDataDisplayNameDeclaringType = typeof(SomeClass))|}]
                [TestMethod]
                public void TestMethod24(object[] o)
                {
                }

                public static IEnumerable<object[]> Property => new List<object[]>();

                public static string GetDisplayName() => null;
                public static string GetDisplayName2(MethodInfo methodInfo) => null;
                public static string GetDisplayName3(MethodInfo methodInfo, string[] args) => null;
            }

            public class SomeClass
            {
                public static IEnumerable<object[]> SomeProperty => new List<object[]>();

                public static string GetSomeDisplayName() => null;
                public static string GetSomeDisplayName2(MethodInfo methodInfo) => null;
                public static string GetSomeDisplayName3(MethodInfo methodInfo, string[] args) => null;
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.DisplayMethodSignatureRule).WithLocation(0).WithArguments("MyTestClass", "GetDisplayName"),
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.DisplayMethodSignatureRule).WithLocation(1).WithArguments("MyTestClass", "GetDisplayName"),
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.DisplayMethodSignatureRule).WithLocation(2).WithArguments("SomeClass", "GetSomeDisplayName"),
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.DisplayMethodSignatureRule).WithLocation(3).WithArguments("SomeClass", "GetSomeDisplayName"),
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.DisplayMethodSignatureRule).WithLocation(4).WithArguments("MyTestClass", "GetDisplayName2"),
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.DisplayMethodSignatureRule).WithLocation(5).WithArguments("MyTestClass", "GetDisplayName2"),
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.DisplayMethodSignatureRule).WithLocation(6).WithArguments("SomeClass", "GetSomeDisplayName2"),
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.DisplayMethodSignatureRule).WithLocation(7).WithArguments("SomeClass", "GetSomeDisplayName2"),
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.DisplayMethodSignatureRule).WithLocation(8).WithArguments("MyTestClass", "GetDisplayName3"),
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.DisplayMethodSignatureRule).WithLocation(9).WithArguments("MyTestClass", "GetDisplayName3"),
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.DisplayMethodSignatureRule).WithLocation(10).WithArguments("SomeClass", "GetSomeDisplayName3"),
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.DisplayMethodSignatureRule).WithLocation(11).WithArguments("SomeClass", "GetSomeDisplayName3"));
    }
}
