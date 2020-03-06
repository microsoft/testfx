// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;

    /// <summary>
    /// Attribute to define dynamic data for a test method.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class EnumDataSourceAttribute : Attribute, ITestDataSource
    {
        private readonly Type enumDataSource;
        private readonly object[] exclusions;

        /// <summary>
        /// Initializes a new instance of the <see cref="EnumDataSourceAttribute"/> class.
        /// </summary>
        /// <param name="enumDataSource">
        /// The type of the <see cref="Enum"/> to iterate for the test.
        /// </param>
        public EnumDataSourceAttribute(Type enumDataSource)
            : this(enumDataSource, new object[0])
        {
            // Need to have this constructor explicitely to fix a CLS compliant error.
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EnumDataSourceAttribute"/> class.
        /// </summary>
        /// <param name="enumDataSource">
        /// The type of the <see cref="Enum"/> to iterate for the test.
        /// </param>
        /// <param name="exclusions">
        /// Values to exclude during the iteration.
        /// </param>
        public EnumDataSourceAttribute(Type enumDataSource, params object[] exclusions)
        {
            this.enumDataSource = enumDataSource ??
                                  throw new ArgumentNullException(nameof(enumDataSource), "The enum to iterate is not specified.");

            this.exclusions = exclusions;
        }

        /// <inheritdoc />
        public IEnumerable<object[]> GetData(MethodInfo methodInfo)
        {
            if (!this.enumDataSource.GetTypeInfo().IsEnum)
            {
                throw new InvalidCastException("The requested type is not an enum.");
            }

            foreach (var value in Enum.GetValues(this.enumDataSource))
            {
                if (!this.exclusions.Contains(value))
                {
                    yield return new[] { value };
                }
            }
        }

        /// <inheritdoc />
        public string GetDisplayName(MethodInfo methodInfo, object[] data)
        {
            return data == null ? null : $"{methodInfo.Name} ({string.Join(", ", data)})";
        }
    }
}
