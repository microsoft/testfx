// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP

#if NETFRAMEWORK
using System.Collections;
#endif
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Utilities;

/// <summary>
/// Utility for reflection API's.
/// </summary>
[SuppressMessage("Performance", "CA1852: Seal internal types", Justification = "Overrides required for testability")]
internal class ReflectionUtility
{
    /// <summary>
    /// Gets the custom attributes of the provided type on a memberInfo.
    /// </summary>
    /// <param name="attributeProvider"> The member to reflect on. </param>
    /// <param name="type"> The attribute type. </param>
    /// <returns> The vale of the custom attribute. </returns>
    internal virtual IReadOnlyList<object> GetCustomAttributes(MemberInfo attributeProvider, Type type)
        => GetCustomAttributes(attributeProvider, type, true);

    /// <summary>
    /// Gets all the custom attributes adorned on a member.
    /// </summary>
    /// <param name="memberInfo"> The member. </param>
    /// <param name="inherit"> True to inspect the ancestors of element; otherwise, false. </param>
    /// <returns> The list of attributes on the member. Empty list if none found. </returns>
    internal static IReadOnlyList<object> GetCustomAttributes(MemberInfo memberInfo, bool inherit)
        => GetCustomAttributes(memberInfo, type: null, inherit: inherit);

    /// <summary>
    /// Get custom attributes on a member for both normal and reflection only load.
    /// </summary>
    /// <param name="memberInfo">Member for which attributes needs to be retrieved.</param>
    /// <param name="type">Type of attribute to retrieve.</param>
    /// <param name="inherit">If inherited type of attribute.</param>
    /// <returns>All attributes of give type on member.</returns>
    internal static IReadOnlyList<object> GetCustomAttributes(MemberInfo memberInfo, Type? type, bool inherit)
    {
#if NETFRAMEWORK
        bool shouldGetAllAttributes = type == null;

        if (!IsReflectionOnlyLoad(memberInfo))
        {
            return shouldGetAllAttributes ? memberInfo.GetCustomAttributes(inherit) : memberInfo.GetCustomAttributes(type, inherit);
        }
        else
        {
            List<object> nonUniqueAttributes = [];
            Dictionary<string, object> uniqueAttributes = [];

            int inheritanceThreshold = 10;
            int inheritanceLevel = 0;

            if (inherit && memberInfo.MemberType == MemberTypes.TypeInfo)
            {
                // This code is based on the code for fetching CustomAttributes in System.Reflection.CustomAttribute(RuntimeType type, RuntimeType caType, bool inherit)
                var tempTypeInfo = memberInfo as TypeInfo;

                do
                {
                    IList<CustomAttributeData> attributes = CustomAttributeData.GetCustomAttributes(tempTypeInfo);
                    AddNewAttributes(
                        attributes,
                        shouldGetAllAttributes,
                        type!,
                        uniqueAttributes,
                        nonUniqueAttributes);
                    tempTypeInfo = tempTypeInfo!.BaseType?.GetTypeInfo();
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
                    IList<CustomAttributeData> attributes = CustomAttributeData.GetCustomAttributes(tempMethodInfo);
                    AddNewAttributes(
                        attributes,
                        shouldGetAllAttributes,
                        type!,
                        uniqueAttributes,
                        nonUniqueAttributes);
                    MethodInfo? baseDefinition = tempMethodInfo!.GetBaseDefinition();

                    if (baseDefinition != null
                        && string.Equals(
                            string.Concat(tempMethodInfo.DeclaringType.FullName, tempMethodInfo.Name),
                            string.Concat(baseDefinition.DeclaringType.FullName, baseDefinition.Name), StringComparison.Ordinal))
                    {
                        break;
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
                IList<CustomAttributeData> firstLevelAttributes =
                CustomAttributeData.GetCustomAttributes(memberInfo);
                AddNewAttributes(firstLevelAttributes, shouldGetAllAttributes, type!, uniqueAttributes, nonUniqueAttributes);
            }

            nonUniqueAttributes.AddRange(uniqueAttributes.Values);
            return nonUniqueAttributes;
        }
#else
        return type == null
            ? memberInfo.GetCustomAttributes(inherit)
            : memberInfo.GetCustomAttributes(type, inherit);
#endif
    }

#if NETFRAMEWORK
    internal static List<Attribute> GetCustomAttributes(Assembly assembly, Type type)
    {
        if (!assembly.ReflectionOnly)
        {
            return assembly.GetCustomAttributes(type).ToList();
        }

        List<CustomAttributeData> customAttributes = [.. CustomAttributeData.GetCustomAttributes(assembly)];

        List<Attribute> attributesArray = [];

        foreach (CustomAttributeData attribute in customAttributes)
        {
            if (!IsTypeInheriting(attribute.Constructor.DeclaringType, type)
                    && !attribute.Constructor.DeclaringType.AssemblyQualifiedName.Equals(
                        type.AssemblyQualifiedName, StringComparison.Ordinal))
            {
                continue;
            }

            Attribute? attributeInstance = CreateAttributeInstance(attribute);
            if (attributeInstance != null)
            {
                attributesArray.Add(attributeInstance);
            }
        }

        return attributesArray;
    }

    /// <summary>
    /// Create instance of the attribute for reflection only load.
    /// </summary>
    /// <param name="attributeData">The attribute data.</param>
    /// <returns>An attribute.</returns>
    private static Attribute? CreateAttributeInstance(CustomAttributeData attributeData)
    {
        object? attribute = null;
        try
        {
            // Create instance of attribute. For some case, constructor param is returned as ReadOnlyCollection
            // instead of array. So convert it to array else constructor invoke will fail.
            var attributeType = Type.GetType(attributeData.Constructor.DeclaringType.AssemblyQualifiedName);

            List<Type> constructorParameters = [];
            List<object> constructorArguments = [];
            foreach (CustomAttributeTypedArgument parameter in attributeData.ConstructorArguments)
            {
                var parameterType = Type.GetType(parameter.ArgumentType.AssemblyQualifiedName);
                constructorParameters.Add(parameterType);
                if (!parameterType.IsArray
                    || parameter.Value is not IEnumerable enumerable)
                {
                    constructorArguments.Add(parameter.Value);
                    continue;
                }

                ArrayList list = [];
                foreach (object? item in enumerable)
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

            ConstructorInfo constructor = attributeType.GetConstructor(constructorParameters.ToArray());
            attribute = constructor.Invoke(constructorArguments.ToArray());

            foreach (CustomAttributeNamedArgument namedArgument in attributeData.NamedArguments)
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

    private static void AddNewAttributes(
        IList<CustomAttributeData> customAttributes,
        bool shouldGetAllAttributes,
        Type type,
        Dictionary<string, object> uniqueAttributes,
        List<object> nonUniqueAttributes)
    {
        foreach (CustomAttributeData attribute in customAttributes)
        {
            if (!shouldGetAllAttributes
                && !IsTypeInheriting(attribute.Constructor.DeclaringType, type)
                    && !attribute.Constructor.DeclaringType.AssemblyQualifiedName.Equals(
                        type.AssemblyQualifiedName, StringComparison.Ordinal))
            {
                continue;
            }

            Attribute? attributeInstance = CreateAttributeInstance(attribute);
            if (attributeInstance == null)
            {
                continue;
            }

            Type attributeType = attributeInstance.GetType();
            IReadOnlyList<object> attributeUsageAttributes = GetCustomAttributes(
                attributeType,
                typeof(AttributeUsageAttribute),
                true);
            if (attributeUsageAttributes.Count > 0
                && attributeUsageAttributes[0] is AttributeUsageAttribute { AllowMultiple: false })
            {
                if (!uniqueAttributes.ContainsKey(attributeType.FullName))
                {
                    uniqueAttributes.Add(attributeType.FullName, attributeInstance);
                }
            }
            else
            {
                nonUniqueAttributes.Add(attributeInstance);
            }
        }
    }

    /// <summary>
    /// Check whether the member is loaded in a reflection only context.
    /// </summary>
    /// <param name="memberInfo"> The member Info. </param>
    /// <returns> True if the member is loaded in a reflection only context. </returns>
    private static bool IsReflectionOnlyLoad(MemberInfo? memberInfo)
        => memberInfo != null && memberInfo.Module.Assembly.ReflectionOnly;

    private static bool IsTypeInheriting(Type? type1, Type type2)
    {
        while (type1 != null)
        {
            if (type1.AssemblyQualifiedName.Equals(type2.AssemblyQualifiedName, StringComparison.Ordinal))
            {
                return true;
            }

            type1 = type1.BaseType;
        }

        return false;
    }
#endif
}

#endif
