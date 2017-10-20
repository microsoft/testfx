// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;
    using System.Security;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    internal class ReflectHelper : MarshalByRefObject
    {
        /// <summary>
        /// Contains the memberInfo Vs the name/type of the attributes defined on that member. (FYI: - MemberInfo denotes properties, fields, methods, events)
        /// </summary>
        private Dictionary<MemberInfo, Dictionary<string, object>> attributeCache = new Dictionary<MemberInfo, Dictionary<string, object>>();

        /// <summary>
        /// Checks to see if the parameter memberInfo contains the parameter attribute or not.
        /// </summary>
        /// <param name="memberInfo">Member/Type to test</param>
        /// <param name="attributeType">Attribute to search for</param>
        /// <param name="inherit">Look throug inheritence or not</param>
        /// <returns>True if the attribute of the specified type is defined.</returns>
        public virtual bool IsAttributeDefined(MemberInfo memberInfo, Type attributeType, bool inherit)
        {
            if (memberInfo == null)
            {
                throw new ArgumentNullException(nameof(memberInfo));
            }

            if (attributeType == null)
            {
                throw new ArgumentNullException(nameof(attributeType));
            }

            Debug.Assert(attributeType != null, "attrbiuteType should not be null.");

            // Get attributes defined on the member from the cache.
            Dictionary<string, object> attributes = this.GetAttributes(memberInfo, inherit);
            if (attributes == null)
            {
                // If we could not obtain all attributes from cache, just get the one we need.
                var specificAttributes = GetCustomAttributes(memberInfo, attributeType, inherit);
                var requiredAttributeQualifiedName = attributeType.AssemblyQualifiedName;

                return specificAttributes.Any(a => string.Equals(a.GetType().AssemblyQualifiedName, requiredAttributeQualifiedName));
            }

            string nameToFind = attributeType.AssemblyQualifiedName;
            if (attributes.ContainsKey(nameToFind))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Checks to see if the parameter memberInfo contains the parameter attribute or not.
        /// </summary>
        /// <param name="type">Member/Type to test</param>
        /// <param name="attributeType">Attribute to search for</param>
        /// <param name="inherit">Look throug inheritence or not</param>
        /// <returns>True if the specified attribute is defined on the type.</returns>
        public virtual bool IsAttributeDefined(Type type, Type attributeType, bool inherit)
        {
            return this.IsAttributeDefined(type.GetTypeInfo(), attributeType, inherit);
        }

        /// <summary>
        /// Returns true when specified class/member has attribute derived from specific attribute.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <param name="baseAttributeType">The base attribute type.</param>
        /// <param name="inherit">Should look at inheritance tree.</param>
        /// <returns>An object derived from Attribute that corresponds to the instance of found attribute.</returns>
        public virtual bool HasAttributeDerivedFrom(Type type, Type baseAttributeType, bool inherit)
        {
            return this.HasAttributeDerivedFrom(type.GetTypeInfo(), baseAttributeType, inherit);
        }

        /// <summary>
        /// Returns true when specified class/member has attribute derived from specific attribute.
        /// </summary>
        /// <param name="memberInfo">The member info.</param>
        /// <param name="baseAttributeType">The base attribute type.</param>
        /// <param name="inherit">Should look at inheritance tree.</param>
        /// <returns>An object derived from Attribute that corresponds to the instance of found attribute.</returns>
        public bool HasAttributeDerivedFrom(MemberInfo memberInfo, Type baseAttributeType, bool inherit)
        {
            if (memberInfo == null)
            {
                throw new ArgumentNullException(nameof(memberInfo));
            }

            if (baseAttributeType == null)
            {
                throw new ArgumentNullException(nameof(baseAttributeType));
            }

            // Get all attributes on the member.
            Dictionary<string, object> attributes = this.GetAttributes(memberInfo, inherit);
            if (attributes == null)
            {
                PlatformServiceProvider.Instance.AdapterTraceLogger.LogWarning("ReflectHelper.HasAttributeDerivedFrom: Failed to get attribute cache. Ignoring attribute inheritance and falling into 'type defines Attribute model', so that we have some data.");

                return this.IsAttributeDefined(memberInfo, baseAttributeType, inherit);
            }

            // Try to find the attribute that is derived from baseAttrType.
            foreach (object attribute in attributes.Values)
            {
                Debug.Assert(attribute != null, "ReflectHeler.DefinesAttributeDerivedFrom: internal error: wrong value in the attrs dictionary.");

                Type attributeType = attribute.GetType();
                if (attributeType.GetTypeInfo().IsSubclassOf(baseAttributeType))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Resolves the expected exception attribute. The function will try to
        /// get all the expected exception attributes defined for a testMethod.
        /// </summary>
        /// <param name="methodInfo">The MethodInfo instance.</param>
        /// <param name="testMethod">The test method.</param>
        /// <returns>
        /// The expected exception attribute found for this test. Null if not found.
        /// </returns>
        public virtual ExpectedExceptionBaseAttribute ResolveExpectedExceptionHelper(MethodInfo methodInfo, TestMethod testMethod)
        {
            Debug.Assert(methodInfo != null, "MethodInfo should be non-null");

            // Get the expected exception attribute
            ExpectedExceptionBaseAttribute[] expectedExceptions;
            try
            {
                expectedExceptions = GetCustomAttributes(methodInfo, typeof(ExpectedExceptionBaseAttribute), true).OfType<ExpectedExceptionBaseAttribute>().ToArray();
            }
            catch (Exception ex)
            {
                // If construction of the attribute throws an exception, indicate that there was an
                // error when trying to run the test
                string errorMessage = string.Format(
                                                    CultureInfo.CurrentCulture,
                                                    Resource.UTA_ExpectedExceptionAttributeConstructionException,
                                                    testMethod.FullClassName,
                                                    testMethod.Name,
                                                    StackTraceHelper.GetExceptionMessage(ex));
                throw new TypeInspectionException(errorMessage);
            }

            if (expectedExceptions == null || expectedExceptions.Length == 0)
            {
                return null;
            }

            // Verify that there is only one attribute (multiple attributes derived from
            // ExpectedExceptionBaseAttribute are not allowed on a test method)
            if (expectedExceptions.Length > 1)
            {
                string errorMessage = string.Format(
                                                    CultureInfo.CurrentCulture,
                                                    Resource.UTA_MultipleExpectedExceptionsOnTestMethod,
                                                    testMethod.FullClassName,
                                                    testMethod.Name);
                throw new TypeInspectionException(errorMessage);
            }

            // Set the expected exception attribute to use for this test
            ExpectedExceptionBaseAttribute expectedException = expectedExceptions[0];

            return expectedException;
        }

        /// <summary>
        /// Returns object to be used for controlling lifetime, null means infinite lifetime.
        /// </summary>
        /// <returns>
        /// The <see cref="object"/>.
        /// </returns>
        [SecurityCritical]
        public override object InitializeLifetimeService()
        {
            return null;
        }

        /// <summary>
        /// Match return type of method.
        /// </summary>
        /// <param name="method">The method to inspect.</param>
        /// <param name="returnType">The return type to match.</param>
        /// <returns>True if there is a match.</returns>
        internal static bool MatchReturnType(MethodInfo method, Type returnType)
        {
            if (method == null)
            {
                throw new ArgumentNullException(nameof(method));
            }

            if (returnType == null)
            {
                throw new ArgumentNullException(nameof(returnType));
            }

            return method.ReturnType.Equals(returnType);
        }

        /// <summary>
        /// Get custom attributes on a member for both normal and reflection only load.
        /// </summary>
        /// <param name="memberInfo">Memeber for which attributes needs to be retrieved.</param>
        /// <param name="type">Type of attribute to retrieve.</param>
        /// <param name="inherit">If inheritied type of attribute.</param>
        /// <returns>All attributes of give type on member.</returns>
        internal static Attribute[] GetCustomAttributes(MemberInfo memberInfo, Type type, bool inherit)
        {
            if (memberInfo == null)
            {
                return null;
            }

            var attributesArray = PlatformServiceProvider.Instance.ReflectionOperations.GetCustomAttributes(
                memberInfo,
                type,
                inherit);

            return attributesArray.OfType<Attribute>().ToArray();
        }

        /// <summary>
        /// Get custom attributes on a member for both normal and reflection only load.
        /// </summary>
        /// <param name="memberInfo">Memeber for which attributes needs to be retrieved.</param>
        /// <param name="inherit">If inheritied type of attribute.</param>
        /// <returns>All attributes of give type on member.</returns>
        internal static object[] GetCustomAttributes(MemberInfo memberInfo, bool inherit)
        {
            if (memberInfo == null)
            {
                return null;
            }

            var attributesArray = PlatformServiceProvider.Instance.ReflectionOperations.GetCustomAttributes(
                memberInfo,
                inherit);

            return attributesArray.ToArray();
        }

        /// <summary>
        /// Returns the first attribute of the specified type or null if no attribute
        /// of the specified type is set on the method.
        /// </summary>
        /// <typeparam name="AttributeType">The type of attribute to return.</typeparam>
        /// <param name="method">The method on which the attribute is defined.</param>
        /// <returns>The attribute or null if none exists.</returns>
        internal AttributeType GetAttribute<AttributeType>(MethodInfo method)
            where AttributeType : class
        {
            if (this.IsAttributeDefined(method, typeof(AttributeType), false))
            {
                object[] attributes = GetCustomAttributes(method, typeof(AttributeType), false);
                Debug.Assert(attributes.Length == 1, "Should only be one attribute.");
                return attributes[0] as AttributeType;
            }

            return null;
        }

        /// <summary>
        /// Returns the attribute of the specified type. Null if no attribute of the specified type is defined.
        /// </summary>
        /// <param name="attributeType">The attribute type.</param>
        /// <param name="method">The method to inspect.</param>
        /// <returns>Attribute of the specified type. Null if not found.</returns>
        internal Attribute GetAttribute(Type attributeType, MethodInfo method)
        {
            if (this.IsAttributeDefined(method, attributeType, false))
            {
                object[] attributes = GetCustomAttributes(method, attributeType, false);
                Debug.Assert(attributes.Length == 1, "Should only be one attribute.");
                return attributes[0] as Attribute;
            }

            return null;
        }

        /// <summary>
        /// Returns true when the method is delcared in the assembly where the type is declared.
        /// </summary>
        /// <param name="method">The method to check for.</param>
        /// <param name="type">The type declared in the assembly to check.</param>
        /// <returns>True if the method is declared in the assembly where the type is declared.</returns>
        internal virtual bool IsMethodDeclaredInSameAssemblyAsType(MethodInfo method, Type type)
        {
            return method.DeclaringType.GetTypeInfo().Assembly.Equals(type.GetTypeInfo().Assembly);
        }

        /// <summary>
        /// Get categories applied to the test method
        /// </summary>
        /// <param name="categoryAttributeProvider">The member to inspect.</param>
        /// <returns>Categories defined.</returns>
        internal virtual string[] GetCategories(MemberInfo categoryAttributeProvider)
        {
            var categories = this.GetCustomAttributesRecursively(categoryAttributeProvider, typeof(TestCategoryBaseAttribute));
            List<string> testCategories = new List<string>();

            if (categories != null)
            {
                foreach (TestCategoryBaseAttribute category in categories)
                {
                    testCategories.AddRange(category.TestCategories);
                }
            }

            return testCategories.ToArray();
        }

        /// <summary>
        /// Get the parallelization behavior for a test method.
        /// </summary>
        /// <param name="testMethod">Test method.</param>
        /// <returns>True if test method should not run in parallel.</returns>
        internal bool IsDoNotParallelizeSet(MemberInfo testMethod)
        {
            return this.GetCustomAttributes(testMethod, typeof(DoNotParallelizeAttribute)).Any()
                   || this.GetCustomAttributes(testMethod.DeclaringType.GetTypeInfo(), typeof(DoNotParallelizeAttribute)).Any();
        }

        /// <summary>
        /// Gets custom attributes at the class and assembly for a method.
        /// </summary>
        /// <param name="attributeProvider">Method Info or Member Info or a Type</param>
        /// <param name="type"> What type of CustomAttribute you need. For instance: TestCategory, Owner etc.,</param>
        /// <returns>The categories of the specified type on the method. </returns>
        internal IEnumerable<object> GetCustomAttributesRecursively(MemberInfo attributeProvider, Type type)
        {
            var categories = this.GetCustomAttributes(attributeProvider, typeof(TestCategoryBaseAttribute));
            if (categories != null)
            {
                categories = categories.Concat(this.GetCustomAttributes(attributeProvider.DeclaringType.GetTypeInfo(), typeof(TestCategoryBaseAttribute))).ToArray();
            }

            if (categories != null)
            {
                categories = categories.Concat(this.GetCustomAttributeForAssembly(attributeProvider, typeof(TestCategoryBaseAttribute))).ToArray();
            }

            if (categories != null)
            {
                return categories;
            }

            return Enumerable.Empty<object>();
        }

        /// <summary>
        /// Gets the custom attributes on the assembly of a member info
        /// NOTE: having it as separate virtual method, so that we can extend it for testing.
        /// </summary>
        /// <param name="memberInfo">The member to inspect.</param>
        /// <param name="type">The attribute type to find.</param>
        /// <returns>Custom attributes defined.</returns>
        internal virtual Attribute[] GetCustomAttributeForAssembly(MemberInfo memberInfo, Type type)
        {
            return
                PlatformServiceProvider.Instance.ReflectionOperations.GetCustomAttributes(
                    memberInfo.DeclaringType.GetTypeInfo().Assembly,
                    type).OfType<Attribute>().ToArray();
        }

        /// <summary>
        /// Gets the custom attributes of the provided type on a memberInfo
        /// </summary>
        /// <param name="attributeProvider"> The member to reflect on. </param>
        /// <param name="type"> The attribute type. </param>
        /// <returns>Attributes defined.</returns>
        internal virtual Attribute[] GetCustomAttributes(MemberInfo attributeProvider, Type type)
        {
            return GetCustomAttributes(attributeProvider, type, true);
        }

        /// <summary>
        /// KeyValue pairs that are provided by TestOwnerAttribute of the given test method.
        /// </summary>
        /// <param name="ownerAttributeProvider">The member to inspect.</param>
        /// <returns>The owner trait.</returns>
        internal virtual Trait GetTestOwnerAsTraits(MemberInfo ownerAttributeProvider)
        {
            string owner = this.GetOwner(ownerAttributeProvider);

            if (string.IsNullOrEmpty(owner))
            {
                return null;
            }
            else
            {
                return new Trait("Owner", owner);
            }
        }

        /// <summary>
        /// KeyValue pairs that are provided by TestPriorityAttributes of the given test method.
        /// </summary>
        /// <param name="testPriority">The priority</param>
        /// <returns>The corresponding trait.</returns>
        internal virtual Trait GetTestPriorityAsTraits(int? testPriority)
        {
            if (testPriority == null)
            {
                return null;
            }
            else
            {
                return new Trait("Priority", ((int)testPriority).ToString(CultureInfo.InvariantCulture));
            }
        }

        /// <summary>
        /// Priority if any set for test method. Will return priority if attribute is applied to TestMethod
        /// else null;
        /// </summary>
        /// <param name="priorityAttributeProvider">The member to inspect.</param>
        /// <returns>Priority value if defined. Null otherwise.</returns>
        internal virtual int? GetPriority(MemberInfo priorityAttributeProvider)
        {
            var priorityAttribute = GetCustomAttributes(priorityAttributeProvider, typeof(PriorityAttribute), true);

            if (priorityAttribute == null || priorityAttribute.Length != 1)
            {
                return null;
            }

            return (priorityAttribute[0] as PriorityAttribute).Priority;
        }

        /// <summary>
        /// Priority if any set for test method. Will return priority if attribute is applied to TestMethod
        /// else null;
        /// </summary>
        /// <param name="ignoreAttributeProvider">The member to inspect.</param>
        /// <returns>Priority value if defined. Null otherwise.</returns>
        internal virtual string GetIgnoreMessage(MemberInfo ignoreAttributeProvider)
        {
            var ignoreAttribute = GetCustomAttributes(ignoreAttributeProvider, typeof(IgnoreAttribute), true);

            if (!ignoreAttribute.Any())
            {
                return null;
            }

            return (ignoreAttribute?.FirstOrDefault() as IgnoreAttribute).IgnoreMessage;
        }

        /// <summary>
        /// KeyValue pairs that are provided by TestPropertyAttributes of the given test method.
        /// </summary>
        /// <param name="testPropertyProvider">The member to inspect.</param>
        /// <returns>List of traits.</returns>
        internal virtual IEnumerable<Trait> GetTestPropertiesAsTraits(MemberInfo testPropertyProvider)
        {
            var testPropertyAttributes = this.GetTestPropertyAttributes(testPropertyProvider);

            foreach (TestPropertyAttribute testProperty in testPropertyAttributes)
            {
                Trait testPropertyPair;
                if (testProperty.Name == null)
                {
                    testPropertyPair = new Trait(string.Empty, testProperty.Value);
                }
                else
                {
                    testPropertyPair = new Trait(testProperty.Name, testProperty.Value);
                }

                yield return testPropertyPair;
            }
        }

        /// <summary>
        /// Get attribute defined on a method which is of given type of subtype of given type.
        /// </summary>
        /// <typeparam name="AttributeType">The attribute type.</typeparam>
        /// <param name="memberInfo">The member to inspect.</param>
        /// <param name="inherit">Look at inheritance chain.</param>
        /// <returns>An instance of the attribute.</returns>
        internal AttributeType GetDerivedAttribute<AttributeType>(MemberInfo memberInfo, bool inherit)
            where AttributeType : Attribute
        {
            Dictionary<string, object> attributes = this.GetAttributes(memberInfo, inherit);
            if (attributes == null)
            {
                return null;
            }

            // Try to find the attribute that is derived from baseAttrType.
            foreach (object attribute in attributes.Values)
            {
                Debug.Assert(attribute != null, "ReflectHeler.DefinesAttributeDerivedFrom: internal error: wrong value in the attrs dictionary.");

                Type attributeType = attribute.GetType();
                if (attributeType.Equals(typeof(AttributeType)) || attributeType.GetTypeInfo().IsSubclassOf(typeof(AttributeType)))
                {
                    return attribute as AttributeType;
                }
            }

            return null;
        }

        /// <summary>
        /// Get attribute defined on a method which is of given type of subtype of given type.
        /// </summary>
        /// <typeparam name="AttributeType">The attribute type.</typeparam>
        /// <param name="type">The type to inspect.</param>
        /// <param name="inherit">Look at inheritance chain.</param>
        /// <returns>An instance of the attribute.</returns>
        internal AttributeType GetDerivedAttribute<AttributeType>(Type type, bool inherit)
            where AttributeType : Attribute
        {
            var attributes = GetCustomAttributes(type.GetTypeInfo(), inherit);
            if (attributes == null)
            {
                return null;
            }

            // Try to find the attribute that is derived from baseAttrType.
            foreach (object attribute in attributes)
            {
                Debug.Assert(attribute != null, "ReflectHeler.DefinesAttributeDerivedFrom: internal error: wrong value in the attrs dictionary.");

                Type attributeType = attribute.GetType();
                if (attributeType.Equals(typeof(AttributeType)) || attributeType.GetTypeInfo().IsSubclassOf(typeof(AttributeType)))
                {
                    return attribute as AttributeType;
                }
            }

            return null;
        }

        /// <summary>
        /// Returns owner if attribute is applied to TestMethod, else null;
        /// </summary>
        /// <param name="ownerAttributeProvider">The member to inspect.</param>
        /// <returns>owner if attribute is applied to TestMethod, else null;</returns>
        private string GetOwner(MemberInfo ownerAttributeProvider)
        {
            var ownerAttribute = GetCustomAttributes(ownerAttributeProvider, typeof(OwnerAttribute), true).ToArray();

            if (ownerAttribute == null || ownerAttribute.Length != 1)
            {
                return null;
            }

            return (ownerAttribute[0] as OwnerAttribute).Owner;
        }

        /// <summary>
        /// Return TestProperties attributes applied to TestMethod
        /// </summary>
        /// <param name="propertyAttributeProvider">The member to inspect.</param>
        /// <returns>TestProperty attributes if defined. Empty otherwise.</returns>
        private IEnumerable<Attribute> GetTestPropertyAttributes(MemberInfo propertyAttributeProvider)
        {
            return GetCustomAttributes(propertyAttributeProvider, typeof(TestPropertyAttribute), true);
        }

        /// <summary>
        /// Get the Attributes (TypeName/TypeObject) for a given member.
        /// </summary>
        /// <param name="memberInfo">The member to inspect.</param>
        /// <param name="inherit">Look at inheritance chain.</param>
        /// <returns>attributes defined.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Requirement is to handle all kinds of user exceptions and message appropriately.")]
        private Dictionary<string, object> GetAttributes(MemberInfo memberInfo, bool inherit)
        {
            // If the information is cached, then use it otherwise populate the cache using
            // the reflection APIs.
            Dictionary<string, object> attributes;
            lock (this.attributeCache)
            {
                if (!this.attributeCache.TryGetValue(memberInfo, out attributes))
                {
                    // Populate the cache
                    attributes = new Dictionary<string, object>();

                    object[] customAttributesArray = null;
                    try
                    {
                        customAttributesArray = GetCustomAttributes(memberInfo, inherit);
                    }
                    catch (Exception ex)
                    {
                        // Get the exception description
                        string description;
                        try
                        {
                            // Can throw if the Message or StackTrace properties throw exceptions
                            description = ex.ToString();
                        }
                        catch (Exception ex2)
                        {
                            description =
                                ex.GetType().FullName +
                                ": (Failed to get exception description due to an exception of type " +
                                    ex2.GetType().FullName + ')';
                        }

                        PlatformServiceProvider.Instance.AdapterTraceLogger.LogWarning(
                            "Getting custom attributes for type {0} threw exception (will ignore and use the reflection way): {1}",
                            memberInfo.GetType().FullName,
                            description);

                        // Since we cannot check by attribute names, do it in reflection way.
                        // Note 1: this will not work for different version of assembly but it is better than nothing.
                        // Note 2: we cannot cache this because we don't know if there are other attributes defined.
                        return null;
                    }

                    Debug.Assert(customAttributesArray != null, "attributes should not be null.");

                    foreach (object customAttribute in customAttributesArray)
                    {
                        Type attrType = customAttribute.GetType();
                        attributes[attrType.AssemblyQualifiedName] = customAttribute;
                    }

                    this.attributeCache.Add(memberInfo, attributes);
                }
            }

            return attributes;
        }
    }
}