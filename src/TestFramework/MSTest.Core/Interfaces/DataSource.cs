// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting.Interfaces
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;

    /// <summary>
    /// Data source for data driven tests.
    /// </summary>
    public abstract class DataSource : Attribute
    {
        /// <summary>
        /// Gets the data from custom data source.
        /// </summary>
        /// <param name="methodInfo">
        /// The method info of test method.
        /// </param>
        /// <returns>
        /// Gets the data for calling test method.
        /// </returns>
        public abstract IEnumerable<object[]> GetData(MethodInfo methodInfo);
    }
}
