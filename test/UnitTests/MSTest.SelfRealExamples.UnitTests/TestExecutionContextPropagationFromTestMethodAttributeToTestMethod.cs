// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTest.SelfRealExamples.UnitTests;

[TestClass]
public sealed class TestExecutionContextPropagationFromTestMethodAttributeToTestMethod
{
    private static readonly AsyncLocal<object> State = new();

    [ClassInitialize]
    public static void ClassInitialize(TestContext testContext)
    {
    }

    [MyTestMethod]
    public void TestAsyncLocalValueIsPreserved()
        => Assert.AreEqual("In Execute", State.Value);

    private sealed class MyTestMethodAttribute : TestMethodAttribute
    {
        public override TestResult[] Execute(ITestMethod testMethod)
        {
            State.Value = "In Execute";
            return base.Execute(testMethod);
        }
    }
}
