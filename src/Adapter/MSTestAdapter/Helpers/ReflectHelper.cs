#define TODO

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;    
    using System.Runtime.CompilerServices;
    using System.Threading.Tasks;

    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    internal class ReflectHelper : MarshalByRefObject
    {
        /// <summary>
        /// Checks to see if the parameter memberInfo contains the parameter attribute or not.
        /// </summary>
        /// <param name="memberInfo">Member/Type to test</param>
        /// <param name="attributeType">Attribute to search for</param>
        /// <param name="inherit">Look throug inheritence or not</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1062:Validate arguments of public methods", MessageId = "1")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1062:Validate arguments of public methods", MessageId = "0")]
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

            Debug.Assert(attributeType != null);

            // Get attributes defined on the member from the cache. 
            Dictionary<string, object> attributes = this.GetAttributes(memberInfo, inherit);
            if (attributes == null)
            {
                // If we could not obtain it in a fast (with cache) way, use slow reflection directly.
                return memberInfo.IsDefined(attributeType, inherit);
            }

            string nameToFind = attributeType.FullName;
            if (attributes.ContainsKey(nameToFind))
            {
                return true;
            }

            return false;
        }
        
        /// <summary>
        /// Checks to see if the parameter memberInfo contains the parameter attribute or not.
        /// </summary>
        /// <param name="memberInfo">Member/Type to test</param>
        /// <param name="attributeType">Attribute to search for</param>
        /// <param name="inherit">Look throug inheritence or not</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1062:Validate arguments of public methods", MessageId = "1")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1062:Validate arguments of public methods", MessageId = "0")]
        public virtual bool IsAttributeDefined(Type type, Type attributeType, bool inherit)
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            if (attributeType == null)
            {
                throw new ArgumentNullException(nameof(attributeType));
            }

            Debug.Assert(attributeType != null);

            // Get attributes defined on the member from the cache. 
            var attributes = type.GetTypeInfo().GetCustomAttributes(attributeType, inherit);

            if (attributes == null)
            {
                // If we could not obtain it in a fast (with cache) way, use slow reflection directly.
                return type.GetTypeInfo().IsDefined(attributeType, inherit);
            }

            Dictionary<string, object> attributesDict = new Dictionary<string, object>();

            foreach (Attribute customAttribute in attributes)
            {
                Type attrType = customAttribute.GetType();
                attributesDict[attrType.FullName] = customAttribute;
            }

            string nameToFind = attributeType.FullName;
            if (attributesDict.ContainsKey(nameToFind))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Returns true when specified class/member has attribute derived from specific attribute.
        /// </summary>
        /// <returns>An object derived from Attribute that corresponds to the instance of found attribute.</returns>
        public virtual bool HasAttributeDerivedFrom(Type type, Type baseAttributeType, bool inherit)
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            if (baseAttributeType == null)
            {
                throw new ArgumentNullException(nameof(baseAttributeType));
            }

            object targetAttribute = null;

            // Get all attributes on the member.
            var attributes = type.GetTypeInfo().GetCustomAttributes(inherit);
            if (attributes == null)
            {
                // TODO: mkolt: important: consider beter way. When running ObjectModel\Common tests I get this.
                PlatformServiceProvider.Instance.AdapterTraceLogger.LogWarning("ReflectHelper.DefinesAttributeDerivedFrom: this does not really work when we failed to get attribute cache. Lose attribute inheritance and fall into 'type defines Attribute model', so that at least do something, not the best...");

                // If we could not obtain attrs in a fast (with cache) way, use slow reflection directly.
                return type.GetTypeInfo().IsDefined(baseAttributeType, inherit);
            }

            // Try to find the attribute that is derived from baseAttrType.
            foreach (object attribute in attributes)
            {
                Debug.Assert(attribute != null, "ReflectHeler.DefinesAttributeDerivedFrom: internal error: wrong value in the attrs dictionary.");

                Type attributeType = attribute.GetType();
                if (attributeType.GetTypeInfo().IsSubclassOf(baseAttributeType))
                {
                    targetAttribute = attribute;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Returns true when specified class/member has attribute derived from specific attribute.
        /// </summary>
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

            object ignoredTargetAttribute;
            return this.HasAttributeDerivedFrom(memberInfo, baseAttributeType, inherit, out ignoredTargetAttribute);
        }

        /// <summary>
        /// Returns true when specified class/member has attribute derived from specific attribute.
        /// </summary>
        /// <returns>An object derived from Attribute that corresponds to the instance of found attribute.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1062:Validate arguments of public methods", MessageId = "0")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1007:UseGenericsWhereAppropriate")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1021:AvoidOutParameters", MessageId = "3#")]
        public bool HasAttributeDerivedFrom(MemberInfo memberInfo, Type baseAttributeType, bool inherit, out object targetAttribute)
        {
            if (memberInfo == null)
            {
                throw new ArgumentNullException(nameof(memberInfo));
            }

            if (baseAttributeType == null)
            {
                throw new ArgumentNullException(nameof(baseAttributeType));
            }

            targetAttribute = null;

            // Get all attributes on the member.
            Dictionary<string, object> attributes = this.GetAttributes(memberInfo, inherit);
            if (attributes == null)
            {
                // TODO: mkolt: important: consider beter way. When running ObjectModel\Common tests I get this.
                PlatformServiceProvider.Instance.AdapterTraceLogger.LogWarning("ReflectHelper.DefinesAttributeDerivedFrom: this does not really work when we failed to get attribute cache. Lose attribute inheritance and fall into 'type defines Attribute model', so that at least do something, not the best...");

                // If we could not obtain attrs in a fast (with cache) way, use slow reflection directly.
                return memberInfo.IsDefined(baseAttributeType, inherit);
            }

            // Try to find the attribute that is derived from baseAttrType.
            foreach (object attribute in attributes.Values)
            {
                Debug.Assert(attribute != null, "ReflectHeler.DefinesAttributeDerivedFrom: internal error: wrong value in the attrs dictionary.");

                Type attributeType = attribute.GetType();
                if (attributeType.GetTypeInfo().IsSubclassOf(baseAttributeType))
                {
                    targetAttribute = attribute;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Remove cache storage
        /// </summary>
        public void ClearCache()
        {
            lock (this.m_attributeCache)
            {
                this.m_attributeCache.Clear();
            }
        }

        /// <summary>
        /// Returns the first attribute of the specified type or null if no attribute
        /// of the specified type is set on the method.
        /// </summary>
        /// <typeparam name="AttributeType">The type of attribute to return.</typeparam>
        /// <param name="method">The method on which the attribute is defined.</param>
        /// <returns>The attribute or null if none exists.</returns>
        internal AttributeType GetAttribute<AttributeType>(MethodInfo method) where AttributeType : class
        {
            if (this.IsAttributeDefined(method, typeof(AttributeType), false))
            {
                object[] attributes = GetCustomAttributes(method, typeof(AttributeType), false);
                Debug.Assert(attributes.Length == 1);
                return attributes[0] as AttributeType;
            }
            return null;
        }

        /// <summary>
        /// Returns the attribute of the specified type. Null if no attribute of the specified type is defined.
        /// </summary>
        internal Attribute GetAttribute(Type attributeType, MethodInfo method)
        {
            if (this.IsAttributeDefined(method, attributeType, false))
            {
                object[] attributes = GetCustomAttributes(method, attributeType, false);
                Debug.Assert(attributes.Length == 1);
                return attributes[0] as Attribute;
            }
            return null;
        }

        /// <summary>
        /// Match retun type of method.
        /// </summary>
        public static bool MatchReturnType(MethodInfo method, Type returnType)
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
        /// Get categories applied to the test method
        /// </summary>
        internal virtual string[] GetCategories(MemberInfo categoryAttributeProvider)
        {
            var categories = GetCustomAttributesRecursively(categoryAttributeProvider, typeof(TestCategoryBaseAttribute));
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
        /// Gets custom attributes at the class and assembly for a method.
        /// </summary>
        /// <param name="attributeProvider">Method Info or Member Info or a Type</param>
        /// <param name="type"> What type of CustomAttribute you need Eg: TestCategory, Owner etc.,</param>
        /// <param name="inherit">Boolean value for inhertiting from base class attributes also</param>
        /// <returns></returns>
        internal IEnumerable<object> GetCustomAttributesRecursively(MemberInfo attributeProvider,
            Type type)
        {            
            var categories = GetCustomAttributes(attributeProvider, typeof(TestCategoryBaseAttribute));
            if(categories != null)
                categories = categories.Concat(GetCustomAttributes(attributeProvider.DeclaringType.GetTypeInfo(), typeof(TestCategoryBaseAttribute))).ToArray();
            if (categories != null)
                categories = categories.Concat(GetCustomAttributeForAssembly(attributeProvider, typeof(TestCategoryBaseAttribute))).ToArray();

            if (categories != null)
                return categories;

            return Enumerable.Empty<object>();
        }

        /// <summary>
        /// Gets the custom attributes on the assembly of a member info
        /// NOTE: having it as separate virtual method, so that we can extendt it for testing.
        /// </summary>
        /// <param name="memberInfo"></param>
        /// <returns></returns>
        public virtual object[] GetCustomAttributeForAssembly(MemberInfo memberInfo,Type type)
        {
            return memberInfo.DeclaringType.GetTypeInfo().Assembly.GetCustomAttributes(type).ToArray();
        }
        
        /// <summary>
        /// Gets the custom attributes of the provided type on a memberInfo
        /// </summary>
        /// <param name="attributeProvider"> The member to reflect on. </param>
        /// <param name="type"> The attribute type. </param>
        /// <returns></returns>
        public virtual object[] GetCustomAttributes(MemberInfo attributeProvider, Type type)
        {
            return GetCustomAttributes(attributeProvider, type, true);
        }

        /// <summary>
        /// Owner if any set for test method. Will return owner if attribute is applied to TestMethod
        /// else null;
        /// </summary>       
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
        /// KeyValue pairs that are provided by TestOwnerAttribute of the given test method.
        /// </summary>
        /// <param name="categoryAttributeProvider"></param>
        /// <returns></returns>
        internal virtual Trait GetTestOwnerAsTraits(MemberInfo ownerAttributeProvider)
        {
            string Owner = this.GetOwner(ownerAttributeProvider);

            if (String.IsNullOrEmpty(Owner))
            {
                return null;
            }
            else
            {
                return new Trait("Owner", Owner);
            }
        }

        /// <summary>
        /// KeyValue pairs that are provided by TestPriorityAttributes of the given test method.
        /// </summary>
        /// <param name="testPriority"></param>
        /// <returns></returns>
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
        internal virtual int? GetPriority(MemberInfo priorityAttributeProvider)
        {
            object[] priorityAttribute = GetCustomAttributes(priorityAttributeProvider, typeof(PriorityAttribute), true);

            if (priorityAttribute == null || priorityAttribute.Length != 1)
            {
                return null;
            }

            return (priorityAttribute[0] as PriorityAttribute).Priority;
        }

        /// <summary>
        /// Will return TestProperties attributes applied to TestMethod
        /// </summary>
        private static IEnumerable<object> GetTestPropertyAttributes(MemberInfo propertyAttributeProvider)
        {
            return GetCustomAttributes(propertyAttributeProvider, typeof(TestPropertyAttribute), true);
        }

        /// <summary>
        /// KeyValue pairs that are provided by TestPropertyAttributes of the given test method.
        /// </summary>
        /// <param name="testPropertyProvider"></param>
        /// <returns></returns>
        internal virtual IEnumerable<Trait> GetTestPropertiesAsTraits(MemberInfo testPropertyProvider)
        {
            var testPropertyAttributes = GetTestPropertyAttributes(testPropertyProvider);

            foreach (TestPropertyAttribute testProperty in testPropertyAttributes)
            {
                Trait testPropertyPair;
                if (testProperty.Name == null)
                {
                    testPropertyPair = new Trait(String.Empty, testProperty.Value);
                }
                else
                {
                    testPropertyPair = new Trait(testProperty.Name, testProperty.Value);
                }

                yield return testPropertyPair;
            }
        }

        /// <summary>
        /// Get custom attributes on a member for both normal and reflection only load.
        /// </summary>
        /// <param name="memberInfo">Memeber for which attributes needs to be retrieved.</param>
        /// <param name="type">Type of attribute to retrieve.</param>
        /// <param name="inherit">If inheritied type of attribute.</param>
        /// <returns>All attributes of give type on member.</returns>
        internal static object[] GetCustomAttributes(MemberInfo memberInfo, Type type, bool inherit)
        {
            if (memberInfo == null)
            {
                return null;
            }

            if (IsReflectionOnlyLoad(memberInfo) == false)
            {
#if TODO
                return (memberInfo.GetCustomAttributes(type, inherit) == null) ? Enumerable.Empty<Object>().ToArray() : memberInfo.GetCustomAttributes(type, inherit).ToArray();
#else
                return memberInfo.GetCustomAttributes(type, inherit);
#endif
            }
            else
            {
#if !TODO
                IList<CustomAttributeData> customAttributes = CustomAttributeData.GetCustomAttributes(memberInfo);
                List<object> attributesArray = new List<object>();
                foreach (var attribute in customAttributes)
                {
                    // Retrieve attribute of given type only. Since normal loaded and reflection only type can't be equated
                    // match qualified names.
                    if ((inherit && IsTypeInheriting(attribute.Constructor.DeclaringType, type)) ||
                        (!inherit && attribute.Constructor.DeclaringType.AssemblyQualifiedName.Equals(type.AssemblyQualifiedName)))
                    {
                        Attribute attributeInstance = CreateAttributeInstance(attribute);
                        if (attributeInstance != null)
                        {
                            attributesArray.Add(attributeInstance);
                        }
                    }
                }
#else
                IEnumerable<Attribute> attributesArray = CustomAttributeExtensions.GetCustomAttributes(memberInfo, inherit);
#endif

                return attributesArray.ToArray();
            }
        }

        /// <summary>
        /// Check if type1 is inheriting from type2
        /// </summary>
        private static bool IsTypeInheriting(Type type1, Type type2)
        {
            while (type1 != null)
            {
                if (type1.AssemblyQualifiedName.Equals(type2.AssemblyQualifiedName))
                {
                    return true;
                }

                type1 = type1.GetTypeInfo().BaseType;
            }

            return false;
        }

        /// <summary>
        /// Check wether member belongs to addembly loaded as refelction only.
        /// </summary>
        private static bool IsReflectionOnlyLoad(MemberInfo memberInfo)
        {
            if (memberInfo != null)
            {
#if !TODO
                return memberInfo.Module.Assembly.ReflectionOnly;
#else
                //An assembly cannot be loaded using the ReflectionOnlyLoadFrom() method on the device side
                //IsReflectionOnlyLoad will always return false
                return false;
#endif
            }

            return false;
        }

        /// <summary>
        /// Get the Attributes (TypeName/TypeObject) for a given member.  
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes")]
        private Dictionary<string, object> GetAttributes(MemberInfo memberInfo, bool inherit)
        {
            // If the information is cached, then use it otherwise populate the cache using
            // the reflection APIs. 
            //

            Dictionary<string, object> attributes;
            lock (this.m_attributeCache)
            {
                if (!this.m_attributeCache.TryGetValue(memberInfo, out attributes))
                {
                    // Populate the cache
                    attributes = new Dictionary<string, object>();

                    object[] customAttributesArray = null;
                    try
                    {
                        if (IsReflectionOnlyLoad(memberInfo) == false)
                        {
                            // GetCustomAttributes instantiates all attributes and throws System.Exception
                            // when there is an attribute with constructor that throws.
#if TODO
                            customAttributesArray = memberInfo.GetCustomAttributes(inherit).ToArray<object>();
#else
                            customAttributesArray = memberInfo.GetCustomAttributes(inherit);
#endif
                        }
                        else
                        {
#if !TODO
                            IList<CustomAttributeData> customAttributes = CustomAttributeData.GetCustomAttributes(memberInfo);

                            List<object> attributesArray = new List<object>();
                            foreach (var attribute in customAttributes)
                            {
                                Attribute attributeInstance = CreateAttributeInstance(attribute);
                                if (attributeInstance != null)
                                {
                                    attributesArray.Add(attributeInstance);
                                }
                            }

                            customAttributesArray = attributesArray.ToArray();
#else
                            customAttributesArray = CustomAttributeExtensions.GetCustomAttributes(memberInfo).ToArray();
#endif
                        }
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
                    Debug.Assert(customAttributesArray != null);

                    foreach (object customAttribute in customAttributesArray)
                    {
                        Type attrType = customAttribute.GetType();

                        // TODO: mkolt: make sure setting values to Type works across versioning.
                        attributes[attrType.FullName] = customAttribute;
                    }

                    this.m_attributeCache.Add(memberInfo, attributes);
                }
            }

            return attributes;
        }

        /// <summary>
        /// Create instance of the attribute for reflection only load.
        /// </summary>
        /// <param name="attributeData"></param>
        /// <returns></returns>
#if !TODO
        private static Attribute CreateAttributeInstance(CustomAttributeData attributeData)
        {
            object attribute = null;
            try
            {
                // Create instance of attribute. For some case, constructor param is returned as ReadOnlyCollection
                // instead of array. So convert it to array else constructor invoke will fail.
                Type attributeType = Type.GetType(attributeData.Constructor.DeclaringType.AssemblyQualifiedName);
                if (null == attributeType)
                {
                   // In case, the assembly is not found in output folder doesn't gets loaded as part of Type.GetType, we will search in additional locations and load.
                   attributeType = Type.GetType(attributeData.Constructor.DeclaringType.AssemblyQualifiedName, AssemblyResolver.LoadAssembly, null);
                }

                List<Type> constructorParameters = new List<Type>();
                List<object> constructorArguments = new List<object>();
                foreach (var parameter in attributeData.ConstructorArguments)
                {
                    Type parameterType = Type.GetType(parameter.ArgumentType.AssemblyQualifiedName);
                    constructorParameters.Add(parameterType);
                    if (parameterType.IsArray) 
                    {
                        IEnumerable enumerable = parameter.Value as IEnumerable;
                        if (enumerable != null)
                        {
                            ArrayList list = new ArrayList();
                            foreach (var item in enumerable)
                            {
                                if (item is CustomAttributeTypedArgument)
                                {
                                    list.Add(((CustomAttributeTypedArgument)item).Value);
                                }
                                else
                                {
                                    list.Add(item);
                                }
                            }
                            
                            constructorArguments.Add(list.ToArray(parameterType.GetElementType()));
                        }
                        else
                        {
                            constructorArguments.Add(parameter.Value);
                        }
                    }
                    else 
                    {
                        constructorArguments.Add(parameter.Value);
                    }
                }

                ConstructorInfo constructor = attributeType.GetConstructor(constructorParameters.ToArray());
                attribute = constructor.Invoke(constructorArguments.ToArray());

                foreach (var namedArgument in attributeData.NamedArguments)
                {
                    attributeType.GetProperty(namedArgument.MemberInfo.Name).SetValue(attribute, namedArgument.TypedValue.Value, null);
                }
            }
            // If not able to create instance of attribute ignore attribute. (May happen for custom user defined attributes).
            catch (BadImageFormatException) { }
            catch (FileLoadException) { }
            catch (TypeLoadException) { }

            return attribute as Attribute;
        }
#endif

        /// <summary>
        /// Test context property name
        /// </summary>
        public const string TestContextPropertyName = "TestContext";

        /// <summary>
        /// Contains the memberInfo Vs the name/type of the attributes defined on that member. (FYI: - MemberInfo denotes properties, fields, methods, events)
        /// </summary>
        private Dictionary<MemberInfo, Dictionary<string, object>> m_attributeCache = new Dictionary<MemberInfo, Dictionary<string, object>>();

        /// <summary>
        /// Get attribute defined on a method which is of given type of subtype of given type.
        /// </summary>
        internal AttributeType GetDerivedAttribute<AttributeType>(MemberInfo memberInfo, bool inherit) where AttributeType : Attribute
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
        internal AttributeType GetDerivedAttribute<AttributeType>(Type type, bool inherit) where AttributeType : Attribute
        {
            var attributes = type.GetTypeInfo().GetCustomAttributes(inherit);
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
    }
}