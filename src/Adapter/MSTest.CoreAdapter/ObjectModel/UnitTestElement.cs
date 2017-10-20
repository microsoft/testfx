// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;

    using Microsoft.VisualStudio.TestPlatform.ObjectModel;

    /// <summary>
    /// The unit test element.
    /// </summary>
    [Serializable]
    internal class UnitTestElement
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="UnitTestElement"/> class.
        /// </summary>
        /// <param name="testMethod"> The test method. </param>
        /// <exception cref="ArgumentNullException"> Thrown when method is null. </exception>
        public UnitTestElement(TestMethod testMethod)
        {
            if (testMethod == null)
            {
                throw new ArgumentNullException("testMethod");
            }

            Debug.Assert(testMethod.FullClassName != null, "Full className cannot be empty");
            this.TestMethod = testMethod;
        }

        /// <summary>
        /// Gets the test method which should be executed as part of this test case
        /// </summary>
        public TestMethod TestMethod { get; private set; }

        /// <summary>
        /// Gets or sets a value indicating whether the unit test should be ignored at run-time
        /// </summary>
        public bool Ignored { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether it is a async test
        /// </summary>
        public bool IsAsync { get; set; }

        /// <summary>
        /// Gets or sets the test categories for test method.
        /// </summary>
        public string[] TestCategory { get; set; }

        /// <summary>
        /// Gets or sets the traits for test method.
        /// </summary>
        public Trait[] Traits { get; set; }

        /// <summary>
        /// Gets or sets the priority of the test method, if any.
        /// </summary>
        public int? Priority { get; set; }

        /// <summary>
        /// Gets or sets if this test method should not execute in parallel.
        /// </summary>
        public bool DoNotParallelize { get; set; }

        /// <summary>
        /// Gets or sets the deployment items for the test method.
        /// </summary>
        public KeyValuePair<string, string>[] DeploymentItems { get; set; }

        /// <summary>
        /// Gets or sets the compiler generated type name for async test method.
        /// </summary>
        internal string AsyncTypeName { get; set; }

        /// <summary>
        /// Convert the UnitTestElement instance to an Object Model testCase instance.
        /// </summary>
        /// <returns> An instance of <see cref="TestCase"/>. </returns>
        internal TestCase ToTestCase()
        {
            var fullName = string.Format(
                CultureInfo.InvariantCulture,
                "{0}.{1}",
                this.TestMethod.FullClassName,
                this.TestMethod.Name);

            TestCase testCase = new TestCase(fullName, TestAdapter.Constants.ExecutorUri, this.TestMethod.AssemblyName);
            testCase.DisplayName = this.TestMethod.Name;

            testCase.SetPropertyValue(TestAdapter.Constants.TestClassNameProperty, this.TestMethod.FullClassName);

            // Many of the tests will not be async, so there is no point in sending extra data
            if (this.IsAsync)
            {
                testCase.SetPropertyValue(TestAdapter.Constants.AsyncTestProperty, this.IsAsync);
            }

            // Set only if some test category is present
            if (this.TestCategory != null && this.TestCategory.Length > 0)
            {
                testCase.SetPropertyValue(TestAdapter.Constants.TestCategoryProperty, this.TestCategory);
            }

            // Set priority if present
            if (this.Priority != null)
            {
                testCase.SetPropertyValue(TestAdapter.Constants.PriorityProperty, this.Priority.Value);
            }

            if (this.Traits != null)
            {
                testCase.Traits.AddRange(this.Traits);
            }

            // The list of items to deploy before running this test.
            if (this.DeploymentItems != null && this.DeploymentItems.Length > 0)
            {
                testCase.SetPropertyValue(TestAdapter.Constants.DeploymentItemsProperty, this.DeploymentItems);
            }

            // Set the Do not parallelize state if present
            if (this.DoNotParallelize)
            {
                testCase.SetPropertyValue(
                    TestAdapter.Constants.DoNotParallelizeProperty,
                    this.DoNotParallelize);
            }

            return testCase;
        }
    }
}
