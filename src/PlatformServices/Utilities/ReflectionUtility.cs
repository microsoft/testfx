// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Utilities
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;

    /// <summary>
    /// Utility for reflection API's
    /// </summary>
    internal class ReflectionUtility
    {
        internal virtual object[] GetCustomAttributes(MemberInfo attributeProvider, Type type)
        {
            return this.GetCustomAttributes(attributeProvider, type, true);
        }

        /// <summary>
        /// Gets all the custom attributes adorned on a member.
        /// </summary>
        /// <param name="memberInfo"> The member. </param>
        /// <param name="inherit"> True to inspect the ancestors of element; otherwise, false. </param>
        /// <returns> The list of attributes on the member. Empty list if none found. </returns>
        internal object[] GetCustomAttributes(MemberInfo memberInfo, bool inherit)
        {
            return this.GetCustomAttributes(memberInfo, type: null, inherit: inherit);
        }

        internal object[] GetCustomAttributes(MemberInfo memberInfo, Type type, bool inherit)
        {
            if (memberInfo == null)
            {
                return null;
            }

            var shouldGetAllAttributes = type == null;

            if (!IsReflectionOnlyLoad(memberInfo))
            {
                if (shouldGetAllAttributes)
                {
                    return memberInfo.GetCustomAttributes(inherit).ToArray();
                }
                else
                {
                    return memberInfo.GetCustomAttributes(type, inherit).ToArray();
                }
            }

#if !NETSTANDARD1_4
            var nonUniqueAttributes = new List<object>();
            var uniqueAttributes = new Dictionary<string, object>();

            var inheritanceThreshold = 10;
            var inheritanceLevel = 0;

            if (inherit && memberInfo.MemberType == MemberTypes.TypeInfo)
            {
                // This code is based on the code for fetching CustomAttributes in System.Reflection.CustomAttribute(RuntimeType type, RuntimeType caType, bool inherit)
                var tempTypeInfo = memberInfo as TypeInfo;

                do
                {
                    var attributes = CustomAttributeData.GetCustomAttributes(tempTypeInfo);
                    this.AddNewAttributes(
                        attributes,
                        shouldGetAllAttributes,
                        type,
                        uniqueAttributes,
                        nonUniqueAttributes);
                    tempTypeInfo = tempTypeInfo.BaseType?.GetTypeInfo();
                    inheritanceLevel++;
                }
                while (tempTypeInfo != null && tempTypeInfo != typeof(object).GetTypeInfo()
                       && inheritanceLevel < inheritanceThreshold);
            }
            else if (inherit && memberInfo.MemberType == MemberTypes.Method)
            {
                // This code is based on the code for fetching CustomAttributes in System.Reflection.CustomAttribute(RuntimeMethodInfo method, RuntimeType caType, bool inherit).
                var tempMethodInfo = memberInfo as MethodInfo;

                do
                {
                    var attributes = CustomAttributeData.GetCustomAttributes(tempMethodInfo);
                    this.AddNewAttributes(
                        attributes,
                        shouldGetAllAttributes,
                        type,
                        uniqueAttributes,
                        nonUniqueAttributes);
                    var baseDefinition = tempMethodInfo.GetBaseDefinition();

                    if (baseDefinition != null)
                    {
                        if (string.Equals(
                            string.Concat(tempMethodInfo.DeclaringType.FullName, tempMethodInfo.Name),
                            string.Concat(baseDefinition.DeclaringType.FullName, baseDefinition.Name)))
                        {
                            break;
                        }
                    }

                    tempMethodInfo = baseDefinition;
                    inheritanceLevel++;
                }
                while (tempMethodInfo != null && inheritanceLevel < inheritanceThreshold);
            }
            else
            {
                // Ideally we should not be reaching here. We only query for attributes on types/methods currently.
                // Return the attributes that CustomAttributeData returns in this cases not considering inheritance.
                var firstLevelAttributes =
                CustomAttributeData.GetCustomAttributes(memberInfo);
                this.AddNewAttributes(firstLevelAttributes, shouldGetAllAttributes, type, uniqueAttributes, nonUniqueAttributes);
            }

            nonUniqueAttributes.AddRange(uniqueAttributes.Values);
            return nonUniqueAttributes.ToArray();
#else
            // We should never hit this, since NETSTANDARD1_4 doesn't support ReflectionOnlyLoad.
            throw new InvalidOperationException();
#endif
        }

        internal object[] GetCustomAttributes(Assembly assembly, Type type)
        {
            if (IsReflectionOnlyLoad(assembly))
            {
#if !NETSTANDARD1_4
                List<CustomAttributeData> customAttributes = new List<CustomAttributeData>();
                customAttributes.AddRange(CustomAttributeData.GetCustomAttributes(assembly));

                List<object> attributesArray = new List<object>();

                foreach (var attribute in customAttributes)
                {
                    if (this.IsTypeInheriting(attribute.Constructor.DeclaringType, type)
                            || attribute.Constructor.DeclaringType.AssemblyQualifiedName.Equals(
                                type.AssemblyQualifiedName))
                    {
                        Attribute attributeInstance = CreateAttributeInstance(attribute);
                        if (attributeInstance != null)
                        {
                            attributesArray.Add(attributeInstance);
                        }
                    }
                }

                return attributesArray.ToArray();
#else
                // We should never hit this, since NETSTANDARD1_4 doesn't support ReflectionOnlyLoad.
                throw new InvalidOperationException("ReflectionOnlyLoad is not supported in NETSTANDARD1.4");
#endif
            }
            else
            {
                return assembly.GetCustomAttributes(type).ToArray();
            }
        }

        /// <summary>
        /// Check whether the member is loaded in a reflection only context.
        /// </summary>
        /// <param name="memberInfo"> The member Info. </param>
        /// <returns> True if the member is loaded in a reflection only context. </returns>
        private bool IsReflectionOnlyLoad(MemberInfo memberInfo)
        {
            return IsReflectionOnlyLoad(memberInfo.Module.Assembly);
        }

        /// <summary>
        /// Check whether the member is loaded in a reflection only context.
        /// </summary>
        /// <param name="assembly"> The assembly to check. </param>
        /// <returns> True if the member is loaded in a reflection only context. </returns>
        private bool IsReflectionOnlyLoad(Assembly assembly)
        {
#if !NETSTANDARD1_4
            if (assembly != null)
            {
                return assembly.ReflectionOnly;
            }
#endif

            return false;
        }

#if !NETSTANDARD1_4
/// <summary>
        /// Create instance of the attribute for reflection only load.
        /// </summary>
        /// <param name="attributeData">The attribute data.</param>
        /// <returns>An attribute.</returns>
        private static Attribute CreateAttributeInstance(CustomAttributeData attributeData)
        {
            object attribute = null;
            try
            {
                // Create instance of attribute. For some case, constructor param is returned as ReadOnlyCollection
                // instead of array. So convert it to array else constructor invoke will fail.
                Type attributeType = Type.GetType(attributeData.Constructor.DeclaringType.AssemblyQualifiedName);

                List<Type> constructorParameters = new List<Type>();
                List<object> constructorArguments = new List<object>();
                foreach (var parameter in attributeData.ConstructorArguments)
                {
                    Type parameterType = Type.GetType(parameter.ArgumentType.AssemblyQualifiedName);
                    constructorParameters.Add(parameterType);
                    if (parameterType.IsArray)
                    {
                        if (parameter.Value is IEnumerable enumerable)
                        {
                            ArrayList list = new ArrayList();
                            foreach (var item in enumerable)
                            {
                                if (item is CustomAttributeTypedArgument argument)
                                {
                                    list.Add(argument.Value);
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
            catch (BadImageFormatException)
            {
            }
            catch (FileLoadException)
            {
            }
            catch (TypeLoadException)
            {
            }

            return attribute as Attribute;
        }

        private void AddNewAttributes(
            IList<CustomAttributeData> customAttributes,
            bool shouldGetAllAttributes,
            Type type,
            Dictionary<string, object> uniqueAttributes,
            List<object> nonUniqueAttributes)
        {
            foreach (var attribute in customAttributes)
            {
                if (shouldGetAllAttributes
                    || (this.IsTypeInheriting(attribute.Constructor.DeclaringType, type)
                        || attribute.Constructor.DeclaringType.AssemblyQualifiedName.Equals(
                            type.AssemblyQualifiedName)))
                {
                    Attribute attributeInstance = CreateAttributeInstance(attribute);
                    if (attributeInstance != null)
                    {
                        if (this.GetCustomAttributes(attributeInstance.GetType().GetTypeInfo(), typeof(AttributeUsageAttribute), true).FirstOrDefault() is AttributeUsageAttribute attributeUsageAttribute && !attributeUsageAttribute.AllowMultiple)
                        {
                            if (!uniqueAttributes.ContainsKey(attributeInstance.GetType().FullName))
                            {
                                uniqueAttributes.Add(attributeInstance.GetType().FullName, attributeInstance);
                            }
                        }
                        else
                        {
                            nonUniqueAttributes.Add(attributeInstance);
                        }
                    }
                }
            }
        }
#endif

        /// <summary>
        /// Check whether <paramref name="derivedType"/> inherits <paramref name="baseType"/>.
        /// </summary>
        /// <param name="derivedType">Derived type to check.</param>
        /// <param name="baseType">Base class for the inheritance.</param>
        /// <returns>Whether or not <paramref name="derivedType"/> derived from <paramref name="baseType"/>.</returns>
        public bool IsTypeInheriting(Type derivedType, Type baseType)
        {
            while (derivedType != null)
            {
                if (derivedType.AssemblyQualifiedName.Equals(baseType.AssemblyQualifiedName))
                {
                    return true;
                }

                derivedType = derivedType.GetTypeInfo().BaseType;
            }

            return false;
        }
    }
}
