// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace FxExtensibilityTestProject
{
    using System.Collections.Generic;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [IterativeTestClass(5)]
    public class CustomTestExTests
    {
        private static int customTestMethod1ExecutionCount;
        [IterativeTestMethod(5)]
        public void CustomTestMethod1()
        {
            customTestMethod1ExecutionCount++;
            Assert.AreNotEqual(3, customTestMethod1ExecutionCount);
        }

        [IterativeTestMethod(3)]
        [DataRow("A")]
        [DataRow("B")]
        [DataRow("C")]
        public void CustomTestMethod2(string value)
        {
            Assert.AreEqual("B", value);
        }

        private static int customTestClass1ExecutionCount;
        [TestMethod]
        public void CustomTestClass1()
        {
            customTestClass1ExecutionCount++;
            Assert.AreNotEqual(3, customTestClass1ExecutionCount);
        }
    }

    public class IterativeTestMethodAttribute : TestMethodAttribute
    {
        private readonly int stabilityThreshold;

        public IterativeTestMethodAttribute(int stabilityThreshold)
        {
            this.stabilityThreshold = stabilityThreshold;
        }

        public override TestResult[] Execute(ITestMethod testMethod)
        {
            var results = new List<TestResult>();
            for (int count = 0; count < this.stabilityThreshold; count++)
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
        private readonly int stabilityThreshold;

        public IterativeTestClassAttribute(int stabilityThreshold)
        {
            this.stabilityThreshold = stabilityThreshold;
        }

        public override TestMethodAttribute GetTestMethodAttribute(TestMethodAttribute testMethodAttribute)
        {
            if (testMethodAttribute is IterativeTestMethodAttribute) return testMethodAttribute;

            return new IterativeTestMethodAttribute(this.stabilityThreshold);
        }
    }
}
