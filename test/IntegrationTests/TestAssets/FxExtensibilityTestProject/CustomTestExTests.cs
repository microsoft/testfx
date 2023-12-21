// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FxExtensibilityTestProject;

[IterativeTestClass(5)]
public class CustomTestExTests
{
    private static int s_customTestMethod1ExecutionCount;

    [IterativeTestMethod(5)]
    public void CustomTestMethod1()
    {
        s_customTestMethod1ExecutionCount++;
        Assert.AreNotEqual(3, s_customTestMethod1ExecutionCount);
    }

    [IterativeTestMethod(3)]
    [DataRow("A")]
    [DataRow("B")]
    [DataRow("C")]
    public void CustomTestMethod2(string value)
    {
        Assert.AreEqual("B", value);
    }

    private static int s_customTestClass1ExecutionCount;

    [TestMethod]
    public void CustomTestClass1()
    {
        s_customTestClass1ExecutionCount++;
        Assert.AreNotEqual(3, s_customTestClass1ExecutionCount);
    }
}

public class IterativeTestMethodAttribute : TestMethodAttribute
{
    private readonly int _stabilityThreshold;

    public IterativeTestMethodAttribute(int stabilityThreshold)
    {
        _stabilityThreshold = stabilityThreshold;
    }

    public override TestResult[] Execute(ITestMethod testMethod)
    {
        var results = new List<TestResult>();
        for (int count = 0; count < _stabilityThreshold; count++)
        {
            var testResults = base.Execute(testMethod);
            foreach (var testResult in testResults)
            {
                testResult.DisplayName = $"{testMethod.TestMethodName} - Execution number {count + 1}";
            }

            results.AddRange(testResults);
        }

        return results.ToArray();
    }
}

public class IterativeTestClassAttribute : TestClassAttribute
{
    private readonly int _stabilityThreshold;

    public IterativeTestClassAttribute(int stabilityThreshold)
    {
        _stabilityThreshold = stabilityThreshold;
    }

    public override TestMethodAttribute GetTestMethodAttribute(TestMethodAttribute testMethodAttribute)
    {
        return testMethodAttribute is IterativeTestMethodAttribute
            ? testMethodAttribute
            : new IterativeTestMethodAttribute(_stabilityThreshold);
    }
}
