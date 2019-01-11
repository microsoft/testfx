// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;

    /// <summary>
    /// A testable implementation of reflect helper.
    /// </summary>
    internal class TestableReflectHelper : ReflectHelper
    {
        /// <summary>
        /// A dictionary to hold mock custom attributes. The int represents a hascode of
        /// the Type of custom attribute and the level its applied at :
        /// MemberTypes.All for assembly level
        /// MemberTypes.TypeInfo for class level
        /// MemberTypes.Method for method level
        /// </summary>
        private Dictionary<int, Attribute[]> customAttributes;

        public TestableReflectHelper()
        {
            this.customAttributes = new Dictionary<int, Attribute[]>();
        }

        public void SetCustomAttribute(Type type, Attribute[] values, MemberTypes memberTypes)
        {
            var hashcode = type.FullName.GetHashCode() + memberTypes.GetHashCode();
            if (this.customAttributes.ContainsKey(hashcode))
            {
                this.customAttributes[hashcode] = this.customAttributes[hashcode].Concat(values).ToArray();
            }
            else
            {
                this.customAttributes[hashcode] = values;
            }
        }

        internal override Attribute[] GetCustomAttributeForAssembly(MemberInfo memberInfo, Type type)
        {
            var hashcode = MemberTypes.All.GetHashCode() + type.FullName.GetHashCode();

            if (this.customAttributes.ContainsKey(hashcode))
            {
                return this.customAttributes[hashcode];
            }
            else
            {
                return Enumerable.Empty<Attribute>().ToArray();
            }
        }

        internal override Attribute[] GetCustomAttributes(MemberInfo memberInfo, Type type)
        {
            var hashcode = memberInfo.MemberType.GetHashCode() + type.FullName.GetHashCode();

            if (this.customAttributes.ContainsKey(hashcode))
            {
                return this.customAttributes[hashcode];
            }
            else
            {
                return Enumerable.Empty<Attribute>().ToArray();
            }
        }
    }
}
