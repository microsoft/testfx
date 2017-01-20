// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations
{
    using System;
    using System.Linq;
    using System.Reflection;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
    using System.Collections.Generic;

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
                this.customAttributes[hashcode].Concat(values);
            }
            else
            {
                this.customAttributes[hashcode] = values;
            }
        }

        public override Attribute[] GetCustomAttributeForAssembly(MemberInfo memberInfo, Type type)
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

        public override Attribute[] GetCustomAttributes(MemberInfo memberInfo, Type type)
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
