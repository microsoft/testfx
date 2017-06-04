// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting.Attributes
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;

    using Microsoft.VisualStudio.TestTools.UnitTesting.Interfaces;

    /// <summary>
    /// The dynamic data source type.
    /// </summary>
    public enum DynamicDataSourceType
    {
        /// <summary>
        /// Data is declared as property.
        /// </summary>
        Property = 0,

        /// <summary>
        /// Data is declared in method.
        /// </summary>
        Method = 1
    }

    /// <summary>
    /// Attribute to define dynamic data for a test method.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class DynamicDataAttribute : DataSource
    {
        private string dynamicDataSourceName;

        private Type dynamicDataDeclaringType;

        private DynamicDataSourceType dynamicDataSourceType;

        /// <summary>
        /// Initializes a new instance of the <see cref="DynamicDataAttribute"/> class.
        /// </summary>
        /// <param name="dynamicDataSourceName">
        /// The dynamic Data Source Name.
        /// </param>
        /// <param name="dynamicDataSourceType">
        /// The dynamic Data Source Type.
        /// </param>
        public DynamicDataAttribute(string dynamicDataSourceName, DynamicDataSourceType dynamicDataSourceType = DynamicDataSourceType.Property)
        {
            this.dynamicDataSourceName = dynamicDataSourceName;
            this.dynamicDataSourceType = dynamicDataSourceType;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DynamicDataAttribute"/> class.
        /// </summary>
        /// <param name="dynamicDataDeclaringType">
        /// The declaring type of property or method having data.
        /// </param>
        /// <param name="dynamicDataSourceName">
        /// The dynamic Data Source Name.
        /// </param>
        /// <param name="dynamicDataSourceType">
        /// The dynamic Data Source Type.
        /// </param>
        public DynamicDataAttribute(Type dynamicDataDeclaringType, string dynamicDataSourceName, DynamicDataSourceType dynamicDataSourceType = DynamicDataSourceType.Property)
        {
            this.dynamicDataSourceName = dynamicDataSourceName;
            this.dynamicDataDeclaringType = dynamicDataDeclaringType;
            this.dynamicDataSourceType = dynamicDataSourceType;
        }

        /// <inheritdoc />
        public override IEnumerable<object[]> GetData(MethodInfo methodInfo)
        {
            // Check if the class type of attribute is passed in constructor. If not, default to test method's class.
            if (this.dynamicDataDeclaringType == null)
            {
                this.dynamicDataDeclaringType = methodInfo.DeclaringType;
            }

            object obj = null;

            switch (this.dynamicDataSourceType)
            {
                case DynamicDataSourceType.Property:
                    var property = this.dynamicDataDeclaringType.GetTypeInfo().GetDeclaredProperty(this.dynamicDataSourceName);
                    if (property == null)
                    {
                        throw new ArgumentNullException(nameof(property));
                    }

                    obj = property.GetValue(null, null);

                    break;

                case DynamicDataSourceType.Method:
                    var method = this.dynamicDataDeclaringType.GetTypeInfo().GetDeclaredMethod(this.dynamicDataSourceName);
                    if (method == null)
                    {
                        throw new ArgumentNullException(nameof(method));
                    }

                    obj = method.Invoke(null, null);

                    break;
            }

            if (obj == null)
            {
                throw new ArgumentNullException(
                    string.Format(
                        FrameworkMessages.DynamicDataIEnumerableNull,
                        this.dynamicDataSourceName,
                        this.dynamicDataDeclaringType.FullName));
            }

            IEnumerable<object[]> enumerable = obj as IEnumerable<object[]>;
            if (enumerable == null)
            {
                throw new ArgumentNullException(
                    string.Format(
                        FrameworkMessages.DynamicDataIEnumerableNull,
                        this.dynamicDataSourceName,
                        this.dynamicDataDeclaringType.FullName));
            }

            return enumerable;
        }
    }
}
