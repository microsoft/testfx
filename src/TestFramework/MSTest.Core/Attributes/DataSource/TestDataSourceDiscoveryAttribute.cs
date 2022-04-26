// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting
{
    using System;

    /// <summary>
    /// Specifies how to discover ITestDataSource tests.
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
    public class TestDataSourceDiscoveryAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TestDataSourceDiscoveryAttribute"/> class.
        /// </summary>
        /// <param name="discoveryOption">
        /// Sets which <see cref="TestDataSourceDiscoveryOption"/> to use when discovering ITestDataSource tests.
        /// </param>
        public TestDataSourceDiscoveryAttribute(TestDataSourceDiscoveryOption discoveryOption)
        {
            DiscoveryOption = discoveryOption;
        }

        /// <summary>
        /// Gets specified discovery option.
        /// </summary>
        public TestDataSourceDiscoveryOption DiscoveryOption { get; }
    }
}
