// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Framework;
using Microsoft.Testing.TestInfrastructure;

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.DynamicDataShouldBeValidAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace MSTest.Analyzers.Test;

[TestGroup]
public sealed class DynamicDataShouldBeValidAnalyzerTests(ITestExecutionContext testExecutionContext) : TestBase(testExecutionContext)
{
    public async Task WhenDataSourceMemberDoesNotExist_Diagnostic()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [DynamicData("MemberNotFound")]
                [TestMethod]
                public void TestMethod1(object[] o)
                {
                }

                [DynamicData("MemberNotFound", typeof(SomeClass))]
                [TestMethod]
                public void TestMethod2(object[] o)
                {
                }

                [DynamicData(dynamicDataSourceName: "MemberNotFound")]
                [TestMethod]
                public void TestMethod3(object[] o)
                {
                }
            
                [DynamicData(dynamicDataDeclaringType: typeof(SomeClass), dynamicDataSourceName: "MemberNotFound")]
                [TestMethod]
                public void TestMethod4(object[] o)
                {
                }
            }

            public class SomeClass
            {
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenDataDisplayMemberDoesNotExist_Diagnostic()
    {
        var code = """
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

    public async Task WhenAppliedToNonTestMethod_Diagnostic()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [DynamicData("SomeProperty")]
                public void TestMethod1(object[] o)
                {
                }
            
                [DynamicData("SomeProperty", typeof(SomeClass))]
                public void TestMethod2(object[] o)
                {
                }
            
                [DynamicData(dynamicDataSourceName: "SomeProperty")]
                public void TestMethod3(object[] o)
                {
                }
            
                [DynamicData(dynamicDataDeclaringType: typeof(SomeClass), dynamicDataSourceName: "SomeProperty")]
                public void TestMethod4(object[] o)
                {
                }
            }

            public class SomeClass
            {
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }
}
