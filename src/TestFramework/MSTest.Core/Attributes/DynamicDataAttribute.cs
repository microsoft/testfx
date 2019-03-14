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
    /// Enum to specify whether the data is stored as property or in method.
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
    public sealed class DynamicDataAttribute : Attribute, ITestDataSource
    {
        private string dynamicDataSourceName;

        private Type dynamicDataDeclaringType;

        private DynamicDataSourceType dynamicDataSourceType;

        /// <summary>
        /// Initializes a new instance of the <see cref="DynamicDataAttribute"/> class.
        /// </summary>
        /// <param name="dynamicDataSourceName">
        /// The name of method or property having test data.
        /// </param>
        /// <param name="dynamicDataSourceType">
        /// Specifies whether the data is stored as property or in method.
        /// </param>
        public DynamicDataAttribute(string dynamicDataSourceName, DynamicDataSourceType dynamicDataSourceType = DynamicDataSourceType.Property)
        {
            this.dynamicDataSourceName = dynamicDataSourceName;
            this.dynamicDataSourceType = dynamicDataSourceType;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DynamicDataAttribute"/> class when the test data is present in a class different
        /// from test method's class.
        /// </summary>
        /// <param name="dynamicDataSourceName">
        /// The name of method or property having test data.
        /// </param>
        /// <param name="dynamicDataDeclaringType">
        /// The declaring type of property or method having data. Useful in cases when declaring type is present in a class different from
        /// test method's class. If null, declaring type defaults to test method's class type.
        /// </param>
        /// <param name="dynamicDataSourceType">
        /// Specifies whether the data is stored as property or in method.
        /// </param>
        public DynamicDataAttribute(string dynamicDataSourceName, Type dynamicDataDeclaringType, DynamicDataSourceType dynamicDataSourceType = DynamicDataSourceType.Property)
            : this(dynamicDataSourceName, dynamicDataSourceType)
        {
            this.dynamicDataDeclaringType = dynamicDataDeclaringType;
        }

        /// <summary>
        /// Gets or sets the name of method used to customize the display name in test results.
        /// </summary>
        public string DynamicDataDisplayName { get; set; }

        /// <summary>
        /// Gets or sets the declaring type used to customize the display name in test results.
        /// </summary>
        public Type DynamicDataDisplayNameDeclaringType { get; set; }

        /// <inheritdoc />
        public IEnumerable<object[]> GetData(MethodInfo methodInfo)
        {
            // Check if the declaring type of test data is passed in constructor. If not, default to test method's class type.
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
                        throw new ArgumentNullException(string.Format("{0} {1}", DynamicDataSourceType.Property, this.dynamicDataSourceName));
                    }

                    obj = property.GetValue(null, null);

                    break;

                case DynamicDataSourceType.Method:
                    var method = this.dynamicDataDeclaringType.GetTypeInfo().GetDeclaredMethod(this.dynamicDataSourceName);
                    if (method == null)
                    {
                        throw new ArgumentNullException(string.Format("{0} {1}", DynamicDataSourceType.Method, this.dynamicDataSourceName));
                    }

                    obj = method.Invoke(null, null);

                    break;
            }

            if (obj == null)
            {
                throw new ArgumentNullException(
                    string.Format(
                        FrameworkMessages.DynamicDataValueNull,
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
            else if (!enumerable.Any())
            {
                throw new ArgumentException(
                    string.Format(
                        FrameworkMessages.DynamicDataIEnumerableEmpty,
                        this.dynamicDataSourceName,
                        this.dynamicDataDeclaringType.FullName));
            }

            return enumerable;
        }

        /// <inheritdoc />
        public string GetDisplayName(MethodInfo methodInfo, object[] data)
        {
            if (this.DynamicDataDisplayName != null)
            {
                var dynamicDisplayNameDeclaringType = this.DynamicDataDisplayNameDeclaringType ?? methodInfo.DeclaringType;

                var method = dynamicDisplayNameDeclaringType.GetTypeInfo().GetDeclaredMethod(this.DynamicDataDisplayName);
                if (method == null)
                {
                    throw new ArgumentNullException(string.Format("{0} {1}", DynamicDataSourceType.Method, this.DynamicDataDisplayName));
                }

                var parameters = method.GetParameters();
                if (parameters.Length != 2 ||
                    parameters[0].ParameterType != typeof(MethodInfo) ||
                    parameters[1].ParameterType != typeof(object[]) ||
                    method.ReturnType != typeof(string) ||
                    !method.IsStatic ||
                    !method.IsPublic)
                {
                    throw new ArgumentNullException(
                        string.Format(
                            FrameworkMessages.DynamicDataDisplayName,
                            this.DynamicDataDisplayName,
                            typeof(string).Name,
                            string.Join(", ", typeof(MethodInfo).Name, typeof(object[]).Name)));
                }

                return method.Invoke(null, new object[] { methodInfo, data }) as string;
            }
            else if (data != null)
            {
                return string.Format(CultureInfo.CurrentCulture, FrameworkMessages.DataDrivenResultDisplayName, methodInfo.Name, string.Join(",", data.AsEnumerable()));
            }

            return null;
        }
    }
}