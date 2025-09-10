// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.DynamicDataShouldBeValidAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class DynamicDataShouldBeValidAnalyzerTests
{
    [TestMethod]
    public async Task WhenDataIsIEnumerableObjectArray_NoDiagnostic()
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

                public static IEnumerable<object[]> Data => new List<object[]>();
                public static IEnumerable<object[]> GetData() => new List<object[]>();
            }
            
            public class SomeClass
            {
                public static IEnumerable<object[]> SomeData => new List<object[]>();
                public static IEnumerable<object[]> GetSomeData() => new List<object[]>();
            }
        """;
        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenDataIsIEnumerableTuple_NoDiagnostic()
    {
        string code = """
            using System;
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
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

                public static IEnumerable<Tuple<int, string>> DataTuple => new List<Tuple<int, string>>();
                public static IEnumerable<Tuple<int, string>> GetDataTuple() => new List<Tuple<int, string>>();
            }
            
            public class SomeClass
            {
                public static IEnumerable<Tuple<int, string>> SomeDataTuple => new List<Tuple<int, string>>();
                public static IEnumerable<Tuple<int, string>> GetSomeDataTuple() => new List<Tuple<int, string>>();
            }
            """;
        await VerifyCS.VerifyAnalyzerAsync(code);
    }

#if NET
    [TestMethod]
    public async Task WhenDataIsIEnumerableValueTuple_NoDiagnostic()
    {
        string code = """
            using System;
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
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

                public static IEnumerable<(int, string)> DataValueTuple => new List<(int, string)>();
                public static IEnumerable<(int, string)> GetDataValueTuple() => new List<(int, string)>();
            }

            public class SomeClass
            {
                public static IEnumerable<(int, string)> SomeDataValueTuple => new List<(int, string)>();
                public static IEnumerable<(int, string)> GetSomeDataValueTuple() => new List<(int, string)>();
            }
            """;
        await VerifyCS.VerifyAnalyzerAsync(code);
    }
#endif

    [TestMethod]
    public async Task WhenDataIsJaggedArrays_NoDiagnostic()
    {
        string code = """
            using System;
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            
            [TestClass]
            public class MyTestClass
            {
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

                public static MyTestClass[][] DataJaggedArray => System.Array.Empty<MyTestClass[]>();
                public static MyTestClass[][] GetDataJaggedArray() => System.Array.Empty<MyTestClass[]>();
            }
            
            public class SomeClass
            {
                public static MyTestClass[][] SomeDataJaggedArray => System.Array.Empty<MyTestClass[]>();
                public static MyTestClass[][] GetSomeDataJaggedArray() => System.Array.Empty<MyTestClass[]>();
            }
            """;
        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenDataIsNonObjectTypeArray_NoDiagnostic()
    {
        string code = """
            using System;
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
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

                public static IEnumerable<MyTestClass[]> DataNonObjectTypeArray => new List<MyTestClass[]>();
                public static IEnumerable<MyTestClass[]> GetDataNonObjectTypeArray() => new List<MyTestClass[]>();
            }
            
            public class SomeClass
            {
                public static IEnumerable<MyTestClass[]> SomeDataNonObjectTypeArray => new List<MyTestClass[]>();
                public static IEnumerable<MyTestClass[]> GetSomeDataNonObjectTypeArray() => new List<MyTestClass[]>();
            }
            """;
        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenDataIsIEnumerableObject_NoDiagnostic()
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
                public void TestMethod501(object[] o)
                {
                }
            
                [DynamicData("SomeData", typeof(SomeClass))]
                [TestMethod]
                public void TestMethod502(object[] o)
                {
                }
            
                [DynamicData(dynamicDataSourceName: "Data")]
                [TestMethod]
                public void TestMethod503(object[] o)
                {
                }
            
                [DynamicData(dynamicDataDeclaringType: typeof(SomeClass), dynamicDataSourceName: "SomeData")]
                [TestMethod]
                public void TestMethod504(object[] o)
                {
                }

                [DynamicData("GetData", DynamicDataSourceType.Method)]
                [TestMethod]
                public void TestMethod511(object[] o)
                {
                }
            
                [DynamicData("GetSomeData", typeof(SomeClass), DynamicDataSourceType.Method)]
                [TestMethod]
                public void TestMethod512(object[] o)
                {
                }
            
                [DynamicData(dynamicDataSourceType: DynamicDataSourceType.Method, dynamicDataSourceName: "GetData")]
                [TestMethod]
                public void TestMethod513(object[] o)
                {
                }
            
                [DynamicData(dynamicDataDeclaringType: typeof(SomeClass), dynamicDataSourceType: DynamicDataSourceType.Method, dynamicDataSourceName: "GetSomeData")]
                [TestMethod]
                public void TestMethod514(object[] o)
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
        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenDataIsIEnumerable_NoDiagnostic()
    {
        string code = """
            using System;
            using System.Collections;
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {

                [DynamicData("Data")]
                [TestMethod]
                public void TestMethod501(object[] o)
                {
                }
            
                [DynamicData("SomeData", typeof(SomeClass))]
                [TestMethod]
                public void TestMethod502(object[] o)
                {
                }
            
                [DynamicData(dynamicDataSourceName: "Data")]
                [TestMethod]
                public void TestMethod503(object[] o)
                {
                }
            
                [DynamicData(dynamicDataDeclaringType: typeof(SomeClass), dynamicDataSourceName: "SomeData")]
                [TestMethod]
                public void TestMethod504(object[] o)
                {
                }

                [DynamicData("GetData", DynamicDataSourceType.Method)]
                [TestMethod]
                public void TestMethod511(object[] o)
                {
                }
            
                [DynamicData("GetSomeData", typeof(SomeClass), DynamicDataSourceType.Method)]
                [TestMethod]
                public void TestMethod512(object[] o)
                {
                }
            
                [DynamicData(dynamicDataSourceType: DynamicDataSourceType.Method, dynamicDataSourceName: "GetData")]
                [TestMethod]
                public void TestMethod513(object[] o)
                {
                }
            
                [DynamicData(dynamicDataDeclaringType: typeof(SomeClass), dynamicDataSourceType: DynamicDataSourceType.Method, dynamicDataSourceName: "GetSomeData")]
                [TestMethod]
                public void TestMethod514(object[] o)
                {
                }

                public static IEnumerable Data => new List<object>();
                public static IEnumerable GetData() => new List<object>();
            }
            
            public class SomeClass
            {
                public static IEnumerable SomeData => new List<object>();
                public static IEnumerable GetSomeData() => new List<object>();
            }
            """;
        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenDataIsTypeArray_NoDiagnostic()
    {
        string code = """
            using System;
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {

                [DynamicData("DataArray")]
                [TestMethod]
                public void TestMethod601(MyTestClass[] o)
                {
                }
            
                [DynamicData("SomeDataArray", typeof(SomeClass))]
                [TestMethod]
                public void TestMethod602(MyTestClass[] o)
                {
                }
            
                [DynamicData(dynamicDataSourceName: "DataArray")]
                [TestMethod]
                public void TestMethod603(MyTestClass[] o)
                {
                }
            
                [DynamicData(dynamicDataDeclaringType: typeof(SomeClass), dynamicDataSourceName: "SomeDataArray")]
                [TestMethod]
                public void TestMethod604(MyTestClass[] o)
                {
                }
            
                [DynamicData("GetDataArray", DynamicDataSourceType.Method)]
                [TestMethod]
                public void TestMethod611(MyTestClass[] o)
                {
                }
            
                [DynamicData("GetSomeDataArray", typeof(SomeClass), DynamicDataSourceType.Method)]
                [TestMethod]
                public void TestMethod612(MyTestClass[] o)
                {
                }
            
                [DynamicData(dynamicDataSourceType: DynamicDataSourceType.Method, dynamicDataSourceName: "GetDataArray")]
                [TestMethod]
                public void TestMethod613(MyTestClass[] o)
                {
                }
            
                [DynamicData(dynamicDataDeclaringType: typeof(SomeClass), dynamicDataSourceType: DynamicDataSourceType.Method, dynamicDataSourceName: "GetSomeDataArray")]
                [TestMethod]
                public void TestMethod614(MyTestClass[] o)
                {
                }    

                public static MyTestClass[] DataArray => System.Array.Empty<MyTestClass>();
                public static MyTestClass[] GetDataArray() => System.Array.Empty<MyTestClass>();
            }

            public class SomeClass
            {
                public static MyTestClass[] SomeDataArray => System.Array.Empty<MyTestClass>();
                public static MyTestClass[] GetSomeDataArray() => System.Array.Empty<MyTestClass>();
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
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

    [TestMethod]
    public async Task WhenAppliedToNonTestMethod_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [{|#4:DynamicData("SomeProperty")|}]
                public void {|#0:TestMethod1|}(object[] o)
                {
                }

                [{|#5:DynamicData("SomeProperty", typeof(SomeClass))|}]
                public void {|#1:TestMethod2|}(object[] o)
                {
                }

                [{|#6:DynamicData(dynamicDataSourceName: "SomeProperty")|}]
                public void {|#2:TestMethod3|}(object[] o)
                {
                }

                [{|#7:DynamicData(dynamicDataDeclaringType: typeof(SomeClass), dynamicDataSourceName: "SomeProperty")|}]
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
            // /0/Test0.cs(6,6): warning MSTEST0018: '[DynamicData]' member 'MyTestClass.SomeProperty' cannot be found
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.MemberNotFoundRule).WithSpan(6, 6, 6, 33).WithArguments("MyTestClass", "SomeProperty"),
            // /0/Test0.cs(7,17): warning MSTEST0018: '[DynamicData]' should only be set on a test method
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.NotTestMethodRule).WithSpan(7, 17, 7, 28),
            // /0/Test0.cs(11,6): warning MSTEST0018: '[DynamicData]' member 'SomeClass.SomeProperty' cannot be found
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.MemberNotFoundRule).WithSpan(11, 6, 11, 52).WithArguments("SomeClass", "SomeProperty"),
            // /0/Test0.cs(12,17): warning MSTEST0018: '[DynamicData]' should only be set on a test method
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.NotTestMethodRule).WithSpan(12, 17, 12, 28),
            // /0/Test0.cs(16,6): warning MSTEST0018: '[DynamicData]' member 'MyTestClass.SomeProperty' cannot be found
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.MemberNotFoundRule).WithSpan(16, 6, 16, 56).WithArguments("MyTestClass", "SomeProperty"),
            // /0/Test0.cs(17,17): warning MSTEST0018: '[DynamicData]' should only be set on a test method
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.NotTestMethodRule).WithSpan(17, 17, 17, 28),
            // /0/Test0.cs(21,6): warning MSTEST0018: '[DynamicData]' member 'SomeClass.SomeProperty' cannot be found
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.MemberNotFoundRule).WithSpan(21, 6, 21, 101).WithArguments("SomeClass", "SomeProperty"),
            // /0/Test0.cs(22,17): warning MSTEST0018: '[DynamicData]' should only be set on a test method
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.NotTestMethodRule).WithSpan(22, 17, 22, 28));
    }

    [TestMethod]
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

    [TestMethod]
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

                [{|#8:DynamicData(nameof(DataField), DynamicDataSourceType.Method)|}]
                [TestMethod]
                public void TestMethod15(object[] o)
                {
                }

                [{|#9:DynamicData(nameof(DataField), DynamicDataSourceType.Property)|}]
                [TestMethod]
                public void TestMethod16(object[] o)
                {
                }

                public static IEnumerable<object[]> Data => new List<object[]>();
                public static IEnumerable<object[]> GetData() => new List<object[]>();
                public static IEnumerable<object[]> DataField = new List<object[]>();
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
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.SourceTypePropertyRule).WithLocation(7).WithArguments("SomeClass", "SomeData"),
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.SourceTypeFieldRule).WithLocation(8).WithArguments("MyTestClass", "DataField"),
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.SourceTypeFieldRule).WithLocation(9).WithArguments("MyTestClass", "DataField"));
    }

    [TestMethod]
    public async Task WhenDataIsField_NoDiagnostic()
    {
        string code = """
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [DynamicData("DataField")]
                [TestMethod]
                public void TestMethod1Auto(object[] o)
                {
                }

                [DynamicData("DataField", DynamicDataSourceType.Field)]
                [TestMethod]
                public void TestMethod1Field(object[] o)
                {
                }

                [DynamicData("SomeDataField", typeof(SomeClass))]
                [TestMethod]
                public void TestMethod2Auto(object[] o)
                {
                }

                [DynamicData("SomeDataField", typeof(SomeClass), DynamicDataSourceType.Field)]
                [TestMethod]
                public void TestMethod2Field(object[] o)
                {
                }

                [DynamicData(dynamicDataSourceName: "DataField")]
                [TestMethod]
                public void TestMethod3Auto(object[] o)
                {
                }

                [DynamicData(dynamicDataDeclaringType: typeof(SomeClass), dynamicDataSourceName: "SomeDataField")]
                [TestMethod]
                public void TestMethod4Auto(object[] o)
                {
                }

                [DynamicData(dynamicDataDeclaringType: typeof(SomeClass), dynamicDataSourceName: "SomeDataField", dynamicDataSourceType: DynamicDataSourceType.Field)]
                [TestMethod]
                public void TestMethod4Field(object[] o)
                {
                }

                public static IEnumerable<object[]> DataField = new[]
                {
                    new object[] { 1, 2 },
                    new object[] { 3, 4 }
                };
            }

            public class SomeClass
            {
                public static IEnumerable<object[]> SomeDataField = new[]
                {
                    new object[] { 1, 2 },
                    new object[] { 3, 4 }
                };
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenFieldMemberKindIsMixedUp_Diagnostic()
    {
        string code = """
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [{|#0:DynamicData("DataField", DynamicDataSourceType.Property)|}]
                [TestMethod]
                public void TestMethod1(object[] o)
                {
                }

                [{|#1:DynamicData("DataField", DynamicDataSourceType.Method)|}]
                [TestMethod]
                public void TestMethod2(object[] o)
                {
                }

                [{|#2:DynamicData("Data", DynamicDataSourceType.Field)|}]
                [TestMethod]
                public void TestMethod3(object[] o)
                {
                }

                [{|#3:DynamicData("GetData", DynamicDataSourceType.Field)|}]
                [TestMethod]
                public void TestMethod4(object[] o)
                {
                }

                public static IEnumerable<object[]> Data => new[]
                {
                    new object[] { 1, 2 },
                    new object[] { 3, 4 }
                };

                public static IEnumerable<object[]> GetData() => new[]
                {
                    new object[] { 1, 2 },
                    new object[] { 3, 4 }
                };

                public static IEnumerable<object[]> DataField = new[]
                {
                    new object[] { 1, 2 },
                    new object[] { 3, 4 }
                };
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(
            code,
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.SourceTypeFieldRule).WithLocation(0).WithArguments("MyTestClass", "DataField"),
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.SourceTypeFieldRule).WithLocation(1).WithArguments("MyTestClass", "DataField"),
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.SourceTypePropertyRule).WithLocation(2).WithArguments("MyTestClass", "Data"),
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.SourceTypeMethodRule).WithLocation(3).WithArguments("MyTestClass", "GetData"));
    }

    [TestMethod]
    public async Task WhenMemberIsNotStatic_Diagnostic()
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

    [TestMethod]
    public async Task WhenMemberIsNotPublic_NoDiagnostic()
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

    [TestMethod]
    public async Task WhenMemberIsShadowingBase_NoDiagnostic()
    {
        string code = """
            using System;
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public abstract class MyTestClassBase
            {
                public static IEnumerable<object[]> Data => new List<object[]>();
                public static IEnumerable<object[]> GetData() => new List<object[]>();
            }

            [TestClass]
            public class MyTestClass : MyTestClassBase
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

                public static IEnumerable<object[]> Data => new List<object[]>();
                public static IEnumerable<object[]> GetData() => new List<object[]>();
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenMemberIsFromBase_NoDiagnostic()
    {
        string code = """
            using System;
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public abstract class MyTestClassBase
            {
                public static IEnumerable<object[]> Data => new List<object[]>();
                public static IEnumerable<object[]> GetData() => new List<object[]>();
            }

            [TestClass]
            public class MyTestClass : MyTestClassBase
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
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenMethodHasParameters_Diagnostic()
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

    [TestMethod]
    public async Task WhenMethodIsGeneric_Diagnostic()
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
    [GitHubWorkItem("https://github.com/microsoft/testfx/issues/4922")]
    public async Task WhenDataImplementsIEnumerable_NoDiagnostic()
        => await VerifyCS.VerifyAnalyzerAsync("""
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [DynamicData(nameof(GetData), DynamicDataSourceType.Method)]
                public void TestMethod2(int i, int j)
                {
                }

                private static List<object[]> GetData()
                {
                    return new List<object[]>
                    {
                        new object[] { 1, 2 },
                        new object[] { 3, 4 },
                    };
                }
            }
            """);

    [TestMethod]
    public async Task WhenDataIsString_Diagnostic()
        => await VerifyCS.VerifyAnalyzerAsync(
            """            
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [{|#0:DynamicData(nameof(GetData), DynamicDataSourceType.Method)|}]
                public void TestMethod2(char c)
                {
                }

                private static string GetData() => "abc";
            }
            
            """,
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.MemberTypeRule).WithLocation(0).WithArguments("MyTestClass", "GetData"));

    [TestMethod]
    public async Task WhenDataIsObjectButCollectionsAreCasted_Diagnostic()
        => await VerifyCS.VerifyAnalyzerAsync(
            """            
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [{|#0:DynamicData(nameof(GetData), DynamicDataSourceType.Method)|}]
                public void TestMethod2(char c)
                {
                }

                private static object GetData() => new List<object> { 1, 2, 3 };
            }
            
            """,
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.MemberTypeRule).WithLocation(0).WithArguments("MyTestClass", "GetData"));

    [TestMethod]
    public async Task WhenDataIsPointer_Diagnostic()
        => await VerifyCS.VerifyAnalyzerAsync(
            """            
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [{|#0:DynamicData(nameof(GetData), DynamicDataSourceType.Method)|}]
                public void TestMethod2(char c)
                {
                }

                private static unsafe int* GetData() => null;
            }
            
            """,
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.MemberTypeRule).WithLocation(0).WithArguments("MyTestClass", "GetData"));

    [TestMethod]
    public async Task WhenDataWithArgument_NoDiagnostic()
        => await VerifyCS.VerifyAnalyzerAsync(
            """            
            using System;
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [DynamicData(nameof(GetData), 4)]
                public void TestMethod(int a)
                    => Assert.IsInRange(4, 6, a);

                public static IEnumerable<int> GetData(int i)
                {
                    yield return i++;
                    yield return i++;
                    yield return i++;
                }
            }
            
            """);

    [TestMethod]
    public async Task WhenDataWithArgument_ParameterCountMismatch_Diagnostic()
        => await VerifyCS.VerifyAnalyzerAsync(
            """            
            using System;
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [{|#0:DynamicData(nameof(GetData), 4, 5)|}]
                public void TestMethod(int a)
                {
                }

                public static IEnumerable<int> GetData(int i)
                {
                    yield return i++;
                    yield return i++;
                    yield return i++;
                }
            }
            
            """,
            VerifyCS.Diagnostic(DynamicDataShouldBeValidAnalyzer.DataMemberSignatureRule).WithLocation(0).WithArguments("MyTestClass", "GetData"));
}
