// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Discovery
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Reflection;

    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Determines if a method is a valid test method.
    /// </summary>
    internal class TestMethodValidator
    {
        private readonly ReflectHelper reflectHelper;

        /// <summary>
        /// Initializes a new instance of the <see cref="TestMethodValidator"/> class.
        /// </summary>
        /// <param name="reflectHelper">An instance to reflection helper for type information.</param>
        internal TestMethodValidator(ReflectHelper reflectHelper)
        {
            this.reflectHelper = reflectHelper;
        }

        /// <summary>
        /// Determines if a method is a valid test method.
        /// </summary>
        /// <param name="testMethodInfo"> The reflected method. </param>
        /// <param name="type"> The reflected type. </param>
        /// <param name="warnings"> Contains warnings if any, that need to be passed back to the caller. </param>
        /// <returns> Return true if a method is a valid test method. </returns>
        internal virtual bool IsValidTestMethod(MethodInfo testMethodInfo, Type type, ICollection<string> warnings)
        {
            if (!this.reflectHelper.IsAttributeDefined(testMethodInfo, typeof(TestMethodAttribute), false)
                && !this.reflectHelper.HasAttributeDerivedFrom(testMethodInfo, typeof(TestMethodAttribute), false))
            {
                return false;
            }

            // Generic method Definitions are not valid.
            if (testMethodInfo.IsGenericMethodDefinition)
            {
                var message = string.Format(CultureInfo.CurrentCulture, Resource.UTA_ErrorGenericTestMethod, testMethodInfo.DeclaringType.FullName, testMethodInfo.Name);
                warnings.Add(message);
                return false;
            }

            // Todo: Decide whether parameter count matters.
            // The isGenericMethod check below id to verify that there are no closed generic methods slipping through.
            // Closed generic methods being GenericMethod<int> and open being GenericMethod<T>.
            var isValidTestMethod = testMethodInfo.IsPublic && !testMethodInfo.IsAbstract && !testMethodInfo.IsStatic
                                    && !testMethodInfo.IsGenericMethod
                                    && testMethodInfo.IsVoidOrTaskReturnType();

            if (!isValidTestMethod)
            {
                var message = string.Format(CultureInfo.CurrentCulture, Resource.UTA_ErrorIncorrectTestMethodSignature, type.FullName, testMethodInfo.Name);
                warnings.Add(message);
                return false;
            }

            return true;
        }
    }
}
