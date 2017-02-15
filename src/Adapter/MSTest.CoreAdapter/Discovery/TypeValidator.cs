// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Discovery
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Reflection;

    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Determines whether a type is a valid test class for this adapter.
    /// </summary>
    internal class TypeValidator
    {
        private static readonly string TestContextFullName = typeof(TestContext).FullName;
        private readonly ReflectHelper reflectHelper;

        /// <summary>
        /// Initializes a new instance of the <see cref="TypeValidator"/> class.
        /// </summary>
        /// <param name="reflectHelper">An instance to reflection helper for type information.</param>
        internal TypeValidator(ReflectHelper reflectHelper)
        {
            this.reflectHelper = reflectHelper;
        }

        /// <summary>
        /// Determines if a type is a valid test class for this adapter.
        /// </summary>
        /// <param name="type">The reflected type.</param>
        /// <param name="warnings">Contains warnings if any, that need to be passed back to the caller.</param>
        /// <returns>Return true if it is a valid test class.</returns>
        internal virtual bool IsValidTestClass(Type type, ICollection<string> warnings)
        {
            if (type.GetTypeInfo().IsClass &&
                    (this.reflectHelper.IsAttributeDefined(type, typeof(TestClassAttribute), false) ||
                    this.reflectHelper.HasAttributeDerivedFrom(type, typeof(TestClassAttribute), false)))
            {
                var isPublic = type.GetTypeInfo().IsPublic || (type.GetTypeInfo().IsNested && type.GetTypeInfo().IsNestedPublic);

                // non-public class
                if (!isPublic)
                {
                    var warning = string.Format(CultureInfo.CurrentCulture, Resource.UTA_ErrorNonPublicTestClass, type.FullName);
                    warnings.Add(warning);
                    return false;
                }

                // Generic class
                if (type.GetTypeInfo().IsGenericTypeDefinition && !type.GetTypeInfo().IsAbstract)
                {
                    // In IDE generic classes that are not abstract are treated as not runnable. Keep consistence.
                    var warning = string.Format(CultureInfo.CurrentCulture, Resource.UTA_ErrorNonPublicTestClass, type.FullName);
                    warnings.Add(warning);
                    return false;
                }

                // Class is not valid if the testContext property is incorrect
                if (!this.HasCorrectTestContextSignature(type))
                {
                    var warning = string.Format(CultureInfo.CurrentCulture, Resource.UTA_ErrorInValidTestContextSignature, type.FullName);
                    warnings.Add(warning);
                    return false;
                }

                // Abstract test classes can be base classes for derived test classes.
                //   There is no way to see if there are derived test classes.
                //   Thus if a test class is abstract, just ignore all test methods from it
                //   (they will be visible in derived classes). No warnings (such as test method, deployment item,
                //   etc attribute is defined on the class) will be generated for this class:
                // What we do is:
                //   - report the class as "not valid" test class. This will cause to skip enumerating tests from it.
                //   - Do not generate warnings/do not create NOT RUNNABLE tests.
                if (type.GetTypeInfo().IsAbstract)
                {
                    return false;
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Determines if the type has a valid TestContext property definition.
        /// </summary>
        /// <param name="type">The reflected type.</param>
        /// <returns>Returns true if type has a valid TestContext property definition.</returns>
        internal bool HasCorrectTestContextSignature(Type type)
        {
            Debug.Assert(type != null, "HasCorrectTestContextSignature type is null");

            var propertyInfoEnumerable = type.GetTypeInfo().DeclaredProperties;
            var propertyInfo = new List<PropertyInfo>();

            foreach (var pinfo in propertyInfoEnumerable)
            {
                // PropertyType.FullName can be null if the property is a generic type.
                if (TestContextFullName.Equals(pinfo.PropertyType.FullName, StringComparison.Ordinal))
                {
                    propertyInfo.Add(pinfo);
                }
            }

            if (propertyInfo.Count == 0)
            {
                return true;
            }

            foreach (var pinfo in propertyInfo)
            {
                var setInfo = pinfo.SetMethod;
                if (setInfo == null)
                {
                    // we have a getter, but not a setter.
                    return false;
                }

                if (setInfo.IsPrivate || setInfo.IsStatic || setInfo.IsAbstract)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
