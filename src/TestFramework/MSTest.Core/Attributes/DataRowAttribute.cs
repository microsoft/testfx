// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;

    /// <summary>
    /// Attribute to define inline data for a test method.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class DataRowAttribute : Attribute, ITestDataSource
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DataRowAttribute"/> class.
        /// </summary>
        /// <param name="data1"> The data object. </param>
        public DataRowAttribute(object data1)
        {
            // Need to have this constructor explicitly to fix a CLS compliance error.
            this.Data = new object[] { data1 };
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DataRowAttribute"/> class which takes in an array of arguments.
        /// </summary>
        /// <param name="data1"> A data object. </param>
        /// <param name="moreData"> More data. </param>
        public DataRowAttribute(object data1, params object[] moreData)
        {
            if (moreData == null)
            {
                // This actually means that the user wants to pass in a 'null' value to the test method.
                moreData = new object[] { null };
            }

            this.Data = new object[moreData.Length + 1];
            this.Data[0] = data1;
            Array.Copy(moreData, 0, this.Data, 1, moreData.Length);
        }

        /// <summary>
        /// Gets data for calling test method.
        /// </summary>
        public object[] Data { get; private set; }

        /// <summary>
        /// Gets or sets display name in test results for customization.
        /// </summary>
        public string DisplayName { get; set; }

        /// <inheritdoc />
        public IEnumerable<object[]> GetData(MethodInfo methodInfo)
        {
            return new[] { this.Data };
        }

        /// <inheritdoc />
        public string GetDisplayName(MethodInfo methodInfo, object[] data)
        {
            if (!string.IsNullOrWhiteSpace(this.DisplayName))
            {
                return this.DisplayName;
            }
            else
            {
                if (data != null)
                {
                    return string.Format(CultureInfo.CurrentCulture, FrameworkMessages.DataDrivenResultDisplayName, methodInfo.Name, string.Join(",", data.AsEnumerable()));
                }
            }

            return null;
        }
    }
}
